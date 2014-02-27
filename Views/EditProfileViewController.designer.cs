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
	[Register ("EditProfileViewController")]
	partial class EditProfileViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextView About { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Birthday { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField City { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Country { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISegmentedControl GenderSelect { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIImageView ProfileImage { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Region { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton SaveButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Username { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Birthday != null) {
				Birthday.Dispose ();
				Birthday = null;
			}

			if (City != null) {
				City.Dispose ();
				City = null;
			}

			if (Country != null) {
				Country.Dispose ();
				Country = null;
			}

			if (GenderSelect != null) {
				GenderSelect.Dispose ();
				GenderSelect = null;
			}

			if (ProfileImage != null) {
				ProfileImage.Dispose ();
				ProfileImage = null;
			}

			if (Region != null) {
				Region.Dispose ();
				Region = null;
			}

			if (SaveButton != null) {
				SaveButton.Dispose ();
				SaveButton = null;
			}

			if (Username != null) {
				Username.Dispose ();
				Username = null;
			}

			if (About != null) {
				About.Dispose ();
				About = null;
			}
		}
	}
}
