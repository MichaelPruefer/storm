﻿using System;
using System.Collections;
using System.Data;
using System.Linq;
using Eshava.Storm.Constants;
using Eshava.Storm.Interfaces;
using Eshava.Storm.Models;

namespace Eshava.Storm.Extensions
{
	internal static class TypeExtensions
	{
		private static readonly Type _typeOfString = typeof(string);
		private static readonly Type _typeOfDecimal = typeof(decimal);
		private static readonly Type _typeOfGuid = typeof(Guid);
		private static readonly Type _typeOfDateTime = typeof(DateTime);
		private static readonly Type _typeOfTimeSpan = typeof(TimeSpan);
		private static readonly Type _typeOfByteArray = typeof(byte[]);

		internal static DbType LookupDbType(this Type type, string name, bool demand, out ITypeHandler handler)
		{
			DbType dbType;
			handler = null;
			var nullUnderlyingType = Nullable.GetUnderlyingType(type);
			if (nullUnderlyingType != null)
			{
				type = nullUnderlyingType;
			}

			if (type.ImplementsIEnumerable())
			{
				type = type.GetDataTypeFromIEnumerable();
			}

			if (type.IsArray)
			{
				type = type.GetElementType();
			}

			if (type.IsEnum && !DbTypeMap.Map.ContainsKey(type))
			{
				type = Enum.GetUnderlyingType(type);
			}

			if (DbTypeMap.Map.TryGetValue(type, out dbType))
			{
				return dbType;
			}

			if (type.GetDataType() == _typeOfDateTime)
			{
				return Settings.EnableDateTimeHighAccuracy ? DbType.DateTime2 : DbType.DateTime;
			}

			if (type.FullName == DefaultNames.LINQBINARY)
			{
				return DbType.Binary;
			}

			if (TypeHandlerMap.Map.TryGetValue(type, out handler))
			{
				return DbType.Object;
			}
			
			if (demand)
			{
				throw new NotSupportedException($"The member {name} of type {type.FullName} cannot be used as a parameter value");
			}

			return DbType.Object;
		}

		internal static TypeCode GetTypeCode(Type type)
		{
			return Type.GetTypeCode(type);
		}

		internal static Type GetDataType(this Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			if (type.IsDataTypeNullable())
			{
				type = Nullable.GetUnderlyingType(type);
			}

			return type;
		}

		internal static bool IsDataTypeNullable(this Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
		}

		internal static bool IsClass(this Type type)
		{
			return type.IsClass
				&& !Equals(type, typeof(string))
				&& !type.ImplementsIEnumerable()
				;
		}

		internal static bool ImplementsIEnumerable(this Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			return type.IsGenericType && ImplementsInterface(type, typeof(IEnumerable));
		}

		internal static bool ImplementsInterface(this Type type, Type interfaceType)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			if (interfaceType == null)
			{
				throw new ArgumentNullException(nameof(interfaceType));
			}

			return type.GetInterfaces().Any(t => t == interfaceType);
		}

		internal static Type GetDataTypeFromIEnumerable(this Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			if (type.ImplementsIEnumerable())
			{
				type = type.GetGenericArguments()[0];
			}

			return type;
		}

		internal static bool IsNoClass(this Type type)
		{
			var propertyType = type.GetDataType();

			if (propertyType.IsPrimitive
				|| propertyType.IsEnum
				|| propertyType == _typeOfString
				|| propertyType == _typeOfDecimal
				|| propertyType == _typeOfGuid
				|| propertyType == _typeOfDateTime
				|| propertyType == _typeOfTimeSpan
				|| propertyType == _typeOfByteArray)
			{
				return true;
			}

			return false;
		}
	}
}