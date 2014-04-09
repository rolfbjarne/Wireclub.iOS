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
	[Register ("NotificationsViewController")]
	partial class NotificationsViewController
	{
		[Outlet]
		MonoTouch.UIKit.UISwitch ClubActivity { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISwitch Invitations { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISwitch NewMessages { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton Save { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (ClubActivity != null) {
				ClubActivity.Dispose ();
				ClubActivity = null;
			}

			if (Invitations != null) {
				Invitations.Dispose ();
				Invitations = null;
			}

			if (NewMessages != null) {
				NewMessages.Dispose ();
				NewMessages = null;
			}

			if (Save != null) {
				Save.Dispose ();
				Save = null;
			}
		}
	}
}
