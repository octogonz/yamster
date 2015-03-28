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
using System.Collections.ObjectModel;
using System.Diagnostics;
using Yamster.Core.SQLite;
using System.Reflection;

namespace Yamster.Core
{
    public class YamsterUser : YamsterModel
    {
        readonly long userId;
        DbUser dbUser;

        internal YamsterUser(long userId, YamsterCache yamsterCache)
            : base(yamsterCache)
        {
            this.userId = userId;
            this.dbUser = new DbUser() 
            {
                UserId = userId,
                FullName = "(User #" + userId + ")",
                ChangeNumber = 0
            };
        }

        public override YamsterModelType ModelType { get { return YamsterModelType.User; } }

        internal DbUser DbUser
        {
            get { return this.dbUser; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("DbUser");
                if (value.UserId != userId)
                    throw new ArgumentException("Cannot change ID");
                this.dbUser = value;
                UpdateLoadedStatus();
            }
        }

        public long UserId { get { return this.userId; } }
        static internal PropertyInfo Info_UserId = Utilities.GetPropertyInfo(typeof(YamsterUser), "UserId");

        public string Alias { get { return dbUser.Alias; } }
        public string FullName { get { return dbUser.FullName; } }
        public string JobTitle { get { return dbUser.JobTitle; } }
        public string WebUrl { get { return dbUser.WebUrl; } }
        public string MugshotUrl { get { return dbUser.MugshotUrl; } }

        protected override bool CheckIfLoaded() // abstract
        {
            return this.dbUser.ChangeNumber != 0;
        }

        internal override void FireChangeEvent(YamsterModelChangeType yamsterModelChangeType) // abstract
        {
            this.YamsterCache.FireChangedEvent(new YamsterUserChangedEventArgs(this, yamsterModelChangeType));
        }

        public override string ToString()
        {
            return GetNotLoadedPrefix() + "User #" + this.UserId + " \"" + this.FullName + "\"";
        }

    }

    public class YamsterUserChangedEventArgs : ModelChangedEventArgs
    {
        public YamsterUser User { get; private set; }

        public YamsterUserChangedEventArgs(YamsterUser user, YamsterModelChangeType changeType)
            : base(changeType)
        {
            this.User = user;
        }

        public override YamsterModel Model
        {
            get { return this.User; }
        }

        public override string ToString()
        {
            return string.Format("User {0}: ID={1} Name={1}", this.ChangeType, this.User.UserId,
                this.User.FullName);
        }
    }

}
