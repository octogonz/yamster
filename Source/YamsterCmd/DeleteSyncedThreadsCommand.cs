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
using System.Linq;
using System.Threading;
using Yamster.Core;

namespace YamsterCmd
{
    class DeleteSyncedThreadsCommand : Command
    {
        public int GroupId = YamsterGroup.AllCompanyGroupId;
        public int OlderThanDays;
        public bool IgnoreErrors;
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

  -IgnoreErrors
    If specified, then the command will not stop if an error occurs 
    while attempting to delete a message.

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
                    case "-IGNOREERRORS":
                        IgnoreErrors = true;
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

                ProcessThreads(group, olderThanCutoff, (thread) => { });

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

                try
                {
                    ProcessThreads(group, olderThanCutoff,
                        (thread) => {
                            ++threadCount;

                            int messageNumber = 1;
                            foreach (YamsterMessage message in thread.Messages.Reverse())
                            {
                                if (ShouldSkipMessage(message))
                                {
                                    Console.Write(" S");
                                }
                                else
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
                                        if (!this.IgnoreErrors)
                                            throw;
                                        Utils.Log("");
                                        Utils.Log("ERROR: " + ex.Message);
                                    }
                                }

                                ++messageNumber;
                            }
                            Console.WriteLine();
                        }
                    );
                }
                catch (Exception ex)
                {
                    Utils.Log("");
                    Utils.Log("ERROR: " + ex.Message);
                }

                Utils.Log("");
                Utils.Log("Deleted {0} messages from {1} threads", deletedMessageCount, threadCount);
            }
        }

        private bool ShouldSkipMessage(YamsterMessage message)
        {
            // System messages cannot be deleted
            if (message.MessageType == DbMessageType.System)
                return true;
            return false;
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
