﻿using System;
using System.Data;

namespace Eshava.Storm.Handler
{
	public abstract class StringTypeHandler<T> : TypeHandler<T>
	{
		/// <summary>
		/// Parse a string into the expected type (the string will never be null)
		/// </summary>
		/// <param name="xml">The string to parse.</param>
		protected abstract T Parse(string xml);

		/// <summary>
		/// Format an instance into a string (the instance will never be null)
		/// </summary>
		/// <param name="xml">The string to format.</param>
		protected abstract string Format(T xml);

		/// <summary>
		/// Assign the value of a parameter before a command executes
		/// </summary>
		/// <param name="parameter">The parameter to configure</param>
		/// <param name="value">Parameter value</param>
		public override void SetValue(IDbDataParameter parameter, T value)
		{
			parameter.Value = value == null ? (object)DBNull.Value : Format(value);
		}

		/// <summary>
		/// Parse a database value back to a typed value
		/// </summary>
		/// <param name="value">The value from the database</param>
		/// <returns>The typed value</returns>
		public override T Parse(object value)
		{
			if (value == null || value is DBNull)
			{
				return default(T);
			}

			return Parse((string)value);
		}
	}
}