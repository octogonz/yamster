
// This file has been generated by the GUI designer. Do not modify.
namespace Yamster
{
	public partial class AddGroupWindow
	{
		private global::Gtk.VBox vbox1;
		
		private global::Gtk.HBox hbox1;
		
		private global::Gtk.Entry txtSearch;
		
		private global::Gtk.Button btnSearch;
		
		private global::Yamster.Grid ctlGrid;
		
		private global::Gtk.HBox hbox2;
		
		private global::Gtk.Button btnCancel;
		
		private global::Gtk.Button btnAddGroup;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget Yamster.AddGroupWindow
			this.Name = "Yamster.AddGroupWindow";
			this.Title = global::Mono.Unix.Catalog.GetString ("Add Yammer Group");
			this.Icon = global::Gdk.Pixbuf.LoadFromResource ("Yamster.Resources.Yamster-16x16.png");
			this.WindowPosition = ((global::Gtk.WindowPosition)(4));
			// Container child Yamster.AddGroupWindow.Gtk.Container+ContainerChild
			this.vbox1 = new global::Gtk.VBox ();
			this.vbox1.Name = "vbox1";
			this.vbox1.Spacing = 6;
			this.vbox1.BorderWidth = ((uint)(8));
			// Container child vbox1.Gtk.Box+BoxChild
			this.hbox1 = new global::Gtk.HBox ();
			this.hbox1.Name = "hbox1";
			this.hbox1.Spacing = 6;
			// Container child hbox1.Gtk.Box+BoxChild
			this.txtSearch = new global::Gtk.Entry ();
			this.txtSearch.CanFocus = true;
			this.txtSearch.Name = "txtSearch";
			this.txtSearch.IsEditable = true;
			this.txtSearch.InvisibleChar = '●';
			this.hbox1.Add (this.txtSearch);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.txtSearch]));
			w1.Position = 0;
			// Container child hbox1.Gtk.Box+BoxChild
			this.btnSearch = new global::Gtk.Button ();
			this.btnSearch.CanFocus = true;
			this.btnSearch.Name = "btnSearch";
			this.btnSearch.UseUnderline = true;
			this.btnSearch.Label = global::Mono.Unix.Catalog.GetString ("_Search Yammer");
			this.hbox1.Add (this.btnSearch);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.btnSearch]));
			w2.Position = 1;
			w2.Expand = false;
			w2.Fill = false;
			this.vbox1.Add (this.hbox1);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.hbox1]));
			w3.Position = 0;
			w3.Expand = false;
			w3.Fill = false;
			// Container child vbox1.Gtk.Box+BoxChild
			this.ctlGrid = new global::Yamster.Grid ();
			this.ctlGrid.Events = ((global::Gdk.EventMask)(256));
			this.ctlGrid.Name = "ctlGrid";
			this.vbox1.Add (this.ctlGrid);
			global::Gtk.Box.BoxChild w4 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.ctlGrid]));
			w4.Position = 1;
			// Container child vbox1.Gtk.Box+BoxChild
			this.hbox2 = new global::Gtk.HBox ();
			this.hbox2.Name = "hbox2";
			this.hbox2.Spacing = 6;
			// Container child hbox2.Gtk.Box+BoxChild
			this.btnCancel = new global::Gtk.Button ();
			this.btnCancel.WidthRequest = 90;
			this.btnCancel.CanFocus = true;
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.UseUnderline = true;
			this.btnCancel.Label = global::Mono.Unix.Catalog.GetString ("_Cancel");
			this.hbox2.Add (this.btnCancel);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.btnCancel]));
			w5.PackType = ((global::Gtk.PackType)(1));
			w5.Position = 1;
			w5.Expand = false;
			w5.Fill = false;
			// Container child hbox2.Gtk.Box+BoxChild
			this.btnAddGroup = new global::Gtk.Button ();
			this.btnAddGroup.WidthRequest = 90;
			this.btnAddGroup.CanFocus = true;
			this.btnAddGroup.Name = "btnAddGroup";
			this.btnAddGroup.UseUnderline = true;
			this.btnAddGroup.Label = global::Mono.Unix.Catalog.GetString ("_Add Group");
			this.hbox2.Add (this.btnAddGroup);
			global::Gtk.Box.BoxChild w6 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.btnAddGroup]));
			w6.PackType = ((global::Gtk.PackType)(1));
			w6.Position = 2;
			w6.Expand = false;
			w6.Fill = false;
			this.vbox1.Add (this.hbox2);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.hbox2]));
			w7.Position = 2;
			w7.Expand = false;
			w7.Fill = false;
			this.Add (this.vbox1);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.DefaultWidth = 400;
			this.DefaultHeight = 300;
			this.Hide ();
			this.txtSearch.KeyReleaseEvent += new global::Gtk.KeyReleaseEventHandler (this.txtSearch_KeyRelease);
			this.btnSearch.Clicked += new global::System.EventHandler (this.btnSearch_Clicked);
			this.ctlGrid.FocusedItemChanged += new global::System.EventHandler (this.ctlGrid_FocusedItemChanged);
			this.btnAddGroup.Clicked += new global::System.EventHandler (this.btnAddGroup_Clicked);
			this.btnCancel.Clicked += new global::System.EventHandler (this.btnCancel_Clicked);
		}
	}
}
