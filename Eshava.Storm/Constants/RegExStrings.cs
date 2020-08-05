﻿using System.Text.RegularExpressions;

namespace Eshava.Storm.Constants
{
	internal static class RegExStrings
	{
		public static readonly Regex LiteralTokens = new Regex(@"(?<![a-z0-9_])\{=([a-z0-9_]+)\}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

		public static readonly Regex TablesAliases = new Regex(@"(\bFROM\b|\bJOIN\b){1,1}\s*\b(\S+)\b\s*\b(?!ON\b)\S+?\b\s*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	}
}