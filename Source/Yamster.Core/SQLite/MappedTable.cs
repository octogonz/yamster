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
using System.Text;

namespace Yamster.Core.SQLite
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true, Inherited=true)]
    public class MappedTableIndexAttribute : Attribute
    {
        public string IndexName { get; set; }
        public bool Unique { get; set; }

        // Example: "MyColumn"
        // Example: "MyColumn COLLATE NOCASE DESC"
        public ReadOnlyCollection<string> ColumnDefinitions { get; internal set; }

        public MappedTableIndexAttribute(params string[] columnDefinitions)
        {
            this.ColumnDefinitions = new ReadOnlyCollection<string>(columnDefinitions);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("IndexName=\"{0}\", ", IndexName ?? "");
            builder.Append("Columns = ( ");
            builder.Append(
                string.Join(", ",
                    ColumnDefinitions.Select(x => SQLiteMapperHelpers.GetEscaped(x ?? ""))
                )
            );
            builder.AppendFormat(" ), Unique = {0}", Unique);
            return builder.ToString();
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public class SQLiteMapperTableAttribute : Attribute
    {
    }

    public abstract class MappedTable
    {
        public string TableName { get; internal set; }
        public Type RecordType { get; internal set; }
        public SQLiteDataContext Context { get; internal set; }
        /// <summary>
        /// True if RecordType implements IMappedRecordWithChangeTracking.
        /// </summary>
        public bool TrackChanges { get; internal set; }
        public SQLiteMappedColumnSet ColumnSet { get; internal set; }

        internal readonly List<MappedTableIndexAttribute> Indexes = new List<MappedTableIndexAttribute>();

        internal MappedTable()
        {
        }
    }

    public class MappedTable<T> : MappedTable
    {
        public event EventHandler<MappedRecordChangedEventArgs<T>> RecordChanged;

        long lastChangeNumber = -1;

        internal MappedTable()
        {
        }

        public void CheckForChanges()
        {
            if (!TrackChanges)
                return;

            string sqlQuery = "SELECT * FROM " + SQLiteMapperHelpers.GetEscaped(this.TableName)
                        + " WHERE " + SQLiteMapperHelpers.GetEscaped(SQLiteMapper.ChangeNumberColumnName)
                        + " > " + lastChangeNumber
                        + " ORDER BY " + SQLiteMapperHelpers.GetEscaped(SQLiteMapper.ChangeNumberColumnName);

            var rows = Context.Mapper.Query<T>(sqlQuery, this.RecordType);

            foreach (var row in rows)
            {
                lastChangeNumber = ((IMappedRecordWithChangeTracking)row).ChangeNumber;

                if (RecordChanged != null)
                {
                    var args = new MappedRecordChangedEventArgs<T>(row, this);
                    RecordChanged(this, args);
                }
            }
        }

        public void DiscardChanges()
        {
            // Get the last change
            string sqlQuery = "SELECT * FROM " + SQLiteMapperHelpers.GetEscaped(this.TableName)
                        + " ORDER BY " + SQLiteMapperHelpers.GetEscaped(SQLiteMapper.ChangeNumberColumnName)
                        + " DESC LIMIT 1";

            var row = Context.Mapper.Query<T>(sqlQuery, this.RecordType).FirstOrDefault();

            if (row != null)
            {
                lastChangeNumber = ((IMappedRecordWithChangeTracking)row).ChangeNumber;
            }
        }

    }

    public class MappedRecordChangedEventArgs<T> : EventArgs
    {
        public T Record { get; private set; }
        public MappedTable<T> Table { get; private set; }

        public MappedRecordChangedEventArgs(T record, MappedTable<T> table)
        {
            this.Record = record;
            this.Table = table;
        }
    }

}
