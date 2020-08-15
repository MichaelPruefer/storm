﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eshava.Storm.Extensions;
using Eshava.Storm.Interfaces;
using Eshava.Storm.MetaData;
using Eshava.Storm.MetaData.Models;
using Eshava.Storm.Models;

namespace Eshava.Storm.Engines
{
	internal class CRUDCommandEngine : AbstractCRUDCommandEngine
	{
		private readonly IObjectGenerator _objectGenerator;

		public CRUDCommandEngine(IObjectGenerator objectGenerator)
		{
			_objectGenerator = objectGenerator;
		}

		public void ProcessInsertRequest<T>(CommandDefinition<T> commandDefinition) where T : class
		{
			var type = CheckCommandConditions(commandDefinition, "insert");

			var entityTypeResult = EntityCache.GetEntity(type) ?? TypeAnalyzer.AnalyzeType(type);
			if (!entityTypeResult.HasPrimaryKey())
			{
				throw new ArgumentException("At least one key column property must be defined.");
			}

			var properties = GetProperties(new PropertyRequest
			{
				Type = type,
				Entity = commandDefinition.Entity
			});


			var sql = new StringBuilder();
			var sqlColumns = new StringBuilder();
			var sqlValues = new StringBuilder();

			var parameters = new List<KeyValuePair<string, object>>();
			var firstColumn = true;
			foreach (var property in properties)
			{
				if (firstColumn)
				{
					firstColumn = false;
				}
				else
				{
					sqlColumns.Append(",");
					sqlValues.Append(",");
				}

				sqlValues.Append("@");
				if (!property.Prefix.IsNullOrEmpty())
				{
					sqlColumns.Append(property.Prefix);
					sqlValues.Append(property.Prefix);
				}

				sqlColumns.Append(property.ColumnName);
				sqlValues.Append(property.PropertyInfo.Name);

				parameters.Add(new KeyValuePair<string, object>($"{property.Prefix}{property.PropertyInfo.Name}", property.PropertyInfo.GetValue(property.Entity)));
			}

			sql.Append("INSERT INTO ");
			sql.Append(entityTypeResult.TableName);
			sql.Append("(");
			sql.Append(sqlColumns.ToString());
			sql.AppendLine(")");
			sql.Append("VALUES (");
			sql.Append(sqlValues.ToString());
			sql.AppendLine(");");

			if (entityTypeResult.HasAutoGeneratedPrimaryKey())
			{
				sql.AppendLine("SELECT SCOPE_IDENTITY();");
			}
			else
			{
				sql.Append("SELECT @");
				sql.Append(entityTypeResult.GetFirstPrimaryKey().Name);
				sql.AppendLine(";");
			}

			commandDefinition.UpdateCommand(sql.ToString(), parameters);
		}

		public void ProcessUpdateRequest<T>(CommandDefinition<T> commandDefinition, object partialEntity = null) where T : class
		{
			var type = CheckCommandConditions(commandDefinition, "update", partialEntity);
		
			var entityTypeResult = EntityCache.GetEntity(type) ?? TypeAnalyzer.AnalyzeType(type);
			var keyColumns = GetKeyColumns(type, partialEntity?.GetType());

			if (!keyColumns.Any())
			{
				throw new ArgumentException("At least one key column property must be defined.");
			}

			if (partialEntity != default)
			{
				commandDefinition.Entity = _objectGenerator.CreateEmptyInstance<T>();
			}

			var properties = GetProperties(new PropertyRequest
			{
				Type = type,
				Entity = commandDefinition.Entity,
				PartialEntity = partialEntity
			});

			var sql = new StringBuilder();
			var parameters = new List<KeyValuePair<string, object>>();

			sql.Append("UPDATE ");
			sql.AppendLine(entityTypeResult.TableName);
			sql.AppendLine(" SET");

			var firstColumn = true;
			foreach (var property in properties)
			{
				if (SkipPropertyForUpdate(property, keyColumns))
				{
					continue;
				}

				sql.Append("\t");
				if (firstColumn)
				{
					firstColumn = false;
					sql.Append(" ");
				}
				else
				{
					sql.Append(",");
				}

				if (!property.Prefix.IsNullOrEmpty())
				{
					sql.Append(property.Prefix);
				}
								
				sql.Append(property.ColumnName);
				sql.Append(" = @");

				if (!property.Prefix.IsNullOrEmpty())
				{
					sql.Append(property.Prefix);
				}
				sql.AppendLine(property.PropertyInfo.Name);

				parameters.Add(new KeyValuePair<string, object>($"{property.Prefix}{property.PropertyInfo.Name}", property.PropertyInfo.GetValue(property.Entity)));
			}

			AppendWhereCondition(new WhereCondition
			{
				Query = sql,
				TableName = entityTypeResult.TableName,
				Parameters = parameters,
				Properties = keyColumns
			},
			partialEntity ?? commandDefinition.Entity);

			commandDefinition.UpdateCommand(sql.ToString(), parameters);
		}

		public void ProcessDeleteRequest<T>(CommandDefinition<T> commandDefinition) where T : class
		{
			var type = CheckCommandConditions(commandDefinition, "delete");
			var entityTypeResult = EntityCache.GetEntity(type) ?? TypeAnalyzer.AnalyzeType(type);
			var keyColumns = GetKeyColumns(type);

			if (!keyColumns.Any())
			{
				throw new ArgumentException("At least one key column property must be defined.");
			}

			var sql = new StringBuilder();
			var parameters = new List<KeyValuePair<string, object>>();

			sql.Append("DELETE FROM ");
			sql.AppendLine(entityTypeResult.TableName);
			AppendWhereCondition(new WhereCondition
			{
				Query = sql,
				TableName = entityTypeResult.TableName,
				Parameters = parameters,
				Properties = keyColumns
			},
			commandDefinition.Entity);

			commandDefinition.UpdateCommand(sql.ToString(), parameters);
		}

		private void AppendWhereCondition(WhereCondition condition, object entity)
		{
			condition.Query.Append("WHERE ");

			var firstKey = true;
			foreach (var property in condition.Properties)
			{
				if (firstKey)
				{
					firstKey = false;
				}
				else
				{
					condition.Query.Append("AND ");
				}

				condition.Query.Append(condition.TableName);
				condition.Query.Append(".");
				condition.Query.Append(property.ColumnName);
				condition.Query.Append(" = @");
				condition.Query.AppendLine(property.PropertyInfo.Name);

				condition.Parameters.Add(new KeyValuePair<string, object>(property.PropertyInfo.Name, property.PropertyInfo.GetValue(entity)));
			}
		}

		private Type CheckCommandConditions<T>(CommandDefinition<T> commandDefinition, string action, object partialEntity = null) where T : class
		{
			var type = typeof(T);

			if (type.IsArray || type.ImplementsIEnumerable())
			{
				throw new ArgumentException($"Entity to {action} must be a single instance. No enumeration or array.");
			}

			if (commandDefinition.Entity == default && partialEntity == default)
			{
				throw new ArgumentNullException($"Entity to {action} must not be NULL");
			}

			return type;
		}
	}
}