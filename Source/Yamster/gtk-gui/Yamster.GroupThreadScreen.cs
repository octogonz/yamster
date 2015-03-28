
// This file has been generated by the GUI designer. Do not modify.
namespace Yamster
{
	public partial class GroupThreadScreen
	{
		private global::Gtk.HPaned hpaned1;
		
		private global::Gtk.VBox vbox12;
		
		private global::Gtk.Label label3;
		
		private global::Yamster.GroupGrid ctlGroupGrid;
		
		private global::Gtk.CheckButton chkNonSyncedGroups;
		
		private global::Gtk.VBox vbox13;
		
		private global::Gtk.HBox hbox2;
		
		private global::Gtk.Label label4;
		
		private global::Gtk.Label label5;
		
		private global::Yamster.ThreadGrid ctlThreadGrid;
		
		private global::Gtk.HBox hbox1;
		
		private global::Gtk.CheckButton chkShowReadThreads;
		
		private global::Gtk.Label lblMarkAllRead;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget Yamster.GroupThreadScreen
			global::Stetic.BinContainer.Attach (this);
			this.Name = "Yamster.GroupThreadScreen";
			// Container child Yamster.GroupThreadScreen.Gtk.Container+ContainerChild
			this.hpaned1 = new global::Gtk.HPaned ();
			this.hpaned1.CanFocus = true;
			this.hpaned1.Name = "hpaned1";
			this.hpaned1.Position = 170;
			this.hpaned1.BorderWidth = ((uint)(6));
			// Container child hpaned1.Gtk.Paned+PanedChild
			this.vbox12 = new global::Gtk.VBox ();
			this.vbox12.Name = "vbox12";
			// Container child vbox12.Gtk.Box+BoxChild
			this.label3 = new global::Gtk.Label ();
			this.label3.Name = "label3";
			this.label3.Xalign = 0F;
			this.label3.LabelProp = global::Mono.Unix.Catalog.GetString ("Groups");
			this.vbox12.Add (this.label3);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.vbox12 [this.label3]));
			w1.Position = 0;
			w1.Expand = false;
			w1.Fill = false;
			// Container child vbox12.Gtk.Box+BoxChild
			this.ctlGroupGrid = new global::Yamster.GroupGrid ();
			this.ctlGroupGrid.WidthRequest = 200;
			this.ctlGroupGrid.Events = ((global::Gdk.EventMask)(256));
			this.ctlGroupGrid.Name = "ctlGroupGrid";
			this.vbox12.Add (this.ctlGroupGrid);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.vbox12 [this.ctlGroupGrid]));
			w2.Position = 1;
			// Container child vbox12.Gtk.Box+BoxChild
			this.chkNonSyncedGroups = new global::Gtk.CheckButton ();
			this.chkNonSyncedGroups.CanFocus = true;
			this.chkNonSyncedGroups.Name = "chkNonSyncedGroups";
			this.chkNonSyncedGroups.Label = global::Mono.Unix.Catalog.GetString ("Show non-synced groups");
			this.chkNonSyncedGroups.DrawIndicator = true;
			this.chkNonSyncedGroups.UseUnderline = true;
			this.vbox12.Add (this.chkNonSyncedGroups);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.vbox12 [this.chkNonSyncedGroups]));
			w3.Position = 2;
			w3.Expand = false;
			w3.Fill = false;
			this.hpaned1.Add (this.vbox12);
			global::Gtk.Paned.PanedChild w4 = ((global::Gtk.Paned.PanedChild)(this.hpaned1 [this.vbox12]));
			w4.Resize = false;
			// Container child hpaned1.Gtk.Paned+PanedChild
			this.vbox13 = new global::Gtk.VBox ();
			this.vbox13.Name = "vbox13";
			// Container child vbox13.Gtk.Box+BoxChild
			this.hbox2 = new global::Gtk.HBox ();
			this.hbox2.Name = "hbox2";
			this.hbox2.Spacing = 6;
			// Container child hbox2.Gtk.Box+BoxChild
			this.label4 = new global::Gtk.Label ();
			this.label4.Name = "label4";
			this.label4.Xalign = 0F;
			this.label4.LabelProp = global::Mono.Unix.Catalog.GetString ("Threads");
			this.hbox2.Add (this.label4);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.label4]));
			w5.Position = 0;
			w5.Expand = false;
			w5.Fill = false;
			// Container child hbox2.Gtk.Box+BoxChild
			this.label5 = new global::Gtk.Label ();
			this.label5.Name = "label5";
			this.label5.Xalign = 0F;
			this.label5.LabelProp = global::Mono.Unix.Catalog.GetString ("(press SPACE to mark read)");
			this.hbox2.Add (this.label5);
			global::Gtk.Box.BoxChild w6 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.label5]));
			w6.PackType = ((global::Gtk.PackType)(1));
			w6.Position = 1;
			w6.Expand = false;
			w6.Fill = false;
			this.vbox13.Add (this.hbox2);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.vbox13 [this.hbox2]));
			w7.Position = 0;
			w7.Expand = false;
			w7.Fill = false;
			// Container child vbox13.Gtk.Box+BoxChild
			this.ctlThreadGrid = new global::Yamster.ThreadGrid ();
			this.ctlThreadGrid.Events = ((global::Gdk.EventMask)(256));
			this.ctlThreadGrid.Name = "ctlThreadGrid";
			this.ctlThreadGrid.TrackRead = false;
			this.vbox13.Add (this.ctlThreadGrid);
			global::Gtk.Box.BoxChild w8 = ((global::Gtk.Box.BoxChild)(this.vbox13 [this.ctlThreadGrid]));
			w8.Position = 1;
			// Container child vbox13.Gtk.Box+BoxChild
			this.hbox1 = new global::Gtk.HBox ();
			this.hbox1.Name = "hbox1";
			this.hbox1.Spacing = 6;
			// Container child hbox1.Gtk.Box+BoxChild
			this.chkShowReadThreads = new global::Gtk.CheckButton ();
			this.chkShowReadThreads.CanFocus = true;
			this.chkShowReadThreads.Name = "chkShowReadThreads";
			this.chkShowReadThreads.Label = global::Mono.Unix.Catalog.GetString ("Show read threads");
			this.chkShowReadThreads.DrawIndicator = true;
			this.chkShowReadThreads.UseUnderline = true;
			this.hbox1.Add (this.chkShowReadThreads);
			global::Gtk.Box.BoxChild w9 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.chkShowReadThreads]));
			w9.Position = 0;
			// Container child hbox1.Gtk.Box+BoxChild
			this.lblMarkAllRead = new global::Gtk.Label ();
			this.lblMarkAllRead.Name = "lblMarkAllRead";
			this.lblMarkAllRead.Xalign = 1F;
			this.lblMarkAllRead.LabelProp = global::Mono.Unix.Catalog.GetString ("<a href=\"\">Mark All Read</a>");
			this.lblMarkAllRead.UseMarkup = true;
			this.hbox1.Add (this.lblMarkAllRead);
			global::Gtk.Box.BoxChild w10 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.lblMarkAllRead]));
			w10.Position = 1;
			w10.Expand = false;
			w10.Fill = false;
			this.vbox13.Add (this.hbox1);
			global::Gtk.Box.BoxChild w11 = ((global::Gtk.Box.BoxChild)(this.vbox13 [this.hbox1]));
			w11.PackType = ((global::Gtk.PackType)(1));
			w11.Position = 2;
			w11.Expand = false;
			w11.Fill = false;
			this.hpaned1.Add (this.vbox13);
			this.Add (this.hpaned1);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.Hide ();
			this.ctlGroupGrid.FocusedItemChanged += new global::System.EventHandler (this.ctlGroupGrid_FocusedItemChanged);
			this.chkNonSyncedGroups.Toggled += new global::System.EventHandler (this.chkNonSyncedGroups_Toggled);
			this.ctlThreadGrid.FocusedItemChanged += new global::System.EventHandler (this.ctlThreadGrid_FocusedItemChanged);
			this.chkShowReadThreads.Toggled += new global::System.EventHandler (this.chkShowReadThreads_Toggled);
			this.lblMarkAllRead.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.lblMarkAllRead_ButtonPress);
		}
	}
}
