// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent

open CoreGraphics
open Foundation
open UIKit

open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models
open Routes

module WebView =
    let navigateDelegate = {
        new UIWebViewDelegate() with
        override this.ShouldStartLoad (view, request, navigationType) =
            if navigationType = UIWebViewNavigationType.LinkClicked then
                let uri = new Uri(request.Url.AbsoluteString)
                Navigation.navigate (uri.ToString()) None
                false
            else
                true
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
    inherit UITableViewController (handle)

    let mutable isBlocked = false
    let mutable isFriend = false
    let mutable image = Image.placeholderMale 
    let mutable isSelf = false

    member val Entity: Entity option = None with get, set

    [<Outlet>]
    member val ImageView: UIView = null with get, set

    [<Outlet>]
    member val ChatButton: UIButton = null with get, set

    [<Outlet>]
    member val BlockButton: UIButton = null with get, set

    [<Outlet>]
    member val FriendButton: UIButton = null with get, set

    [<Outlet>]
    member val UnblockButton: UIButton = null with get, set

    [<Outlet>]
    member val UnfriendButton: UIButton = null with get, set

    [<Outlet>]
    member val ProfileLabel: UILabel = null with get, set

    [<Outlet>]
    member val LocationLabel: UILabel = null with get, set

    static member val Placeholder = lazy Image.resize (new CGSize (nfloat 320.0f, nfloat 320.0f)) Image.placeholder

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
   
        this.AutomaticallyAdjustsScrollViewInsets <- false

        match this.Entity with
        | Some user -> 
            this.NavigationItem.Title <- user.Label
            Image.loadImageWithContinuation (App.imageUrl user.Image 320)  UserViewController.Placeholder.Value (fun i _ ->
                image <- i
                let imageView = new UIImageView(image)
                for view in this.ImageView.Subviews do view.RemoveFromSuperview ()
                this.ImageView.Add imageView
                this.TableView.ReloadRows([| NSIndexPath.FromRowSection(nint 0, nint 0) |], UITableViewRowAnimation.None)
            )

            this.TableView.ReloadData()

            this.ChatButton.TouchUpInside.Add(fun _ ->            
                Navigation.navigate ("/privateChat/session/" + user.Slug) this.Entity
            )

            let setupButton (button:UIButton) verb description computation continuation =
                button.TouchUpInside.Add(fun _ ->
                    let alert = new UIAlertView (Title = description, Message="")
                    alert.AddButton "Cancel" |> ignore
                    alert.AddButton verb |> ignore
                    alert.Show ()
                    alert.Dismissed.Add(fun args ->
                        match int args.ButtonIndex with
                        | 1 -> 
                            Async.startNetworkWithContinuation
                                computation
                                continuation
                        | _ -> ()
                    )
                )

            setupButton
                this.FriendButton
                "Send"
                (String.Format( "Send {0} a friend request?", user.Label))
                (User.addFriend user.Slug)
                (function 
                    | Api.ApiOk profile ->
                        this.FriendButton.Hidden <- true
                        this.UnfriendButton.Hidden <- false
                    | error -> this.HandleApiFailure error
                )

            setupButton
                this.UnfriendButton
                "Unfriend"
                (String.Format( "Remove {0} as a friend?", user.Label))
                (User.removeFriend user.Slug)
                (function 
                    | Api.ApiOk profile ->
                        this.FriendButton.Hidden <- false
                        this.UnfriendButton.Hidden <- true
                    | error -> this.HandleApiFailure error
                )

            setupButton
                this.BlockButton
                "Block"
                (String.Format( "Block {0}?", user.Label))
                (User.block user.Slug)
                (function 
                    | Api.ApiOk profile ->
                        this.BlockButton.Hidden <- true
                        this.UnblockButton.Hidden <- false
                    | error -> this.HandleApiFailure error
                )

            setupButton
                this.UnblockButton
                "Unblock"
                (String.Format( "Unblock {0}?", user.Label))
                (User.unblock user.Slug)
                (function 
                    | Api.ApiOk profile ->
                        this.BlockButton.Hidden <- false
                        this.UnblockButton.Hidden <- true
                    | error -> this.HandleApiFailure error
                )


            Async.startWithContinuation
                (User.fetch user.Slug)
                (function 
                    | Api.ApiOk profile ->
                        this.ProfileLabel.Text <- (String.Format("{0}, {1}", profile.Age, profile.Gender))
                        this.LocationLabel.Text <- profile.Location
                        isSelf <- profile.Id = Api.userId
                        this.TableView.ReloadData()

                        this.BlockButton.Hidden <- profile.Blocked
                        this.UnblockButton.Hidden <- not profile.Blocked
                        this.FriendButton.Hidden <- profile.Friend
                        this.UnfriendButton.Hidden <- not profile.Friend

                        if isSelf then
                            this.ChatButton.Hidden <- true
                            this.BlockButton.Hidden <- true
                            this.UnblockButton.Hidden <- true
                            this.FriendButton.Hidden <- true
                            this.UnfriendButton.Hidden <- true

                    | error -> this.HandleApiFailure error
                )
        | _ -> failwith "No user"

    override this.GetHeightForHeader (tableView, index) =
        match int index with
        | 0 -> nfloat 64.0f
        | _-> tableView.SectionHeaderHeight

    override this.GetHeightForRow(tableView, indexPath) =
        match indexPath.Section, indexPath.Row with
        | 0, 0 -> match image with | null -> nfloat 320.f | image -> image.Size.Height
        | 1, _ when isSelf -> nfloat 0.0f //HACK does not look good with the single bar but simpler than any other solution I found to hiding these rows
        | _ -> tableView.RowHeight
