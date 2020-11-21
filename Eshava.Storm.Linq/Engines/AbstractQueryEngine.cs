﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Eshava.Storm.Linq.Extensions;
using Eshava.Storm.Linq.Models;

namespace Eshava.Storm.Linq.Engines
{
	internal abstract class AbstractQueryEngine
	{
		private const string PARAMETER_PLACEHOLDER = "###";
		private const string METHOD_CONTAINS = "contains";
		private const string METHOD_ANY = "any";
		private const string METHOD_STARTSWITH = "startswith";
		private const string METHOD_ENDSWITH = "endswith";
		private const string METHOD_COMPARETO = "compareto";
		private const string METHOD_CONTAINEDIN = "containedin";

		protected const string SQL_AND = "AND";

		private static Dictionary<ExpressionType, string> _expressionTypeMappings = new Dictionary<ExpressionType, string>
		{
			{  ExpressionType.And, SQL_AND },
			{  ExpressionType.AndAlso, SQL_AND },
			{  ExpressionType.Equal, "=" },
			{  ExpressionType.NotEqual, "!=" },
			{  ExpressionType.GreaterThan, ">" },
			{  ExpressionType.GreaterThanOrEqual, ">=" },
			{  ExpressionType.LessThan, "<" },
			{  ExpressionType.LessThanOrEqual, "<=" },
			{  ExpressionType.Or, "OR" },
			{  ExpressionType.OrElse, "OR" }
		};

		protected string ProcessExpression(Expression expression, WhereQueryData data)
		{
			var unaryExpression = expression as UnaryExpression;
			if (unaryExpression != default && expression.NodeType == ExpressionType.Not)
			{
				return ProcessUnaryExpressionNot(unaryExpression, data);
			}

			if (unaryExpression != default && expression.NodeType == ExpressionType.Convert)
			{
				return ProcessUnaryExpressionConvert(unaryExpression, data);
			}

			var binaryExpression = expression as BinaryExpression;
			if (binaryExpression != default)
			{
				return ProcessBinaryExpression(binaryExpression, data);
			}

			var memberExpression = expression as MemberExpression;
			if (memberExpression != default && expression.NodeType == ExpressionType.MemberAccess)
			{
				if (memberExpression.Expression.NodeType == ExpressionType.Constant)
				{
					return ProcessDisplayClassConstantExpression(memberExpression, data);
				}

				return ProcessMemberExpression(memberExpression, data);
			}

			var constantExpression = expression as ConstantExpression;
			if (expression.NodeType == ExpressionType.Constant)
			{
				return ProcessConstantExpression(constantExpression, data);
			}

			var methodCallExpression = expression as MethodCallExpression;
			if (methodCallExpression != default)
			{
				return ProcessMethodCallExpression(methodCallExpression, data);
			}

			if (expression.NodeType == ExpressionType.Parameter)
			{
				return ProcessParameterExpression(expression as ParameterExpression, data);
			}

			return "";
		}

		protected string MapPropertyPath(QuerySettings data, string propertyName)
		{
			if (propertyName.IsNullOrEmpty() || !propertyName.StartsWith("."))
			{
				return propertyName;
			}

			if (!data.PropertyMappings.ContainsKey(propertyName))
			{
				if (data.PropertyMappings.ContainsKey("."))
				{
					return data.PropertyMappings["."] + propertyName;
				}

				return propertyName.Substring(1);
			}

			return data.PropertyMappings[propertyName];
		}

		private string ProcessBinaryExpression(BinaryExpression binaryExpression, WhereQueryData data)
		{
			var left = ProcessExpression(binaryExpression.Left, data);
			var right = ProcessExpression(binaryExpression.Right, data);

			if (!_expressionTypeMappings.ContainsKey(binaryExpression.NodeType))
			{
				// Skip unsupported expression 

				return "";
			}

			var nodeType = _expressionTypeMappings[binaryExpression.NodeType];

			if (left == "")
			{
				// Inner lamba expression 

				return $"({PARAMETER_PLACEHOLDER} {nodeType} {MapPropertyPath(data, right)})";
			}
			else if (right == "")
			{
				// Inner lamba expression 

				return $"({MapPropertyPath(data, left)} {nodeType} {PARAMETER_PLACEHOLDER})";
			}

			// Special case handling for NULL and NOT NULL 
			if (binaryExpression.NodeType == ExpressionType.Equal && right == null)
			{
				nodeType = "IS";
				right = "NULL";
			}
			else if (binaryExpression.NodeType == ExpressionType.NotEqual && right == null)
			{
				nodeType = "IS NOT";
				right = "NULL";
			}

			return $"({MapPropertyPath(data, left)} {nodeType} {right})";
		}

		private string ProcessMemberExpression(MemberExpression memberExpression, WhereQueryData data)
		{
			var property = memberExpression.Member.Name;
			var memberDataType = GetDataType(memberExpression.Member);

			if (memberDataType != default && data.PropertyTypeMappings.ContainsKey(memberDataType))
			{
				return data.PropertyTypeMappings[memberDataType];
			}

			var parent = ProcessExpression(memberExpression.Expression, data);

			return $"{parent}.{property}";
		}

		private string ProcessParameterExpression(ParameterExpression parameterExpression, WhereQueryData data)
		{
			if (data.PropertyTypeMappings.ContainsKey(parameterExpression.Type))
			{
				return data.PropertyTypeMappings[parameterExpression.Type];
			}

			return "";
		}

