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

using System.Collections.Generic;
using Yamster.Core;

namespace YamsterCmd
{
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
}
