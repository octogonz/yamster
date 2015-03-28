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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yamster
{
    public enum ActionLaggerQueueing
    {
        Automatic,
        ForceImmediate,
        ForceDelayed
    }

    public class ActionLagger : IDisposable
    {
        Action action;
        public int LagIntervalMs { get; private set; }

        int lastActionTime;
        bool actionQueued = false;
        bool timerActive = false;
        bool disposed = false;
        bool performingAction = false;

        public ActionLagger(Action action, int lagIntervalMs=700)
        {
            this.action = action;
            this.LagIntervalMs = lagIntervalMs;
            lastActionTime = unchecked(Environment.TickCount - lagIntervalMs);
        }

        public void Dispose()
        {
            this.disposed = true;
        }

        public bool ActionQueued { get { return this.actionQueued; } }

        public void RequestAction(ActionLaggerQueueing queueing = ActionLaggerQueueing.Automatic)
        {
            if (this.disposed)
                throw new ObjectDisposedException("ActionLagger");

            if (performingAction)
            {
                Debug.WriteLine("ActionLagger: Warning: Reentrant call");
            }

            int now = Environment.TickCount;
            int nextActionTime = unchecked(lastActionTime + LagIntervalMs);
            int timeUntilNextAction = unchecked(nextActionTime - now);

            if (queueing == ActionLaggerQueueing.ForceDelayed)
                timeUntilNextAction = LagIntervalMs; // wait the maximum lag interval

            if (timeUntilNextAction <= 0 || queueing == ActionLaggerQueueing.ForceImmediate)
            {
                // Fire immediately
                lastActionTime = now;
                actionQueued = false;

                try
                {
                    performingAction = true;
                    action();
                    lastActionTime = Environment.TickCount;
                }
                finally
                {
                    performingAction = false;
                }
                return;
            }

            // Is an event already scheduled?
            if (!actionQueued)
            {
                if (!timerActive)
                {
                    actionQueued = true;
                    timerActive = true;

                    if (timeUntilNextAction <= 0)
                        timeUntilNextAction = 1; // add at least a small delay
                    GLib.Timeout.Add((uint)timeUntilNextAction, TimerCallback);
                }
                else
                {
                    Debug.WriteLine("LaggedAction: Timer is stuck");
                }
            }
        }

        public void CancelAction()
        {
            this.actionQueued = false;
        }

        bool TimerCallback()
        {
            if (this.disposed)
                return false;

            timerActive = false;
            if (actionQueued)
            {
                actionQueued = false;

                try
                {
                    lastActionTime = Environment.TickCount;
                    try
                    {
                        performingAction = true;
                        action();
                        lastActionTime = Environment.TickCount;
                    }
                    finally
                    {
                        performingAction = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("LaggedAction: Discarding exception in timer event: " + ex.Message);
                }
            }
            return false;
        }

    }
}