		private string ProcessDisplayClassConstantExpression(MemberExpression memberExpression, WhereQueryData data)
		{
			var constantExpression = memberExpression.Expression as ConstantExpression;
			if (constantExpression.Value == default)
			{
				return null;
			}

			var value = GetValueFromDisplayClass(memberExpression.Member, constantExpression);
			var newConstantExpression = Expression.Constant(value, memberExpression.Type);

			return ProcessExpression(newConstantExpression, data);
		}

		private string ProcessUnaryExpressionConvert(UnaryExpression unaryExpression, WhereQueryData data)
		{
			return ProcessExpression(unaryExpression.Operand, data);
		}

		private string ProcessUnaryExpressionNot(UnaryExpression unaryExpression, WhereQueryData data)
		{
			return $"NOT({ProcessExpression(unaryExpression.Operand, data)})";
		}

		private string ProcessConstantExpression(ConstantExpression constantExpression, WhereQueryData data)
		{
			if (constantExpression.Value == null)
			{
				return null;
			}

			var parameterName = "@p" + data.Index;
			if (constantExpression.Value.GetType().GetDataType().IsEnum)
			{
				data.QueryParameter.Add("p" + data.Index, Convert.ToInt32(constantExpression.Value));
			}
			else
			{
				data.QueryParameter.Add("p" + data.Index, constantExpression.Value);
			}

			data.Index++;

			return parameterName;
		}

		private string ProcessMethodCallExpression(MethodCallExpression methodCallExpression, WhereQueryData data)
		{
			var method = methodCallExpression.Method.Name.ToLower();
			var rawProperty = "";
			var value = "";

			if (method == METHOD_ANY)
			{
				return ProcessMethodCallExpressionAny(methodCallExpression, data);
			}

			if (method == METHOD_CONTAINS && methodCallExpression.Arguments.Count == 2)
			{
				//DisplayClass
				rawProperty = ProcessExpression(methodCallExpression.Arguments.First(), data);
				value = ProcessExpression(methodCallExpression.Arguments.Last(), data);
			}
			else
			{
				rawProperty = ProcessExpression(methodCallExpression.Object, data);
				value = ProcessExpression(methodCallExpression.Arguments.First(), data);
			}

			var property = "";
			if (method == METHOD_CONTAINS && rawProperty.StartsWith("@"))
			{
				rawProperty = rawProperty.Replace("@", "");
				property = MapPropertyPath(data, value);
				method = METHOD_CONTAINEDIN;

				data.QueryParameter.Add(rawProperty + "Array", data.QueryParameter[rawProperty]);
				data.QueryParameter.Remove(rawProperty);
				value = $"@{rawProperty}Array";
			}
			else
			{
				property = MapPropertyPath(data, rawProperty);
			}

			switch (method)
			{
				case METHOD_CONTAINS:
					ManipulateParameterValue(data, value, v => $"%{v}%");

					return $"{property} LIKE {value}";
				case METHOD_STARTSWITH:
					ManipulateParameterValue(data, value, v => $"{v}%");

					return $"{property} LIKE {value}";
				case METHOD_ENDSWITH:
					ManipulateParameterValue(data, value, v => $"%{v}");

					return $"{property} LIKE {value}";
				case METHOD_COMPARETO:
					// EF Core compatibility

					return $"(CASE WHEN {property} = {value} THEN 0 WHEN {property} > {value} THEN 1 ELSE -1 END)";
				case METHOD_CONTAINEDIN:

					return $"{property} IN {value}";
			}

			return "";
		}

		private string ProcessMethodCallExpressionAny(MethodCallExpression methodCallExpression, WhereQueryData data)
		{
			// Display Class
			var memberExpression = methodCallExpression.Arguments.First() as MemberExpression;
			var constantExpression = memberExpression.Expression as ConstantExpression;
			var enumerable = GetValueFromDisplayClass(memberExpression.Member, constantExpression) as System.Collections.IEnumerable;

			// Inner function expression
			var lambdaExpression = methodCallExpression.Arguments.Last() as LambdaExpression;
			var lambdaAsQuery = ProcessExpression(lambdaExpression.Body, data);

			var queryParts = new List<string>();
			foreach (var item in enumerable)
			{
				var parameterName = "p" + data.Index;
				data.QueryParameter.Add(parameterName, item);
				data.Index++;

				queryParts.Add(lambdaAsQuery.Replace(PARAMETER_PLACEHOLDER, "@" + parameterName));
			}

			return $"({String.Join(" OR ", queryParts) })";
		}

		private void ManipulateParameterValue(WhereQueryData data, string parameterName, Func<object, string> manipulate)
		{
			parameterName = parameterName.Replace("@", "");

			data.QueryParameter[parameterName] = manipulate(data.QueryParameter[parameterName]);
		}

		private object GetValueFromDisplayClass(MemberInfo memberInfo, ConstantExpression constantExpression)
		{
			var value = default(object);
			if (memberInfo.MemberType == MemberTypes.Field)
			{
				value = ((FieldInfo)memberInfo).GetValue(constantExpression.Value);
			}
			else if (memberInfo.MemberType == MemberTypes.Property)
			{
				value = ((PropertyInfo)memberInfo).GetValue(constantExpression.Value);
			}

			return value;
		}

		private Type GetDataType(MemberInfo memberInfo)
		{
			if (memberInfo.MemberType == MemberTypes.Field)
			{
				return ((FieldInfo)memberInfo).FieldType;
			}
			else if (memberInfo.MemberType == MemberTypes.Property)
			{
				return ((PropertyInfo)memberInfo).PropertyType;
			}

			return null;
		}

	}
}
