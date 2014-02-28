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
	[Register ("AsyncPickerViewController")]
	partial class AsyncPickerViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIPickerView Picker { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIActivityIndicatorView Progress { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Progress != null) {
				Progress.Dispose ();
				Progress = null;
			}

			if (Picker != null) {
				Picker.Dispose ();
				Picker = null;
			}
		}
	}
}
