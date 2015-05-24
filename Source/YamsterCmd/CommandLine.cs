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
        PostMessage,
        DeleteSyncedThreads
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
    The integer identifier of the Yammer Group where the message will be 
    posted. If omitted or 0, the message will be posted to 
    the ""All Company"" group.

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

    class DeleteSyncedThreadsCommand : Command
    {
        public int GroupId = YamsterGroup.AllCompanyGroupId;
        public int OlderThanDays;
        public bool WhatIf;

        public override CommandId CommandId
        {
            get { return CommandId.DeleteSyncedThreads; }
        }

        public override void ShowHelp(bool detailed)
        {
            Utils.Log(
@"YamsterCmd -DeleteSyncedThreads [-GroupId <int>] [-OlderThanDays <int>]
           [-WhatIf]
");
            if (detailed)
            {
                Utils.Log(
@"Permanently deletes discussion threads from Yammer's server.  This 
command deletes all messages belonging to a single Yammer group only.
It will only find messages that have been synced to in Yamster's local 
database, so you should run ""YamsterCmd -Sync"" beforehand.

NOTE: In order to delete messages posted by other users, your Yammer
account must have administrator permissions.

  -GroupId <int>
    Specifies the group whose threads will be deleted.  If omitted, then the
    ""All Company"" group is used by default.  To find a group's id, visit
    the group's page on the Yammer web site, and then take the ""feedId""
    query parameter from the URL.

  -OlderThanDays
    If specified, then the command will only delete threads whose most 
    recent update was older than the given number of days.  Otherwise, it 
    will delete every thread in the group.

  -WhatIf
    If specified, then the command will display a report of threads that 
    would have been deleted, without actually deleting anything.

  Examples:
    YamsterCmd -DeleteSyncedThreads -GroupId 12345
    YamsterCmd -DeleteSyncedThreads -OlderThanDays 90 -WhatIf
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
                    case "-GROUPID":
                        if (!int.TryParse(nextArg, out GroupId)
                            || GroupId < 1)
                        {
                            return "Invalid group ID \"" + nextArg + "\"";
                        }
                        ++i; // consume nextArg
                        break;
                    case "-OLDERTHANDAYS":
                        if (!int.TryParse(nextArg, out OlderThanDays)
                            || OlderThanDays <= 0)
                        {
                            return "Invalid OlderThanDays amount \"" + nextArg + "\"";
                        }
                        ++i; // consume nextArg
                        break;
                    case "-WHATIF":
                        WhatIf = true;
                        break;
                    default:
                        return "Unrecognized flag \"" + flag + "\"";
                }
            }
            return null;
        }

        public override void Run()
        {
            var appContext = AppContext.Default;

            YamsterGroup group = appContext.YamsterCache.GetGroupById(GroupId);

            Utils.Log("Yammer Network: {0}", appContext.YamsterCache.CurrentNetworkUrl);
            Utils.Log("Login user: {0}", appContext.YamsterCache.CurrentUserAlias);
            Utils.Log("Group to delete: \"{0}\"", group.GroupName);

            DateTime? olderThanCutoff = null;
            if (this.OlderThanDays > 0)
            {
                olderThanCutoff = DateTime.Now.Date.AddDays(-OlderThanDays);
                Utils.Log("Only deleting threads older than {0:yyyy-MM-dd}", olderThanCutoff.Value);
                Utils.Log("");
            }
            Utils.Log("");

            if (WhatIf)
            {
                Utils.Log("Messages that would be deleted:");
                Utils.Log("");

                ProcessThreads(group, olderThanCutoff, (thread) => {});

                Utils.Log("");
                Utils.Log("No messages were actually deleted because -WhatIf was specified.");
            }
            else
            {
                Console.Write(
@"THIS COMMAND WILL *PERMANENTLY* DELETE MESSAGES FROM YOUR YAMMER NETWORK!

Yamster cannot guarantee that this command will work as expected.  In no event
shall the authors or copyright holders be liable for any accidental data loss
or other damages that may result.  Proceed at your own risk!

If accept the risks involved with bulk deleting, and have read the Yamster
license agreement and accept those terms, then type ""I AGREE"": ");

                string reply = Console.ReadLine();
                Utils.Log("");

                if (reply.ToUpper().Trim().TrimEnd('.') != "I AGREE")
                {
                    Utils.Log("ERROR: Aborting because you did not agree to the terms.");
                    return;
                }

                Utils.Log("Connecting to Yammer...");
                Utils.VerifyLogin(appContext);

                int threadCount = 0;
                int deletedMessageCount = 0;

                ProcessThreads(group, olderThanCutoff, (thread) => 
                    {
                        ++threadCount;

                        int messageNumber = 1;
                        foreach (YamsterMessage message in thread.Messages.Reverse())
                        {
                            bool wroteDot = false;
                            while (!AppContext.Default.YamsterApi.IsSafeToRequest(increasedPriority: false))
                            {
                                if (!wroteDot)
                                    Console.Write(" ");
                                Console.Write(".");
                                wroteDot = true;
                                Thread.Sleep(2000);
                            }

                            try
                            {
                                message.DeleteFromServer();
                                Console.Write(" " + messageNumber);
                                ++deletedMessageCount;
                            }
                            catch (ServerObjectNotFoundException)
                            {
                                Console.Write(" E");
                            }
                            catch (Exception ex)
                            {
                                Utils.Log("");
                                Utils.Log("ERROR: " + ex.Message);
                                break;
                            }
                            ++messageNumber;
                        }
                        Console.WriteLine();
                    }
                );

                Utils.Log("");
                Utils.Log("Deleted {0} messages from {1} threads", deletedMessageCount, threadCount);
            }
        }

        void ProcessThreads(YamsterGroup group, DateTime? olderThanCutoff, Action<YamsterThread> action)
        {
            // Id         LastUpdate              # Msgs  Message
            // 187041710  2014-05-23 01:16:04 AM  1       has created the Test Group group.
            Utils.Log("Id         LastUpdate              # Msgs  Message");
            Utils.Log("---------- ----------------------- ------- ----------------------------------");

            foreach (YamsterThread thread in group.Threads.OrderBy(x => x.LastUpdate))
            {
                if (olderThanCutoff != null)
                {
                    if (thread.LastUpdate > olderThanCutoff.Value)
                        continue;
                }

                Utils.Log("{0,-10} {1:yyyy-MM-dd hh:mm:ss tt}  {2,-7} {3}",
                    thread.ThreadId,
                    thread.LastUpdate,
                    thread.Messages.Count,
                    Utilities.TruncateWithEllipsis(
                        thread.ThreadStarterMessage.GetPreviewText(), 34));

                action(thread);
            }

        }
    }
}
