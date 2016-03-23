using System;
using System.Data;

namespace JX.SQLiteFramework
{
	public class SqliteColumnAttribute : Attribute
	{
		public string ColumnName;
		public string Description;
		public bool IsKey;
		public bool AllowNull;
		public bool ReadOnly;
		public DbType Type = DbType.String;
		public object DefaultValue;
	}

	public class SqliteTableAttribute : Attribute
	{
		public string TableName;
	}
}
