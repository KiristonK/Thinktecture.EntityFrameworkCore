using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using Thinktecture.EntityFrameworkCore.Query.SqlExpressions;

namespace Thinktecture.EntityFrameworkCore.Query
{
   /// <inheritdoc />
   [SuppressMessage("ReSharper", "EF1001")]
   public class ThinktectureSqliteQuerySqlGenerator : SqliteQuerySqlGenerator
   {
      /// <inheritdoc />
      public ThinktectureSqliteQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
         : base(dependencies)
      {
      }

      /// <inheritdoc />
      protected override Expression VisitSelect(SelectExpression selectExpression)
      {
         if (selectExpression.TryGetDeleteExpression(out var deleteExpression))
            return GenerateDeleteStatement(selectExpression, deleteExpression);

         return base.VisitSelect(selectExpression);
      }

      private Expression GenerateDeleteStatement(SelectExpression selectExpression, DeleteExpression deleteExpression)
      {
         if (selectExpression.IsDistinct)
            throw new NotSupportedException("A DISTINCT clause is not supported in a DELETE statement.");

         if (selectExpression.Tables.Count == 0)
            throw new NotSupportedException("A DELETE statement without any tables is invalid.");

         if (selectExpression.Tables.Count != 1)
            throw new NotSupportedException("SQLite supports only 1 outermost table in a DELETE statement.");

         if (selectExpression.GroupBy.Count > 0)
            throw new NotSupportedException("A GROUP BY clause is not supported in a DELETE statement.");

         if (selectExpression.Having is not null)
            throw new NotSupportedException("A HAVING clause is not supported in a DELETE statement.");

         if (selectExpression.Offset is not null)
            throw new NotSupportedException("An OFFSET clause (i.e. Skip(x)) is not supported in a DELETE statement.");

         if (selectExpression.Limit is not null)
            throw new NotSupportedException("A TOP/LIMIT clause (i.e. Take(x)) is not supported in a DELETE statement.");

         Sql.Append("DELETE FROM ");

         Visit(selectExpression.Tables[0]);

         if (selectExpression.Predicate is not null)
         {
            Sql.AppendLine().Append("WHERE ");
            Visit(selectExpression.Predicate);
         }

         Sql.Append(Dependencies.SqlGenerationHelper.StatementTerminator).AppendLine()
            .Append("SELECT CHANGES()").Append(Dependencies.SqlGenerationHelper.StatementTerminator);

         return selectExpression;
      }
   }
}