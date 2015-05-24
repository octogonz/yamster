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
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;

namespace Yamster.Core.SQLite
{
    public class SQLiteDataContextUpgradeEventArgs : EventArgs
    {
        public string DatabaseName { get; private set; }
        public string DetailMessage { get; private set; }

        public int DatabaseVersion { get; private set; }
        public int LatestVersion { get; private set; }

        public bool CancelUpgrade { get; set; }

        public SQLiteDataContextUpgradeEventArgs(string databaseName, string detailMessage,
            int databaseVersion, int latestVersion)
        {
            this.DatabaseName = databaseName;
            this.DetailMessage = detailMessage;
            this.DatabaseVersion = databaseVersion;
            this.LatestVersion = latestVersion;
        }

        public string GetUpgradeMessage()
        {
            string message = string.Format(
                "The {0} database (version {1}) needs to be upgraded to the latest version {2}.",
                this.DatabaseName, 
                this.DatabaseVersion, 
                this.LatestVersion);
            if (!string.IsNullOrEmpty(DetailMessage))
                message += "  " + DetailMessage;
            return message;
        }
    }

    public abstract class SQLiteDataContext 
    {
        public readonly EventHandler<SQLiteDataContextUpgradeEventArgs> BeforeUpgradeHandler;
        public readonly EventHandler AfterUpgradeHandler;

        readonly SQLiteMapper mapper;

        readonly List<MappedTable> mappedTables = new List<MappedTable>();

        public SQLiteDataContext(SQLiteMapper mapper, 
            EventHandler<SQLiteDataContextUpgradeEventArgs> beforeUpgradeHandler,
            EventHandler afterUpgradeHandler)
        {
            this.mapper = mapper;
            this.BeforeUpgradeHandler = beforeUpgradeHandler;
            this.AfterUpgradeHandler = afterUpgradeHandler;

            CollectMappedTables();
        }

        public SQLiteMapper Mapper { get { return mapper; } }

        public SQLiteTransaction BeginTransaction()
        {
            return Mapper.BeginTransaction();
        }

        void CollectMappedTables()
        {
            mappedTables.Clear();

            foreach (MemberInfo memberInfo in GetType().GetMembers(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
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

                SQLiteMapperTableAttribute tableAttribute = memberInfo
                    .GetCustomAttributes(typeof(SQLiteMapperTableAttribute), inherit: true)
                    .Cast<SQLiteMapperTableAttribute>()
                    .FirstOrDefault();
                if (tableAttribute == null)
                    continue;

                if (!memberType.IsGenericType
                    || !typeof(MappedTable<>).IsAssignableFrom(memberType.GetGenericTypeDefinition()))
                {
                    throw new ArgumentException("Invalid type for property " + memberInfo.Name 
                        + ": must be based on MappedTable<T>");
                }

                MappedTable mappedTable = (MappedTable)Activator.CreateInstance(memberType, nonPublic: true);
                mappedTable.TableName = memberInfo.Name;
                mappedTable.RecordType = memberType.GetGenericArguments()[0];
                mappedTable.Context = this;
                mappedTable.TrackChanges = typeof(IMappedRecordWithChangeTracking).IsAssignableFrom(mappedTable.RecordType);
                var columnSet = SQLiteMapperHelpers.CreateColumnSetForType(mappedTable.RecordType);
                mappedTable.ColumnSet = columnSet;

                string errorPrefix = "Problem with definition of table \"" + mappedTable.TableName + "\":  ";

                foreach (var column in mappedTable.ColumnSet.Columns)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(column.Name, SQLiteMapper.ChangeNumberColumnName))
                    {
                        if (!mappedTable.TrackChanges)
                        {
                            throw new InvalidOperationException(errorPrefix
                                + "The reserved " + SQLiteMapper.ChangeNumberColumnName + " column cannot be used"
                                + " unless the record implements the " + typeof(IMappedRecordWithChangeTracking).Name
                                + " interface");
                        }

                        if (column.Nullable)
                        {
                            throw new InvalidOperationException(errorPrefix
                                + "The " + SQLiteMapper.ChangeNumberColumnName + " column cannot be nullable");
                        }

                        bool defaultValueOkay = false;                        
                        try
                        {
                            long value = (long)SQLiteMapperHelpers.ConvertObjectToSql(typeof(long), column.SqlDefaultValue);
                            if (value == -1)
                                defaultValueOkay = true;
                        }
                        catch
                        {
                        }
                        
                        if (!defaultValueOkay)
                        {
                            throw new InvalidOperationException(errorPrefix
                                + "The " + SQLiteMapper.ChangeNumberColumnName + " column must have -1 as its default value");
                        }

                    }
                }

                mappedTable.Indexes.AddRange(
                    mappedTable.RecordType.GetCustomAttributes(typeof(MappedTableIndexAttribute), inherit: true)
                    .Cast<MappedTableIndexAttribute>()
                    // Ensure nondeterministic ordering
                    .OrderBy(x => x.ToString())
                );

                    if (propertyInfo != null)
                {
                    propertyInfo.SetValue(this, mappedTable, null);
                }
                else
                {
                    fieldInfo.SetValue(this, mappedTable);
                }

                mappedTables.Add(mappedTable);
            }
        }

        public void CreateTables()
        {
            foreach (var mappedTable in mappedTables)
            {
                mapper.CreateTable(mappedTable);
            }
        }

        protected void OnBeforeUpgrade(SQLiteDataContextUpgradeEventArgs args)
        {
            if (BeforeUpgradeHandler != null)
            {
                BeforeUpgradeHandler(this, args);
            }
        }

        protected void OnAfterUpgrade()
        {
            if (AfterUpgradeHandler != null)
            {
                AfterUpgradeHandler(this, EventArgs.Empty);
            }
        }

    }

    public static class SQLiteDataContextExtensions
    {
        public static void InsertRecord<T>(this MappedTable<T> table, T record,
            SQLiteConflictResolution conflictResolution = SQLiteConflictResolution.Abort)
        {
            table.Context.Mapper.InsertRecord(table, record, conflictResolution);
        }

        public static int DeleteRecords<T>(this MappedTable<T> table, string sqlWhereClause)
        {
            return table.Context.Mapper.DeleteRecords(table, sqlWhereClause);
        }

        public static bool DeleteRecordUsingPrimaryKey<T>(this MappedTable<T> table, T recordToDelete)
        {
            return table.Context.Mapper.DeleteRecordUsingPrimaryKey(table, recordToDelete);
        }

        public static IEnumerable<T> Query<T>(this MappedTable<T> table, string sqlWhereClause)
        {
            string sqlQuery = "SELECT * FROM " + SQLiteMapperHelpers.GetEscaped(table.TableName)
                + " " + sqlWhereClause;
            return table.Context.Mapper.Query<T>(sqlQuery);
        }

        public static IEnumerable<T> QueryAll<T>(this MappedTable<T> table)
        {
            return Query<T>(table, "");
        }

        public static bool DoesTableExist<T>(this MappedTable<T> table)
        {
            return table.Context.Mapper.DoesTableExist(table.TableName);
        }

        public static long GetCount<T>(this MappedTable<T> table, string whereClause="")
        {
            return table.Context.Mapper.GetCount(table.TableName, whereClause);
        }
    }
}
