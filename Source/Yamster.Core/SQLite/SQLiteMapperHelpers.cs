#region MIT License

// Yamster!
// Copyright (c) Microsoft Corporation
// All rights reserved. 
// 
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
// associated documentation files (the ""Software""), to deal in the Software without restriction, 
// including without limitation the rights to use, copy, modify, merge, publish, distribute, 
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or 
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT 
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Yamster.Core;

namespace Yamster.Core.SQLite
{
    // Choices for "ON CONFLICT" clause, documented here http://www.sqlite.org/lang_conflict.html
    public enum SQLiteConflictResolution
    {
        Abort = 0, // the default
        Rollback,
        Fail,
        Ignore,
        Replace
    }

    /// <summary>
    /// If the MappedTable record implements this interface, then the table will track changes.
    /// This is accomplished by adding two triggers for "AFTER UPDATE" and "AFTER INSERT";
    /// these triggers assign ChangeNumber=Max(ChangeNumber)+1 whenever a row is inserted
    /// or updated.  An index will automatically be defined for the ChangeNumber column.
    /// </summary>
    public interface IMappedRecordWithChangeTracking
    {
        long ChangeNumber { get; set; }
    }

    public abstract class MappedRecordWithChangeTracking : IMappedRecordWithChangeTracking
    {
        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public long ChangeNumber { get; set; }

        public void CopyFrom(MappedRecordWithChangeTracking source)
        {
            this.ChangeNumber = source.ChangeNumber;
        }
    }

    public static class SQLiteMapperHelpers
    {
        #region class LambdaPropertyDescriptor

        class LambdaPropertyDescriptor : PropertyDescriptor
        {
            Type propertyType;
            Func<object, object> getter;
            Action<object, object> setter;

            public LambdaPropertyDescriptor(string name, Type propertyType, Func<object, object> getter, Action<object, object> setter)
                : base(name, new Attribute[0])
            {
                this.propertyType = propertyType;
                this.getter = getter;
                this.setter = setter;
            }

            public override bool CanResetValue(object component)
            {
                return false;
            }

            public override Type ComponentType
            {
                get { return typeof(object); }
            }

            public override object GetValue(object component)
            {
                return getter(component);
            }

            public override bool IsReadOnly
            {
                get { return false; }
            }

            public override Type PropertyType
            {
                get { return propertyType; }
            }

            public override void ResetValue(object component)
            {
                throw new NotImplementedException();
            }

            public override void SetValue(object component, object value)
            {
                setter(component, value);
            }

            public override bool ShouldSerializeValue(object component)
            {
                return false;
            }
        }

        #endregion

        static Dictionary<string, TypeAffinity> typeAffinityByName = new Dictionary<string, TypeAffinity>();

        static SQLiteMapperHelpers()
        {
            typeAffinityByName.Add(typeof(Int16).FullName, TypeAffinity.Int64);
            typeAffinityByName.Add(typeof(UInt16).FullName, TypeAffinity.Int64);
            typeAffinityByName.Add(typeof(Int32).FullName, TypeAffinity.Int64);
            typeAffinityByName.Add(typeof(UInt32).FullName, TypeAffinity.Int64);
            typeAffinityByName.Add(typeof(Int64).FullName, TypeAffinity.Int64);
            typeAffinityByName.Add(typeof(UInt64).FullName, TypeAffinity.Int64);

            typeAffinityByName.Add(typeof(Boolean).FullName, TypeAffinity.Int64);

            typeAffinityByName.Add(typeof(Single).FullName, TypeAffinity.Double);
            typeAffinityByName.Add(typeof(Double).FullName, TypeAffinity.Double);

            typeAffinityByName.Add(typeof(String).FullName, TypeAffinity.Text);

            typeAffinityByName.Add(typeof(DateTime).FullName, TypeAffinity.Text);

            typeAffinityByName.Add(typeof(SQLiteIdList).FullName, TypeAffinity.Text);
        }

