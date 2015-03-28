
// This file has been generated by the GUI designer. Do not modify.
namespace Yamster
{
	public partial class MessageComposer
	{
		private global::Gtk.EventBox ctlWidgetBox;
		
		private global::Gtk.HBox hbox2;
		
		private global::Gtk.Fixed fixed1;
		
		private global::Gtk.VBox vbox1;
		
		private global::Gtk.HBox ctlReplyToBox;
		
		private global::Gtk.Label lblReplyTo;
		
		private global::Gtk.EventBox ctlCancelBox;
		
		private global::Gtk.Image ctlCancelImage;
		
		private global::Gtk.ScrolledWindow GtkScrolledWindow;
		
		private global::Gtk.TextView txtBody;
		
		private global::Gtk.HBox hbox3;
		
		private global::Gtk.Label label1;
		
		private global::Yamster.UserEntryWidget ctlCCUserEntry;
		
		private global::Gtk.Button btnSend;
		
		private global::Gtk.Fixed fixed2;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget Yamster.MessageComposer
			global::Stetic.BinContainer.Attach (this);
			this.Name = "Yamster.MessageComposer";
			// Container child Yamster.MessageComposer.Gtk.Container+ContainerChild
			this.ctlWidgetBox = new global::Gtk.EventBox ();
			this.ctlWidgetBox.Name = "ctlWidgetBox";
			// Container child ctlWidgetBox.Gtk.Container+ContainerChild
			this.hbox2 = new global::Gtk.HBox ();
			this.hbox2.Name = "hbox2";
			this.hbox2.Spacing = 6;
			this.hbox2.BorderWidth = ((uint)(3));
			// Container child hbox2.Gtk.Box+BoxChild
			this.fixed1 = new global::Gtk.Fixed ();
			this.fixed1.WidthRequest = 33;
			this.fixed1.Name = "fixed1";
			this.fixed1.HasWindow = false;
			this.hbox2.Add (this.fixed1);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.fixed1]));
			w1.Position = 0;
			w1.Expand = false;
			// Container child hbox2.Gtk.Box+BoxChild
			this.vbox1 = new global::Gtk.VBox ();
			this.vbox1.Name = "vbox1";
			this.vbox1.Spacing = 4;
			// Container child vbox1.Gtk.Box+BoxChild
			this.ctlReplyToBox = new global::Gtk.HBox ();
			this.ctlReplyToBox.Name = "ctlReplyToBox";
			this.ctlReplyToBox.Spacing = 6;
			// Container child ctlReplyToBox.Gtk.Box+BoxChild
			this.lblReplyTo = new global::Gtk.Label ();
			this.lblReplyTo.Name = "lblReplyTo";
			this.lblReplyTo.Xalign = 0F;
			this.lblReplyTo.LabelProp = global::Mono.Unix.Catalog.GetString ("<b>...replying to John Doe</b>");
			this.lblReplyTo.UseMarkup = true;
			this.ctlReplyToBox.Add (this.lblReplyTo);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.ctlReplyToBox [this.lblReplyTo]));
			w2.Position = 0;
			w2.Expand = false;
			w2.Fill = false;
			// Container child ctlReplyToBox.Gtk.Box+BoxChild
			this.ctlCancelBox = new global::Gtk.EventBox ();
			this.ctlCancelBox.Name = "ctlCancelBox";
			// Container child ctlCancelBox.Gtk.Container+ContainerChild
			this.ctlCancelImage = new global::Gtk.Image ();
			this.ctlCancelImage.Name = "ctlCancelImage";
			this.ctlCancelImage.Yalign = 0.8F;
			this.ctlCancelImage.Pixbuf = global::Gdk.Pixbuf.LoadFromResource ("Yamster.Resources.CancelX.png");
			this.ctlCancelBox.Add (this.ctlCancelImage);
			this.ctlReplyToBox.Add (this.ctlCancelBox);
			global::Gtk.Box.BoxChild w4 = ((global::Gtk.Box.BoxChild)(this.ctlReplyToBox [this.ctlCancelBox]));
			w4.Position = 1;
			w4.Expand = false;
			w4.Fill = false;
			this.vbox1.Add (this.ctlReplyToBox);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.ctlReplyToBox]));
			w5.Position = 0;
			w5.Expand = false;
			w5.Fill = false;
			// Container child vbox1.Gtk.Box+BoxChild
			this.GtkScrolledWindow = new global::Gtk.ScrolledWindow ();
			this.GtkScrolledWindow.Name = "GtkScrolledWindow";
			this.GtkScrolledWindow.ShadowType = ((global::Gtk.ShadowType)(1));
			// Container child GtkScrolledWindow.Gtk.Container+ContainerChild
			this.txtBody = new global::Gtk.TextView ();
			this.txtBody.CanFocus = true;
			this.txtBody.Name = "txtBody";
			this.txtBody.WrapMode = ((global::Gtk.WrapMode)(3));
			this.GtkScrolledWindow.Add (this.txtBody);
			this.vbox1.Add (this.GtkScrolledWindow);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.GtkScrolledWindow]));
			w7.Position = 1;
			// Container child vbox1.Gtk.Box+BoxChild
			this.hbox3 = new global::Gtk.HBox ();
			this.hbox3.Name = "hbox3";
			this.hbox3.Spacing = 6;
			// Container child hbox3.Gtk.Box+BoxChild
			this.label1 = new global::Gtk.Label ();
			this.label1.Name = "label1";
			this.label1.LabelProp = global::Mono.Unix.Catalog.GetString ("CC:");
			this.hbox3.Add (this.label1);
			global::Gtk.Box.BoxChild w8 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.label1]));
			w8.Position = 0;
			w8.Expand = false;
			w8.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.ctlCCUserEntry = new global::Yamster.UserEntryWidget ();
			this.ctlCCUserEntry.Events = ((global::Gdk.EventMask)(256));
			this.ctlCCUserEntry.Name = "ctlCCUserEntry";
			this.hbox3.Add (this.ctlCCUserEntry);
			global::Gtk.Box.BoxChild w9 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.ctlCCUserEntry]));
			w9.Position = 1;
			// Container child hbox3.Gtk.Box+BoxChild
			this.btnSend = new global::Gtk.Button ();
			this.btnSend.CanFocus = true;
			this.btnSend.Name = "btnSend";
			this.btnSend.UseUnderline = true;
			this.btnSend.Label = global::Mono.Unix.Catalog.GetString ("_Send");
			this.hbox3.Add (this.btnSend);
			global::Gtk.Box.BoxChild w10 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.btnSend]));
			w10.Position = 2;
			w10.Expand = false;
			w10.Fill = false;
			this.vbox1.Add (this.hbox3);
			global::Gtk.Box.BoxChild w11 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.hbox3]));
			w11.Position = 2;
			w11.Expand = false;
			w11.Fill = false;
			this.hbox2.Add (this.vbox1);
			global::Gtk.Box.BoxChild w12 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.vbox1]));
			w12.Position = 1;
			// Container child hbox2.Gtk.Box+BoxChild
			this.fixed2 = new global::Gtk.Fixed ();
			this.fixed2.WidthRequest = 2;
			this.fixed2.Name = "fixed2";
			this.fixed2.HasWindow = false;
			this.hbox2.Add (this.fixed2);
			global::Gtk.Box.BoxChild w13 = ((global::Gtk.Box.BoxChild)(this.hbox2 [this.fixed2]));
			w13.Position = 2;
			w13.Expand = false;
			this.ctlWidgetBox.Add (this.hbox2);
			this.Add (this.ctlWidgetBox);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.lblReplyTo.Hide ();
			this.ctlCancelImage.Hide ();
			this.Hide ();
			this.ctlCancelBox.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.ctlCancelBox_ButtonPress);
			this.txtBody.WidgetEvent += new global::Gtk.WidgetEventHandler (this.txtBody_WidgetEvent);
			this.btnSend.Clicked += new global::System.EventHandler (this.btnSend_Clicked);
		}
	}
}
