namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit

[<Register ("DialogViewController")>]
type DialogViewController (url:string) =
    inherit UIViewController ()

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        //this.NavigationItem.Title <- user.Name
        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(url)))
        this.WebView.LoadFinished.Add(fun _ ->
            ()
        )
