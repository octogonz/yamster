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
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class JsonMessagePullerSpan : IComparable<JsonMessagePullerSpan>
    {
        // For the Yammer thread that marks the *start* of this time span,
        // this is the latest (i.e. highest) message ID at the time when we
        // were doing the query.
        [JsonProperty]
        public long StartMessageId;

        // For the Yammer thread that marks the *end* of this time span,
        // this is the latest (i.e. highest) message ID at the time when we
        // were doing the query.
        [JsonProperty]
        public long EndMessageId;

        // The timestamp of the message at the end of this span.  This is used
        // to select which groups should be synced next.
        [JsonProperty]
        public DateTime StartTimeUtc;

        public JsonMessagePullerSpan()
        {
        }

        int IComparable<JsonMessagePullerSpan>.CompareTo(JsonMessagePullerSpan other)
        {
            int diff = StartMessageId.CompareTo(other.StartMessageId);
            if (diff != 0)
                return diff;
                
            return EndMessageId.CompareTo(other.EndMessageId);
        }
    }

    [JsonObject(MemberSerialization=MemberSerialization.OptIn)]
    public class JsonSyncingFeed
    {
        // For convenience, this points back to the associated DbSyncingFeed; it is not
        // persisted.
        internal DbGroupState GroupState = null;

        // For groups, this is GroupState.GroupId.  Otherwise, it is InboxFeedId.
        public long FeedId;

        // For the "messages/in_group" part of the algorithm, each "span" represents a range
        // of threads that have been downloaded (or maybe are currently downloading if 
        // GappedThreads is nonempty).  The span boundaries tell us where to look for stuff
        // that we have NOT pulled yet.
        [JsonProperty]
        public readonly List<JsonMessagePullerSpan> Spans = new List<JsonMessagePullerSpan>();

        // This is set to true when we have synced back far enough that Yammer stops
        // returning any more messages.
        [JsonProperty]
        public bool ReachedEmptyResult;

        // The number of spans we processed since the last time we checked for new messages.
        // It is used to make sure we are making progress downloading the history, i.e. not
        // spending too much time looking for new messages.
        [JsonProperty]
        public long SpanCyclesSinceCheckNew = 0;

        // Last time we pulled anything for this group
        [JsonProperty]
        public DateTime LastUpdateUtc = DateTime.MinValue;

        // Last time we checked for new messages in this group
        [JsonProperty]
        public DateTime LastCheckNewUtc = DateTime.MinValue;

        // Whether we have closed all the span gaps for this group,
        // i.e. if JsonSyncingFeed.Spans.Count <= 1
        public bool HasSpanGaps
        {
            get { return this.Spans.Count > 1; }
        }

        // True if we have not started syncing data for this group
        public bool NeverSynced
        {
            get { return this.Spans.Count == 0 && !ReachedEmptyResult; }
        }

        // Used to determine which group the MessagePuller should work on next.
        // This value basically represents where in time we expect the next
        // "older_than" REST query to start from, i.e. how much progress we have 
        // made syncing the historical messages.  We try to balance this across 
        // the groups that are being downloaded.
        public DateTime GetNextOlderThanTime()
        {
            if (this.Spans.Count == 0)
                return DateTime.MaxValue;
            return Spans.Last().StartTimeUtc;
        }

        internal void AddSpan(JsonMessagePullerSpan newSpan)
        {
            if (newSpan.StartMessageId > newSpan.EndMessageId)
                throw new ArgumentException("Invalid span: startMessageId > endMessageId");

            foreach (var span in Spans.ToArray())
            {
                // Are the spans overlapping (or adjacent)?
                if (span.StartMessageId <= (newSpan.EndMessageId + 1) && (span.EndMessageId + 1) >= newSpan.StartMessageId)
                {
                    // Expand newSpan to include span
                    if (span.EndMessageId > newSpan.EndMessageId)
                        newSpan.EndMessageId = span.EndMessageId;
                    if (span.StartMessageId < newSpan.StartMessageId)
                    {
                        newSpan.StartMessageId = span.StartMessageId;
                        newSpan.StartTimeUtc = span.StartTimeUtc;
                    }

                    // Discard the old span
                    Spans.Remove(span);
                }
            }

            // Append newSpan
            Spans.Add(newSpan);
            Spans.Sort();
        }
    }
}
