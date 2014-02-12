namespace Wireclub.iOS

open System
open System.Threading
open System.Collections.Generic
open System.Collections.Concurrent
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Routes
        
[<Register ("UserFeedViewController")>]
type UserFeedViewController (handle:nativeint) =
    inherit UIViewController (handle)

(*[<Register ("UserFeedViewController")>]
type UserFeedViewController (handle:nativeint) =
    inherit UIViewController (handle)*)


[<Register ("UserViewController")>]
type UserViewController (handle:nativeint) =
    inherit UIViewController (handle)

    member val User: Wireclub.Boundary.Chat.PrivateChatFriend option = None with get, set // TODO: Generic....

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
            this.ChatButton.TouchUpInside.Add(fun _ ->            
                Navigation.navigate ("/privateChat/session/" + user.Slug) (user :> obj)
            )
        | _ -> failwith "No user"