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
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Yamster.Core.SQLite
{
    /// <summary>
    /// Frequently a table needs to store a list of RowIDs that define a SQL relationship
    /// with another table.  To avoid the overhead of introducing an additional SQL table,
    /// SQLiteIdList provides a lightweight way to encode these IDs in a text column.
    /// The syntax "|12345|-54321|" allows SQL queries to efficiently search for integers
    /// in the list, e.g. "WHERE IdList LIKE '%|12345|%'".
    /// </summary>
    public class SQLiteIdList : List<long>
    {
        static readonly Regex idListRegex = new Regex(@"^\s*(?:\|([\-0-9]+))+\|\s*$", RegexOptions.Compiled);
        public SQLiteIdList()
        {
        }
        public SQLiteIdList(int capacity)
            : base(capacity)
        {
        }
        public SQLiteIdList(IEnumerable<long> values)
            : base(values)
        {
        }

        public static SQLiteIdList ParseString(string encodedIdList)
        {
            if (string.IsNullOrEmpty(encodedIdList))
                return new SQLiteIdList(0);
            var match = SQLiteIdList.idListRegex.Match(encodedIdList);
            if (!match.Success)
                throw new ArgumentException("Invalid ID list syntax: " + encodedIdList);
            var captures = match.Groups[1].Captures;
            var idList = new SQLiteIdList(captures.Count);
            for (int i = 0; i < captures.Count; ++i)
            {
                idList.Add(long.Parse(captures[i].Value));
            }
            return idList;
        }

        public override string ToString()
        {
            if (this.Count == 0)
                return "";
            string result = "|";
            for (int i = 0; i < this.Count; ++i)
            {
                result += this[i].ToString() + "|";
            }
            return result;
        }

        public void AssignFrom(IEnumerable<long> enumerable)
        {
            this.Clear();
            var array = enumerable.ToArray();
            this.AddRange(array);
        }
    }

}
