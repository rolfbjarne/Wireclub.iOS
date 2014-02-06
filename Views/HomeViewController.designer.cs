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
	[Register ("HomeViewController")]
	partial class HomeViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIView ContentView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISegmentedControl Filter { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Filter != null) {
				Filter.Dispose ();
				Filter = null;
			}

			if (ContentView != null) {
				ContentView.Dispose ();
				ContentView = null;
			}
		}
	}
}
