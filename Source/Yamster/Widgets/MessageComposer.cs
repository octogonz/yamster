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
using System.Net;
using System.Threading.Tasks;
using Gdk;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class MessageComposer : Gtk.Bin
    {
        public MessageComposerMode ComposerMode { get; private set; }
        public YamsterGroup GroupContext { get; private set; }
        public YamsterThread ThreadContext { get; private set; }
        public YamsterMessage MessageBeingRepliedTo { get; private set; }

        ActionLagger updateUiLagger;

        AppContext appContext;

        bool waitingForPost = false;

        public MessageComposer()
        {
            this.Build();

            appContext = AppContext.Default;

            txtBody.Buffer.Changed += Buffer_Changed;

            ctlWidgetBox.ModifyBg(Gtk.StateType.Normal, new Color(255,255,255));
            ctlCancelBox.ModifyBg(Gtk.StateType.Normal, new Color(255,255,255));
            this.ComposerMode = MessageComposerMode.Idle;

            // This is workaround for a GTK bug where ctlReplyToBox.Visible = false
            // has no effect during initialization.
            updateUiLagger = new ActionLagger(UpdateUI, lagIntervalMs: 100);
            updateUiLagger.RequestAction(ActionLaggerQueueing.ForceDelayed);
        }

        public override void Dispose()
        {
            if (updateUiLagger != null)
            {
                updateUiLagger.Dispose();
                updateUiLagger = null;
            }
            base.Dispose();
        }

        public void UpdateUI()
        {
            this.Sensitive = !waitingForPost;
            if (waitingForPost)
                return;

            switch (this.ComposerMode)
            {
                case MessageComposerMode.ReplyToMessage:
                    lblReplyTo.LabelProp = "<b>...replying to "
                        + WebUtility.HtmlEncode(this.MessageBeingRepliedTo.SenderName) + "</b>";
                    ctlReplyToBox.Visible = true;
                    break;
                case MessageComposerMode.NewThread:
                    lblReplyTo.LabelProp = "<b>...posting in "
                        + WebUtility.HtmlEncode(this.GroupContext.GroupName) + "</b>";
                    ctlReplyToBox.Visible = true;
                    break;
                default:
                    ctlReplyToBox.Visible = false;
                    break;

            }
            ctlCancelBox.Visible = this.ComposerMode == MessageComposerMode.ReplyToMessage;
            txtBody.Editable = this.ComposerMode != MessageComposerMode.Idle;
            ctlCCUserEntry.Sensitive = this.ComposerMode != MessageComposerMode.Idle;

            txtBody.ModifyBase(Gtk.StateType.Normal, 
                txtBody.Editable ? new Color(255,255,255)
                : new Color(240,240,240)
            );

            var window = ctlCancelBox.GdkWindow;
            if (window != null)
            {
                window.Cursor = new Cursor(CursorType.Hand1);
            }

            UpdateButtonUI();
        }

        void UpdateButtonUI()
        {
            btnSend.Sensitive = !string.IsNullOrWhiteSpace(txtBody.Buffer.Text);
        }

        void Buffer_Changed(object sender, EventArgs e)
        {
            this.UpdateButtonUI();
        }

        public void ReplyToMessage(YamsterMessage messageBeingRepliedTo)
        {
            if (messageBeingRepliedTo == null)
                throw new ArgumentNullException("messageBeingRepliedTo");
            this.ComposerMode = MessageComposerMode.ReplyToMessage;
            this.GroupContext = messageBeingRepliedTo.Group;
            this.ThreadContext = messageBeingRepliedTo.Thread;
            this.MessageBeingRepliedTo = messageBeingRepliedTo;
            UpdateUI();
        }

        public void ReplyToThread(YamsterThread threadBeingRepliedTo)
        {
            if (threadBeingRepliedTo == null)
                throw new ArgumentNullException("threadBeingRepliedTo");
            this.ComposerMode = MessageComposerMode.ReplyToThread;
            this.GroupContext = threadBeingRepliedTo.Group;
            this.ThreadContext = threadBeingRepliedTo;
            this.MessageBeingRepliedTo = null;
            UpdateUI();
        }

        public void StartNewThread(YamsterGroup group)
        {
            if (group == null)
                throw new ArgumentNullException("group");
            this.ComposerMode = MessageComposerMode.NewThread;
            this.GroupContext = group;
            this.ThreadContext = null;
            this.MessageBeingRepliedTo = null;
            UpdateUI();
        }

        public void SetIdle()
        {
            this.ComposerMode = MessageComposerMode.Idle;
            this.GroupContext = null;
            this.ThreadContext = null;
            this.MessageBeingRepliedTo = null;
            UpdateUI();
        }

        public void FocusEditor()
        {
            txtBody.GrabFocus();
        }

        protected void ctlCancelBox_ButtonPress(object o, ButtonPressEventArgs args)
        {
            if (this.ComposerMode == MessageComposerMode.ReplyToMessage)
            {
                this.ReplyToThread(this.ThreadContext);
            }
        }

        static bool showedPostWarning = false;

        protected void btnSend_Clicked(object sender, EventArgs e)
        {
            if (!this.appContext.MessagePuller.Enabled)
            {
                Utilities.ShowMessageBox("Yamster is currently running in offline mode.  In order to"
                    + " post a message, you must enable syncing.",
                    "Post Message", ButtonsType.Ok, MessageType.Info);
                return;
            }

            List<YamsterUser> ccUsers = this.ctlCCUserEntry.ParseUserList();

            if (!showedPostWarning)
            {
                if (Utilities.ShowMessageBox(
@"Posting is new in Yamster.  There are currently some limitations:
● No realtime updates; always click ""Refresh"" before posting to check for new replies
● No spell check",
                    "Under Construction", ButtonsType.OkCancel, MessageType.Warning) != ResponseType.Ok)
                {
                    return;
                }

                // Don't suppress the warning if they clicked cancel
                showedPostWarning = true;
            }

            YamsterNewMessage newMessage;

            switch (this.ComposerMode)
            {
                case MessageComposerMode.NewThread:
                    newMessage = YamsterNewMessage.CreateNewThread(this.GroupContext);
                    break;
                case MessageComposerMode.ReplyToMessage:
                    newMessage = YamsterNewMessage.CreateReply(this.MessageBeingRepliedTo);
                    break;
                case MessageComposerMode.ReplyToThread:
                    newMessage = YamsterNewMessage.CreateReply(this.ThreadContext);
                    break;
                default:
                    throw new InvalidOperationException("Invalid message composer state");
            }

            newMessage.Body = txtBody.Buffer.Text;
            newMessage.CarbonCopyUsers.AddRange(ccUsers);
            var task = this.PostMessageAsync(newMessage);
        }

        async Task PostMessageAsync(YamsterNewMessage newMessage)
        {
            try
            {
                waitingForPost = true;
                UpdateUI();
                await this.appContext.YamsterCache.PostMessageAsync(newMessage);
                this.appContext.RequireForegroundThread();
                txtBody.Buffer.Text = "";
                ctlCCUserEntry.Text = "";
            }
            catch (Exception ex)
            {
                this.appContext.RequireForegroundThread();
                Utilities.ShowApplicationException(ex);
            }
            finally
            {
                waitingForPost = false;
                UpdateUI();
            }
        }

        protected void txtBody_WidgetEvent(object o, WidgetEventArgs args)
        {
            if (!txtBody.Editable)
                return;

            // Did the user type "@"?
            if (args.Event.Type == EventType.KeyPress)
            {
                EventKey eventKey = (EventKey)args.Event;
                if (eventKey.Key == Gdk.Key.at)
                {
                    // The user typed "@", so run the chooser for @-mentions
                    using (var window = new UserChooserWindow())
                    {
                        // Find the coordinates of the text editor
                        int editorX;
                        int editorY;
                        txtBody.GdkWindow.GetOrigin(out editorX, out editorY);

                        // Find the location of the cursor within the editor
                        TextIter iter = txtBody.Buffer.GetIterAtOffset(txtBody.Buffer.CursorPosition);
                        int cursorY;
                        int lineHeight;
                        txtBody.GetLineYrange(iter, out cursorY, out lineHeight);
                        
                        // Position the window immediately below the line that the cursor is on,
                        // but aligned to the left of the editor frame
                        window.SetPosition(WindowPosition.None);
                        window.Move(editorX,editorY + cursorY + lineHeight);

                        Utilities.RunWindowAsDialog(window);
                        
                        // Did the user select something?  (If not, fall through and let the
                        // "@" key be typed normally.)
                        if (window.ChosenUser != null)
                        {
                            // Prevent the editor from typing the "@" key
                            args.RetVal = true;

                            // If we're in the middle of typing a word, insert a space
                            if (iter.EndsWord() || iter.InsideWord())
                            {
                                txtBody.Buffer.InsertAtCursor(" ");
                            }

                            // Insert the alias for the user that was chosen
                            txtBody.Buffer.InsertAtCursor("@" + window.ChosenUser.Alias + " ");
                        }
                    }
                }
            }
        }
    }

    public enum MessageComposerMode
    {
        Idle,
        NewThread,
        ReplyToThread,
        ReplyToMessage
    }
}

