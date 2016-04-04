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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Gdk;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    // Used by ThreadViewer.  ThreadViewerMessageTile renders one message
    // as part of the thread view.
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ThreadViewerMessageTile : Gtk.Bin
    {
        AppContext appContext;
        bool highlighted = false;
        YamsterMessage loadedMessage = null;

        bool loadingUserImage = false;
        bool loadingAttachmentImage = false;

        public ThreadViewerMessageTile(ThreadViewer ownerThreadView = null)
        {
            this.Build();

            this.lblSender.WidthRequest = MainWindow.ChatPaneWidth - 80;
            this.lblBody.WidthRequest = MainWindow.ChatPaneWidth - 80;
            this.lblLikes.WidthRequest = MainWindow.ChatPaneWidth - 110;

#if DEBUG
            // This feature is experimental
            lblDelete.Visible = true;
#else
            lblDelete.Visible = false;
#endif

            this.OwnerThreadView = ownerThreadView;

            appContext = AppContext.Default;

            ctlWidgetBox.ModifyBg(Gtk.StateType.Normal, new Color(255,255,255));
            ctlAttachmentBox.ModifyBg(Gtk.StateType.Normal, new Color(255, 255, 255));

            // Clear out the placeholder data for these fields, since GTK ignores
            // assignments with invalid markup which would cause the placeholder data
            // to be displayed for real
            this.lblSender.LabelProp = "<b>???</b>";
            this.lblBody.Text = "???";

            this.UpdateUI();
        }

        public ThreadViewer OwnerThreadView
        {
            get; private set;
        }

        [DefaultValue(false)]
        public bool Highlighted
        {
            get { return Highlighted; }
            set
            {
                highlighted = value;
                UpdateUI();
            }
        }

        [Browsable(false)]
        public bool ShowSeparator
        {
            get { return ctlSeparator.Visible; }
            set { ctlSeparator.Visible = value; }
        }

        [DefaultValue(false)]
        public YamsterMessage LoadedMessage 
        {
            get { return this.loadedMessage; }
        }

        [DefaultValue(false)]
        public bool FinishedLoading 
        {
            get { return !(loadingUserImage || loadingAttachmentImage); }
        }

        protected override void OnShown()
        {
            base.OnShown();
            AssignCursors();
        }

        void AssignCursors()
        {
            var window = ctlStarEventBox.GdkWindow;
            if (window != null)
            {
                window.Cursor = new Cursor(CursorType.Hand1);
            }
            window = ctlAttachmentBox.GdkWindow;
            if (window != null)
            {
                window.Cursor = new Cursor(CursorType.Hand1);
            }
        }

        public void UpdateUI()
        {
            Color highlightColor = highlighted ? new Color(184,213,255) : new Color(255,255,255);
            ctlHighlightBox.ModifyBg(Gtk.StateType.Normal, highlightColor);

            if (this.loadedMessage != null)
            {
                var starred = this.loadedMessage.Starred;
                ctlImageStarOn.Visible = starred;
                ctlImageStarOff.Visible = !starred;

                bool liked = loadedMessage.LikedByCurrentUser;
                lblLike.Visible = !liked;
                lblUnlike.Visible = liked;

                int likesCount = loadedMessage.LikesCount;

                if (likesCount > 0)
                {
                    lblLikes.LabelProp = FormatLikingUsers();
                    ctlLikesHbox.Visible = true;
                }
                else
                {
                    ctlLikesHbox.Visible = false;
                }

                if (this.loadedMessage.Deleted)
                    this.ShowAsDeleted();
            }
        }

        string FormatLikingUsers()
        {
            var builder = new StringBuilder();

            if (loadedMessage.LikesCount == 1 && loadedMessage.LikedByCurrentUser)
            {
                builder.Append("You like this.");
            }
            else
            {
                var names = new List<string>();
                if (loadedMessage.LikedByCurrentUser)
                {
                    names.Add("You");
                }
                long currentUserId = this.appContext.YamsterCache.CurrentUserId;

                names.AddRange(
                    loadedMessage.LikingUsers
                        .Where(x => x.UserId != currentUserId)
                        .Select(x => "<b>" + WebUtility.HtmlEncode(x.FullName) + "</b>")
                );

                int unknownUsers = loadedMessage.LikesCount - loadedMessage.LikingUsers.Count;

                if (unknownUsers > 0)
                {
                    string suffix = (unknownUsers == 1) ? " other user" : " other users";
                    names.Add(unknownUsers.ToString() + suffix);
                }

                if (names.Count == 1)
                {
                    builder.AppendFormat("{0} likes this.", names[0]);
                }
                else if (names.Count == 2)
                {
                    builder.AppendFormat("{0} and {1} like this.", names[0], names[1]);
                }
                else
                {
                    for (int i = 0; i < names.Count; ++i)
                    {
                        if (builder.Length > 0)
                            builder.Append(", ");
                        if (i == names.Count - 1)
                            builder.Append("and ");
                        builder.Append(names[i]);
                    }
                    builder.Append(" like this.");
                }
            }

            return builder.ToString();
        }

        public void LoadMessage(YamsterMessage message)
        {
            this.loadedMessage = message;

            string senderLine = "<b>" + WebUtility.HtmlEncode(message.SenderName) + "</b>";

            var repliedUser = message.UserRepliedTo;
            if (repliedUser != null)
            {
                senderLine += " in reply to <b>" + WebUtility.HtmlEncode(repliedUser.FullName) + "</b>";
            }
            this.lblSender.LabelProp = senderLine;

            // TODO:
            // TextBuffer doesn't support HTML.  Yammer doesn't really support it either; 
            // the tags seen in message.Data.Body.Rich are:
            // - <br/> for newlines (versus regular CrLf in Body.Parsed and Body.Plain)
            // - <a> for user/topic references
            // - <span> for metadata
            // So rather than parsing HTML, it would be less work to detect the references
            // in Body.Parsed and replace them with hyperlinks (using a GTK TextTag and
            // custom click event handler).  Note that message_type=announcement 
            // and message_type=update have totally different syntaxes for the Body fields.
            this.lblBody.Text = message.Body;

            this.lblTimestamp.Text = message.CreatedDate.ToString("hh:mmtt M/d/yyyy")
                .ToLowerInvariant();

            UpdateUI();

            loadingUserImage = true;
            loadingAttachmentImage = true;

            FinishLoading();
        }

        public void FinishLoading()
        {
            if (FinishedLoading)
                return;

            if (this.loadedMessage == null)
            {
                loadingUserImage = false;
                loadingAttachmentImage = false;
                return;
            }

            if (loadingUserImage)
            {
                string mugshotUrl = loadedMessage.Sender.MugshotUrl;
                if (!string.IsNullOrEmpty(mugshotUrl))
                {
                    if (mugshotUrl.StartsWith("/"))
                    {
                        mugshotUrl = "http://yammer.com" + mugshotUrl;
                    }

                    var request = new YamsterHttpRequest(mugshotUrl);
                    
                    // Sometime in late 2015 or early 2016, the Yammer protocol changed such that 
                    // user profile thumbnails (but not other images) failed to load with this error:
                    // "This server does not support requests with Authorization header set"
                    // Removing the header seems to resolve the problem.
                    request.AddAuthorizationHeader = false;

                    Exception loadError = null;
                    Pixbuf image = appContext.ImageCache.TryGetImageResized(request, new Size(33, 33), out loadError);
                    if (image != null)
                    {
                        ctlUserImage.Pixbuf = image;
                        loadingUserImage = false;
                    }
                    else if (loadError != null)
                    {
                        loadingUserImage = false;
                    }
                }
            }

            if (loadingAttachmentImage)
            {
                string imageUrlTemplate = loadedMessage.AttachmentScaledUrlTemplate;
                Size originalSize = new Size(loadedMessage.AttachmentWidth, loadedMessage.AttachmentHeight);
                if (string.IsNullOrWhiteSpace(imageUrlTemplate)
                    || originalSize.Width <= 0 || originalSize.Height <= 0)
                {
                    loadingAttachmentImage = false;
                }
                else
                {
                    // If the width exceeds 250, ask the Yammer server to downscale the image
                    const int maxWidth = 250;
                    Size scaledSize = new Size();
                    if (originalSize.Width <= maxWidth)
                    {
                        scaledSize = originalSize;
                    }
                    else
                    {
                        scaledSize.Width = maxWidth;
                        scaledSize.Height = (int)(originalSize.Height * scaledSize.Width / (double)originalSize.Width);
                    }

                    string imageUrl = imageUrlTemplate
                        .Replace("{{width}}", scaledSize.Width.ToString())
                        .Replace("{{height}}", scaledSize.Height.ToString());

                    ctlAttachmentBox.Visible = true;
                    ctlAttachmentBox.TooltipText = loadedMessage.AttachmentFilename;

                    var request = new YamsterHttpRequest(imageUrl);

                    Exception loadError = null;
                    Pixbuf image = appContext.ImageCache.TryGetImage(request, out loadError);
                    if (image != null)
                    {
                        ctlAttachmentImage.Pixbuf = image;
                        AssignCursors();
                        loadingAttachmentImage = false;
                    }
                    else if (loadError != null)
                    {
                        loadingAttachmentImage = false;
                    }
                }
            }

        }

        protected void ctlStarEventBox_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            if (this.loadedMessage == null)
                return;

            // Toggle the star status
            this.loadedMessage.Starred = !this.loadedMessage.Starred;

            this.UpdateUI();
        }

        protected void ctlAttachmentBox_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            if (this.loadedMessage == null)
                return;
            string url = this.loadedMessage.AttachmentWebUrl;
            if (string.IsNullOrWhiteSpace(url))
                return;
            Process.Start(url);
        }

        protected void lblReply_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            if (OwnerThreadView != null)
                OwnerThreadView.NotifyReplyClicked(this);
        }

        protected void lblLike_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            var task = SetLikeStatusAsync(liked: true);
        }

        protected void lblUnlike_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            var task = SetLikeStatusAsync(liked: false);
        }

        async Task SetLikeStatusAsync(bool liked)
        {
            if (this.loadedMessage == null)
                return;
            try
            {
                await this.loadedMessage.SetLikeStatusAsync(liked);
                this.appContext.RequireForegroundThread();

            }
            catch (Exception ex)
            {
                this.appContext.RequireForegroundThread();
                Utilities.ShowApplicationException(ex);
            }
            finally
            {
                UpdateUI();
            }
        }

        protected void lblDelete_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            if (this.LoadedMessage != null)
            {
                this.LoadedMessage.DeleteFromServer();
                this.ShowAsDeleted();
            }
        }

        void ShowAsDeleted()
        {
            ctlStarEventBox.Visible = false;
            lblDelete.Visible = false;
            lblReply.Visible = false;
            lblUnlike.Visible = false;
            lblLike.Visible = false;
            lblDeleted.Visible = true;
        }
    }
}

