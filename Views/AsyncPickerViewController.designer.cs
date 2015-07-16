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
	[Register ("AsyncPickerViewController")]
	partial class AsyncPickerViewController
	{
		[Outlet]
		UIKit.UIPickerView Picker { get; set; }

		[Outlet]
		UIKit.UIActivityIndicatorView Progress { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Picker != null) {
				Picker.Dispose ();
				Picker = null;
			}

			if (Progress != null) {
				Progress.Dispose ();
				Progress = null;
			}
		}
	}
}
