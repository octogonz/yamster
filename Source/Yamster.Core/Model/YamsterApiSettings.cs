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
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Yamster.Core
{

    public class YamsterApiSettings
    {
        public const string SettingsFilename = "Settings.xml";

        const string ProtectedPrefix = "(Protected)";
        const string UnprotectedPrefix = "(Unprotected)";

        AppContext appContext;
        string oAuthToken;
        bool protectTokenWhenSaving;

        public YamsterApiSettings(AppContext appContext)
        {
            this.appContext = appContext;
            this.ResetDefaults();
        }

        #region Properties

        /// <summary>
        /// The session token for accessing the Yammer REST service.
        /// </summary>
        public string OAuthToken
        {
            get { return this.oAuthToken; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("OAuthToken");
                this.oAuthToken = value;
            }
        }

        /// <summary>
        /// Indicates whether the OAuthToken property should be encrypted using the current user's
        /// login credentials when saving the settings file.  This is true by default, and improves
        /// security by preventing another person from stealing the token and using it to access 
        /// the Yammer service.  However, if you want to copy your settings file to another PC
        /// (e.g. to try the Mac OS X build of Yamster), you can set this to false to make the 
        /// file portable between machines.
        /// </summary>
        public bool ProtectTokenWhenSaving
        {
            get { return this.protectTokenWhenSaving; }
            set { this.protectTokenWhenSaving = value; }
        }

        #endregion

        string GetSettingsFilePath()
        {
            return Path.Combine(this.appContext.AppDataFolder, SettingsFilename);
        }

        public void ResetDefaults()
        {
            oAuthToken = "";
            protectTokenWhenSaving = true;
        }

        public void Load()
        {
            Load(GetSettingsFilePath());
        }

        private void Load(string filename)
        {
            if (!File.Exists(filename))
            {
                Debug.WriteLine("The settings file " + filename + " is missing; resetting to defaults");
                this.ResetDefaults();
                return;
            }

            bool succeeded = false;
            try
            {
                using (StreamReader streamReader = new StreamReader(filename))
                {
                    XDocument document = XDocument.Load(streamReader, LoadOptions.SetLineInfo);

                    ReadAuthenticationProperties(document.Root);
                }
                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                    this.ResetDefaults();
            }
        }

        private void ReadAuthenticationProperties(XElement rootElement)
        {
            var authenticationElement = XmlUtilities.GetChildElement(rootElement, "Authentication");

            var oAuthTokenElement = XmlUtilities.GetChildElement(authenticationElement, "OAuthToken");
            string unprocessedToken = oAuthTokenElement.Value ?? "";

            if (string.IsNullOrWhiteSpace(unprocessedToken))
            {
                this.OAuthToken = "";
            } 
            else if (unprocessedToken.StartsWith(ProtectedPrefix))
            {
                string encrypted = unprocessedToken.Substring(ProtectedPrefix.Length);
                byte[] bytes2 = Convert.FromBase64String(encrypted);
                byte[] bytes1 = ProtectedData.Unprotect(bytes2, null, DataProtectionScope.LocalMachine);
                byte[] bytes0 = ProtectedData.Unprotect(bytes1, null, DataProtectionScope.CurrentUser);
                this.OAuthToken = Encoding.UTF8.GetString(bytes0);
            }
            else if (unprocessedToken.StartsWith(UnprotectedPrefix))
            {
                this.OAuthToken = unprocessedToken.Substring(UnprotectedPrefix.Length);
            }
            else
            {
                throw new InvalidDataException("Invalid OAuthToken value in settings file");
            }

            this.ProtectTokenWhenSaving = XmlUtilities.GetBooleanAttribute(oAuthTokenElement, "ProtectTokenWhenSaving");
        }

        public void Save()
        {
            Save(GetSettingsFilePath());
        }

        private void Save(string filename)
        {
            using (StreamWriter streamWriter = new StreamWriter(filename))
            {
                XElement rootElement = new XElement("YamsterSettings");

                XDocument xmlDocument = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    rootElement
                );

                rootElement.Add(new XAttribute("Version", "1.0"));
                WriteAuthenticationProperties(rootElement);

                var xmlWriterSettings = new XmlWriterSettings() { Indent = true, IndentChars = "    " };
                using (var xmlWriter = XmlWriter.Create(streamWriter, xmlWriterSettings))
                {
                    xmlDocument.Save(xmlWriter);
                }
                streamWriter.WriteLine(); // append a newline
            }
        }

        private void WriteAuthenticationProperties(XElement rootElement)
        {
            var authenticationElement = new XElement("Authentication");
            rootElement.Add(authenticationElement);

            string processedToken;
            if (string.IsNullOrWhiteSpace(this.OAuthToken))
            {
                processedToken = "";
            }
            else if (this.ProtectTokenWhenSaving)
            {
                byte[] bytes0 = Encoding.UTF8.GetBytes(this.OAuthToken);
                byte[] bytes1 = ProtectedData.Protect(bytes0, null, DataProtectionScope.CurrentUser);
                byte[] bytes2 = ProtectedData.Protect(bytes1, null, DataProtectionScope.LocalMachine);
                
                processedToken = ProtectedPrefix + Convert.ToBase64String(bytes2);
            }
            else
            {
                processedToken = UnprotectedPrefix + this.OAuthToken;
            }

            var oAuthTokenElement = new XElement(
                "OAuthToken", 
                new XAttribute("ProtectTokenWhenSaving", this.ProtectTokenWhenSaving),
                processedToken
            );
            authenticationElement.Add(oAuthTokenElement);
        }
    }

}
