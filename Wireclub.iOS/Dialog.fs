// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit

[<Register ("DialogViewController")>]
type DialogViewController (url:string) =
    inherit UIViewController ()

    let url = Api.fullUrl url

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =

        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(url)))
        this.AutomaticallyAdjustsScrollViewInsets <- false
        
        this.WebView.ShouldStartLoad <- UIWebLoaderControl(fun webView request navigationType ->
            if 
                request.Url.ToString() = url && 
                request.Headers.Keys |> Array.exists ((=) (NSObject.FromObject "x-auth-token")) = false then

                printfn "[Dialog] loading %s" url

                let headers = new NSMutableDictionary (request.Headers)
                headers.SetValueForKey(NSObject.FromObject (Api.userToken), new NSString("x-auth-token"))
                let request = request.MutableCopy () :?> NSMutableUrlRequest

                request.Headers <- headers
                webView.LoadRequest request
                false
            else
                true
        )
        this.WebView.LoadFinished.Add(fun _ ->
            ()
        )
        this.WebView.LoadError.Add(fun args ->
            printfn "[Dialog] Error: %s" (args.Error.ToString ())
            ()
        )
