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
    public enum FreshenThreadState
    {
        Queued,
        Processing,
        Completed
    }

    public class FreshenThreadRequest
    {
        public event EventHandler StateChanged;

        public FreshenThreadState State { get; private set; }

        public YamsterThread Thread { get; private set; }
        public MessagePuller MessagePuller { get; private set; }

        public Exception Error { get; private set; }

        internal FreshenThreadRequest(YamsterThread thread, MessagePuller messagePuller)
        {
            this.Thread = thread;
            this.MessagePuller = messagePuller;
            this.State = FreshenThreadState.Queued;
            this.Error = null;
        }

        internal void SetState(FreshenThreadState newState)
        {
            this.State = newState;
            try
            {
                if (StateChanged != null)
                    StateChanged(this, EventArgs.Empty);
            }
            finally
            {
                // Detach all event handlers
                if (this.State == FreshenThreadState.Completed)
                {
                    StateChanged = null;
                }
            }
        }

        internal void SetError(Exception error)
        {
            if (this.State != FreshenThreadState.Completed)
            {
                this.Error = error;
                this.SetState(FreshenThreadState.Completed);
            }
        }
    }

}
