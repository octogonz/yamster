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
using System.Data.SQLite;
using System.Linq;
using System.Text;
using Yamster.Core;

namespace Yamster.Core.SQLite
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
        AllowMultiple = false, Inherited = true)]
    public class SQLiteMapperPropertyAttribute : Attribute
    {
        public SQLiteMapperPropertyAttribute()
        {
        }

        public bool PrimaryKey { get; set; }
        public OptionalBool Nullable { get; set; }

        /// <summary>
        /// This is the default value for the underlying SQL table.
        /// The SQLiteMapper will not necessarily recognize it, since C# properties
        /// can have their own defaults.
        /// </summary>
        public object SqlDefaultValue { get; set; }
    }

    public class MappedColumn : FreezableObject
    {
        string name;
        Type fieldType;
        TypeAffinity affinity;

        bool nullable = false;
        object sqlDefaultValue = null;
        bool primaryKey = false;

        public MappedColumn(string name, Type fieldType, TypeAffinity affinity)
        {
            this.name = name;
            this.fieldType = fieldType;
            this.affinity = affinity;
        }

        public string Name { get { return name; } }
        public Type FieldType { get { return fieldType; } }
        public TypeAffinity Affinity { get { return affinity; } }

        public bool Nullable 
        {
            get { return this.nullable; }
            set 
            {
                base.RequireNotFrozen();
                this.nullable = value;
            }
        }

        /// <summary>
        /// This is the default value for the underlying SQL table.
        /// The SQLiteMapper will not necessarily recognize it, since C# properties
        /// can have their own defaults.
        /// </summary>
        public object SqlDefaultValue
        {
            get { return this.sqlDefaultValue; }
            set
            {
                base.RequireNotFrozen();

                if (value == null)
                {
                    // It is legal for a non-nullable field to have null as its default value,
                    // since this merely asserts that the INSERT statement should provide it.
                    // However for this case, we need to skip the validation.
                    this.sqlDefaultValue = value;
                }
                else
                {
                    // Make sure the provided type is convertible
                    object sqlValue = SQLiteMapperHelpers.ConvertObjectToSql(this.fieldType, value);
                    object normalizedValue = SQLiteMapperHelpers.ConvertSqlToObject(this.fieldType, sqlValue);

                    this.sqlDefaultValue = normalizedValue;
                }
            }
        }

        public bool PrimaryKey
        {
            get { return this.primaryKey; }
            set
            {
                base.RequireNotFrozen();
                this.primaryKey = value;
            }
        }
    }
}
