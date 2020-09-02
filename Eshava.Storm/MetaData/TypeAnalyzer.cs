﻿using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Eshava.Storm.Extensions;
using Eshava.Storm.MetaData.Builders;
using Eshava.Storm.MetaData.Interfaces;
using Eshava.Storm.MetaData.Models;

namespace Eshava.Storm.MetaData
{
	public static class TypeAnalyzer
	{
		public static void AddType<TEntity>(IEntityTypeConfiguration<TEntity> configuration) where TEntity : class
		{
			var builder = new EntityTypeBuilder<TEntity>();
			configuration.Configure(builder);

			AnalyzeType(typeof(TEntity));
		}

		public static string GetTableName<TEntity>() where TEntity : class
		{
			var type = typeof(TEntity);
			var entity = EntityCache.GetEntity(type);

			if (entity == default)
			{
				entity = AnalyzeType(type);
			}

			return entity.TableName;
		}

		internal static Entity AnalyzeType(Type type)
		{
			var entity = EntityCache.GetEntity(type);

			if (entity == default)
			{
				entity = new Entity(type, Enums.ConfigurationSource.DataAnnotation);
				EntityCache.AddEntity(entity);
			}

			AnalyzeType(entity);

			if (entity.TableName.IsNullOrEmpty())
			{
				entity.SetTableName(GetTableName(type));
			}

			if (!entity.HasPrimaryKey())
			{
				DeterminePrimaryKeyByConvention(entity);
			}

			return entity;
		}

		private static void AnalyzeType(AbstractEntity entity)
		{
			foreach (var propertyInfo in entity.Type.GetProperties())
			{
				var property = default(Property);

				if (!propertyInfo.CanWrite || !propertyInfo.CanRead)
				{
					continue;
				}

				var propertyType = propertyInfo.PropertyType.GetDataType();

				if (propertyType.IsNoClass())
				{
					property = entity.GetProperty(propertyInfo.Name);
					if (property == default)
					{
						property = new Property(propertyInfo.Name, propertyInfo.PropertyType, propertyInfo, Enums.ConfigurationSource.Convention);
						entity.AddProperty(property);
					}

					if (!property.IsPrimaryKey && propertyInfo.IsPrímaryKey())
					{
						property.SetPrimiaryKey(true, Enums.ConfigurationSource.DataAnnotation);
					}

					if (property.AutoGeneratedOption == DatabaseGeneratedOption.None)
					{
						if (property.IsPrimaryKey)
						{
							property.SetAutoGeneratedOption(IsAutoGenerated(propertyInfo));
						}
						else
						{
							property.SetAutoGeneratedOption(propertyInfo.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption ?? DatabaseGeneratedOption.None);
						}
					}
				}
				else if (propertyType.IsClass() || Storm.Models.TypeHandlerMap.Map.ContainsKey(propertyType))
				{
					property = entity.GetProperty(propertyInfo.Name);
					if (property == default || !property.IsOwnsOne)
					{
						if (propertyInfo.IsOwnsOne())
						{
							if (property == default)
							{
								property = new Property(propertyInfo.Name, propertyInfo.PropertyType, propertyInfo, Enums.ConfigurationSource.DataAnnotation);
								entity.AddProperty(property);
							}

							var ownsOneEntity = new OwnsOneEntity(propertyInfo.PropertyType, Enums.ConfigurationSource.DataAnnotation);
							property.SetOwnsOneEntity(ownsOneEntity);
							AnalyzeType(ownsOneEntity);
						}
					}

					if (property == default || !property.IsOwnsOne)
					{
						propertyInfo.PropertyType.LookupDbType("", false, out var _);
						if (Storm.Models.TypeHandlerMap.Map.ContainsKey(propertyInfo.PropertyType))
						{
							property = entity.GetProperty(propertyInfo.Name);
							if (property == default)
							{
								property = new Property(propertyInfo.Name, propertyInfo.PropertyType, propertyInfo, Enums.ConfigurationSource.DataAnnotation);
								entity.AddProperty(property);
							}
						}
					}
				}

				if (property != default && property.ColumnName.IsNullOrEmpty())
				{
					property.SetColumnName(propertyInfo.GetColumnName());
				}
			}
		}

		private static DatabaseGeneratedOption IsAutoGenerated(PropertyInfo propertyInfo)
		{
			var databaseGenerated = propertyInfo.GetCustomAttribute<DatabaseGeneratedAttribute>();
			if (databaseGenerated != default)
			{
				return databaseGenerated.DatabaseGeneratedOption;
			}

			return Settings.DefaultKeyColumnValueGeneration;
		}

		private static string GetTableName(Type type)
		{
			var tableAttribute = type.GetCustomAttribute<TableAttribute>();

			if (tableAttribute != default)
			{
				if (!tableAttribute.Schema.IsNullOrEmpty())
				{
					return $"[{tableAttribute.Schema}].[{tableAttribute.Name}]";
				}

				return $"[{tableAttribute.Name}]";
			}

			if (type.Name.ToLower().EndsWith("y"))
			{
				return $"[{type.Name.Substring(0, type.Name.Length - 1)}ies]";
			}

			if (type.Name.ToLower().EndsWith("s") || type.Name.ToLower().EndsWith("x"))
			{
				return $"[{type.Name}es]";
			}

			return $"[{type.Name}s]";
		}

		private static void DeterminePrimaryKeyByConvention(Entity entity)
		{
			var property = entity.GetProperty("Id") ?? entity.GetProperty("id");

			if (property != default)
			{
				property.SetPrimiaryKey(true, Enums.ConfigurationSource.Convention);
			}
		}
	}
}