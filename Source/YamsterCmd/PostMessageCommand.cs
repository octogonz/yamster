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
using System.Threading.Tasks;
using Yamster.Core;

namespace YamsterCmd
{
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
}
