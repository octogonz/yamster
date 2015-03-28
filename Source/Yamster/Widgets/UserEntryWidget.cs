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
using System.Text.RegularExpressions;
using Gdk;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class UserEntryWidget : Gtk.Bin
    {
        AppContext appContext;

        public UserEntryWidget()
        {
            this.Build();

            appContext = AppContext.Default;

            UpdateHighlight();
        }

        public string Text
        {
            get { return txtEntry.Text; }
            set { txtEntry.Text = value; }
        }

        protected void txtEntry_WidgetEvent(object o, Gtk.WidgetEventArgs args)
        {
            // Did the user type "@"?
            if (args.Event.Type == EventType.KeyPress)
            {
                EventKey eventKey = (EventKey)args.Event;
                if (eventKey.Key == Gdk.Key.at)
                {
                    // Prevent the editor from typing the "@" key
                    args.RetVal = true;

                    // The user typed "@", so run the chooser for @-mentions
                    using (var window = new UserChooserWindow())
                    {
                        // Find the coordinates of the text editor
                        int editorX;
                        int editorY;
                        txtEntry.GdkWindow.GetOrigin(out editorX, out editorY);

                        // If the CC input box is empty, position the chooser on top of the input box.
                        // Otherwise position it just below the input box, so the existing text is not hidden.
                        window.SetPosition(WindowPosition.None);
                        if (string.IsNullOrWhiteSpace(txtEntry.Text))
                        {
                            window.Move(editorX, editorY);
                        }
                        else
                        {
                            int editorW;
                            int editorH;
                            txtEntry.GdkWindow.GetSize(out editorW, out editorH);
                            window.Move(editorX, editorY + editorH);
                        }

                        Utilities.RunWindowAsDialog(window);

                        // Did the user select something?  (If not, fall through and let the
                        // "@" key be typed normally.)
                        if (window.ChosenUser != null)
                        {
                            // Insert the alias for the user that was chosen
                            if (txtEntry.Text.Length > 0 && !txtEntry.Text.EndsWith(" "))
                                txtEntry.Text += " ";
                            txtEntry.Text += "@" + window.ChosenUser.Alias;

                            txtEntry.Position = txtEntry.Text.Length;
                        }
                    }
                }
            }
        }

        protected void txtEntry_Changed(object sender, EventArgs e)
        {
            UpdateHighlight();
        }

        void UpdateHighlight()
        {
            if (!atMentionRegex.IsMatch(txtEntry.Text))
            {
                txtEntry.ModifyBase(Gtk.StateType.Normal, new Color(255, 200, 200));
            }
            else
            {
                txtEntry.ModifyBase(Gtk.StateType.Normal, new Color(255, 255, 255));
            }
        }

        /// <summary>
        /// Given an input such as " @alias1 @alias2 @alias3 ", this returns
        /// the list of referenced users.  If the string contains a syntax error, or
        /// if the users cannot be found, then null is returned.
        /// </summary>
        public List<YamsterUser> ParseUserList()
        {
            Match match = atMentionRegex.Match(txtEntry.Text);
            if (!match.Success)
            {
                throw new InvalidOperationException("The user list is not in the correct format."
                + " It should be a list of Yammer user aliases such as \"@alias1 @alias2 @alias3\".");
            }

            HashSet<long> matchedIds = new HashSet<long>();
            List<YamsterUser> matchedUsers = new List<YamsterUser>();
            foreach (Capture capture in match.Groups[1].Captures)
            {
                YamsterUser user = this.appContext.YamsterCache.GetUserByAlias(capture.Value);
                if (!matchedIds.Contains(user.UserId))
                {
                    matchedIds.Add(user.UserId);
                    matchedUsers.Add(user);
                }
            }
            return matchedUsers;
        }

        static Regex atMentionRegex = new Regex(
@"^\s*
(?:
   @(?<alias>[a-z0-9_-]+)
   (?: \s+ @(?<alias>[a-z0-9_-]+) )*
)?
\s*$", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    }
}

