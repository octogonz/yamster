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

namespace Yamster
{
    public class SsoUrlParser
    {
        private readonly Uri uri;

        public enum CompleteStatus
        {
            Incomplete = -1,
            Unknown = 0,
            Success = 1,
            Redirect = 2,
            Error = 3,
        }

        private CompleteStatus status;

        public SsoUrlParser(Uri uri)
        {
            this.uri = uri;
            status = CompleteStatus.Unknown;

            parseUri();
        }

        private void parseUri()
        {
            if (!uri.AbsolutePath.Contains("/sso_session/complete"))
            {
                status = CompleteStatus.Incomplete;
                return;
            }

            var paramDict = new Dictionary<string, string>();
            var query = uri.Query.TrimStart('?');
            foreach (var keyPairs in query.Split('&'))
            {
                var keyValue = keyPairs.Split('=');
                var key = keyValue[0];
                var val = keyValue[1];
                if (!string.IsNullOrEmpty(key))
                {
                    paramDict.Add(key, string.IsNullOrEmpty(val) ? string.Empty : val);
                }
            }

            AccessToken = GetOrDefault(paramDict, "access_token", string.Empty);

            RefreshToken = GetOrDefault(paramDict, "refresh_token", string.Empty);
            ErrorMessage = GetOrDefault(paramDict, "error", string.Empty);
            RedirectUrl = GetOrDefault(paramDict, "redirect", string.Empty);

            if (!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(RefreshToken))
                status = CompleteStatus.Success;
            else if (!string.IsNullOrEmpty(ErrorMessage))
                status = CompleteStatus.Error;
            else if (!string.IsNullOrEmpty(RedirectUrl))
                status = CompleteStatus.Redirect;
        }

        internal static TVal GetOrDefault<TKey, TVal>(Dictionary<TKey, TVal> dictionary, TKey key, TVal defaultValue)
        {
            if (dictionary.ContainsKey(key))
                return dictionary[key];

            return defaultValue;
        }

        public CompleteStatus Status
        {
            get { return status; }
        }

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public string ErrorMessage { get; private set; }
        public string RedirectUrl { get; private set; }
    }
}
