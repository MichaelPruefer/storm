﻿using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Eshava.Storm.Extensions;
using Eshava.Storm.MetaData.Constants;
using Eshava.Storm.MetaData.Enums;

namespace Eshava.Storm.MetaData.Models
{
	internal class Property
	{
		internal Property(string name, Type type, PropertyInfo propertyInfo, ConfigurationSource configurationSource)
		{
			Name = name;
			Type = type;
			PropertyInfo = propertyInfo;
			ConfigurationSource = configurationSource;
			IsNoClass = type.IsNoClass();

			AutoGeneratedOption = DatabaseGeneratedOption.None;
		}

		public string Name { get; }
		public Type Type { get; }
		public PropertyInfo PropertyInfo { get; }
		public ConfigurationSource ConfigurationSource { get; private set; }

		public DatabaseGeneratedOption AutoGeneratedOption { get; private set; }
		public bool IsPrimaryKey { get; private set; }
		public bool IsOwnsOne { get; private set; }
		public bool IsNoClass { get; set; }
		public bool IsIgnored { get; private set; }
		public OwnsOneEntity OwnsOne { get; private set; }
		public string ColumnName { get; private set; }


		public void SetPrimiaryKey(bool isPrimaryKey, ConfigurationSource configurationSource)
		{
			IsPrimaryKey = isPrimaryKey;
			ConfigurationSource = configurationSource;
		}

		public void Ignore()
		{
			IsIgnored = true;
		}

		public void SetOwnsOneEntity(OwnsOneEntity entity)
		{
			IsOwnsOne = entity != default;
			OwnsOne = entity;
			AutoGeneratedOption = DatabaseGeneratedOption.None;
		}

		public void SetColumnName(string columnName)
		{
			ColumnName = columnName;
		}

		public void SetAutoGeneratedOption(DatabaseGeneratedOption option)
		{
			if (IsOwnsOne && option != DatabaseGeneratedOption.None)
			{
				throw new ArgumentException(
					String.Format(
						MessageConstants.InvalidPropertyOption,
						Name,
						nameof(DatabaseGeneratedOption),
						option,
						"OwnsOne Properties"
					)
				);
			}

			AutoGeneratedOption = option;
		}
	}
}