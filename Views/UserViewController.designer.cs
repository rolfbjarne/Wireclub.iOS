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
	[Register ("UserViewController")]
	partial class UserViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIImageView Avatar { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton BlockButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton ChatButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton FriendButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UILabel ProfileLabel { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton UnblockButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton UnfriendButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Avatar != null) {
				Avatar.Dispose ();
				Avatar = null;
			}

			if (BlockButton != null) {
				BlockButton.Dispose ();
				BlockButton = null;
			}

			if (ChatButton != null) {
				ChatButton.Dispose ();
				ChatButton = null;
			}

			if (FriendButton != null) {
				FriendButton.Dispose ();
				FriendButton = null;
			}

			if (ProfileLabel != null) {
				ProfileLabel.Dispose ();
				ProfileLabel = null;
			}

			if (UnfriendButton != null) {
				UnfriendButton.Dispose ();
				UnfriendButton = null;
			}

			if (UnblockButton != null) {
				UnblockButton.Dispose ();
				UnblockButton = null;
			}
		}
	}
}
