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
	[Register ("MessagingOptionsViewController")]
	partial class MessagingOptionsViewController
	{
		[Outlet]
		MonoTouch.UIKit.UISwitch Picture { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton Save { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISwitch Verified { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Picture != null) {
				Picture.Dispose ();
				Picture = null;
			}

			if (Save != null) {
				Save.Dispose ();
				Save = null;
			}

			if (Verified != null) {
				Verified.Dispose ();
				Verified = null;
			}
		}
	}
}
