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
	[Register ("PrivateChatSessionViewController")]
	partial class PrivateChatSessionViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIActivityIndicatorView Progress { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton SendButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField Text { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIWebView WebView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (SendButton != null) {
				SendButton.Dispose ();
				SendButton = null;
			}

			if (Text != null) {
				Text.Dispose ();
				Text = null;
			}

			if (WebView != null) {
				WebView.Dispose ();
				WebView = null;
			}

			if (Progress != null) {
				Progress.Dispose ();
				Progress = null;
			}
		}
	}
}
