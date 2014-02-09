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
	[Register ("SignupViewController")]
	partial class SignupViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextField Email { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Password { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton SignupButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Email != null) {
				Email.Dispose ();
				Email = null;
			}

			if (Password != null) {
				Password.Dispose ();
				Password = null;
			}

			if (SignupButton != null) {
				SignupButton.Dispose ();
				SignupButton = null;
			}
		}
	}
}
