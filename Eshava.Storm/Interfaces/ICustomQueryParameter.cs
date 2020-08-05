﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Eshava.Storm.Interfaces
{
	/// <summary>
	/// Implement this interface to pass an arbitrary db specific parameter to Storm
	/// </summary>
	public interface ICustomQueryParameter
	{
		/// <summary>
		/// Add the parameter needed to the command before it executes
		/// </summary>
		/// <param name="command">The raw command prior to execution</param>
		/// <param name="name">Parameter name</param>
		void AddParameter(IDbCommand command, string name);
	}
}
