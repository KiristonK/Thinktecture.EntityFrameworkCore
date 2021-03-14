using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Thinktecture.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Thinktecture.Extensions.QueryableExtensionsTests
{
   public class WithTableHints : IntegrationTestsBase
   {
      public WithTableHints(ITestOutputHelper testOutputHelper)
         : base(testOutputHelper, true)
      {
      }

      [Fact]
      public async Task Should_add_table_hints_to_table()
      {
         var query = ActDbContext.TestEntities.WithTableHints(SqlServerTableHint.NoLock);

         query.ToQueryString().Should().Be(@"SELECT [t].[Id], [t].[ConvertibleClass], [t].[Count], [t].[Name], [t].[ParentId], [t].[PropertyWithBackingField], [t].[_privateField]
FROM [_tests].[TestEntities] AS [t] WITH (NOLOCK)");

         var result = await query.ToListAsync();
         result.Should().BeEmpty();
      }

      [Fact]
      public void Should_not_mess_up_table_hints()
      {
         var query = ActDbContext.TestEntities.WithTableHints(SqlServerTableHint.NoLock);

         query.ToQueryString().Should().Be(@"SELECT [t].[Id], [t].[ConvertibleClass], [t].[Count], [t].[Name], [t].[ParentId], [t].[PropertyWithBackingField], [t].[_privateField]
FROM [_tests].[TestEntities] AS [t] WITH (NOLOCK)");

         query = ActDbContext.TestEntities.WithTableHints(SqlServerTableHint.UpdLock);

         query.ToQueryString().Should().Be(@"SELECT [t].[Id], [t].[ConvertibleClass], [t].[Count], [t].[Name], [t].[ParentId], [t].[PropertyWithBackingField], [t].[_privateField]
FROM [_tests].[TestEntities] AS [t] WITH (UPDLOCK)");
      }

      [Fact]
      public async Task Should_add_table_hints_to_table_without_touching_included_navigations()
      {
         var query = ActDbContext.TestEntities.WithTableHints(SqlServerTableHint.NoLock)
                                 .Include(e => e.Children);

         query.ToQueryString().Should().Be(@"SELECT [t].[Id], [t].[ConvertibleClass], [t].[Count], [t].[Name], [t].[ParentId], [t].[PropertyWithBackingField], [t].[_privateField], [t0].[Id], [t0].[ConvertibleClass], [t0].[Count], [t0].[Name], [t0].[ParentId], [t0].[PropertyWithBackingField], [t0].[_privateField]
FROM [_tests].[TestEntities] AS [t] WITH (NOLOCK)
LEFT JOIN [_tests].[TestEntities] AS [t0] ON [t].[Id] = [t0].[ParentId]
ORDER BY [t].[Id], [t0].[Id]");

         var result = await query.ToListAsync();
         result.Should().BeEmpty();
      }

      [Fact]
      public async Task Should_add_table_hints_to_table_without_touching_owned_entities()
      {
         var query = ActDbContext.TestEntities_Own_SeparateMany_SeparateMany.WithTableHints(SqlServerTableHint.NoLock);

         query.ToQueryString().Should().Be(@"SELECT [t].[Id], [t0].[TestEntity_Owns_SeparateMany_SeparateManyId], [t0].[Id], [t0].[IntColumn], [t0].[StringColumn], [t0].[OwnedEntity_Owns_SeparateManyTestEntity_Owns_SeparateMany_SeparateManyId], [t0].[OwnedEntity_Owns_SeparateManyId], [t0].[Id0], [t0].[IntColumn0], [t0].[StringColumn0]
FROM [_tests].[TestEntities_Own_SeparateMany_SeparateMany] AS [t] WITH (NOLOCK)
LEFT JOIN (
    SELECT [s].[TestEntity_Owns_SeparateMany_SeparateManyId], [s].[Id], [s].[IntColumn], [s].[StringColumn], [s0].[OwnedEntity_Owns_SeparateManyTestEntity_Owns_SeparateMany_SeparateManyId], [s0].[OwnedEntity_Owns_SeparateManyId], [s0].[Id] AS [Id0], [s0].[IntColumn] AS [IntColumn0], [s0].[StringColumn] AS [StringColumn0]
    FROM [_tests].[SeparateEntitiesMany_SeparateEntitiesMany] AS [s]
    LEFT JOIN [_tests].[SeparateEntitiesMany_SeparateEntitiesMany_Inner] AS [s0] ON ([s].[TestEntity_Owns_SeparateMany_SeparateManyId] = [s0].[OwnedEntity_Owns_SeparateManyTestEntity_Owns_SeparateMany_SeparateManyId]) AND ([s].[Id] = [s0].[OwnedEntity_Owns_SeparateManyId])
) AS [t0] ON [t].[Id] = [t0].[TestEntity_Owns_SeparateMany_SeparateManyId]
ORDER BY [t].[Id], [t0].[TestEntity_Owns_SeparateMany_SeparateManyId], [t0].[Id], [t0].[OwnedEntity_Owns_SeparateManyTestEntity_Owns_SeparateMany_SeparateManyId], [t0].[OwnedEntity_Owns_SeparateManyId], [t0].[Id0]");

         var result = await query.ToListAsync();
         result.Should().BeEmpty();
      }

      [Fact]
      public async Task Should_add_table_hints_to_temp_table()
      {
         ConfigureModel = builder => builder.ConfigureTempTable<Guid>();

         var tempTable = await ActDbContext.BulkInsertValuesIntoTempTableAsync(new[] { Guid.Empty });
         var query = tempTable.Query.WithTableHints(SqlServerTableHint.UpdLock, SqlServerTableHint.RowLock);

         query.ToQueryString().Should().Be(@"SELECT [#].[Column1]
FROM [#TempTable<Guid>_1] AS [#] WITH (UPDLOCK, ROWLOCK)");

         var result = await query.ToListAsync();
         result.Should().HaveCount(1);
         result[0].Column1.Should().Be(Guid.Empty);
      }
   }
}