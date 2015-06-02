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
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    public class YamsterApi
    {
        AsyncRestCaller asyncRestCaller;
        YamsterApiSettings settings;

        Queue<DateTime> talliedRequestsFor30Secs = new Queue<DateTime>();
        int backOffTimemark = 0;

        public YamsterApi(AppContext appContext)
        {
            this.asyncRestCaller = appContext.AsyncRestCaller;
            this.settings = appContext.Settings;
        }

        public async Task<JsonMessageEnvelope> GetMessagesInThreadAsync(long threadId, long? olderThan = null)
        {
            string restPath = string.Format("/api/v1/messages/in_thread/{0}.json", threadId);

            var parameters = new Dictionary<string,string>();

            if (olderThan != null)
            {
                parameters["older_than"] = olderThan.Value.ToString();
            }

            TallyRequest();

            return await GetMessageEnvelopeJsonAsync(restPath, parameters);
        }

        public async Task<JsonMessageEnvelope> GetMessagesInFeedAsync(long feedId, long? olderThan = null)
        {
            string restPath;
            if (feedId == YamsterGroup.AllCompanyGroupId)
                restPath = "/api/v1/messages/general.json";
            else if (feedId == YamsterGroup.ConversationsGroupId)
                restPath = "/api/v1/messages/private.json";
            else if (feedId == YamsterGroup.InboxFeedId)
                restPath = "/api/v1/messages/inbox.json";
            else
                restPath = string.Format("/api/v1/messages/in_group/{0}.json", feedId);

            var parameters = new Dictionary<string, string>();
            parameters["threaded"] = "extended";
            if (olderThan != null)
            {
                parameters["older_than"] = olderThan.Value.ToString();
            }

            TallyRequest();
            return await GetMessageEnvelopeJsonAsync(restPath, parameters);
        }

        private async Task<JsonMessageEnvelope> GetMessageEnvelopeJsonAsync(string url,
            Dictionary<string, string> parameters = null)
        {
            JsonMessageEnvelopeUntyped untypedEnvelope = await this.asyncRestCaller
                .ProcessRequestAsync<JsonMessageEnvelopeUntyped>(HttpRequestMethod.Get, url, parameters);
            return ConvertArchiveMessageEnvelope(untypedEnvelope);
        }

        static JsonMessageEnvelope ConvertArchiveMessageEnvelope(JsonMessageEnvelopeUntyped untypedEnvelope)
        {
            string reserializedString = SQLiteJsonConverter.SaveToJson(untypedEnvelope);
            JsonMessageEnvelope envelope = SQLiteJsonConverter.LoadFromJson<JsonMessageEnvelope>(reserializedString);

            // Use untypedEnvelope to fill in the RawJson properties
            for (int i = 0; i < untypedEnvelope.Messages.Length; ++i)
            {
                JObject archiveMessage = untypedEnvelope.Messages[i];
                string reserializedMessage = SQLiteJsonConverter.SaveToJson(archiveMessage);
                envelope.Messages[i].RawJson = reserializedMessage;
            }

            foreach (var pair in untypedEnvelope.ThreadedExtended)
            {
                long threadId = pair.Key;
                JObject[] archiveMessageList = pair.Value;

                var messageList = envelope.ThreadedExtended[threadId];
                for (int i = 0; i < archiveMessageList.Length; ++i)
                {
                    JObject archiveMessage = archiveMessageList[i];
                    string reserializedMessage = SQLiteJsonConverter.SaveToJson(archiveMessage);
                    messageList[i].RawJson = reserializedMessage;
                }
            }

            // Two indexes are needed because YamsterReferenceJsonConverter discards unrecognized items
            // from envelope.References; we should probably fix that.
            for (int archiveIndex = 0, index = 0; ; )
            {
                if (archiveIndex >= untypedEnvelope.References.Length)
                    break;
                if (index >= envelope.References.Length)
                    break;

                var reference = envelope.References[index];

                JObject archiveReference = untypedEnvelope.References[archiveIndex];
                long archiveReferenceId = Convert.ToInt64(((JValue)archiveReference["id"]).Value);

                if (reference.Id == archiveReferenceId)
                {
                    string reserializedMessage = SQLiteJsonConverter.SaveToJson(archiveReference);
                    envelope.References[index].RawJson = reserializedMessage;
                    ++index;
                }
                ++archiveIndex;
            }
#if DEBUG
            foreach (var reference in envelope.References)
                Debug.Assert(reference.RawJson != null);
#endif
            return envelope;
        }

        public void TestServerConnection()
        {
            this.asyncRestCaller.ProcessRequest<JsonMessageEnvelope>(HttpRequestMethod.Get,
                "/api/v1/messages.json");
        }

        public IList<JsonSearchedGroup> SearchForGroups(string keyword, int maxResults)
        {
            
            var url = this.settings.YammerServiceUrl + "/api/v1/autocomplete/ranked";
            
            var parameters = new Dictionary<string, string>();
            parameters["prefix"] = keyword;
            parameters["models"] = "group:" + maxResults.ToString();

            JsonAutoCompleteResult result = this.asyncRestCaller.ProcessRequest<JsonAutoCompleteResult>(
                HttpRequestMethod.Get, url, parameters);
            return result.Groups;
        }

        internal async Task<JsonMessageEnvelope> PostMessageAsync(YamsterNewMessage messageToPost)
        {
            var parameters = messageToPost.BuildParameters();

            try
            {
                byte[] result = await this.asyncRestCaller.PostFormAsync("/api/v1/messages", parameters);
                var untypedEnvelope = YamsterApi.ParseJsonResponse<JsonMessageEnvelopeUntyped>(result);
                var envelope = ConvertArchiveMessageEnvelope(untypedEnvelope);
                return envelope;
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Fish out the JSON error message from the response, e.g. something like this:
                    //   {"base":["Cannot reply to deleted message: 12345"]}
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        throw new YamsterProtocolException("POST failed:\r\n\r\n" + json);
                    }
                }
                throw;
            }
        }

        internal async Task DeleteMessageAsync(long messageId)
        {
            // TODO: Need to schedule this request rather than executing it immediately
            TallyRequest();

            var parameters = new NameValueCollection();
            parameters["_method"] = "DELETE";
            string url = string.Format("/api/v1/messages/{0}.json", messageId);
            try
            {
                // NOTE: Yammer doesn't seem to impose any rate limits for deletes,
                // so we don't need to check for RateLimitExceededException here.
                await this.asyncRestCaller.PostFormAsync(url, parameters);

                // But be a good citizen about it:
                await Task.Delay(750);
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    var response = (HttpWebResponse) ex.Response;
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new ServerObjectNotFoundException("Unable to delete the message."
                            + "  It could have been deleted already, or you might not have permissions.",
                            ex);
                    }
                }
                throw;
            }
        }

        internal async Task SetMessageLikeStatusAsync(long messageId, bool liked)
        {
            // TODO: Need to schedule this request rather than executing it immediately
            TallyRequest();

            var parameters = new NameValueCollection();
            parameters["message_id"] = messageId.ToString();

            try
            {
                if (liked)
                {
                    await this.asyncRestCaller.PostFormAsync("/api/v1/messages/liked_by/current.json", parameters,
                        HttpRequestMethod.Post);
                }
                else
                {
                    parameters["_method"] = "DELETE";
                    await this.asyncRestCaller.PostFormAsync("/api/v1/messages/liked_by/current.json", parameters,
                        HttpRequestMethod.Delete);
                }
            }
            catch (WebException ex)
            {
                try
                {
                    YamsterApi.CheckForErrors(ex);
                }
                catch (RateLimitExceededException)
                {
                    NotifyRateLimitExceeded();
                    throw;
                }
                throw;
            }
        }

        static public void CheckForErrors(WebException ex)
        {
            HttpWebResponse response = ex.Response as HttpWebResponse;
            string str = null;
            if (response != null)
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    str = reader.ReadToEnd();
                }
            }
            if (!string.IsNullOrEmpty(str))
            {
                try
                {
                    JsonErrorResponse response2 = JsonConvert.DeserializeObject<JsonErrorResponse>(str);
                    if (response2 != null)
                    {
                        Exception exception = GetExceptionForError(response2);
                        if (exception != null)
                        {
                            throw exception;
                        }
                    }
                }
                catch (JsonReaderException exception2)
                {
                    Debug.WriteLine("Could not translate error response: " + exception2.Message);
                }
            }
        }

        static private Exception GetExceptionForError(JsonErrorResponse response)
        {
            switch (response.Code)
            {
                case 4:
                    // User account was deactivated
                    return new TokenNotFoundException();
                case 16:
                    return new TokenNotFoundException();
                case 32:
                case 33:
                    return new RateLimitExceededException();
                default:
                    return null;
            }
        }

        public static T ParseJsonResponse<T>(byte[] content)
        {
            string response = Encoding.UTF8.GetString(content);
            return (T)JsonConvert.DeserializeObject(response, typeof(T));
        }

        #region Request Throttling

        void TallyRequest()
        {
            DateTime now = DateTime.Now;
            talliedRequestsFor30Secs.Enqueue(now);
            PurgeTalliedRequests(now);
        }

        void PurgeTalliedRequests(DateTime now)
        {
            // Pull out any requests older than 30 seconds
            while (talliedRequestsFor30Secs.Count > 0)
            {
                var first = talliedRequestsFor30Secs.Peek();
                var elapsed = now.Subtract(first);

                if (elapsed.TotalSeconds < 30)
                    break;

                talliedRequestsFor30Secs.Dequeue();
            }
        }

        public bool IsSafeToRequest(bool increasedPriority)
        {
            // Was BackOff() called?
            if (backOffTimemark != 0)
            {
                if (unchecked(Environment.TickCount - backOffTimemark) < 0)
                    return false;
                backOffTimemark = 0;
                Throttled = false;
            }

            // "API calls are subject to rate limiting. Exceeding any rate limits will result in all 
            // endpoints returning a status code of 429 (Too Many Requests). Rate limits are per user
            // per app. There are four rate limits:
            // Autocomplete: 10 requests in 10 seconds.
            // Messages: 10 requests in 30 seconds.
            // Notifications: 10 requests in 30 seconds.
            // All Other Resources: 10 requests in 10 seconds."
            // https://developer.yammer.com/restapi/

            DateTime now = DateTime.Now;
            PurgeTalliedRequests(now);

            // Stay reasonably below the maximum
            int maxRequests = increasedPriority ? 8 : 7;
            return talliedRequestsFor30Secs.Count < maxRequests;
        }

        // Call this if RateLimitExceededException is thrown
        public void NotifyRateLimitExceeded()
        {
            Throttled = true;
            BackOff();
        }

        // Call this to suspend requests for some other reason than RateLimitExceededException
        public void BackOff()
        {
            backOffTimemark = Environment.TickCount + 30 * 1000; // sleep 30 seconds
        }

        public bool Throttled
        {
            get; private set;
        }

        public int TotalTalliedRequestsFor30Secs 
        { 
            get { return talliedRequestsFor30Secs.Count(); }
        }

        #endregion
    }
}
