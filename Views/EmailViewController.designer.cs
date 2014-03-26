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
	[Register ("EmailViewController")]
	partial class EmailViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextField Confirm { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField New { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Old { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Password { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton Save { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Old != null) {
				Old.Dispose ();
				Old = null;
			}

			if (New != null) {
				New.Dispose ();
				New = null;
			}

			if (Confirm != null) {
				Confirm.Dispose ();
				Confirm = null;
			}

			if (Password != null) {
				Password.Dispose ();
				Password = null;
			}

			if (Save != null) {
				Save.Dispose ();
				Save = null;
			}
		}
	}
}
