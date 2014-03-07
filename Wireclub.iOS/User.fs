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
            Image.loadImageForView (App.imageUrl user.Image 200) Image.placeholder this.Avatar

            this.ChatButton.TouchUpInside.Add(fun _ ->            
                Navigation.navigate ("/privateChat/session/" + user.Slug) this.User
            )

            this.FriendButton.TouchUpInside.Add(fun _ ->
                let alert = new UIAlertView (Title="Send Friend Request?", Message="")
                alert.AddButton "Cancel" |> ignore
                alert.AddButton "Send" |> ignore
                alert.Show ()
                alert.Dismissed.Add(fun args ->
                    match args.ButtonIndex with
                    | 0 -> ()
                    | _ -> 
                        Async.startWithContinuation
                            (User.addFriend user.Slug)
                            (this.HandleApiResult >> ignore)
                )
            )

        | _ ->
            //TODO make this async load a user
            failwith "No user"