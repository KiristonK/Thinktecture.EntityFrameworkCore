using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Thinktecture.EntityFrameworkCore.Data;

namespace Thinktecture.EntityFrameworkCore.BulkOperations
{
   /// <summary>
   /// Executes bulk operations.
   /// </summary>
   public class SqlServerBulkOperationExecutor : ISqlServerBulkOperationExecutor
   {
      /// <inheritdoc />
      public Task BulkInsertAsync<T>(DbContext ctx, IEnumerable<T> entities, SqlBulkInsertOptions options, CancellationToken cancellationToken = default)
         where T : class
      {
         var entityType = ctx.GetEntityType<T>();

         if (entityType.IsQueryType)
            throw new InvalidOperationException("The provided 'entities' are of 'Query Type' that do not have a table to insert into. Use the other overload that takes the 'tableName' as a parameter.");

         var tableId = entityType.GetTableIdentifier();

         return BulkInsertAsync(ctx, entities, tableId.Schema, tableId.TableName, options, cancellationToken);
      }

      /// <inheritdoc />
      public async Task BulkInsertAsync<T>(DbContext ctx,
                                           IEnumerable<T> entities,
                                           string schema,
                                           string tableName,
                                           SqlBulkInsertOptions options,
                                           CancellationToken cancellationToken = default)
         where T : class
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (entities == null)
            throw new ArgumentNullException(nameof(entities));
         if (tableName == null)
            throw new ArgumentNullException(nameof(tableName));
         if (options == null)
            throw new ArgumentNullException(nameof(options));

         var factory = ctx.GetService<IEntityDataReaderFactory>();
         var entityType = ctx.GetEntityType<T>();
         var properties = GetProperties(options, entityType);
         var sqlCon = (SqlConnection)ctx.Database.GetDbConnection();
         var sqlTx = (SqlTransaction)ctx.Database.CurrentTransaction?.GetDbTransaction();

         using (var reader = factory.Create(entities, properties))
         using (var bulkCopy = new SqlBulkCopy(sqlCon, options.SqlBulkCopyOptions, sqlTx))
         {
            bulkCopy.DestinationTableName = $"[{tableName}]";

            if (!String.IsNullOrWhiteSpace(schema))
               bulkCopy.DestinationTableName = $"[{schema}].{bulkCopy.DestinationTableName}";

            bulkCopy.EnableStreaming = options.EnableStreaming;

            if (options.BulkCopyTimeout.HasValue)
               bulkCopy.BulkCopyTimeout = (int)options.BulkCopyTimeout.Value.TotalSeconds;

            if (options.BatchSize.HasValue)
               bulkCopy.BatchSize = options.BatchSize.Value;

            foreach (var property in reader.Properties)
            {
               var index = reader.GetPropertyIndex(property);
               bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(index, property.Relational().ColumnName));
            }

            await ctx.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await bulkCopy.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
         }
      }

      [NotNull]
      private static IReadOnlyList<IProperty> GetProperties([NotNull] SqlBulkInsertOptions options, [NotNull] IEntityType entityType)
      {
         if (options.EntityMembersProvider == null)
            return entityType.GetProperties().Where(p => !p.IsShadowProperty).ToList();

         var memberInfos = options.EntityMembersProvider.GetMembers();
         var properties = new IProperty[memberInfos.Count];

         for (var i = 0; i < memberInfos.Count; i++)
         {
            var memberInfo = memberInfos[i];
            var property = FindProperty(entityType, memberInfo);

            properties[i] = property ?? throw new ArgumentException($"The member '{memberInfo.Name}' not found on entity '{entityType.Name}'.", nameof(options));
         }

         return properties;
      }

      [CanBeNull]
      private static IProperty FindProperty([NotNull] IEntityType entityType, [NotNull] MemberInfo memberInfo)
      {
         foreach (var property in entityType.GetProperties())
         {
            if (property.PropertyInfo == memberInfo || property.FieldInfo == memberInfo)
               return property;
         }

         return null;
      }
   }
}