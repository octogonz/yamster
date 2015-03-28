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
using Newtonsoft.Json;

namespace Yamster.Core
{
    public class JsonMessageEnvelope
    {
        public JsonMessageEnvelope()
        {
            Messages = new JsonMessage[0];
            References = new IReferenceJson[0];
            ThreadedExtended = new Dictionary<long, JsonMessage[]>();
        }

        [JsonProperty("meta")]
        public JsonMessagesMetaEnvelope Meta { get; set; }

        [JsonProperty("messages")]
        public JsonMessage[] Messages { get; set; }

        private IReferenceJson[] references;

        [JsonConverter(typeof(YamsterReferenceJsonConverter))]
        [JsonProperty("references")]
        public IReferenceJson[] References
        {
            get { return references; }
            set
            {
                references = value;
            }
        }

        [JsonProperty("threaded_extended")]
        public Dictionary<long, JsonMessage[]> ThreadedExtended { get; set; }
    }

    public class JsonMessagesMetaEnvelope
    {
        [JsonProperty("current_user_id")]
        public long CurrentUserId;

        [JsonProperty("realtime")]
        protected Dictionary<string, string> realtime;

        [JsonProperty("unseen_message_count_by_thread")]
        protected Dictionary<long, int> unseenMessageCounts;

        public string URL { get { return realtime["uri"]; } }
        public string Token { get { return realtime["authentication_token"]; } }
        public string Channel { get { return realtime["channel_id"]; } }

        public bool IsThreadUnread(long threadId)
        {
            if (unseenMessageCounts == null)
                return false;

            if (unseenMessageCounts.ContainsKey(threadId))
                return unseenMessageCounts[threadId] > 0;

            return false;
        }
    }


    public class JsonMessage
    {
        public string RawJson { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("thread_id")]
        public long ThreadId { get; set; }

        [JsonProperty("conversation_id")]
        public long ConversationId { get; set; }

        [JsonProperty("sender_id")]
        public long SenderId { get; set; }

        [JsonProperty("sender_type")]
        public string SenderType { get; set; }

        [JsonProperty("body")]
        public JsonMessageBody Body { get; set; }

        [JsonProperty("created_at")]
        public DateTime Created { get; set; }

        [JsonProperty("message_type")]
        public string MessageType { get; set; }

        [JsonProperty("network_id")]
        public long NetworkId { get; set; }

        [JsonProperty("chat_client_sequence")]
        public long? ClientSequence { get; set; }

        [JsonProperty("liked_by")]
        public JsonLikeStats Likes { get; set; }

        [JsonProperty("notified_user_ids")]
        public long[] NotifiedUserIds { get; set; }

        [JsonProperty("web_url")]
        public string Permalink { get; set; }

        [JsonProperty("group_id")]
        public long? GroupId { get; set; }

        [JsonProperty("replied_to_id")]
        public long? RepliedToId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("direct_message")]
        public bool IsDirectMessage { get; set; }

        [JsonProperty("attachments")]
        public JsonAttachment[] Attachments { get; set; }

        public class JsonAttachment
        {
            [JsonProperty("id")]
            public long Id { get; set; }

            [JsonProperty("type")]
            public string AttachmentType { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("scaled_url")]
            public string ScaledUrlTemplate { get; set; }

            [JsonProperty("web_url")]
            public string WebUrl { get; set; }

            [JsonProperty("height")]
            public int? Height { get; set; }

            [JsonProperty("width")]
            public int? Width { get; set; }
        }

        public class JsonMessageBody
        {
            [JsonProperty("parsed")]
            public string Parsed { get; set; }

            [JsonProperty("plain")]
            public string Plain { get; set; }

            [JsonProperty("rich")]
            public string Rich { get; set; }
        }

        public class JsonLikeStats
        {
            [JsonProperty("count")]
            public int Count { get; set; }

            [JsonProperty("names")]
            public JsonLikingUser[] Users { get; set; }
        }

        public class JsonLikingUser
        {
            [JsonProperty("full_name")]
            public string FullName { get; set; }

            [JsonProperty("permalink")]
            public string Alias { get; set; }

            [JsonProperty("user_id")]
            public long UserId { get; set; }
        }
    }

}
