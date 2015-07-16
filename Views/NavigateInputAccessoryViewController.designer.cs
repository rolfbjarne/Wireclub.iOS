// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace Views
{
	[Register ("NavigateInputAccessoryViewController")]
	partial class NavigateInputAccessoryViewController
	{
		[Outlet]
		UIKit.UIButton DoneButton { get; set; }

		[Outlet]
		UIKit.UIButton NextButton { get; set; }

		[Outlet]
		UIKit.UIButton PrevButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (DoneButton != null) {
				DoneButton.Dispose ();
				DoneButton = null;
			}

			if (NextButton != null) {
				NextButton.Dispose ();
				NextButton = null;
			}

			if (PrevButton != null) {
				PrevButton.Dispose ();
				PrevButton = null;
			}
		}
	}
}
