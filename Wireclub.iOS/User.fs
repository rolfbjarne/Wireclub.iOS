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
        
[<Register ("UserFeedViewController")>]
type UserFeedViewController (handle:nativeint) =
    inherit UIViewController (handle)

[<Register ("UserViewController")>]
type UserViewController (handle:nativeint) =
    inherit UIViewController (handle)

    member val User: Entity option = None with get, set

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