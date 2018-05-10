using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SpanCsv
{
    class MemberAccessCacher : ExpressionVisitor
    {
        public Dictionary<string, ParameterExpression> Cache {get;} = new Dictionary<string, ParameterExpression>();
        public List<Expression> Assignments {get;} = new List<Expression>();

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is MemberExpression innerAccess)
            {
                var accessorStr = innerAccess.ToString();
                if (!Cache.TryGetValue(accessorStr, out var accessorVar))
                {
                    accessorVar = Expression.Parameter(innerAccess.Type);
                    
                    Assignments.Add(Expression.Assign(accessorVar, VisitMember(innerAccess)));
                    Cache.Add(accessorStr, accessorVar);
                }

                return Expression.MakeMemberAccess(accessorVar, node.Member);
            }

            return base.VisitMember(node);
        }
    }
}
