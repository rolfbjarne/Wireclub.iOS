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
	[Register ("FriendsViewController")]
	partial class FriendsViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIView ContentView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UISegmentedControl OnlineState { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (OnlineState != null) {
				OnlineState.Dispose ();
				OnlineState = null;
			}

			if (ContentView != null) {
				ContentView.Dispose ();
				ContentView = null;
			}
		}
	}
}
