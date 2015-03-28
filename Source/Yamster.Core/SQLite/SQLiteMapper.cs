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
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;

namespace Yamster.Core.SQLite
{
    public enum SQLiteMapperLogLevel
    {
        Normal = 0, // log very little
        Verbose, // log high-level operations
        SqlTrace    // trace each SQL command
    }

    public class SQLiteMapper : IDisposable
    {
        public const string ChangeNumberColumnName = "ChangeNumber";

        SQLiteConnection connection;

        public static SQLiteMapperLogLevel DefaultLogLevel = SQLiteMapperLogLevel.Normal;

        public SQLiteMapper(string sqliteFilename, bool createIfMissing = false)
        {
            SQLiteConnectionStringBuilder connectionStringBuilder = new SQLiteConnectionStringBuilder();
            connectionStringBuilder.DataSource = sqliteFilename;
            connectionStringBuilder.Pooling = true;
            connectionStringBuilder.FailIfMissing = !createIfMissing;

            connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);

            connection.Trace += connection_Trace;
        }

        public void Dispose() // IDisposable
        {
            if (connection != null)
            {
                connection.Trace -= connection_Trace;
                connection.Dispose();
                connection = null;
            }
        }

        public event SQLiteTraceEventHandler Trace
        {
            add { connection.Trace += value; }
            remove { connection.Trace -= value; }
        }

        public void Open()
        {
            connection.Open();
        }

        public SQLiteTransaction BeginTransaction()
        {
            return (SQLiteTransaction)connection.BeginTransaction();
        }

