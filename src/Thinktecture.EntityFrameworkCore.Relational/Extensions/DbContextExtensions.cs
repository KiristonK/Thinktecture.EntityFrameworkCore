using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Thinktecture.EntityFrameworkCore.TempTables;

// ReSharper disable once CheckNamespace
namespace Thinktecture
{
   /// <summary>
   /// Extension methods for <see cref="DbContext"/>.
   /// </summary>
   public static class DbContextExtensions
   {
      /// <summary>
      /// Fetches meta data for entity of type <typeparamref name="T"/>.
      /// </summary>
      /// <param name="ctx">Database context.</param>
      /// <typeparam name="T">Entity type.</typeparam>
      /// <returns>An instance of type <see cref="IEntityType"/>.</returns>
      /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">The provided type <typeparamref name="T"/> is not known by provided <paramref name="ctx"/>.</exception>
      [NotNull]
      public static IEntityType GetEntityType<T>([NotNull] this DbContext ctx)
      {
         return GetEntityType(ctx, typeof(T));
      }

      /// <summary>
      /// Fetches meta data for entity of provided <paramref name="type"/>.
      /// </summary>
      /// <param name="ctx">Database context.</param>
      /// <param name="type">Entity type.</param>
      /// <returns>An instance of type <see cref="IEntityType"/>.</returns>
      /// <exception cref="ArgumentNullException">
      /// <paramref name="ctx"/> is <c>null</c>
      /// - or
      /// <paramref name="type"/> is <c>null</c>.
      /// </exception>
      /// <exception cref="ArgumentException">The provided type <paramref name="type"/> is not known by provided <paramref name="ctx"/>.</exception>
      [NotNull]
      public static IEntityType GetEntityType([NotNull] this DbContext ctx, [NotNull] Type type)
      {
         if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
         if (type == null)
            throw new ArgumentNullException(nameof(type));

         var entityType = ctx.Model.FindEntityType(type);

         if (entityType == null)
            throw new ArgumentException($"The provided type '{type.DisplayName()}' is not known by the database context '{ctx.GetType().DisplayName()}'.", nameof(type));

         return entityType;
      }

      /// <summary>
      /// Get the table schema and name of the provided <paramref name="entityType"/>.
      /// </summary>
      /// <param name="entityType">Entity type to fetch the table schema and name for.</param>
      /// <returns>Table schema and table name for provided entity type.</returns>
      /// <exception cref="ArgumentNullException"><paramref name="entityType"/> is <c>null</c>.</exception>
      public static (string Schema, string TableName) GetTableIdentifier([NotNull] this IEntityType entityType)
      {
         if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));

         var relational = entityType.Relational();

         return (relational.Schema, relational.TableName);
      }

      /// <summary>
      /// Creates a temp table using custom type '<typeparamref name="T"/>'.
      /// </summary>
      /// <param name="ctx">Database context to use.</param>
      /// <param name="makeTableNameUnique">Indication whether the table name should be unique.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <typeparam name="T">Type of custom temp table.</typeparam>
      /// <returns>Table name</returns>
      /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentException">The provided type <typeparamref name="T"/> is not known by provided <paramref name="ctx"/>.</exception>
      [NotNull, ItemNotNull]
      public static Task<string> CreateTempTableAsync<T>([NotNull] this DbContext ctx, bool makeTableNameUnique = false, CancellationToken cancellationToken = default)
         where T : class
      {
         return ctx.CreateTempTableAsync(typeof(T), new TempTableCreationOptions { MakeTableNameUnique = makeTableNameUnique }, cancellationToken);
      }

      /// <summary>
      /// Creates a temp table.
      /// </summary>
      /// <param name="ctx">Database context to use.</param>
      /// <param name="type">Type of the entity.</param>
      /// <param name="options">Options.</param>
      /// <param name="cancellationToken">Cancellation token.</param>
      /// <returns>Table name</returns>
      /// <exception cref="ArgumentNullException">
      /// <paramref name="ctx"/> is <c>null</c>
      /// - or
      /// <paramref name="type"/> is <c>null</c>.
      /// </exception>
      /// <exception cref="ArgumentException">The provided type <paramref name="type"/> is not known by provided <paramref name="ctx"/>.</exception>
      [NotNull, ItemNotNull]
      public static Task<string> CreateTempTableAsync([NotNull] this DbContext ctx, [NotNull] Type type, [NotNull] TempTableCreationOptions options, CancellationToken cancellationToken)
      {
         var entityType = ctx.GetEntityType(type);
         return ctx.GetService<ITempTableCreator>().CreateTempTableAsync(ctx, entityType, options, cancellationToken);
      }
   }
}
