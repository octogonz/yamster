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
using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace Yamster.Core
{
    /// <summary>Use this to post a new message to the Yammer service.</summary>
    public class YamsterNewMessage
    {
        public YamsterMessage MessageBeingRepliedTo { get; private set; }
        public YamsterGroup Group { get; private set; }

        public string Body { get; set; }
        public List<YamsterUser> CarbonCopyUsers = new List<YamsterUser>();

        private YamsterNewMessage()
        {
        }

        #region Posting Scenarios

        public static YamsterNewMessage CreateReply(YamsterMessage messageBeingRepliedTo)
        {
            if (messageBeingRepliedTo == null)
                throw new ArgumentNullException("messageRepliedTo");
            var newMessage = new YamsterNewMessage();
            newMessage.MessageBeingRepliedTo = messageBeingRepliedTo;
            newMessage.Group = messageBeingRepliedTo.Group;
            return newMessage;
        }
        public static YamsterNewMessage CreateReply(YamsterThread threadBeingRepliedTo)
        {
            return CreateReply(threadBeingRepliedTo.ThreadStarterMessage);
        }

        public static YamsterNewMessage CreateNewThread(YamsterGroup group)
        {
            if (group == null)
                throw new ArgumentNullException("group");
            var newMessage = new YamsterNewMessage();
            newMessage.Group = group;
            return newMessage;
        }

        public static YamsterNewMessage CreatePrivateConversation(YamsterCache yamsterCache, IEnumerable<YamsterUser> users)
        {
            var conversationsGroup = yamsterCache.GetGroupById(YamsterGroup.ConversationsGroupId);

            var newMessage = CreateNewThread(conversationsGroup);
            newMessage.CarbonCopyUsers.AddRange(users);
            return newMessage;
        }

        public static YamsterNewMessage CreatePrivateConversation(YamsterCache yamsterCache, YamsterUser user)
        {
            return CreatePrivateConversation(yamsterCache, new YamsterUser[] { user });
        }

        #endregion

        internal NameValueCollection BuildParameters()
        {
            if (string.IsNullOrWhiteSpace(this.Body))
                throw new InvalidOperationException("The message body cannot be empty");

            var parameters = new NameValueCollection();
            parameters["body"] = this.Body.Trim();

            if (Group.GroupId == YamsterGroup.ConversationsGroupId)
            {
                // direct message
                if (this.CarbonCopyUsers.Count == 0 && MessageBeingRepliedTo == null)
                {
                    throw new InvalidOperationException("In order to create a private conversation,"
                        + " CarbonCopyUsers must contain at least one user");
                }

                foreach (var user in this.CarbonCopyUsers)
                {
                    // [sic] - multiple copies of this key are added
                    parameters.Add("direct_to_user_ids[]", user.UserId.ToString());
                }
            }
            else
            {
                if (Group.GroupId != YamsterGroup.AllCompanyGroupId)
                {
                    parameters["group_id"] = Group.GroupId.ToString();
                }

                // The clients send this key, but it always seems to be empty
                parameters["invited_user_ids"] = "";

                // This is sent by the web client, but not Android
                // parameters["skip_body_notifications"] = "true";

                if (this.CarbonCopyUsers.Count > 0)
                {
                    // e.g. "[[user:100001]],[[user:100002]]"
                    parameters["cc"] = string.Join(",",
                        this.CarbonCopyUsers.Select(x => "[[user:" + x.UserId + "]]")
                    );
                }
            }

            if (MessageBeingRepliedTo != null)
                parameters["replied_to_id"] = MessageBeingRepliedTo.MessageId.ToString();

            return parameters;
        }
    }

}
