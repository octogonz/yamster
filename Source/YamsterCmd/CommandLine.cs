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
using System.Threading;
using System.Threading.Tasks;
using Yamster.Core.SQLite;
using Yamster.Core;

namespace YamsterCmd
{
    enum CommandId
    {
        None,
        Sync,
        SetGroupSync,
        PostMessage
    }

    abstract class Command
    {
        abstract public CommandId CommandId { get; }
        abstract public void ShowHelp(bool detailed);
        abstract public string Parse(IList<string> args);
        abstract public void Run();

    }

    class SetGroupSyncCommand : Command
    {
        // SetGroupSync
        public long GroupId;
        public bool Off;

        public override CommandId CommandId
        {
            get { return CommandId.SetGroupSync; }
        }

        public override void ShowHelp(bool detailed)
        {
            Utils.Log(
@"YamsterCmd -SetGroupSync -GroupId <string> [-Off]
");
            if (detailed)
            {
                Utils.Log(
@"Configures how Yamster will sync the specified Yammer Group.

  -GroupId <string>      
    The <string> is either the word ""Private"" to indicate the user's
    private message feed, or else the integer from the feedId query parameter
    that appears in the web browser URL when viewing the Yammer Group page.

  -Off
    Turn syncing off.  If omitted, the command turns syncing on.

  Examples:
    YamsterCmd -SetGroupSync -GroupId 12345
    YamsterCmd -SetGroupSync -GroupId Private -Off
");
//---------------------------------------------------------------------------
            }
        }

        public override string Parse(IList<string> args)
        {
            bool gotGroupId = false;

            for (int i = 0; i < args.Count; ++i)
            {
                string flag = args[i];

                string nextArg = null;
                if (i + 1 < args.Count)
                    nextArg = args[i + 1];

                switch (flag.ToUpperInvariant())
                {
                    case "-GROUPID":
                        if (nextArg == null)
                            return "Missing group ID";

                        long groupId;
                        if (nextArg.Trim().ToUpper() == "PRIVATE")
                        {
                            groupId = -1;
                        }
                        else if (!long.TryParse(nextArg, out groupId))
                        {
                            return "Invalid group ID \"" + nextArg + "\"";
                        }
                        ++i; // consume nextArg
                        GroupId = groupId;
                        gotGroupId = true;
                        break;
                    case "-OFF":
                        Off = true;
                        break;
                    default:
                        return "Unrecognized flag \"" + flag + "\"";
                }
            }

            if (!gotGroupId)
                return "The -GroupId option was not specified";
            return null;
        }

        public override void Run()
        {
            var appContext = AppContext.Default;

            bool shouldSync = !this.Off;

            // Make sure the group is registered
            var group = appContext.YamsterCache.AddGroupToYamster(this.GroupId);

            // Set the sync flag
            group.ShouldSync = shouldSync;

            Utils.Log("Successfully updated group settings: ShouldSync=" + shouldSync);
        }
    }

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
  
  Attempt to download some messages for the Yammer Groups that were subscribed
  using the -SetGroupSync command.
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

    class PostMessageCommand : Command
    {
        public string Body;
        public long GroupId;

        public override CommandId CommandId
        {
            get { return CommandId.PostMessage; }
        }

        public override void ShowHelp(bool detailed)
        {
            Utils.Log(
@"YamsterCmd -PostMessage -Body <string> [-GroupId <int>]
");
            if (detailed)
            {
                Utils.Log(
@"Posts a new Yammer message.

  -Body <string>
    The message body.

  -GroupId
    The integer identifier of the Yammer Group where the message will be posted.
    If omitted or 0, the message will be posted to the ""All Company"" group.

  Examples:
    YamsterCmd -PostMessage -Body ""Hello to All Company!""
    YamsterCmd -PostMessage -Body ""Hello to private group!"" -GroupId 12345
");
                //---------------------------------------------------------------------------
            }
        }

        public override string Parse(IList<string> args)
        {
            // Default to All Company
            this.GroupId = YamsterGroup.AllCompanyGroupId;

            for (int i = 0; i < args.Count; ++i)
            {
                string flag = args[i];

                string nextArg = null;
                if (i + 1 < args.Count)
                    nextArg = args[i + 1];

                switch (flag.ToUpperInvariant())
                {
                    case "-BODY":
                        this.Body = nextArg;
                        ++i; // consume nextArg
                        break;
                    case "-GROUPID":
                        if (!long.TryParse(nextArg, out this.GroupId))
                        {
                            return "Invalid number \"" + nextArg + "\"";
                        }
                        ++i; // consume nextArg
                        break;
                    default:
                        return "Unrecognized flag \"" + flag + "\"";
                }
            }
            if (string.IsNullOrWhiteSpace(this.Body))
                return "The message body is empty";
            return null;
        }

        public override void Run()
        {
            var appContext = AppContext.Default;
            Utils.VerifyLogin(appContext);

            var group = appContext.YamsterCache.GetGroupById(this.GroupId, nullIfMissing: true);
            if (group == null)
            {
                throw new InvalidOperationException("The group #" + this.GroupId 
                    + " was not found in Yamster's database; you may need to sync it first");
            }
            var newMessage = YamsterNewMessage.CreateNewThread(group);
            newMessage.Body = this.Body;

            Utils.Log("Posting message...");

            Task<YamsterMessage> task = appContext.YamsterCache.PostMessageAsync(newMessage);
            ForegroundSynchronizationContext.RunSynchronously(task);
            var postedMessage = task.Result;

            Utils.Log("Successfully posted with MessageId={0}", postedMessage.MessageId);
        }
    }

}
