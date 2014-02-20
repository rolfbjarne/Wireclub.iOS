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
		MonoTouch.UIKit.UITextField Birthday { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextView Description { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField First { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISegmentedControl GenderSelect { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Last { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Location { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIImageView ProfileImage { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton SaveButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Birthday != null) {
				Birthday.Dispose ();
				Birthday = null;
			}

			if (Description != null) {
				Description.Dispose ();
				Description = null;
			}

			if (First != null) {
				First.Dispose ();
				First = null;
			}

			if (GenderSelect != null) {
				GenderSelect.Dispose ();
				GenderSelect = null;
			}

			if (Last != null) {
				Last.Dispose ();
				Last = null;
			}

			if (Location != null) {
				Location.Dispose ();
				Location = null;
			}

			if (ProfileImage != null) {
				ProfileImage.Dispose ();
				ProfileImage = null;
			}

			if (SaveButton != null) {
				SaveButton.Dispose ();
				SaveButton = null;
			}
		}
	}
}
