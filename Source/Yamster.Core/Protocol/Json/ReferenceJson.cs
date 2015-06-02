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
using Newtonsoft.Json;

namespace Yamster.Core
{
    public interface IReferenceJson
    {
        string RawJson { get; set; }
        long Id { get; }
        string Type { get; }
    }

    public class JsonGroupReference : IReferenceJson
    {
        public string RawJson { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }
        public string Type { get { return "group"; } }
        public string DisplayValue { get { return FullName; } }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("privacy")]
        public string Privacy { get; set; }


        public string Permalink
        {
            get { return WebUrl; }
        }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("mugshot_url")]
        public string MugshotUrl { get; private set; }

        [JsonProperty("web_url")]
        public string WebUrl { get; set; }
    }

    public class JsonMessageReference : IReferenceJson
    {
        public string RawJson { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }
        public string Type { get { return "message"; } }

        public string DisplayValue { get { return ""; } }

        [JsonProperty("web_url")]
        public string Permalink { get; set; }

        [JsonProperty("replied_to_id")]
        public long? RepliedToId { get; set; }
        [JsonProperty("group_id")]
        public long? GroupId { get; set; }
        [JsonProperty("sender_id")]
        public long SenderId { get; set; }
        [JsonProperty("thread_id")]
        public long ThreadId { get; set; }

        [JsonProperty("created_at")]
        public DateTime Created { get; set; }

        [JsonProperty("body")]
        public MessageReferenceBody Body { get; set; }

        public class MessageReferenceBody
        {
            [JsonProperty("plain")]
            public string Plain { get; set; }
        }
    }

    public class JsonUserReference : IReferenceJson
    {
        public string RawJson { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("network_id")]
        public int NetworkId { get; protected set; }

        [JsonProperty("network_name")]
        public string NetworkName { get; protected set; }

        [JsonProperty("job_title")]
        public string JobTitle { get; protected set; }

        [JsonProperty("name")]
        public string Alias { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("ranking")]
        public decimal Ranking { get; protected set; }

        [JsonIgnore]
        public bool HasRanking { get { return Ranking > 0; } }

        [JsonProperty("mugshot_url")]
        public string MugshotUrl { get; protected set; }

        [JsonProperty("mugshot_url_template")]
        public string MugshotUrlTemplate { get; protected set; }

        // Some endpoints use "photo" instead of "mugshot_url"
        [JsonProperty("photo")]
        protected string Photo
        {
            get { return MugshotUrl; }
            set { MugshotUrl = value; }
        }

        [JsonProperty("type")]
        public string Type { get { return "user"; } }

        [JsonIgnore]
        public string DisplayValue { get { return FullName; } }

        [JsonProperty("web_url")]
        public string Permalink { get; set; }
    }

    public class JsonConversationReference : IReferenceJson
    {
        public string RawJson { get; set; }

        public long Id { get; set; }
        public string Type { get { return "conversation"; } }

        [JsonProperty("participating_names")]
        public JsonParticipant[] Participants { get; set; }
    }

    public class JsonParticipant
    {
        [JsonProperty("id")]
        public long Id { get; set; }

    }

    public class JsonThreadReferenceStats
    {
        // Number of messages in the thread, including the thread starter message.
        [JsonProperty("updates")]
        public int MessagesCount { get; set; }

        [JsonProperty("shares")]
        public int SharesCount { get; set; }

    }

    public class ThreadReferenceJson : IReferenceJson
    {
        public string RawJson { get; set; }

        public long Id { get; set; }
        public string Type { get { return "thread"; } }

        [JsonProperty("thread_starter_id")]
        public long ThreadStarterId { get; set; }

        [JsonProperty("group_id")]
        public long? GroupId { get; set; }

        [JsonProperty("stats")]
        public readonly JsonThreadReferenceStats Stats = new JsonThreadReferenceStats();
    }

    public class JsonPageReference : IReferenceJson
    {
        public string RawJson { get; set; }

        public long Id { get; set; }
        public string Type { get { return "page"; } }
    }

    public class JsonTopicReference : IReferenceJson
    {
        public string RawJson { get; set; }

        public long Id { get; set; }
        public string Type { get { return "tag"; } }
    }

}
