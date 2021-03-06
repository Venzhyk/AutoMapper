using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapper.Impl;

namespace AutoMapper.QueryableExtensions.Impl.QueryMapper
{
    public class SourceInjectedQueryProvider<TSource, TDestination> : IQueryProvider
    {
        private readonly IQueryable<TDestination> _rootQuery;
        private readonly IMappingEngine _mappingEngine;
        private readonly IQueryable<TSource> _dataSource;

        public SourceInjectedQueryProvider(IQueryable<TDestination> rootQuery,
            IMappingEngine mappingEngine,
            IQueryable<TSource> dataSource)
        {
            _rootQuery = rootQuery;
            _mappingEngine = mappingEngine;
            _dataSource = dataSource;
        }

        public SourceInjectedQueryInspector Inspector { get; set; }

        public IQueryable CreateQuery(Expression expression)
        {
            return new SourceInjectedQuery<TSource, TDestination>(this, expression, _dataSource);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return (IQueryable<TElement>)
                new SourceInjectedQuery<TSource, TDestination>(this, expression, _dataSource);
        }

        public object Execute(Expression expression)
        {
            Inspector.StartQueryExecuteInterceptor(null, expression);

            var sourceExpression = ConvertDestinationExpressionToSourceExpression(expression);
            var sourceResult = InvokeSourceQuery(null, sourceExpression);

            Inspector.SourceResult(sourceExpression, sourceResult);
            return sourceResult;
        }

        public TResult Execute<TResult>(Expression expression)
        {
            Inspector.StartQueryExecuteInterceptor(typeof(TResult), expression);

            var sourceExpression = ConvertDestinationExpressionToSourceExpression(expression);

            var destResultType = typeof(TResult);
            var sourceResultType = CreateSourceResultType(destResultType);

            var sourceResult = InvokeSourceQuery(sourceResultType, sourceExpression);

            Inspector.SourceResult(sourceExpression, sourceResult);

            var destResult = _mappingEngine.Map(sourceResult, sourceResultType, destResultType);
            Inspector.DestResult(sourceResult);

            return (TResult)destResult;
        }

        private object InvokeSourceQuery(Type sourceResultType, Expression sourceExpression)
        {
            MethodInfo executeMi = null;
            if (sourceResultType != null)
            {
                var mi = typeof(IQueryProvider)
                    .GetMethods()
                    .First(m => m.IsGenericMethod && m.Name == "Execute");

                executeMi = mi.MakeGenericMethod(sourceResultType);
            }
            else
                executeMi = typeof(IQueryProvider)
                        .GetMethods()
                        .First(m => !m.IsGenericMethod && m.Name == "Execute");

            var result = executeMi.Invoke(_dataSource.Provider, new object[] { sourceExpression });
            return result;
        }

        private static Type CreateSourceResultType(Type destResultType)
        {
            var sourceResultType = destResultType.ReplaceItemType(typeof(TDestination), typeof(TSource));
            return sourceResultType;
        }

        public Expression ConvertDestinationExpressionToSourceExpression(Expression expression)
        {
            var visitor = new QueryMapperVisitor(typeof(TDestination),
                typeof(TSource), _dataSource, _mappingEngine);
            var sourceExpression = visitor.Visit(expression);
            return sourceExpression;
        }
    }
}