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

namespace Yamster.Core
{
    public class YamsterProtocolException : Exception
    {
        public YamsterProtocolException(string message, Exception innerException=null)
            : base("Protocol Error: " + message, innerException)
        {
        }
    }

    public class YamsterFailedSyncException : Exception
    {
        public long FailedFeedId { get; private set; }
        public YamsterFailedSyncException(long failedFeedId, Exception innerException)
            : base(GetMessage(failedFeedId, innerException))
        {
            FailedFeedId = failedFeedId;
        }

        static string GetMessage(long failedGroupId, Exception innerException)
        {
            return string.Format("Syncing for group #{0} was disabled due to an error: {1}",
                failedGroupId, innerException.Message);
        }
    }

    public class YamsterEmptyResultException : Exception
    {
        public long FeedId { get; private set; }
        public long ThreadId { get; private set; }
        public long? OlderThan { get; private set; }
        public int RetryCount { get; private set; }
        public bool AssumedDeleted { get { return RetryCount < 0; } }

        public YamsterEmptyResultException(long feedId, long threadId, long? olderThan, int retryCount)
        {
            this.FeedId = feedId;
            this.ThreadId = threadId;
            this.OlderThan = olderThan;
            this.RetryCount = retryCount;
        }

        public override string Message
        {
            get
            {
                if (this.AssumedDeleted)
                {
                    return string.Format("Empty result for thread {0} -- assuming deleted after several retries", ThreadId);
                }
                else
                {
                    return string.Format("Empty result for thread {0}, retryCount={1}", ThreadId, RetryCount);
                }
            }
        }
    }

    public enum MessagePullerAlgorithm
    {
        /// <summary>
        ///  Check for recent messages at periodic intervals, and download the history
        ///  incrementally.  This mode is best for reading the latest messages.
        /// </summary>
        OptimizeReading,
        /// <summary>
        /// Download the entire history first.  Check for recent messages at periodic
        /// intervals only after the history is completely filled.  This mode is more 
        /// efficient for bulk downloads.
        /// </summary>
        BulkDownload
    }

    public class MessagePullerCallingServiceEventArgs : EventArgs
    {
        public long FeedId { get; private set; }
        public long? ThreadId { get; private set; }

        public MessagePullerCallingServiceEventArgs(long feedId, long? threadId)
        {
            FeedId = feedId;
            ThreadId = threadId;
        }
    }

    public class MessagePullerErrorEventArgs : EventArgs
    {
        public Exception Exception { get; private set; }

        public MessagePullerErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
