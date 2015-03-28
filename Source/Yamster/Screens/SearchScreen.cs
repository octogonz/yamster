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
using Gtk;
using Yamster.Core;

namespace Yamster
{
    // This is the "Search" tab on the main window.
    // (NOTE: The ThreadViewer on the right is part of the MainWindow,
    // and this screen interacts with it via the MessageFocused event.)
    [System.ComponentModel.ToolboxItem(true)]
    public partial class SearchScreen : Gtk.Bin
    {
        AppContext appContext;
        YamsterCache yamsterCache;
        YamsterMessageView messageView;

        public SearchScreen()
        {
            this.Build();

            this.appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;
            lblSearchResults.Text = "No search results.";

            messageView = new YamsterMessageView(appContext);
        }

        public override void Dispose()
        {
            base.Dispose();

            if (messageView != null)
            {
                messageView.Dispose();
                messageView = null;
            }
        }

        public ThreadViewer ThreadViewer { get; set; }

        protected void ctlMessageViewer_FocusedItemChanged(object sender, EventArgs e)
        {
            if (ThreadViewer != null && ctlMessageViewer.FocusedItem != null)
            {
                var message = ctlMessageViewer.FocusedItem;
                ThreadViewer.LoadThread(message.Thread, messageToHighlight: message);
            }
        }

        protected void btnSearch_Clicked(object sender, EventArgs e)
        {
            YamsterMessageQuery query = BuildQuery();

            this.messageView.LoadQuery(query);

            var results = this.messageView.GetMessagesInView()
                    .OrderByDescending(x => x.CreatedDate)
                    .ToList();

            ctlMessageViewer.LoadMessages(results);

            lblSearchResults.Text = string.Format("{0} yams matched your search.", results.Count);
            lblSearchResults.Visible = true;
        }

        YamsterMessageQuery BuildQuery()
        {
            var predicateNodes = new List<YqlNode>();

            var sentByUsers = ctlSentByUserEntry.ParseUserList();
            if (sentByUsers.Count > 0)
            {
                var sentByNodes = new List<YqlNode>();
                foreach (YamsterUser user in sentByUsers)
                {
                    sentByNodes.Add(
                        new YqlOperationNode(YqlOperation.Equal,
                            new YqlUserPropertyNode(YqlUserProperty.Id,
                                new YqlMessagePropertyNode(YqlMessageProperty.Sender)
                            ),
                            new YqlValueNode(user.UserId)
                        )
                    );
                }
                predicateNodes.Add(
                    new YqlOperationNode(YqlOperation.Or, sentByNodes.ToArray())
                );
            }

            var mentioningUsers = ctlMentioningUserEntry.ParseUserList();
            if (mentioningUsers.Count > 0)
            {
                var mentioningNodes = new List<YqlNode>();
                foreach (YamsterUser user in mentioningUsers)
                {
                    // "in reply to" X
                    mentioningNodes.Add(
                        new YqlOperationNode(YqlOperation.Equal,
                            new YqlMessagePropertyNode(YqlMessageProperty.UserIdRepliedTo),
                            new YqlValueNode(user.UserId)
                        )
                    );
                    // "CC:" to X
                    mentioningNodes.Add(
                        new YqlListContainsNode(
                            new YqlMessagePropertyNode(YqlMessageProperty.NotifiedUsers),
                            new YqlValueNode(user.UserId)
                        )
                    );
                }
                predicateNodes.Add(
                    new YqlOperationNode(YqlOperation.Or, mentioningNodes.ToArray())
                );
            }

            var likedByUsers = ctlLikedByUserEntry.ParseUserList();
            if (likedByUsers.Count > 0)
            {
                var likedByNodes = new List<YqlNode>();
                foreach (YamsterUser user in likedByUsers)
                {
                    likedByNodes.Add(
                        new YqlListContainsNode(
                            new YqlMessagePropertyNode(YqlMessageProperty.LikingUsers),
                            new YqlValueNode(user.UserId)
                        )
                    );
                }
                predicateNodes.Add(
                    new YqlOperationNode(YqlOperation.Or, likedByNodes.ToArray())
                );
            }

            YqlTextMatchNode textMatchNode = new YqlTextMatchNode(
                new YqlMessagePropertyNode(YqlMessageProperty.Body),
                txtSearch.Text,
                wholeWords: chkWholeWords.Active,
                matchAllWords: rbMatchAll.Active
            );
            predicateNodes.Add(textMatchNode);

            YamsterMessageQuery query = new YamsterMessageQuery(
                "Search",
                new YqlOperationNode(YqlOperation.And, predicateNodes.ToArray())
            );
            return query;
        }

        protected void txtSearch_KeyRelease(object sender, KeyReleaseEventArgs args)
        {
            if (args.Event.Key == Gdk.Key.Return)
                btnSearch_Clicked(sender, EventArgs.Empty);
        }

        protected void btnShowYql_Clicked(object sender, EventArgs e)
        {
            YamsterMessageQuery query = BuildQuery();
            string xml = query.SaveToXmlString();

            using (var window = new EditYqlWindow(xml))
            {
                Utilities.RunWindowAsDialog(window);
            }

        }
    }
}

