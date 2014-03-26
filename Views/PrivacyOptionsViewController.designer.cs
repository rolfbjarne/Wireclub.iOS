// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoTouch.Foundation;
using System.CodeDom.Compiler;

namespace Views
{
	[Register ("PrivacyOptionsViewController")]
	partial class PrivacyOptionsViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextField Contact { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField InviteGames { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISwitch PictureRankings { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField PrivateChat { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField ViewBlog { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField ViewPictures { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField ViewProfile { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Contact != null) {
				Contact.Dispose ();
				Contact = null;
			}

			if (PrivateChat != null) {
				PrivateChat.Dispose ();
				PrivateChat = null;
			}

			if (ViewProfile != null) {
				ViewProfile.Dispose ();
				ViewProfile = null;
			}

			if (ViewBlog != null) {
				ViewBlog.Dispose ();
				ViewBlog = null;
			}

			if (ViewPictures != null) {
				ViewPictures.Dispose ();
				ViewPictures = null;
			}

			if (InviteGames != null) {
				InviteGames.Dispose ();
				InviteGames = null;
			}

			if (PictureRankings != null) {
				PictureRankings.Dispose ();
				PictureRankings = null;
			}
		}
	}
}
