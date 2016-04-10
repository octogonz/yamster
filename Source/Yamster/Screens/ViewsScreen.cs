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
using System.Diagnostics;
using System.Linq;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ViewsScreen : Gtk.Bin
    {
        const int ThreadNotebookPage = 0;
        const int MessageNotebookPage = 1;

        #region class GridQuery
        class GridQuery
        {
            public ViewsScreen OwnerScreen { get; private set; }
            public YamsterModelQuery Query { get; private set; }
            public YamsterModelView View { get; private set; }

            public GridQuery(ViewsScreen ownerScreen, YamsterModelQuery query)
            {
                this.OwnerScreen = ownerScreen;
                this.Query = query;

                if (query.ModelType == YamsterModelType.Thread)
                {
                    View = new YamsterThreadView(AppContext.Default);
                }
                else if (query.ModelType == YamsterModelType.Message)
                {
                    View = new YamsterMessageView(AppContext.Default);
                }
                else
                {
                    throw new NotSupportedException();
                }
                View.ViewChanged += View_ViewChanged;
                
            }

            public YamsterModelType ModelType 
            {
                get { return this.Query.ModelType; }
            }

            void View_ViewChanged(object sender, ViewChangedEventArgs e)
            {
                OwnerScreen.NotifyViewChanged(this, e);
            }

        }
        #endregion

        AppContext appContext;

        readonly List<GridQuery> gridQueries = new List<GridQuery>();

        GridQuery focusedGridQuery = null;

        ActionLagger reloadModelGridLagger;

        public ViewsScreen()
        {
            this.Build();

            this.appContext = AppContext.Default;

            reloadModelGridLagger = new ActionLagger(ReloadModelGridAction);

            this.ctlNotebook.ShowTabs = false;

            ctlQueriesGrid.ItemType = typeof(GridQuery);

            SetupViewsGridColumns();

            ctlQueriesGrid.FormatCell += ctlQueriesGrid_FormatCell;

            SetupQueries();

        }

        public ThreadViewer ThreadViewer { get; set; }

        void SetupViewsGridColumns()
        {
            ctlQueriesGrid.AddTextColumn("Name", 140,
                (GridQuery gridQuery) => {
                    string title = gridQuery.Query.Title;
                    if (appContext.Settings.ShowUnreadThreadCount)
                    {
                        if (gridQuery.View.UnreadItemCount > 0)
                        {
                            title += " [" + gridQuery.View.UnreadItemCount + "]";
                        }
                    }
                    return title;
                }
            );
            ctlQueriesGrid.AddTextColumn("View Type", 80,
                (GridQuery gridQuery) => {
                    switch (gridQuery.Query.ModelType)
                    {
                        case YamsterModelType.Thread: 
                            return "Thread";
                        case YamsterModelType.Message:
                            return "Yam";
                        default:
                            return "?";
                    }                    
                }
            );
        }

        private void ctlQueriesGrid_FormatCell(object sender, GridFormatCellEventArgs e)
        {
            var gridQuery = (GridQuery) e.Item;

            bool bold = !gridQuery.View.Read;
            e.Renderer.Weight = bold ? 800 : 400;
        }

        #region SetupQueries()
        void SetupQueries()
        {
            this.gridQueries.Clear();

            AddQuery(new YamsterThreadQuery("Yammer Inbox",
                new YqlThreadPropertyNode(
                    YqlThreadProperty.SeenInInboxFeed
                )
            ));

            AddQuery(new YamsterMessageQuery("Starred Yams",
                new YqlOperationNode(YqlOperation.And,
                    new YqlValueNode(true),
                    new YqlOperationNode(YqlOperation.Or,
                        new YqlValueNode(false),
                        new YqlValueNode(true)
                    ),
                    new YqlMessagePropertyNode(YqlMessageProperty.Starred)
                )
            ));

            AddQuery(new YamsterMessageQuery("Sent By Me",
                new YqlOperationNode(YqlOperation.Equal,
                    new YqlUserPropertyNode(YqlUserProperty.Id,
                        new YqlMessagePropertyNode(YqlMessageProperty.Sender)
                    ),
                    new YqlOperationNode(YqlOperation.MyUserId)
                )
            ));

            AddQuery(new YamsterMessageQuery("Mentioning Me",
                new YqlOperationNode(YqlOperation.Or,
                    // "in reply to" me
                    new YqlOperationNode(YqlOperation.Equal,
                        new YqlMessagePropertyNode(YqlMessageProperty.UserIdRepliedTo),
                        new YqlOperationNode(YqlOperation.MyUserId)
                    ),
                    // "CC:" to me
                    new YqlListContainsNode(
                        new YqlMessagePropertyNode(YqlMessageProperty.NotifiedUsers),
                        new YqlOperationNode(YqlOperation.MyUserId)
                    )
                )
            ));

            AddQuery(new YamsterMessageQuery("Liked By Me",
                new YqlListContainsNode(
                    new YqlMessagePropertyNode(YqlMessageProperty.LikingUsers),
                    new YqlOperationNode(YqlOperation.MyUserId)
                )
            ));

            AddQuery(new YamsterThreadQuery("All Threads",
                new YqlValueNode(true)
            ));

            AddQuery(new YamsterMessageQuery("All Yams",
                new YqlValueNode(true)
            ));

            LoadQueries();
        }

        void AddQuery(YamsterModelQuery query)
        {
            gridQueries.Add(new GridQuery(this, query));
        }

        #endregion

        public void LoadQueries()
        {
            ctlQueriesGrid.ReplaceAllItems(gridQueries);
        }

        protected void ctlQueriesGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            ReloadQuery();
        }

        void ReloadQuery()
        {
            focusedGridQuery = (GridQuery) ctlQueriesGrid.FocusedItem;

            if (focusedGridQuery != null)
            {

                if (focusedGridQuery.ModelType == YamsterModelType.Thread)
                {
                    var copyQuery = focusedGridQuery.Query.Clone();
                    if (!chkShowReadThreads.Active)
                    {
                        // Add a filter clause to hide threads that were already read
                        copyQuery.FilterNode = new YqlOperationNode(YqlOperation.And,
                            new YqlOperationNode(YqlOperation.Not,
                                new YqlThreadPropertyNode(YqlThreadProperty.Read)
                            ),
                            copyQuery.FilterNode
                        );
                    }
                    focusedGridQuery.View.LoadQuery(copyQuery);
                }
                else
                {
                    focusedGridQuery.View.LoadQuery(focusedGridQuery.Query);
                }
            }
            reloadModelGridLagger.RequestAction();
        }

        void NotifyViewChanged(GridQuery gridQuery, ViewChangedEventArgs e)
        {
            if (e.ChangeType == YamsterViewChangeType.StatisticsChanged)
            {
                ctlQueriesGrid.QueueDraw();
            }
            else 
            {
                // For all other events, rebuild everything
                if (gridQuery == focusedGridQuery)
                    reloadModelGridLagger.RequestAction();
            }
        }

        void ReloadModelGridAction()
        {
            if (focusedGridQuery == null)
            {
                ctlThreadGrid.ClearThreads();
                ctlMessageGrid.ClearMessages();
                ctlNotebook.Page = MessageNotebookPage;
                return;
            }

            if (focusedGridQuery.ModelType == YamsterModelType.Thread)
            {
                var threadView = (YamsterThreadView) focusedGridQuery.View;
                var threads = threadView.GetThreadsInView()
                    .OrderByDescending(x => x.LastUpdate)
                    .ToArray();
                ctlThreadGrid.LoadThreads(threads);

                ctlNotebook.Page = ThreadNotebookPage;
                ctlMessageGrid.ClearMessages();
            }
            else
            {
                Debug.Assert(focusedGridQuery.ModelType == YamsterModelType.Message);

                var messageView = (YamsterMessageView)focusedGridQuery.View;
                var messages = messageView.GetMessagesInView()
                    .OrderByDescending(x => x.CreatedDate)
                    .ToArray();
                ctlMessageGrid.LoadMessages(messages);

                ctlNotebook.Page = MessageNotebookPage;
                ctlThreadGrid.ClearThreads();
            }
        }

        protected void ctlThreadGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            if (ThreadViewer != null && ctlThreadGrid.FocusedItem != null)
            {
                ThreadViewer.LoadThread(ctlThreadGrid.FocusedItem);
            }
        }

        protected void ctlMessageGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            if (ThreadViewer != null && ctlMessageGrid.FocusedItem != null)
            {
                var message = ctlMessageGrid.FocusedItem;
                ThreadViewer.LoadThread(message.Thread, messageToHighlight: message);
            }
        }

        protected void chkShowReadThreads_Toggled(object sender, EventArgs e)
        {
            ReloadQuery();
        }

        protected void lblMarkAllRead_ButtonPress(object o, ButtonPressEventArgs args)
        {
            if (focusedGridQuery == null || focusedGridQuery.ModelType != YamsterModelType.Thread)
                return;
            var view = (YamsterThreadView)focusedGridQuery.View;
            var threadIdsToMark = view.GetThreadsInView()
                .Where(x => !x.Read)
                .Select(x => x.ThreadId)
                .ToList();

            if (threadIdsToMark.Count == 0)
                return;

            if (Utilities.ShowMessageBox(
                string.Format("Are you sure you want to mark {0} threads as read?",
                    threadIdsToMark.Count),
                "Yamster",
                Gtk.ButtonsType.YesNo, Gtk.MessageType.Question) != Gtk.ResponseType.Yes)
                return;

            string updateSql = string.Format(
@"UPDATE [MessageStates]
SET [Read] = 1
WHERE [MessageId] IN (
    SELECT [MessageId] FROM [Messages] WHERE [ThreadId] IN 
    (
        {0}
    )
)", string.Join(",", threadIdsToMark));

            AppContext.Default.YamsterCoreDb.Mapper.ExecuteNonQuery(updateSql);
        }

    }
}

