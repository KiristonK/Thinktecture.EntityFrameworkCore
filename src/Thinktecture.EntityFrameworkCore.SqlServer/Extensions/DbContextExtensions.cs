using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Thinktecture.EntityFrameworkCore;
using Thinktecture.EntityFrameworkCore.TempTables;
using Thinktecture.EntityFrameworkCore.ValueConversion;

// ReSharper disable once CheckNamespace
namespace Thinktecture
{
   /// <summary>
   /// Extension methods for <see cref="DbContext"/>.
   /// </summary>
   public static class DbContextExtensions
   {
      private static readonly RowVersionValueConverter _rowVersionConverter;

      static DbContextExtensions()
      {
         _rowVersionConverter = new RowVersionValueConverter();
      }

      /// <summary>
      /// Fetches MIN_ACTIVE_ROWVERSION from SQL Server.
      /// </summary>
      /// <param name="ctx">Database context to use.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <returns>The result of MIN_ACTIVE_ROWVERSION call.</returns>
      /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
      public static async Task<ulong> GetMinActiveRowVersionAsync([NotNull] this DbContext ctx, CancellationToken cancellationToken)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));

         using (var command = ctx.Database.GetDbConnection().CreateCommand())
         {
            command.Transaction = ctx.Database.CurrentTransaction?.GetDbTransaction();

            await ctx.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            command.CommandText = "SELECT MIN_ACTIVE_ROWVERSION();";
            var bytes = (byte[])await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return (ulong)_rowVersionConverter.ConvertFromProvider(bytes);
         }
      }

      /// <summary>
      /// Creates a temp table using custom type '<typeparamref name="T"/>'.
      /// </summary>
      /// <param name="ctx">Database context to use.</param>
      /// <typeparam name="T">Type of custom temp table.</typeparam>
      /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
      public static async Task CreateCustomTempTableAsync<T>([NotNull] this DbContext ctx)
         where T : class
      {
         await CreateTempTableAsync(ctx, typeof(T)).ConfigureAwait(false);
      }

      /// <summary>
      /// Creates a temp table.
      /// </summary>
      /// <param name="ctx">Database context to use.</param>
      /// <typeparam name="TColumn1">Type of the column 1.</typeparam>
      /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
      public static async Task CreateTempTableAsync<TColumn1>([NotNull] this DbContext ctx)
      {
         await CreateTempTableAsync(ctx, typeof(TempTable<TColumn1>)).ConfigureAwait(false);
      }

      /// <summary>
      /// Creates a temp table.
      /// </summary>
      /// <param name="ctx">Database context to use.</param>
      /// <typeparam name="TColumn1">Type of the column 1.</typeparam>
      /// <typeparam name="TColumn2">Type of the column 2.</typeparam>
      /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
      public static async Task CreateTempTableAsync<TColumn1, TColumn2>([NotNull] this DbContext ctx)
      {
         await CreateTempTableAsync(ctx, typeof(TempTable<TColumn1, TColumn2>)).ConfigureAwait(false);
      }

      private static async Task CreateTempTableAsync([NotNull] DbContext ctx, [NotNull] Type type)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (type == null)
            throw new ArgumentNullException(nameof(type));

         var sql = GetTempTableCreationSql(ctx, type);

         await ctx.Database.ExecuteSqlCommandAsync(sql).ConfigureAwait(false);
      }

      [NotNull]
      private static string GetTempTableCreationSql([NotNull] DbContext ctx, [NotNull] Type type)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (type == null)
            throw new ArgumentNullException(nameof(type));

         var (_, tableName) = ctx.GetTableIdentifier(type);

         var sql = $@"
IF(OBJECT_ID('tempdb..{tableName}') IS NOT NULL)
      TRUNCATE TABLE [{tableName}];
ELSE
BEGIN
      CREATE TABLE [{tableName}]
      (
{GetColumnsDefinitions(ctx, type)}
      );
