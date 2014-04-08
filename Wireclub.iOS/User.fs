namespace Wireclub.iOS

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models
open Routes

module WebView =
    let navigateDelegate = {
        new UIWebViewDelegate() with
        override this.ShouldStartLoad (view, request, navigationType) =
            let uri = new Uri(request.Url.AbsoluteString)
            Navigation.navigate (uri.ToString()) None
            false
    }

    let setupWebView (webView:UIWebView, url:string) =
            webView.LoadRequest(new NSUrlRequest(new NSUrl(url)))

            webView.ShouldStartLoad <- UIWebLoaderControl(fun webView request navigationType ->
                if request.Url.ToString() = url && request.Headers.Keys |> Array.exists ((=) (NSObject.FromObject "x-auth-token")) = false then

                    let headers = new NSMutableDictionary (request.Headers)
                    headers.SetValueForKey(NSObject.FromObject (Api.userToken), new NSString("x-auth-token"))
                    let request = request.MutableCopy () :?> NSMutableUrlRequest

                    request.Headers <- headers
                    webView.LoadRequest request
                    false
                else
                    true
            )

            webView.LoadFinished.Add(fun _ ->
                webView.Delegate <- navigateDelegate
                webView.SetBodyBackgroundColor (colorToCss Utility.grayLightAccent)
            )

        
type UserBaseViewController (handle:nativeint) =
    inherit UIViewController (handle)

    member val User: Entity option = None with get, set


[<Register ("UserFeedViewController")>]
type UserFeedViewController (handle:nativeint) as controller =
    inherit UserBaseViewController (handle)

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        match this.User with
        | Some user -> WebView.setupWebView(this.WebView, Api.baseUrl + "/users/" + controller.User.Value.Slug)
        | _ -> ()

[<Register ("UserGalleryViewController")>]
type UserGalleryViewController (handle:nativeint) as controller =
    inherit UserBaseViewController (handle)

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        match this.User with
        | Some user -> WebView.setupWebView(this.WebView, Api.baseUrl + "/users/" + controller.User.Value.Slug + "/pictures")
        | _ -> ()

[<Register ("UserBlogViewController")>]
type UserBlogViewController (handle:nativeint) as controller =
    inherit UserBaseViewController (handle)

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        match this.User with
        | Some user -> WebView.setupWebView(this.WebView, Api.baseUrl + "/users/" + controller.User.Value.Slug + "/blog")
        | _ -> ()


[<Register ("UserViewController")>]
type UserViewController (handle:nativeint) =
    inherit UserBaseViewController (handle)

    [<Outlet>]
    member val Avatar: UIImageView = null with get, set

    [<Outlet>]
    member val BlockButton: UIButton = null with get, set

    [<Outlet>]
    member val ChatButton: UIButton = null with get, set

    [<Outlet>]
    member val FriendButton: UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
    
        match this.User with
        | Some user -> 
            let url = (App.imageUrl user.Image 320) 
            Image.loadImageForView url Image.placeholder this.Avatar

            this.ChatButton.TouchUpInside.Add(fun _ ->            
                Navigation.navigate ("/privateChat/session/" + user.Slug) this.User
            )

            this.NavigationItem.Title <- user.Label
            this.NavigationItem.RightBarButtonItem <- null

            this.FriendButton.TouchUpInside.Add(fun _ ->
                let alert = new UIAlertView (Title="Send Friend Request?", Message="")
                alert.AddButton "Cancel" |> ignore
                alert.AddButton "Send" |> ignore
                alert.Show ()
                alert.Dismissed.Add(fun args ->
                    match args.ButtonIndex with
                    | 1 -> 
                        Async.startNetworkWithContinuation
                            (User.addFriend user.Slug)
                            (this.HandleApiResult >> ignore)
                    | _ -> ()
                )
            )

        | _ ->
            //TODO make this async load a user
            failwith "No user"