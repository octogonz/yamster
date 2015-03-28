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
using System.Linq;
using System.Reflection;

namespace Yamster.Core
{
    public class YamsterUserSet : IEquatable<YamsterUserSet>, IReadOnlyList<YamsterUser>
    {
        class UserComparer : IComparer<YamsterUser>
        {
            public int Compare(YamsterUser x, YamsterUser y)
            {
                return Math.Sign(x.UserId - y.UserId);
            }
        }
        static UserComparer userComparer = new UserComparer();

        readonly List<YamsterUser> users = new List<YamsterUser>();
        int hashCode = 0;
        string cachedToString = null;
        long cachedUserIdToExclude = 0;

        static internal YamsterUserSet EmptyUserSet = new YamsterUserSet();

        public YamsterUserSet()
        {
        }

        public YamsterUserSet(IEnumerable<YamsterUser> users)
        {
            foreach (var user in users)
                AddUser(user);
        }

        internal void Clear()
        {
            users.Clear();
            hashCode = 0;
            cachedToString = null;
        }

        internal void AddUser(YamsterUser user)
        {
            int foundIndex = users.BinarySearch(user, userComparer);

            if (foundIndex >= 0)
                return; // already in list

            users.Insert(~foundIndex, user);

            hashCode = ((hashCode << 5) + hashCode) ^ user.UserId.GetHashCode();
            cachedToString = null;
        }

        public override int GetHashCode()
        {
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return this.Equals((YamsterUserSet)obj);
        }

        public bool Equals(YamsterUserSet other) // IEquatable<YamsterUserSet>
        {
            if (hashCode != other.hashCode)
                return false;
            for (int i = 0; i < users.Count; ++i)
            {
                if (users[i].UserId != other.users[i].UserId)
                    return false;
            }
            return true;
        }

        public string ToString(long userIdToExclude = 0)
        {
            if (cachedToString == null || cachedUserIdToExclude != userIdToExclude)
            {
                cachedUserIdToExclude = userIdToExclude;
                cachedToString = string.Join(", ", 
                    users.Where(x => x.UserId != userIdToExclude)
                        .OrderBy(x => x.FullName)
                        .Select(x => x.FullName)
                );
            }
            return cachedToString;
        }

        public override string ToString()
        {
            return this.ToString(0);
        }

        public YamsterUser FindUserById(long currentUserId)
        {
            int foundIndex = Utilities.CustomBinarySearch(this.users,
                user => user.UserId.CompareTo(currentUserId)
            );
            if (foundIndex >= 0)
                return users[foundIndex];
            return null;
        }
        static internal MethodInfo Info_FindUserById
            = Utilities.GetMethodInfo(typeof(YamsterUserSet), "FindUserById");

        #region IReadOnlyList<YamsterUser> Members

        public YamsterUser this[int index]
        {
            get { return this.users[index]; }
        }

        public int Count
        {
            get { return this.users.Count; }
        }

        public IEnumerator<YamsterUser> GetEnumerator()
        {
            return this.users.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((System.Collections.IEnumerable)this.users).GetEnumerator();
        }

        #endregion
    }

}