        public static SQLiteMappedColumnSet CreateColumnSetForType(Type classType)
        {
            SQLiteMappedColumnSet columnSet = new SQLiteMappedColumnSet();

            foreach (MemberInfo memberInfo in classType.GetMembers(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Type memberType;
                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                FieldInfo fieldInfo = memberInfo as FieldInfo;
                if (propertyInfo != null)
                {
                    memberType = propertyInfo.PropertyType;
                }
                else if (fieldInfo != null)
                {
                    memberType = fieldInfo.FieldType;
                }
                else continue;

                SQLiteMapperPropertyAttribute attribute = null;
                foreach (Attribute baseAttribute in memberInfo.GetCustomAttributes(true))
                {
                    attribute = baseAttribute as SQLiteMapperPropertyAttribute;
                    if (attribute != null)
                        break;
                }
                if (attribute == null)
                    continue;

                TypeAffinity affinity = GetSQLiteAffinityForType(memberType);

                MappedColumn column = new MappedColumn(memberInfo.Name, memberType, affinity);
                column.PrimaryKey = attribute.PrimaryKey;

                bool nullable;
                if (attribute.Nullable == OptionalBool.Unspecified && memberType != typeof(SQLiteIdList))
                {
                    nullable = Nullable.GetUnderlyingType(memberType) != null;
                }
                else
                {
                    nullable = attribute.Nullable != OptionalBool.False;
                }

                column.Nullable = nullable;
                columnSet.Columns.Add(column);
            }
            return columnSet;
        }

        internal static List<PropertyDescriptor> GetPropertyDescriptors(Type classType, SQLiteMappedColumnSet columnSet)
        {
            var descriptorsByColumn = new Dictionary<MappedColumn,PropertyDescriptor>();

            var emptyArray = new object[0];

            foreach (MemberInfo memberInfo in classType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                Type memberType;
                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                FieldInfo fieldInfo = memberInfo as FieldInfo;
                if (propertyInfo != null)
                {
                    memberType = propertyInfo.PropertyType;
                }
                else if (fieldInfo != null)
                {
                    memberType = fieldInfo.FieldType;
                }
                else continue;

                MappedColumn column = columnSet.Columns.FirstOrDefault(x => x.Name == memberInfo.Name);
                if (column == null)
                    continue;

                PropertyDescriptor descriptor;

                if (propertyInfo != null)
                {
                    descriptor = new LambdaPropertyDescriptor(
                        column.Name,
                        memberType,
                        (object record) => propertyInfo.GetValue(record, emptyArray),
                        delegate(object record, object value) {
                            propertyInfo.SetValue(record, value, emptyArray);
                        }
                    );
                }
                else
                {
                    descriptor = new LambdaPropertyDescriptor(
                        column.Name,
                        memberType,
                        (object record) => fieldInfo.GetValue(record),
                        delegate(object record, object value) {
                            fieldInfo.SetValue(record, value);
                        }
                    );
                }

                descriptorsByColumn.Add(column, descriptor);
            }

            List<PropertyDescriptor> descriptors = new List<PropertyDescriptor>(columnSet.Columns.Count);
            foreach (var column in columnSet.Columns)
            {
                PropertyDescriptor descriptor = null;
                if (!descriptorsByColumn.TryGetValue(column, out descriptor))
                    throw new Exception("No match for column " + column.Name);
                descriptors.Add(descriptor);
            }
            return descriptors;
        }

        public static object ConvertObjectToSql(Type fieldType, object obj)
        {
            object sqlValue;
            if (fieldType == typeof(DateTime) && obj != null)
            {
                DateTime dateTime = (DateTime)obj;
                // This is what JSON.NET does by default
                sqlValue = dateTime.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    CultureInfo.InvariantCulture);
            }
            else if (fieldType.IsEnum)
            {
                sqlValue = ((Enum)obj).ToString();
            }
            else if (fieldType == typeof(SQLiteIdList))
            {
                sqlValue = ((SQLiteIdList)obj).ToString();
            }
            else 
            {
                sqlValue = obj;
            }
            return sqlValue;
        }

        public static object ConvertSqlToObject(Type fieldType, object sqlValue)
        {
            Type nonNullableType = Nullable.GetUnderlyingType(fieldType);

            if (nonNullableType != null)
            {
                // If the type is nullable, then don't attempt to convert null values
                if (sqlValue == null || sqlValue == DBNull.Value)
                    return null;
            }
            else
            {
                nonNullableType = fieldType;
            }

            object obj;
            if (nonNullableType == typeof(DateTime) && sqlValue != null)
            {
                obj = DateTime.Parse((string)sqlValue);
            }
            else if (nonNullableType.IsEnum)
            {
                obj = Enum.Parse(fieldType, (string)sqlValue);
            }
            else if (nonNullableType == typeof(SQLiteIdList))
            {
                obj = SQLiteIdList.ParseString((string)sqlValue);
            }
            else
            {
                obj = Convert.ChangeType(sqlValue, nonNullableType);
            }

            return obj;
        }

        public static string GetCreateTableStatement(MappedTable mappedTable)
        {
            string tableName = mappedTable.TableName;
            var columnSet = mappedTable.ColumnSet;

            // --> Table definition
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("DROP TABLE IF EXISTS " + GetEscaped(tableName) + ";");
            builder.Append("CREATE TABLE " + GetEscaped(tableName) + " (");
            bool needsComma = false;

            foreach (MappedColumn column in columnSet.Columns)
            {
                if (needsComma)
                    builder.Append(",");
                needsComma = true;
                builder.AppendLine();

                builder.Append("  " + GetEscaped(column.Name));
                builder.Append(" " + GetSQLiteAffinityName(column.Affinity));
                if (column.PrimaryKey)
                    builder.Append(" PRIMARY KEY");

                if (!column.Nullable)
                    builder.Append(" NOT NULL");

                if (mappedTable.TrackChanges)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(column.Name, SQLiteMapper.ChangeNumberColumnName))
                    {
                        builder.Append(" DEFAULT -1");
                    }
                }
            }

