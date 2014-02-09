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
	[Register ("LoginViewController")]
	partial class LoginViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton CreateAccountButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Email { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton ForgotPasswordButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton LoginButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Password { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (CreateAccountButton != null) {
				CreateAccountButton.Dispose ();
				CreateAccountButton = null;
			}

			if (Email != null) {
				Email.Dispose ();
				Email = null;
			}

			if (ForgotPasswordButton != null) {
				ForgotPasswordButton.Dispose ();
				ForgotPasswordButton = null;
			}

			if (LoginButton != null) {
				LoginButton.Dispose ();
				LoginButton = null;
			}

			if (Password != null) {
				Password.Dispose ();
				Password = null;
			}
		}
	}
}
