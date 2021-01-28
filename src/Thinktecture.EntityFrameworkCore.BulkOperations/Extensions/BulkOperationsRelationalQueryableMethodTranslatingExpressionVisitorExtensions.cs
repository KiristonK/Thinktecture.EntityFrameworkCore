using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Thinktecture.EntityFrameworkCore.Query.SqlExpressions;

// ReSharper disable once CheckNamespace
namespace Thinktecture
{
   /// <summary>
   /// Extensions for <see cref="RelationalQueryableMethodTranslatingExpressionVisitor"/>.
   /// </summary>
   public static class BulkOperationsRelationalQueryableMethodTranslatingExpressionVisitorExtensions
   {
      /// <summary>
      /// Translates custom methods like <see cref="RelationalQueryableExtensions.AsSubQuery{TEntity}"/>.
      /// </summary>
      /// <param name="visitor">The visitor.</param>
      /// <param name="methodCallExpression">Method call to translate.</param>
      /// <param name="typeMappingSource">Type mapping source.</param>
      /// <returns>Translated method call if a custom method is found; otherwise <c>null</c>.</returns>
      /// <exception cref="ArgumentNullException">
      /// <paramref name="visitor"/> or <paramref name="methodCallExpression"/> is <c>null</c>.
      /// </exception>
      public static Expression? TranslateBulkMethods(
         this RelationalQueryableMethodTranslatingExpressionVisitor visitor,
         MethodCallExpression methodCallExpression,
         IRelationalTypeMappingSource typeMappingSource)
      {
         if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));
         if (methodCallExpression == null)
            throw new ArgumentNullException(nameof(methodCallExpression));

         if (methodCallExpression.Method.DeclaringType == typeof(BulkOperationsQueryableExtensions) &&
             methodCallExpression.Method.Name == nameof(BulkOperationsQueryableExtensions.BulkDelete))
         {
            var source = visitor.Visit(methodCallExpression.Arguments[0]);

            if (source is ShapedQueryExpression shapedQueryExpression)
               return TranslateBulkDelete(shapedQueryExpression, typeMappingSource);

            throw new InvalidOperationException(CoreStrings.TranslationFailed(methodCallExpression.Print()));
         }

         return null;
      }

      private static Expression TranslateBulkDelete(ShapedQueryExpression shapedQueryExpression, IRelationalTypeMappingSource typeMappingSource)
      {
         var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;
         selectExpression.ApplyProjection();

         if (selectExpression.Projection.Count == 0)
            throw new NotSupportedException("A DELETE statement is not supported if the table has no columns.");

         var firstProjectionExpression = selectExpression.Projection.First();

         if (firstProjectionExpression.Expression is not ColumnExpression columnExpression)
            throw new NotSupportedException($"The projection '{firstProjectionExpression.Print()}' was expected to be a column but is a '{firstProjectionExpression.GetType().Name}'.");

         selectExpression.ClearProjection();

         var intTypeMapping = typeMappingSource.FindMapping(typeof(int));
         var projectionMapping = new Dictionary<ProjectionMember, Expression> { { new ProjectionMember(), new DeleteExpression(columnExpression.Table, intTypeMapping) } };

         selectExpression.ReplaceProjectionMapping(projectionMapping);

         var conversionToInt = Expression.Convert(new ProjectionBindingExpression(shapedQueryExpression.QueryExpression, new ProjectionMember(), typeof(int)), typeof(int));

         return shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single)
                                     .UpdateShaperExpression(conversionToInt);
      }
   }
}