        public void CreateTable(MappedTable mappedTable)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = SQLiteMapperHelpers.GetCreateTableStatement(mappedTable);
                command.ExecuteNonQuery();
            }
        }

        public void InsertRecords<T>(MappedTable table, IEnumerable<T> records, 
            SQLiteConflictResolution conflictResolution = SQLiteConflictResolution.Abort,
            Type recordType=null)
        {
            if (recordType == null)
                recordType = typeof(T);

            var columnSet = SQLiteMapperHelpers.CreateColumnSetForType(recordType);

            var descriptors = SQLiteMapperHelpers.GetPropertyDescriptors(recordType, columnSet);

            using (SQLiteCommand command = connection.CreateCommand())
            using (SQLiteCommand fixReplaceCommand = connection.CreateCommand())
            {
                command.CommandText = SQLiteMapperHelpers.GetInsertStatement(columnSet, table.TableName, conflictResolution);

                foreach (var column in columnSet.Columns)
                {
                    var parameter = command.CreateParameter();
                    parameter.Direction = ParameterDirection.Input;
                    command.Parameters.Add(parameter);
                }
                command.Prepare();

                // SQLite implements "INSERT OR REPLACE" as a DELETE followed by an INSERT, which can cause
                // trouble when our TrackChanges trigger tries to compute MAX(ChangeNumber).  The workaround
                // is to make sure an accurate ChangeNumber is in the C# record before we start 
                // the INSERT OR REPLACE.  (Normally this is already true, but sometimes it's 0 or a 
                // little outdated so we do a SELECT to be sure.)
                bool fixReplace = table.TrackChanges && conflictResolution == SQLiteConflictResolution.Replace;
                SQLiteParameter fixReplaceParameter = null;
                PropertyDescriptor fixReplacePrimaryKeyDescriptor = null;

                if (fixReplace)
                {
                    if (columnSet.PrimaryKeyColumn == null)
                    {
                        throw new InvalidOperationException(
                            "SQLiteConflictResolution.Replace cannot be used without including the primary key");
                    }
                    int primaryKeyIndex = columnSet.Columns.IndexOf(columnSet.PrimaryKeyColumn);
                    fixReplacePrimaryKeyDescriptor = descriptors[primaryKeyIndex];

                    fixReplaceCommand.CommandText = string.Format(
                        "SELECT [{0}] FROM [{1}] WHERE [{2}] = ?",
                        SQLiteMapper.ChangeNumberColumnName,
                        table.TableName,
                        columnSet.PrimaryKeyColumn.Name);

                    fixReplaceParameter = fixReplaceCommand.CreateParameter();
                    fixReplaceParameter.Direction = ParameterDirection.Input;
                    fixReplaceCommand.Parameters.Add(fixReplaceParameter);
                }

                using (var transaction = this.BeginTransaction())
                {
                    foreach (var record in records)
                    {
                        if (fixReplace)
                        {
                            var castedRecord = record as IMappedRecordWithChangeTracking;
                            if (castedRecord == null)
                            {
                                throw new InvalidOperationException(
                                    "SQLiteConflictResolution.Replace cannot be used without including the ChangeNumber column");
                            }
                            fixReplaceParameter.Value = fixReplacePrimaryKeyDescriptor.GetValue(record);
                            // NOTE: If the SELECT returns no rows, Convert.ToInt64() will convert NULL to 0
                            castedRecord.ChangeNumber = Convert.ToInt64(fixReplaceCommand.ExecuteScalar());
                        }

                        for (int i = 0; i < columnSet.Columns.Count; ++i)
                        {
                            var column = columnSet.Columns[i];
                            var parameter = command.Parameters[i];
                            var descriptor = descriptors[i];
                            object obj = descriptor.GetValue(record);
                            parameter.Value = SQLiteMapperHelpers.ConvertObjectToSql(descriptor.PropertyType, obj);
                        }

                        int recordsModified = command.ExecuteNonQuery();

                        if (SQLiteMapper.DefaultLogLevel >= SQLiteMapperLogLevel.Verbose)
                            Debug.WriteLine("Inserted " + recordsModified + " rows");
                    }
                    transaction.Commit();
                }
            }
        }

        public void InsertRecord<T>(MappedTable table, T record, 
            SQLiteConflictResolution conflictResolution = SQLiteConflictResolution.Abort,
            Type recordType = null)
        {
            InsertRecords(table, new T[1] { record }, conflictResolution, recordType);
        }

        public int DeleteRecords<T>(MappedTable<T> table, string sqlWhereClause)
        {
            return ExecuteNonQuery("DELETE FROM [" + table.TableName + "] " + sqlWhereClause);
        }

        public bool DeleteRecordUsingPrimaryKey<T>(MappedTable<T> table, T recordToDelete)
        {
            var recordType = typeof(T);
            var columnSet = SQLiteMapperHelpers.CreateColumnSetForType(recordType);
            var descriptors = SQLiteMapperHelpers.GetPropertyDescriptors(recordType, columnSet);
            var primaryKey = columnSet.PrimaryKeyColumn;

            if (primaryKey == null)
                throw new InvalidOperationException("Cannot delete the record because there is no primary key");
            
            string sqlQuery = string.Format(
                "DELETE FROM [{0}] WHERE [{1}] = ?",
                table.TableName,
                primaryKey.Name);

            int primaryKeyIndex = columnSet.Columns.IndexOf(columnSet.PrimaryKeyColumn);
            object primaryKeyValue = descriptors[primaryKeyIndex].GetValue(recordToDelete);

            return ExecuteNonQuery(sqlQuery, primaryKeyValue) != 0;
        }

        public int ExecuteNonQuery(string sqlQuery, params object[] parameters)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = sqlQuery;

                foreach (object parameter in parameters)
                {
                    var sqlParameter = command.CreateParameter();
                    sqlParameter.Direction = ParameterDirection.Input;
                    sqlParameter.Value = parameter;
                    command.Parameters.Add(sqlParameter);
                }
                command.Prepare();

                using (var transaction = this.BeginTransaction())
                {
                    int recordsModified = command.ExecuteNonQuery();
                    transaction.Commit();
                    return recordsModified;
                }
            }
        }

        public IEnumerable<T> Query<T>(string sqlQuery, Type recordType = null)
        {
            if (recordType == null)
                recordType = typeof(T);

            var columnSet = SQLiteMapperHelpers.CreateColumnSetForType(recordType);
            var descriptors = SQLiteMapperHelpers.GetPropertyDescriptors(recordType, columnSet);

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = sqlQuery;

                using (var reader = command.ExecuteReader())
                {
                    Dictionary<string, int> fieldNameToIndex = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; ++i)
                    {
                        string fieldName = reader.GetName(i);
                        if (fieldNameToIndex.ContainsKey(fieldName))
                        {
                            fieldNameToIndex[fieldName] = -1; // ambiguous
                        }
                        else
                        {
                            fieldNameToIndex[fieldName] = i;
                        }
                    }

                    List<int> fieldIndexes = new List<int>(columnSet.Columns.Count);

                    foreach (var column in columnSet.Columns)
                    {
                        int fieldIndex = 0;
                        if (fieldNameToIndex.TryGetValue(column.Name, out fieldIndex))
                        {
                            if (fieldIndex < 0)
                                throw new ArgumentException("The SQL result contains more than one column named " + column.Name);
                        }
                        else
                        {
                            throw new ArgumentException("The SQL result does not contain a matching column for " + column.Name);
                        }

                        fieldIndexes.Add(fieldIndex);
                    }

                    while (reader.Read())
                    {
                        object record = Activator.CreateInstance(recordType);
                        for (int i = 0; i < columnSet.Columns.Count; ++i)
                        {
                            var fieldIndex = fieldIndexes[i];
                            var descriptor = descriptors[i];

                            object sqlValue = reader.GetValue(fieldIndex);
                            try
                            {
                                object obj = SQLiteMapperHelpers.ConvertSqlToObject(descriptor.PropertyType, sqlValue);
                                descriptor.SetValue(record, obj);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception("Unable to assign " + columnSet.Columns[i].Name + ": " + ex.Message, ex);
                            }
                        }
                        yield return (T)record;
                    }
                }
            }
        }

        public T QueryScalar<T>(string sqlQuery)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = sqlQuery;

                object value = command.ExecuteScalar();
                if (value == DBNull.Value || value == null)
                {
                    if (object.ReferenceEquals(default(T), null))
                        return default(T);
                }

                Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

                T coercedValue = (T)Convert.ChangeType(value, targetType);
                
                return coercedValue;
            }
        }

        public bool DoesTableExist(string tableName)
        {
            return QueryScalar<int>("SELECT COUNT(name) FROM sqlite_master WHERE type='table' AND Name='" + tableName + "'")
                > 0;
        }

        public long GetCount(string tableName, string whereClause="")
        {
            return QueryScalar<long>("SELECT COUNT(*) FROM " + SQLiteMapperHelpers.GetEscaped(tableName)
                + " " + whereClause);
        }

        void connection_Trace(object sender, TraceEventArgs e)
        {
            if (SQLiteMapper.DefaultLogLevel >= SQLiteMapperLogLevel.SqlTrace)
                Debug.WriteLine("SQL: " + e.Statement);
        }
    }
}