END
";

         return sql;
      }

      [NotNull]
      private static string GetColumnsDefinitions([NotNull] DbContext ctx, [NotNull] Type type)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (type == null)
            throw new ArgumentNullException(nameof(type));

         var entityType = ctx.GetEntityType(type);
         var sb = new StringBuilder();
         var isFirst = true;

         foreach (var property in entityType.GetProperties())
         {
            if (!isFirst)
               sb.AppendLine(",");

            var relational = property.Relational();

            sb.Append("\t\t")
              .Append(relational.ColumnName).Append(" ")
              .Append(relational.ColumnType).Append(" ")
              .Append(property.IsNullable ? "NULL" : "NOT NULL");

            isFirst = false;
         }

         return sb.ToString();
      }

      /// <summary>
      /// Copies <paramref name="entities"/> into a temp table using <see cref="SqlBulkCopy"/>
      /// and returns the query for accessing the inserted records.
      /// </summary>
      /// <param name="ctx">Database context.</param>
      /// <param name="entities">Entities to insert.</param>
      /// <param name="options">Options.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <typeparam name="T">Entity type.</typeparam>
      /// <typeparam name="TColumn1">Type of the values to insert.</typeparam>
      /// <returns>A query for accessing the inserted values.</returns>
      /// <exception cref="ArgumentNullException"> <paramref name="ctx"/> or <paramref name="entities"/> is <c>null</c>.</exception>
      [NotNull]
      public static async Task<IQueryable<T>> BulkInsertCustomTempTableAsync<T, TColumn1>([NotNull] this DbContext ctx,
                                                                                          [NotNull] IEnumerable<T> entities,
                                                                                          [CanBeNull] SqlBulkInsertOptions options = null,
                                                                                          CancellationToken cancellationToken = default)
         where T : class, ITempTable<TColumn1>
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (entities == null)
            throw new ArgumentNullException(nameof(entities));

         await ctx.CreateCustomTempTableAsync<T>().ConfigureAwait(false);

         using (var reader = new TempTableDataReader<T, TColumn1>(entities))
         {
            await BulkInsertAsync<T>(ctx, reader, options, cancellationToken).ConfigureAwait(false);
         }

         return ctx.Query<T>();
      }

      /// <summary>
      /// Copies <paramref name="entities"/> into a temp table using <see cref="SqlBulkCopy"/>
      /// and returns the query for accessing the inserted records.
      /// </summary>
      /// <param name="ctx">Database context.</param>
      /// <param name="entities">Entities to insert.</param>
      /// <param name="options">Options.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <typeparam name="T">Entity type.</typeparam>
      /// <typeparam name="TColumn1">Type of the column 1.</typeparam>
      /// <typeparam name="TColumn2">Type of the column 2.</typeparam>
      /// <returns>A query for accessing the inserted values.</returns>
      /// <exception cref="ArgumentNullException"> <paramref name="ctx"/> or <paramref name="entities"/> is <c>null</c>.</exception>
      [NotNull]
      public static async Task<IQueryable<T>> BulkInsertCustomTempTableAsync<T, TColumn1, TColumn2>([NotNull] this DbContext ctx,
                                                                                                    [NotNull] IEnumerable<T> entities,
                                                                                                    [CanBeNull] SqlBulkInsertOptions options = null,
                                                                                                    CancellationToken cancellationToken = default)
         where T : class, ITempTable<TColumn1, TColumn2>
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (entities == null)
            throw new ArgumentNullException(nameof(entities));

         await ctx.CreateCustomTempTableAsync<T>().ConfigureAwait(false);

         using (var reader = new TempTableDataReader<T, TColumn1, TColumn2>(entities))
         {
            await BulkInsertAsync<T>(ctx, reader, options, cancellationToken).ConfigureAwait(false);
         }

         return ctx.Query<T>();
      }

      /// <summary>
      /// Copies <paramref name="values"/> into a temp table using <see cref="SqlBulkCopy"/>
      /// and returns the query for accessing the inserted records.
      /// </summary>
      /// <param name="ctx">Database context.</param>
      /// <param name="values">Values to insert.</param>
      /// <param name="options">Options.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <typeparam name="TColumn1">Type of the values to insert.</typeparam>
      /// <returns>A query for accessing the inserted values.</returns>
      /// <exception cref="ArgumentNullException"> <paramref name="ctx"/> or <paramref name="values"/> is <c>null</c>.</exception>
      [NotNull]
      public static async Task<IQueryable<TempTable<TColumn1>>> BulkInsertTempTableAsync<TColumn1>([NotNull] this DbContext ctx,
                                                                                                   [NotNull] IEnumerable<TColumn1> values,
                                                                                                   [CanBeNull] SqlBulkInsertOptions options = null,
                                                                                                   CancellationToken cancellationToken = default)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (values == null)
            throw new ArgumentNullException(nameof(values));

         await ctx.CreateTempTableAsync<TColumn1>().ConfigureAwait(false);

         var entities = values.Select(v => new TempTable<TColumn1>(v));

         using (var reader = new TempTableDataReader<TempTable<TColumn1>, TColumn1>(entities))
         {
            await BulkInsertAsync<TempTable<TColumn1>>(ctx, reader, options, cancellationToken).ConfigureAwait(false);
         }

         return ctx.Query<TempTable<TColumn1>>();
      }

      /// <summary>
      /// Copies <paramref name="values"/> into a temp table using <see cref="SqlBulkCopy"/>
      /// and returns the query for accessing the inserted records.
      /// </summary>
      /// <param name="ctx">Database context.</param>
      /// <param name="values">Values to insert.</param>
      /// <param name="options">Options.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <typeparam name="TColumn1">Type of the column 1.</typeparam>
      /// <typeparam name="TColumn2">Type of the column 2.</typeparam>
      /// <returns>A query for accessing the inserted values.</returns>
      /// <exception cref="ArgumentNullException"> <paramref name="ctx"/> or <paramref name="values"/> is <c>null</c>.</exception>
      [NotNull]
      public static async Task<IQueryable<TempTable<TColumn1, TColumn2>>> BulkInsertTempTableAsync<TColumn1, TColumn2>([NotNull] this DbContext ctx,
                                                                                                                       [NotNull] IEnumerable<(TColumn1 column1, TColumn2 column2)> values,
                                                                                                                       [CanBeNull] SqlBulkInsertOptions options = null,
                                                                                                                       CancellationToken cancellationToken = default)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (values == null)
            throw new ArgumentNullException(nameof(values));

         await ctx.CreateTempTableAsync<TColumn1, TColumn2>().ConfigureAwait(false);

         var entities = values.Select(t => new TempTable<TColumn1, TColumn2>(t.column1, t.column2));

         using (var reader = new TempTableDataReader<TempTable<TColumn1, TColumn2>, TColumn1, TColumn2>(entities))
         {
            await BulkInsertAsync<TempTable<TColumn1, TColumn2>>(ctx, reader, options, cancellationToken).ConfigureAwait(false);
         }

         return ctx.Query<TempTable<TColumn1, TColumn2>>();
      }

      private static async Task BulkInsertAsync<T>([NotNull] this DbContext ctx,
                                                   [NotNull] ITempTableDataReaderBase reader,
                                                   [CanBeNull] SqlBulkInsertOptions options,
                                                   CancellationToken cancellationToken)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (reader == null)
            throw new ArgumentNullException(nameof(reader));

         options = options ?? new SqlBulkInsertOptions();

         var entityType = ctx.GetEntityType<T>();
         var (_, tableName) = ctx.GetTableIdentifier(typeof(T));
         var sqlCon = (SqlConnection)ctx.Database.GetDbConnection();
         var sqlTx = (SqlTransaction)ctx.Database.CurrentTransaction?.GetDbTransaction();

         using (var bulkCopy = new SqlBulkCopy(sqlCon, options.SqlBulkCopyOptions, sqlTx))
         {
            bulkCopy.DestinationTableName = $"[{tableName}]";
            bulkCopy.EnableStreaming = options.EnableStreaming;

            if (options.BulkCopyTimeout.HasValue)
               bulkCopy.BulkCopyTimeout = (int)options.BulkCopyTimeout.Value.TotalSeconds;

            if (options.BatchSize.HasValue)
               bulkCopy.BatchSize = options.BatchSize.Value;

            foreach (var property in entityType.GetProperties())
            {
               var relational = property.Relational();
               var index = reader.GetPropertyIndex(property.PropertyInfo);
               bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(index, relational.ColumnName));
            }

            await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
         }
      }
   }
}
