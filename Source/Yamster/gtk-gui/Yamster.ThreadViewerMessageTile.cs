
// This file has been generated by the GUI designer. Do not modify.
namespace Yamster
{
	public partial class ThreadViewerMessageTile
	{
		private global::Gtk.EventBox ctlWidgetBox;
		
		private global::Gtk.HBox hbox1;
		
		private global::Gtk.Image ctlUserImage;
		
		private global::Gtk.VBox vbox2;
		
		private global::Gtk.EventBox ctlHighlightBox;
		
		private global::Gtk.VBox vbox3;
		
		private global::Gtk.Label lblSender;
		
		private global::Gtk.Label lblBody;
		
		private global::Gtk.HBox ctlLikesHbox;
		
		private global::Gtk.Alignment ctlImageAlign;
		
		private global::Gtk.Image ctlThumbImage;
		
		private global::Gtk.Label lblLikes;
		
		private global::Gtk.HBox hbox3;
		
		private global::Gtk.Label lblLike;
		
		private global::Gtk.Label lblUnlike;
		
		private global::Gtk.Label lblReply;
		
		private global::Gtk.Label lblDelete;
		
		private global::Gtk.Label lblTimestamp;
		
		private global::Gtk.EventBox ctlStarEventBox;
		
		private global::Gtk.HBox ctlStarHBox;
		
		private global::Gtk.Image ctlImageStarOn;
		
		private global::Gtk.Image ctlImageStarOff;
		
		private global::Gtk.EventBox ctlAttachmentBox;
		
		private global::Gtk.Image ctlAttachmentImage;
		
