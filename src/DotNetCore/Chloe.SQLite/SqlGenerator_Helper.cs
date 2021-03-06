﻿using Chloe.Core;
using Chloe.DbExpressions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Chloe.SQLite
{
    partial class SqlGenerator : DbExpressionVisitor<DbExpression>
    {
        static string GenParameterName(int ordinal)
        {
            if (ordinal < CacheParameterNames.Count)
            {
                return CacheParameterNames[ordinal];
            }

            return ParameterPrefix + ordinal.ToString();
        }

        static Stack<DbExpression> GatherBinaryExpressionOperand(DbBinaryExpression exp)
        {
            DbExpressionType nodeType = exp.NodeType;

            Stack<DbExpression> items = new Stack<DbExpression>();
            items.Push(exp.Right);

            DbExpression left = exp.Left;
            while (left.NodeType == nodeType)
            {
                exp = (DbBinaryExpression)left;
                items.Push(exp.Right);
                left = exp.Left;
            }

            items.Push(left);
            return items;
        }
        static void EnsureMethodDeclaringType(DbMethodCallExpression exp, Type ensureType)
        {
            if (exp.Method.DeclaringType != ensureType)
                throw UtilExceptions.NotSupportedMethod(exp.Method);
        }
        static void EnsureMethod(DbMethodCallExpression exp, MethodInfo methodInfo)
        {
            if (exp.Method != methodInfo)
                throw UtilExceptions.NotSupportedMethod(exp.Method);
        }


        static void EnsureTrimCharArgumentIsSpaces(DbExpression exp)
        {
            var m = exp as DbMemberExpression;
            if (m == null)
                throw new NotSupportedException();

            DbParameterExpression p;
            if (!DbExpressionExtensions.TryParseToParameterExpression(m, out p))
            {
                throw new NotSupportedException();
            }

            var arg = p.Value;

            if (arg == null)
                throw new NotSupportedException();

            var chars = arg as char[];
            if (chars.Length != 1 || chars[0] != ' ')
            {
                throw new NotSupportedException();
            }
        }
        static bool TryGetCastTargetDbTypeString(Type sourceType, Type targetType, out string dbTypeString, bool throwNotSupportedException = true)
        {
            dbTypeString = null;

            sourceType = Utils.GetUnderlyingType(sourceType);
            targetType = Utils.GetUnderlyingType(targetType);

            if (sourceType == targetType)
                return false;

            if (CastTypeMap.TryGetValue(targetType, out dbTypeString))
            {
                return true;
            }

            if (throwNotSupportedException)
                throw new NotSupportedException(AppendNotSupportedCastErrorMsg(sourceType, targetType));
            else
                return false;
        }
        static string AppendNotSupportedCastErrorMsg(Type sourceType, Type targetType)
        {
            return string.Format("Does not support the type '{0}' converted to type '{1}'.", sourceType.FullName, targetType.FullName);
        }

        static void DbFunction_DATEADD(SqlGenerator generator, string interval, DbMethodCallExpression exp)
        {
            /* DATETIME(@P_0,'+' || 1 || ' years') */

            generator._sqlBuilder.Append("DATETIME(");
            exp.Object.Accept(generator);
            generator._sqlBuilder.Append(",'+' || ");
            exp.Arguments[0].Accept(generator);
            generator._sqlBuilder.Append(" || ' ", interval, "'");
            generator._sqlBuilder.Append(")");
        }
        static void DbFunction_DATEPART(SqlGenerator generator, string interval, DbExpression exp)
        {
            /* CAST(STRFTIME('%M','2016-08-06 09:01:24') AS INTEGER) */
            generator._sqlBuilder.Append("CAST(");
            generator._sqlBuilder.Append("STRFTIME('%", interval, "',");
            exp.Accept(generator);
            generator._sqlBuilder.Append(")");
            generator._sqlBuilder.Append(" AS INTEGER)");
        }

        #region AggregateFunction
        static void Aggregate_Count(SqlGenerator generator)
        {
            generator._sqlBuilder.Append("COUNT(1)");
        }
        static void Aggregate_LongCount(SqlGenerator generator)
        {
            generator._sqlBuilder.Append("COUNT(1)");
        }
        static void Aggregate_Sum(SqlGenerator generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunctionWithCast(generator, exp, retType, "SUM");
        }
        static void Aggregate_Max(SqlGenerator generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunctionWithCast(generator, exp, retType, "MAX");
        }
        static void Aggregate_Min(SqlGenerator generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunctionWithCast(generator, exp, retType, "MIN");
        }
        static void Aggregate_Average(SqlGenerator generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunctionWithCast(generator, exp, retType, "AVG");
        }

        static void AppendAggregateFunctionWithCast(SqlGenerator generator, DbExpression exp, Type retType, string functionName)
        {
            Type unType = Utils.GetUnderlyingType(retType);

            string dbTypeString = null;
            if (unType != UtilConstants.TypeOfDecimal/* We don't know the precision and scale,so,we can not cast exp to decimal,otherwise cause problems. */ && CastTypeMap.TryGetValue(unType, out dbTypeString))
            {
                generator._sqlBuilder.Append("CAST(");
            }

            generator._sqlBuilder.Append(functionName, "(");
            exp.Accept(generator);
            generator._sqlBuilder.Append(")");

            if (dbTypeString != null)
            {
                generator._sqlBuilder.Append(" AS ", dbTypeString, ")");
            }
        }
        #endregion

    }
}
