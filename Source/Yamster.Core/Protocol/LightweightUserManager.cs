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
using System.Security;

namespace Yamster.Core
{
    public class LightweightUserManager
    {
        public event EventHandler LoginSuccess;
        public event EventHandler LogoutSuccess;

        private AppContext appContext;

        public LightweightUserManager(AppContext appContext)
        {
            this.appContext = appContext;
        }

        public JsonUserReference LoginWith(string accessToken)
        {
            if (accessToken == null)
                throw new ArgumentException("Missing access token");

            // Assign the new accessToken, which we will for ProcessRequest() below
            UpdateOAuth(accessToken);

            var request = new YamsterHttpRequest("/api/v1/users/current.json");
            request.Parameters.Add("include_groups", "false");
            JsonUserReference user = this.appContext.AsyncRestCaller.ProcessRequest<JsonUserReference>(request);

            if (LoginSuccess != null)
                LoginSuccess(this, EventArgs.Empty);

            return user;
        }

        private void UpdateOAuth(string accessToken)
        {
            appContext.Settings.OAuthToken = accessToken;
            appContext.Settings.Save();
        }

        /// <summary>
        /// Logs out the current user (removes OAuth tokens from settings)
        /// </summary>
        public void Logout()
        {
            appContext.Settings.OAuthToken = "";
            appContext.Settings.Save();

            if (LogoutSuccess != null)
                LogoutSuccess(this, EventArgs.Empty);
        }

    }

}
