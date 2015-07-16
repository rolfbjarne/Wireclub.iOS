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
	[Register ("ChatDirectoryViewController")]
	partial class ChatDirectoryViewController
	{
		[Outlet]
		UIKit.UIView ContentView { get; set; }

		[Outlet]
		UIKit.UISegmentedControl RoomFilter { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (RoomFilter != null) {
				RoomFilter.Dispose ();
				RoomFilter = null;
			}

			if (ContentView != null) {
				ContentView.Dispose ();
				ContentView = null;
			}
		}
	}
}
