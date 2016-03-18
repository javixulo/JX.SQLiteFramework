using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using JXWPFToolkit.Controls;

namespace SQLiteFramework
{
	public abstract class SqliteObject
	{
		public void FillInstanceFromReader(SQLiteDataReader reader)
		{
			var propertyInfos = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var prop in propertyInfos)
			{
				SqliteColumnAttribute sqliteColumn = prop.GetCustomAttribute<SqliteColumnAttribute>();

				if (sqliteColumn == null)
					continue;

				try
				{
					object value = reader[sqliteColumn.ColumnName];
					prop.SetValue(this, value);
				}
				catch (Exception e)
				{
					Console.WriteLine("{0} was not recognized. Error: {1}", sqliteColumn.ColumnName, e.InnerException);
				}
			}
		}

		public int Save(SQLiteConnection dbConnection, bool isNew = false)
		{
			Type current = GetType();
			string table = current.GetCustomAttribute<SqliteTableAttribute>().TableName;
			var propertyInfos = current.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			Tuple<string, object, DbType> key = null;

			List<Tuple<string, object, DbType>> columns = new List<Tuple<string, object, DbType>>();
			foreach (var prop in propertyInfos)
			{
				SqliteColumnAttribute sqliteColumn = prop.GetCustomAttribute<SqliteColumnAttribute>();
				if (sqliteColumn == null)
					continue;

				if (sqliteColumn.IsKey)
				{
					if (sqliteColumn.ReadOnly)
					{
						key = Tuple.Create(sqliteColumn.ColumnName, prop.GetValue(this), sqliteColumn.Type);
					}
					else
					{
						columns.Add(Tuple.Create(sqliteColumn.ColumnName, prop.GetValue(this), sqliteColumn.Type));
						key = columns[columns.Count - 1];
					}
				}
				else
					columns.Add(Tuple.Create(sqliteColumn.ColumnName, prop.GetValue(this), sqliteColumn.Type));
			}

			StringBuilder sql = new StringBuilder();

			if (isNew)
			{
				string keys = string.Join(",", columns.Select(x => string.Format("[{0}]",x.Item1)));
				string values = string.Join(",", columns.Select(x => ":" + x.Item1));
				sql.AppendFormat("INSERT INTO [{0}] ({1}) VALUES ({2})", table, keys, values);
			}
			else
			{
				string values = string.Join(",", columns.Select(x => String.Format("[{0}]=:{1}", x.Item1, x.Item1)));
				sql.AppendFormat("UPDATE [{0}] SET {1} WHERE {2}=:{2}", table, values, key.Item1);
			}

			using (SQLiteCommand command = new SQLiteCommand(dbConnection))
			{
				command.CommandText = sql.ToString();

				foreach (var value in columns)
					command.Parameters.Add(value.Item1, value.Item3).Value = value.Item2;

				if (!isNew)
				{
					command.Parameters.Add(key.Item1, key.Item3).Value = key.Item2;
				}

				try
				{
					return command.ExecuteNonQuery();
				}
				catch (SQLiteException e)
				{
					Console.WriteLine(e);
					Console.WriteLine(e.StackTrace);
				}
			}

			return -1;
		}

		public int Delete(SQLiteConnection dbConnection)
		{
			Type current = GetType();
			string table = current.GetCustomAttribute<SqliteTableAttribute>().TableName;
			var propertyInfos = current.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			Tuple<string, object, DbType> key = null;

			foreach (var prop in propertyInfos)
			{
				SqliteColumnAttribute sqliteColumn = prop.GetCustomAttribute<SqliteColumnAttribute>();
				if (sqliteColumn == null || !sqliteColumn.IsKey)
					continue;

				key = Tuple.Create(sqliteColumn.ColumnName, prop.GetValue(this), sqliteColumn.Type);
				break;
			}

			using (SQLiteCommand command = new SQLiteCommand(dbConnection))
			{
				command.CommandText = string.Format("DELETE FROM {0} WHERE {1}=:{1}", table, key.Item1);
				command.Parameters.Add(key.Item1, key.Item3).Value = key.Item2;

				return command.ExecuteNonQuery();
			}
		}

		#region InputValuesControl compatibility helpers

		public static List<T> GetAllElements<T>(SQLiteConnection dbConnection) where T : SqliteObject
		{
			SqliteTableAttribute tableAttribute = typeof(T).GetCustomAttribute<SqliteTableAttribute>();

			if (tableAttribute == null)
				return null;

			List<T> elements = new List<T>();

			string sql = string.Format("select * from [{0}]", tableAttribute.TableName);
			SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
			using (SQLiteDataReader reader = command.ExecuteReader())
			{
				while (reader.Read())
				{
					T element = Activator.CreateInstance<T>();
					element.FillInstanceFromReader(reader);
					elements.Add(element);
				}

			}

			return elements;
		}

		public virtual List<InputValuesControl.InputItem> GetAsInputItem(bool isNew)
		{
			List<InputValuesControl.InputItem> items = new List<InputValuesControl.InputItem>();
			var propertyInfos = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var prop in propertyInfos)
			{
				SqliteColumnAttribute sqliteColumn = prop.GetCustomAttribute<SqliteColumnAttribute>();

				if (sqliteColumn == null)
					continue;

				var item = new InputValuesControl.InputItem(prop.Name, prop.PropertyType);
				item.Label = string.Format("{0}: ", sqliteColumn.Description);
				item.Value = prop.GetValue(this);
				item.Required = !sqliteColumn.AllowNull;
				item.ReadOnly = sqliteColumn.ReadOnly;

				if (isNew && sqliteColumn.DefaultValue != null)
					item.Value = sqliteColumn.DefaultValue;

				if (isNew && prop.PropertyType == typeof(DateTime))
					item.Value = DateTime.Now;

				if (prop.PropertyType == typeof(DateTime))
					item.Format = "dd/MM/yyyy";

				items.Add(item);
			}

			return items;
		}

		public void SetValues(IEnumerable<InputValuesControl.InputItem> items)
		{
			var propertyInfos = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var item in items)
			{
				var property = propertyInfos.First(x => x.Name == item.Id);
				if (item.Value is ComboBoxItem)
					property.SetValue(this, ((ComboBoxItem)item.Value).Tag);
				else
					property.SetValue(this, item.Value);

			}
		}

		#endregion
	}
}
