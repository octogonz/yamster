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
using System.Diagnostics;
using Yamster.Core;

namespace YamsterCmd
{
    public static class Utils
    {
        static public void Log(string message, params object[] args)
        {
            string formatted = string.Format(message, args);
            Debug.WriteLine(formatted);
            Console.WriteLine(formatted);
        }

        static public void LogError(string message, params object[] args)
        {
            Log("ERROR: " + message, args);
        }

        static public void VerifyLogin(AppContext appContext)
        {
            Log("Checking Yammer connection...");
            // First try to log in with the existing credentials
            try
            {
                appContext.YamsterApi.TestServerConnection();
            }
            catch (Exception ex)  // TokenNotFoundException, WebException
            {
                // Currently we don't provide a way to login from the command line;
                // instead, we use the credentials that were saved by the desktop app.
                throw new Exception("Unable to login to Yammer -- try running the Yamster desktop app to set up credentials.", ex);
            }
        }

        static public string ParseGroupIdFromCommandLine(string nextArg, out long groupId)
        {
            groupId = 0;

            if (nextArg == null)
                return "The -GroupId option should be followed by an ID";

            if (nextArg.Trim().ToUpper() == "PRIVATE")
            {
                groupId = YamsterGroup.ConversationsGroupId;
            }
            else if (nextArg.Trim().ToUpper() == "COMPANY")
            {
                groupId = YamsterGroup.AllCompanyGroupId;
            }
            else if (!long.TryParse(nextArg, out groupId)
                || groupId <= 0)
            {
                return "Invalid group ID \"" + nextArg + "\"";
            }
            return null;
        }
    }
}
