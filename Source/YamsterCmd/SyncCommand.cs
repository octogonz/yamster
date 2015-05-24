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
using Yamster.Core;
using Yamster.Core.SQLite;

namespace YamsterCmd
{
    class SyncCommand : Command
    {
        public bool SingleStep;
        public bool Continuous;
        public int HistoryLimitDays;

        public override CommandId CommandId
        {
            get { return CommandId.Sync; }
        }

        public override void ShowHelp(bool detailed)
        {
            Utils.Log(
@"YamsterCmd -Sync [-SingleStep] [-Continuous] [-HistoryLimitDays <int>]
");
            if (detailed)
            {
                Utils.Log(
@"Updates the Yamster database by fetching new data from the Yammer service.

  -HistoryLimitDays <int>
    (Default is 90 days.)  This setting conserves bandwidth and database size
    by preventing very old messages from being synced.  Note that this feature
    does not delete old messages, and the time limit refers to thread updates,
    not message creation times, so some messages older than the limit will
    be synced.  Specify 0 to disable the limit.

  -Continuous
    Continue checking checking for new messages indefinitely; without this,
    the command exits after the database is up to date.

  -SingleStep
    Perform a single REST service call; otherwise, the syncing runs until
    all groups are up to date.
  
  Examples:
    YamsterCmd -Sync
    YamsterCmd -Sync -Continuous -HistoryLimitDays 0
");
                //---------------------------------------------------------------------------
            }
        }

        public override string Parse(IList<string> args)
        {
            for (int i = 0; i < args.Count; ++i)
            {
                string flag = args[i];

                string nextArg = null;
                if (i + 1 < args.Count)
                    nextArg = args[i + 1];

                switch (flag.ToUpperInvariant())
                {
                    case "-SINGLESTEP":
                        SingleStep = true;
                        break;
                    case "-CONTINUOUS":
                        Continuous = true;
                        break;
                    case "-HISTORYLIMITDAYS":
                        if (!int.TryParse(nextArg, out HistoryLimitDays))
                        {
                            return "Invalid number \"" + nextArg + "\"";
                        }
                        ++i; // consume nextArg
                        break;
                    default:
                        return "Unrecognized flag \"" + flag + "\"";
                }
            }
            if (Continuous && SingleStep)
                throw new Exception("-Continuous and -SingleStep cannot both be specified");
            return null;
        }

        public override void Run()
        {
            Utils.Log("");
            Utils.Log("Syncing from Yammer...");
            Utils.Log("");

            var appContext = AppContext.Default;
            var messagePuller = appContext.MessagePuller;

            Utils.VerifyLogin(appContext);

            messagePuller.Algorithm = MessagePullerAlgorithm.BulkDownload;
            messagePuller.HistoryLimitDays = this.HistoryLimitDays;

            bool fetchedSomething = false;

            messagePuller.CallingService += delegate(object sender, MessagePullerCallingServiceEventArgs e) {
                if (e.ThreadId != null)
                {
                    Utils.Log("Fetching messages...");
                }
                else
                {
                    Utils.Log("Fetching group index...");
                }
                fetchedSomething = true;
            };

            messagePuller.UpdatedDatabase += delegate(object sender, EventArgs e) {
                var count = appContext.YamsterCoreDb.Messages.GetCount();
                Utils.Log("Total messages in database: {0}", count);
            };

            messagePuller.Error += delegate(object sender, MessagePullerErrorEventArgs e) {
                Utils.LogError(e.Exception.Message);
            };

            for (; ; )
            {
                try
                {
                    messagePuller.Process();

                    if (!this.Continuous && messagePuller.UpToDate)
                    {
                        Utils.Log("All data is up to date.");
                        break;
                    }

                    if (this.SingleStep && fetchedSomething)
                    {
                        Utils.Log("Stopping after one REST service call.");
                        break;
                    }

                    // Don't consume 100% CPU while waiting for changes
                    System.Threading.Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    Utils.LogError(ex.Message);
                    return;
                }
            }
        }
    }
}
