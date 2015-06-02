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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    /// <summary>
    /// Imports a zip archive that was created using the "Export Data" command
    /// on Yammer's administrator control panel.  The imported data completely
    /// replaces any existing data in Yamster's database.
    /// </summary>
    /// <remarks>
    /// This operation can be invoked from YamsterCmd.exe using the "-LoadCsvDump" option.
    /// </remarks>
    public class CsvDumpLoader
    {
        private AppContext appContext;

        public string FolderPath { get; private set;}

        /// <summary>
        /// If specified, then the command will also import CSV objects that are marked
        /// as having been deleted; otherwise, these objects are ignored.
        /// </summary>
        public bool IncludeDeletedObjects { get; set; }

        private long networkId = 0;

        // Ex: https://www.yammer.com/example.com
        private string networkUrl = "";

        private SortedDictionary<long, DbGroup> groupsById = new SortedDictionary<long, DbGroup>();
        private SortedDictionary<long, DbUser> usersById = new SortedDictionary<long, DbUser>();
        private SortedDictionary<long, DbConversation> conversationsById = new SortedDictionary<long, DbConversation>();
        private SortedDictionary<long, DbMessage> messagesById = new SortedDictionary<long, DbMessage>();

        private HashSet<long> deletedGroups = new HashSet<long>();
        private HashSet<long> deletedUsers = new HashSet<long>();
        private HashSet<long> deletedMessages = new HashSet<long>();
        private HashSet<long> notDeletedConversations = new HashSet<long>();

        public CsvDumpLoader(AppContext appContext)
        {
            this.appContext = appContext;
            this.FolderPath = "";
        }

        public void Load(string folderPath)
        {
            if (networkId != 0)
                throw new InvalidOperationException("The Load() method was already called for this instance.");

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("The specified folder does not exist:\r\n\""
                    + folderPath + "\"");
            }

            this.FolderPath = folderPath;

            YamsterCoreDb yamsterCoreDb = appContext.YamsterCoreDb;

            ReadCsvFiles();
            FixupMessageBodies();
            WriteToDatabase(yamsterCoreDb);
        }

        void ReadCsvFiles()
        {
            using (var csvReader = new CsvReader(Path.Combine(this.FolderPath, "Networks.csv")))
            {
                int col_Id = csvReader.GetColumnIndex("id");
                int col_Url = csvReader.GetColumnIndex("url");

                while (csvReader.ReadNextLine())
                {
                    csvReader.WrapExceptions(() =>
                    {
                        networkId = long.Parse(csvReader[col_Id]);
                        networkUrl = csvReader[col_Url];
                    });
                }
            }

            using (var csvReader = new CsvReader(Path.Combine(this.FolderPath, "Groups.csv")))
            {
                int col_Id = csvReader.GetColumnIndex("id");
                int col_Name = csvReader.GetColumnIndex("name");
                int col_Description = csvReader.GetColumnIndex("description");
                int col_Private = csvReader.GetColumnIndex("private");
                int col_Deleted = csvReader.GetColumnIndex("deleted");

                while (csvReader.ReadNextLine())
                {
                    DbGroup group = new DbGroup();
                    bool isDeleted = false;
                    csvReader.WrapExceptions(() =>
                    {
                        group.GroupId = long.Parse(csvReader[col_Id]);
                        group.GroupName = csvReader[col_Name];
                        group.GroupDescription = csvReader[col_Description];

                        // TODO: How to recognize DbGroupPrivacy.Restricted?
                        bool isPrivate = bool.Parse(csvReader[col_Private]);
                        group.Privacy = isPrivate ? DbGroupPrivacy.Private : DbGroupPrivacy.Public;

                        group.WebUrl = networkUrl + "/#/threads/inGroup?type=in_group&feedId=" + group.GroupId;

                        // TODO: How to obtain MugshotUrl?

                        isDeleted = bool.Parse(csvReader[col_Deleted]);
                    });

                    groupsById.Add(group.GroupId, group);

                    if (isDeleted)
                        deletedGroups.Add(group.GroupId);
                }
            }

            using (var csvReader = new CsvReader(Path.Combine(this.FolderPath, "Users.csv")))
            {
                int col_Id = csvReader.GetColumnIndex("id");
                int col_Name = csvReader.GetColumnIndex("name");
                int col_JobTitle = csvReader.GetColumnIndex("job_title");
                int col_Email = csvReader.GetColumnIndex("email");
                int col_State = csvReader.GetColumnIndex("state");

                while (csvReader.ReadNextLine())
                {
                    DbUser user = new DbUser();
                    bool isDeleted = false;
                    csvReader.WrapExceptions(() =>
                    {
                        user.UserId = long.Parse(csvReader[col_Id]);
                        // TODO: Is there a way to obtain the correct alias?
                        user.Alias = csvReader[col_Email].Replace('@', '_');
                        user.Email = csvReader[col_Email];
                        user.FullName = csvReader[col_Name];
                        user.JobTitle = csvReader[col_JobTitle];

                        // TODO: We need the alias to calculate user.WebUrl

                        // TODO: user.MugshotUrl

                        isDeleted = csvReader[col_State].Trim().ToUpper() == "SOFT_DELETE";
                    });

                    usersById.Add(user.UserId, user);

                    if (isDeleted)
                        deletedUsers.Add(user.UserId);
                }
            }

            using (var csvReader = new CsvReader(Path.Combine(this.FolderPath, "Messages.csv")))
            {
                int col_Id = csvReader.GetColumnIndex("id");
                int col_GroupId = csvReader.GetColumnIndex("group_id");
                int col_ThreadId = csvReader.GetColumnIndex("thread_id");
                int col_ConversationId = csvReader.GetColumnIndex("conversation_id");

                int col_InPrivateGroup = csvReader.GetColumnIndex("in_private_group");
                int col_InPrivateConversation = csvReader.GetColumnIndex("in_private_conversation");
                int col_Participants = csvReader.GetColumnIndex("participants");

                int col_CreatedAt = csvReader.GetColumnIndex("created_at");
                int col_SenderId = csvReader.GetColumnIndex("sender_id");
                int col_RepliedToId = csvReader.GetColumnIndex("replied_to_id");
                int col_Body = csvReader.GetColumnIndex("body");
                int col_DeletedAt = csvReader.GetColumnIndex("deleted_at");

                while (csvReader.ReadNextLine())
                {
                    DbMessage message = new DbMessage();
                    bool isDeleted = false;
                    csvReader.WrapExceptions(() =>
                    {
                        message.MessageId = long.Parse(csvReader[col_Id]);
                        message.GroupId = ParseLongWithDefault(csvReader[col_GroupId], YamsterGroup.AllCompanyGroupId);
                        message.ThreadId = long.Parse(csvReader[col_ThreadId]);

                        bool inPrivateGroup = bool.Parse(csvReader[col_InPrivateGroup]);
                        bool inPrivateConversation = bool.Parse(csvReader[col_InPrivateConversation]);

                        if (inPrivateConversation)
                        {
                            // This is apparently broken
                            // long conversationId = ParseLongWithDefault(csvReader[col_ConversationId], 0);
                            long conversationId = message.ThreadId;

                            DbConversation conversation;
                            if (!conversationsById.TryGetValue(conversationId, out conversation))
                            {
                                conversation = new DbConversation();
                                conversation.ConversationId = conversationId;
                                ParseParticipants(conversation.ParticipantUserIds, csvReader[col_Participants]);
                                conversationsById.Add(conversationId, conversation);
                            }

                            message.ConversationId = conversationId;
                            message.GroupId = YamsterGroup.ConversationsGroupId;
                        }

                        message.CreatedDate = DateTime.Parse(csvReader[col_CreatedAt]);
                        message.SenderUserId = long.Parse(csvReader[col_SenderId]);
                        message.MessageIdRepliedTo = ParseLongWithDefault(csvReader[col_RepliedToId], 0);

                        // TODO: Likes?

                        // TODO: Parse message.NotifiedUserIds from "CC" line
                        message.Body = csvReader[col_Body];
                        message.WebUrl = networkUrl + "/messages/" + message.MessageId;

                        // TODO: Attachments?

                        message.MessageType = DbMessageType.Update;

                        isDeleted = !string.IsNullOrWhiteSpace(csvReader[col_DeletedAt]);
                    });

                    messagesById.Add(message.MessageId, message);

                    if (isDeleted)
                    {
                        deletedMessages.Add(message.MessageId);
                    }
                    else
                    {
                        if (message.ConversationId != 0)
                            notDeletedConversations.Add(message.ConversationId);
                    }
                }
            }
        }

        private void ParseParticipants(SQLiteIdList participantIds, string csvArray)
        {
            // Ex: "user:12345,user:12346,user:12347"
            string[] parts = csvArray.Split(',');
            const string header = "user:";

            participantIds.Clear();
            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();
                if (trimmedPart.StartsWith(header))
                {
                    string rest = trimmedPart.Substring(header.Length); // Ex: 12345
                    long userId = long.Parse(rest);
                    participantIds.Add(userId);
                }
            }
        }

        static long ParseLongWithDefault(string text, long defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text))
                return defaultValue;
            return long.Parse(text);
        }

        void FixupMessageBodies()
        {
            foreach (var message in messagesById.Values)
            {
                if (message.Body.Contains("["))
                {
                    string expanded = referenceRegex.Replace(message.Body,
                        (Match match) =>
                        {
                            string referenceToken = match.Groups[1].Value;
                            string referenceId = match.Groups[2].Value;

                            // Replace "[[user:12345]]" with "Joe Smith"
                            string resolved;
                            if (TryResolveReference(referenceToken, referenceId, out resolved))
                            {
                                return resolved;
                            }
                            Debug.WriteLine("Unresolved reference: " + match.Value);
                            return match.Value;
                        }
                    );

                    expanded = inlineReferenceRegex.Replace(expanded,
                        (Match match) =>
                        {
                            // Replace "[Tag:12345:joined]" with "joined"
                            string referenceToken = match.Groups[1].Value;
                            string referenceId = match.Groups[2].Value;
                            string displayText = match.Groups[3].Value;

                            string resolved;
                            if (TryResolveReference(referenceToken, referenceId, out resolved))
                            {
                                return resolved;
                            }
                            return displayText;
                        }
                    );
                    message.Body = expanded;
                }
            }
        }

        private bool TryResolveReference(string token, string id, out string result)
        {
            result = null;
            token = token.ToUpper();
            if (token == "USER")
            {
                long userId = long.Parse(id);
                DbUser foundUser;
                if (usersById.TryGetValue(userId, out foundUser))
                {
                    result = foundUser.FullName;
                    return true;
                }
            }
            else if (token == "GROUP")
            {
                long groupId = long.Parse(id);
                DbGroup foundGroup;
                if (groupsById.TryGetValue(groupId, out foundGroup))
                {
                    result = foundGroup.GroupName;
                    return true;
                }
            }
            return false;
        }

        static Regex referenceRegex = new Regex(@"\[\[([a-z]+):([0-9]+)\]\]", RegexOptions.IgnoreCase);
        static Regex inlineReferenceRegex = new Regex(@"\[([a-z]+):([0-9]+):([^]]+)\]", RegexOptions.IgnoreCase);

        void WriteToDatabase(YamsterCoreDb yamsterCoreDb)
        {
            using (var transaction = yamsterCoreDb.BeginTransaction())
            {
                yamsterCoreDb.DeleteEverything(markArchiveDbInactive: true);

                yamsterCoreDb.UpdateProperties(row => {
                    row.CurrentNetworkId = this.networkId;
                });

                foreach (var user in this.usersById.Values)
                {
                    // NOTE: For now, deleted users are always included because they
                    // are heavily referenced
                    //if (this.IncludeDeletedObjects || !this.deletedUsers.Contains(user.UserId))

                    yamsterCoreDb.Users.InsertRecord(user);
                }

                foreach (var group in this.groupsById.Values)
                {
                    if (this.IncludeDeletedObjects || !this.deletedGroups.Contains(group.GroupId))
                    {
                        yamsterCoreDb.Groups.InsertRecord(group);

                        DbGroupState groupState = new DbGroupState() { GroupId = group.GroupId };
                        groupState.ShowInYamster = true;
                        yamsterCoreDb.GroupStates.InsertRecord(groupState);
                    }
                }

                foreach (var conversation in this.conversationsById.Values)
                {
                    if (this.IncludeDeletedObjects || this.notDeletedConversations.Contains(conversation.ConversationId))
                    {
                        yamsterCoreDb.Conversations.InsertRecord(conversation);
                    }
                }

                foreach (var message in this.messagesById.Values)
                {
                    bool messageIsDeleted = this.deletedMessages.Contains(message.MessageId);

                    if (this.IncludeDeletedObjects || !messageIsDeleted)
                    {
                        yamsterCoreDb.Messages.InsertRecord(message);

                        DbMessageState messageState = new DbMessageState() { MessageId = message.MessageId };
                        messageState.Deleted = messageIsDeleted;
                        yamsterCoreDb.MessageStates.InsertRecord(messageState);

                        // Ensure that every message has a corresponding DbThreadState for its thread
                        DbThreadState threadState = new DbThreadState() { ThreadId = message.ThreadId };
                        yamsterCoreDb.ThreadStates.InsertRecord(threadState, SQLiteConflictResolution.Ignore);
                    }
                }

                transaction.Commit();
            }

        }
    }
}