		private global::Gtk.HSeparator ctlSeparator;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget Yamster.ThreadViewerMessageTile
			global::Stetic.BinContainer.Attach (this);
			this.Name = "Yamster.ThreadViewerMessageTile";
			// Container child Yamster.ThreadViewerMessageTile.Gtk.Container+ContainerChild
			this.ctlWidgetBox = new global::Gtk.EventBox ();
			this.ctlWidgetBox.Name = "ctlWidgetBox";
			// Container child ctlWidgetBox.Gtk.Container+ContainerChild
			this.hbox1 = new global::Gtk.HBox ();
			this.hbox1.Name = "hbox1";
			this.hbox1.Spacing = 6;
			this.hbox1.BorderWidth = ((uint)(3));
			// Container child hbox1.Gtk.Box+BoxChild
			this.ctlUserImage = new global::Gtk.Image ();
			this.ctlUserImage.WidthRequest = 33;
			this.ctlUserImage.HeightRequest = 33;
			this.ctlUserImage.Name = "ctlUserImage";
			this.ctlUserImage.Yalign = 0F;
			this.ctlUserImage.Pixbuf = global::Gdk.Pixbuf.LoadFromResource ("Yamster.Resources.AnonymousPhoto-33x33.png");
			this.hbox1.Add (this.ctlUserImage);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.ctlUserImage]));
			w1.Position = 0;
			w1.Expand = false;
			w1.Fill = false;
			// Container child hbox1.Gtk.Box+BoxChild
			this.vbox2 = new global::Gtk.VBox ();
			this.vbox2.Name = "vbox2";
			this.vbox2.Spacing = 6;
			// Container child vbox2.Gtk.Box+BoxChild
			this.ctlHighlightBox = new global::Gtk.EventBox ();
			this.ctlHighlightBox.Name = "ctlHighlightBox";
			// Container child ctlHighlightBox.Gtk.Container+ContainerChild
			this.vbox3 = new global::Gtk.VBox ();
			this.vbox3.Name = "vbox3";
			this.vbox3.Spacing = 6;
			// Container child vbox3.Gtk.Box+BoxChild
			this.lblSender = new global::Gtk.Label ();
			this.lblSender.WidthRequest = 300;
			this.lblSender.Name = "lblSender";
			this.lblSender.Xalign = 0F;
			this.lblSender.Yalign = 0F;
			this.lblSender.LabelProp = global::Mono.Unix.Catalog.GetString ("<b>Person With Long Name</b> in reply to <b>Person With Long Name</b>");
			this.lblSender.UseMarkup = true;
			this.lblSender.Wrap = true;
			this.lblSender.Selectable = true;
			this.vbox3.Add (this.lblSender);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.vbox3 [this.lblSender]));
			w2.Position = 0;
			w2.Expand = false;
			w2.Fill = false;
			// Container child vbox3.Gtk.Box+BoxChild
			this.lblBody = new global::Gtk.Label ();
			this.lblBody.WidthRequest = 300;
			this.lblBody.Name = "lblBody";
			this.lblBody.LabelProp = global::Mono.Unix.Catalog.GetString (@"FYI I asked the other people, and they said that at last week's book club meeting people were also generally uncomfortable with metaprogramming except in special situations where there was a clear need. The feeling was that if you're having to automatically generate a lot of definitions, maybe the design is wrong.

 As for your point [1], I've heard tales of these ""small"" code bases, but I don't believe they actually exist heheh.");
			this.lblBody.Wrap = true;
			this.lblBody.Selectable = true;
			this.vbox3.Add (this.lblBody);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.vbox3 [this.lblBody]));
			w3.Position = 1;
			w3.Expand = false;
			w3.Fill = false;
			this.ctlHighlightBox.Add (this.vbox3);
			this.vbox2.Add (this.ctlHighlightBox);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.ctlHighlightBox]));
			w5.Position = 0;
			w5.Expand = false;
			w5.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.ctlLikesHbox = new global::Gtk.HBox ();
			this.ctlLikesHbox.Name = "ctlLikesHbox";
			this.ctlLikesHbox.Spacing = 6;
			// Container child ctlLikesHbox.Gtk.Box+BoxChild
			this.ctlImageAlign = new global::Gtk.Alignment (0.5F, 0.5F, 1F, 1F);
			this.ctlImageAlign.Name = "ctlImageAlign";
			this.ctlImageAlign.TopPadding = ((uint)(2));
			// Container child ctlImageAlign.Gtk.Container+ContainerChild
			this.ctlThumbImage = new global::Gtk.Image ();
			this.ctlThumbImage.Name = "ctlThumbImage";
			this.ctlThumbImage.Yalign = 0F;
			this.ctlThumbImage.Pixbuf = global::Gdk.Pixbuf.LoadFromResource ("Yamster.Resources.LikeThumb.png");
			this.ctlImageAlign.Add (this.ctlThumbImage);
			this.ctlLikesHbox.Add (this.ctlImageAlign);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.ctlLikesHbox [this.ctlImageAlign]));
			w7.Position = 0;
			w7.Expand = false;
			w7.Fill = false;
			// Container child ctlLikesHbox.Gtk.Box+BoxChild
			this.lblLikes = new global::Gtk.Label ();
			this.lblLikes.WidthRequest = 270;
			this.lblLikes.Name = "lblLikes";
			this.lblLikes.Xalign = 0F;
			this.lblLikes.Yalign = 0F;
			this.lblLikes.LabelProp = global::Mono.Unix.Catalog.GetString ("You, <b>Person #1</b>, <b>Person #2</b>, <b>Person #3</b> and 4 others like this." +
			"");
			this.lblLikes.UseMarkup = true;
			this.lblLikes.Wrap = true;
			this.lblLikes.Selectable = true;
			this.ctlLikesHbox.Add (this.lblLikes);
			global::Gtk.Box.BoxChild w8 = ((global::Gtk.Box.BoxChild)(this.ctlLikesHbox [this.lblLikes]));
			w8.Position = 1;
			w8.Expand = false;
			w8.Fill = false;
			this.vbox2.Add (this.ctlLikesHbox);
			global::Gtk.Box.BoxChild w9 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.ctlLikesHbox]));
			w9.Position = 1;
			w9.Expand = false;
			w9.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.hbox3 = new global::Gtk.HBox ();
			this.hbox3.Name = "hbox3";
			this.hbox3.Spacing = 6;
			// Container child hbox3.Gtk.Box+BoxChild
			this.lblLike = new global::Gtk.Label ();
			this.lblLike.Name = "lblLike";
			this.lblLike.LabelProp = global::Mono.Unix.Catalog.GetString ("<a href=\"\">Like</a>");
			this.lblLike.UseMarkup = true;
			this.hbox3.Add (this.lblLike);
			global::Gtk.Box.BoxChild w10 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.lblLike]));
			w10.Position = 0;
			w10.Expand = false;
			w10.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.lblUnlike = new global::Gtk.Label ();
			this.lblUnlike.Name = "lblUnlike";
			this.lblUnlike.LabelProp = global::Mono.Unix.Catalog.GetString ("<a href=\"\">Unlike</a>");
			this.lblUnlike.UseMarkup = true;
			this.hbox3.Add (this.lblUnlike);
			global::Gtk.Box.BoxChild w11 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.lblUnlike]));
			w11.Position = 1;
			w11.Expand = false;
			w11.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.lblReply = new global::Gtk.Label ();
			this.lblReply.Name = "lblReply";
			this.lblReply.LabelProp = global::Mono.Unix.Catalog.GetString ("<a href=\"\">Reply</a>");
			this.lblReply.UseMarkup = true;
			this.hbox3.Add (this.lblReply);
			global::Gtk.Box.BoxChild w12 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.lblReply]));
			w12.Position = 2;
			w12.Expand = false;
			w12.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.lblDelete = new global::Gtk.Label ();
			this.lblDelete.Name = "lblDelete";
			this.lblDelete.LabelProp = global::Mono.Unix.Catalog.GetString ("<a href=\"\">Delete</a>");
			this.lblDelete.UseMarkup = true;
			this.hbox3.Add (this.lblDelete);
			global::Gtk.Box.BoxChild w13 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.lblDelete]));
			w13.Position = 3;
			w13.Expand = false;
			w13.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.lblTimestamp = new global::Gtk.Label ();
			this.lblTimestamp.Name = "lblTimestamp";
			this.lblTimestamp.LabelProp = global::Mono.Unix.Catalog.GetString ("1:20pm 1/2/2013");
			this.lblTimestamp.Selectable = true;
			this.hbox3.Add (this.lblTimestamp);
			global::Gtk.Box.BoxChild w14 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.lblTimestamp]));
			w14.PackType = ((global::Gtk.PackType)(1));
			w14.Position = 5;
			w14.Expand = false;
			w14.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.ctlStarEventBox = new global::Gtk.EventBox ();
			this.ctlStarEventBox.Name = "ctlStarEventBox";
			// Container child ctlStarEventBox.Gtk.Container+ContainerChild
			this.ctlStarHBox = new global::Gtk.HBox ();
			this.ctlStarHBox.Name = "ctlStarHBox";
			this.ctlStarHBox.Spacing = 3;
			// Container child ctlStarHBox.Gtk.Box+BoxChild
			this.ctlImageStarOn = new global::Gtk.Image ();
			this.ctlImageStarOn.Name = "ctlImageStarOn";
			this.ctlImageStarOn.Pixbuf = global::Gdk.Pixbuf.LoadFromResource ("Yamster.Resources.Star-OnWhite.png");
			this.ctlStarHBox.Add (this.ctlImageStarOn);
			global::Gtk.Box.BoxChild w15 = ((global::Gtk.Box.BoxChild)(this.ctlStarHBox [this.ctlImageStarOn]));
			w15.Position = 0;
			w15.Expand = false;
			w15.Fill = false;
			// Container child ctlStarHBox.Gtk.Box+BoxChild
			this.ctlImageStarOff = new global::Gtk.Image ();
			this.ctlImageStarOff.Name = "ctlImageStarOff";
			this.ctlImageStarOff.Pixbuf = global::Gdk.Pixbuf.LoadFromResource ("Yamster.Resources.Star-OffWhite.png");
			this.ctlStarHBox.Add (this.ctlImageStarOff);
			global::Gtk.Box.BoxChild w16 = ((global::Gtk.Box.BoxChild)(this.ctlStarHBox [this.ctlImageStarOff]));
			w16.Position = 1;
			w16.Expand = false;
			w16.Fill = false;
			this.ctlStarEventBox.Add (this.ctlStarHBox);
			this.hbox3.Add (this.ctlStarEventBox);
			global::Gtk.Box.BoxChild w18 = ((global::Gtk.Box.BoxChild)(this.hbox3 [this.ctlStarEventBox]));
			w18.PackType = ((global::Gtk.PackType)(1));
			w18.Position = 6;
			w18.Expand = false;
			w18.Fill = false;
			this.vbox2.Add (this.hbox3);
			global::Gtk.Box.BoxChild w19 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.hbox3]));
			w19.Position = 2;
			w19.Expand = false;
			w19.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.ctlAttachmentBox = new global::Gtk.EventBox ();
			this.ctlAttachmentBox.Name = "ctlAttachmentBox";
			// Container child ctlAttachmentBox.Gtk.Container+ContainerChild
			this.ctlAttachmentImage = new global::Gtk.Image ();
			this.ctlAttachmentImage.Name = "ctlAttachmentImage";
			this.ctlAttachmentImage.Pixbuf = global::Gdk.Pixbuf.LoadFromResource ("Yamster.Resources.LoadingImage.png");
			this.ctlAttachmentBox.Add (this.ctlAttachmentImage);
			this.vbox2.Add (this.ctlAttachmentBox);
			global::Gtk.Box.BoxChild w21 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.ctlAttachmentBox]));
			w21.Position = 3;
			w21.Expand = false;
			w21.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.ctlSeparator = new global::Gtk.HSeparator ();
			this.ctlSeparator.Name = "ctlSeparator";
			this.vbox2.Add (this.ctlSeparator);
			global::Gtk.Box.BoxChild w22 = ((global::Gtk.Box.BoxChild)(this.vbox2 [this.ctlSeparator]));
			w22.PackType = ((global::Gtk.PackType)(1));
			w22.Position = 4;
			w22.Expand = false;
			w22.Fill = false;
			this.hbox1.Add (this.vbox2);
			global::Gtk.Box.BoxChild w23 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.vbox2]));
			w23.Position = 1;
			w23.Expand = false;
			w23.Fill = false;
			this.ctlWidgetBox.Add (this.hbox1);
			this.Add (this.ctlWidgetBox);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.ctlAttachmentBox.Hide ();
			this.Hide ();
			this.lblLike.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.lblLike_ButtonPress);
			this.lblUnlike.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.lblUnlike_ButtonPress);
			this.lblReply.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.lblReply_ButtonPress);
			this.lblDelete.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.lblDelete_ButtonPress);
			this.ctlStarEventBox.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.ctlStarEventBox_ButtonPress);
			this.ctlAttachmentBox.ButtonPressEvent += new global::Gtk.ButtonPressEventHandler (this.ctlAttachmentBox_ButtonPress);
		}
	}
}
