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
	[Register ("ChatOptionsViewController")]
	partial class ChatOptionsViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextField Color { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Font { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISwitch PlaySounds { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton Save { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField ShowJoinLeave { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Font != null) {
				Font.Dispose ();
				Font = null;
			}

			if (Color != null) {
				Color.Dispose ();
				Color = null;
			}

			if (ShowJoinLeave != null) {
				ShowJoinLeave.Dispose ();
				ShowJoinLeave = null;
			}

			if (PlaySounds != null) {
				PlaySounds.Dispose ();
				PlaySounds = null;
			}

			if (Save != null) {
				Save.Dispose ();
				Save = null;
			}
		}
	}
}