            builder.AppendLine();
            builder.AppendLine(");");

            // --> Indexes
            int generatedIndex = 0;
            foreach (MappedTableIndexAttribute attribute in mappedTable.Indexes)
            {
                string indexName = attribute.IndexName;
                if (string.IsNullOrEmpty(indexName))
                {
                    indexName = tableName + "_Index" + generatedIndex++;

                    builder.Append("\r\nCREATE");
                    if (attribute.Unique)
                        builder.Append(" UNIQUE");
                    builder.Append(" INDEX " + GetEscaped(indexName) + " ON " + GetEscaped(tableName) + "(");
                    bool first = true;
                    foreach (string columnDefinition in attribute.ColumnDefinitions)
                    {
                        if (!first) 
                            builder.Append(", ");
                        builder.Append(GetEscaped(columnDefinition));
                        first = false;

                    }

                    builder.Append(");");
                }
            }

            // --> Change number feature (index and two triggers)
            if (mappedTable.TrackChanges)
            {
                builder.AppendFormat("\r\nCREATE UNIQUE INDEX {0} ON {1} ({2});",
                    GetEscaped(tableName + "_Index" + generatedIndex++),
                    GetEscaped(tableName),
                    GetEscaped(SQLiteMapper.ChangeNumberColumnName));

                string triggerFormat 
                    = "\r\nCREATE TRIGGER {0} AFTER {1} ON {2}"
                    + "\r\nBEGIN"
                    + "\r\n  UPDATE {2}"
                    + "\r\n  SET {3} = (SELECT MAX({3}) FROM {2} LIMIT 1)+1"
                    + "\r\n  WHERE ROWID = NEW.ROWID;"
                    + "\r\nEND;";

                builder.AppendFormat(triggerFormat,
                    GetEscaped(tableName + "_ChangeNumberTrigger0"),
                    "INSERT",
                    GetEscaped(tableName),
                    GetEscaped(SQLiteMapper.ChangeNumberColumnName));

                builder.AppendFormat(triggerFormat,
                    GetEscaped(tableName + "_ChangeNumberTrigger1"),
                    "UPDATE",
                    GetEscaped(tableName),
                    GetEscaped(SQLiteMapper.ChangeNumberColumnName));
            }
            return builder.ToString();
        }

        public static string GetInsertStatement(SQLiteMappedColumnSet columnSet, string tableName,
            SQLiteConflictResolution conflictResolution)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("INSERT");

            if (conflictResolution != SQLiteConflictResolution.Abort)
            {
                builder.Append(" OR ");
                builder.Append(conflictResolution.ToString().ToUpperInvariant());
            }

            builder.Append(" INTO " + GetEscaped(tableName) + " (");

            string paramList = "";

            bool needsComma = false;
            foreach (MappedColumn column in columnSet.Columns)
            {
                if (needsComma)
                {
                    builder.Append(",");
                    paramList += ",";
                }
                needsComma = true;

                builder.AppendLine();
                builder.Append("  " + GetEscaped(column.Name));
                paramList += "?";
            }
            builder.AppendLine();
            builder.AppendLine(") VALUES (");
            builder.AppendLine("  " + paramList);
            builder.AppendLine(");");

            return builder.ToString();
        }

        public static string GetEscaped(string name)
        {
            return "[" + name + "]";
        }

        private static TypeAffinity GetSQLiteAffinityForType(Type type)
        {
            // For nullable types, recover the underlying type
            var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;
            if (nonNullableType.IsEnum)
                return TypeAffinity.Text;
            TypeAffinity affinity;
            if (typeAffinityByName.TryGetValue(nonNullableType.FullName, out affinity))
                return affinity;
            throw new NotSupportedException("No type mapping for " + type.FullName);
        }

        private static string GetSQLiteAffinityName(TypeAffinity affinity)
        {
            switch (affinity)
            {
                case TypeAffinity.Blob:
                case TypeAffinity.Null:
                case TypeAffinity.Text:
                    return "TEXT";
                case TypeAffinity.DateTime:
                case TypeAffinity.Int64:
                    return "INTEGER";
                case TypeAffinity.Double:
                    return "REAL";
                default:
                    throw new ArgumentException("Invalid affinity: " + affinity.ToString());
            }
        }
    }

}
