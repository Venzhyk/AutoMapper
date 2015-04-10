using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using AutoMapper.Impl;

namespace AutoMapper.QueryableExtensions.Impl.QueryMapper
{
    public class QueryMapperVisitor : ExpressionVisitor
    {
        private readonly IQueryable _destQuery;
        private readonly ParameterExpression _instanceParameter;
        private readonly OrderByConverter _orderByConverter;
        public IMappingEngine MappingEngine { get; private set; }
        public Type SourceParameterType { get; private set; }
        public Type DestinationParameterType { get; private set; }

        public QueryMapperVisitor(Type sourceParameterType, Type destinationParameterType, IQueryable destQuery,
            IMappingEngine mappingEngine)
        {
            SourceParameterType = sourceParameterType;
            DestinationParameterType = destinationParameterType;
            _destQuery = destQuery;
            MappingEngine = mappingEngine;
            _instanceParameter = Expression.Parameter(destinationParameterType, "dto");
            _memberVisitor = new MemberAccessQueryMapperVisitor(this, MappingEngine);
            _orderByConverter = new OrderByConverter(this);
        }

        public static IQueryable<TDestination> Map<TSource, TDestination>(IQueryable<TSource> sourceQuery,
            IQueryable<TDestination> destQuery, IMappingEngine map)
        {
            var visitor = new QueryMapperVisitor(typeof(TSource), typeof(TDestination), destQuery, map);
            var expr = visitor.Visit(sourceQuery.Expression);

            var newDestQuery = destQuery.Provider.CreateQuery<TDestination>(expr);
            return newDestQuery;
        }

        private MemberAccessQueryMapperVisitor _memberVisitor;

        public override Expression Visit(Expression node)
        {
            // OData Client DataServiceQuery initial expression node type
            if (node != null && (int)node.NodeType == 10000)
            {
                return node;
            }
            var newNode = base.Visit(node);
            return newNode;
        }


        protected override Expression VisitConstant(ConstantExpression node)
        {
            var query = node.Value as IQueryable;
            // It is data source of queryable object instance
            if (query != null && query.ElementType == SourceParameterType)
                return _destQuery.Expression;
            return node;
        }


        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            var expressionGenType = typeof(Expression<>);

            var parameterDestType = ChangeLambdaArgTypeFormSourceToDest(node.Type, node.Body.Type);

            var nodeDestType = expressionGenType.MakeGenericType(parameterDestType);

            var destNode = MappingEngine.Map(node, node.GetType(), nodeDestType);
            return (Expression)destNode;

        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_orderByConverter.IsMatch(node))
            {
                return _orderByConverter.Convert(node);
            }

            var args = node.Arguments.Select(a => Visit(a)).ToList();
            var newObject = Visit(node.Object);
            var method = ChangeMethodArgTypeFormSourceToDest(node.Method);

            var newMethodCall = Expression.Call(newObject, method, args);
            return newMethodCall;
        }


        private MethodInfo ChangeMethodArgTypeFormSourceToDest(MethodInfo mi)
        {
            if (!mi.IsGenericMethod)
                return mi;
            var genericMethod = mi.GetGenericMethodDefinition();
            var methodArgs = mi.GetGenericArguments();
            methodArgs = methodArgs.Select(t => t.ReplaceItemType(SourceParameterType, DestinationParameterType)).ToArray();
            return genericMethod.MakeGenericMethod(methodArgs);

        }

        private Type ChangeLambdaArgTypeFormSourceToDest(Type lambdaType, Type returnType)
        {
            if (lambdaType.IsGenericType)
            {
                var genArgs = lambdaType.GetGenericArguments();
                var newGenArgs = genArgs.Select(t => t.ReplaceItemType(SourceParameterType, DestinationParameterType)).ToArray();
                var genericTypeDef = lambdaType.GetGenericTypeDefinition();
                if (genericTypeDef.FullName.StartsWith("System.Func"))
                {
                    newGenArgs[newGenArgs.Length - 1] = returnType;
                }
                return genericTypeDef.MakeGenericType(newGenArgs);
            }
            return lambdaType;
        }
    }
}
