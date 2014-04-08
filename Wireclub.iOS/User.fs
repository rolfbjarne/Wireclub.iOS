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
        
type UserBaseViewController (handle:nativeint) =
    inherit UIViewController (handle)

    member val User: Entity option = None with get, set


type UserWebViewBaseViewController (handle:nativeint) =
    inherit UserBaseViewController (handle)

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    member val Url: string = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        match this.User with
        | Some user ->
            this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(this.Url)))
            this.WebView.LoadFinished.Add(fun _ ->
                this.WebView.Delegate <- WebView.navigateDelegate
                this.WebView.SetBodyBackgroundColor (colorToCss Utility.grayLightAccent)
            )
        | _ -> ()

[<Register ("UserFeedViewController")>]
type UserFeedViewController (handle:nativeint) as controller =
    inherit UserWebViewBaseViewController (handle)

    do controller.Url <- Api.baseUrl + "/users/" + controller.User.Value.Slug

    override this.ViewDidLoad () =
        base.ViewDidLoad ()


[<Register ("UserGalleryViewController")>]
type UserGalleryViewController (handle:nativeint) as controller =
    inherit UserWebViewBaseViewController (handle)

    do controller.Url <- Api.baseUrl + "/users/" + controller.User.Value.Slug + "/pictures"

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

[<Register ("UserBlogViewController")>]
type UserBlogViewController (handle:nativeint) as controller =
    inherit UserWebViewBaseViewController (handle)

    do controller.Url <- Api.baseUrl + "/users/" + controller.User.Value.Slug + "/blog"

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()


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