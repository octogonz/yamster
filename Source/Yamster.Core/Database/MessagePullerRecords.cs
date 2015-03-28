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
    // Yamster may not have all the messages in a thread, but the algorithm guarantees
    // that we at least have a contiguous range starting with the threadstarter.  Since
    // the REST protocol requires us to fetch in backwards order, we have to break this
    // constraint while the thread is being downloaded.  For threads in this state,
    // the MessagePullerGappedThread represents the gap that we need to close to make 
    // the thread contiguous again.
    //
    // For the "messages/in_thread" part of the algorithm, a MessagePullerGappedThread
    // is created for each thread that is missing some messages.  The algorithm processes
    // the gapped threads one by one, removing them from the list as it goes.  The algorithm
    // is not allowed to change the Spans list unless the GappedThreads list is empty
    //  (This constraint could be relaxed later at the price of increased complexity.)
    [MappedTableIndex("FeedId", "ThreadId", Unique = true)]
    public class DbSyncingThread
    {
        // The Yammer message thread that we are pulling.  The thread ID is usually
        // the message ID of the threadstarter message.  However, there are cases
        // where they are different, e.g. if the original threadstarter message
        // was deleted (in which case the first reply becomes the new threadstarter
        // message).
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long ThreadId;

        // Records the feed that introduced this record.  Currently this is only being
        // tracked for diagnostic purposes.
        [SQLiteMapperProperty]
        public long FeedId;

        // The end of the previous iteration, which is where we will pick up when fetching
        // the next batch of messages.
        [SQLiteMapperProperty]
        public long? LastPulledMessageId = null;  // null means start from newest

        // When we reach this message ID, the gap has been closed, and the thread again
        // has a contiguous set of messages.  (It may be missing some latest messages.)
        [SQLiteMapperProperty]
        public long StopMessageId;

        [SQLiteMapperProperty]
        public int RetryCount;
    }

    public class DbSyncingFeed
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long FeedId;

        #region Properties Mirrored from Json

        [SQLiteMapperProperty]
        public DateTime LastUpdateUtc;

        [SQLiteMapperProperty]
        public DateTime LastCheckNewUtc;

        [SQLiteMapperProperty]
        public bool ReachedEmptyResult;

        [SQLiteMapperProperty]
        public bool HasSpanGaps;

        #endregion

        // This is a serialized MessagePullerGroupState
        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string Json;
    }

}
