namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat

[<Register ("ChatRoomViewController")>]
type ChatRoomViewController (room:ChatRoomDataViewModel) as this =
    inherit UIViewController ("PrivateChatSessionViewController", null)

    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        this.ResizeViewToKeyboard args
        //scrollToBottom()
        
    let showObserver = UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
    let hideObserver = UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))


    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    override this.ViewDidLoad () =
        let context = System.Threading.SynchronizationContext.Current
        Async.StartImmediate <| async {
            let! result = Chat.join room.Slug
            match result with
            | Api.ApiOk result ->
                let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
                    let rec loop () = async {
                        let! event = inbox.Receive()
                        do! Async.SwitchToContext context
                        match event with
                        | _ -> ()

                        return! loop ()
                    }

                    loop ()
                )

                processor.Start()
                ChannelClient.handlers.TryAdd(Api.userId, processor) |> ignore
            | Api.BadRequest errors -> ()
            | error -> this.HandleApiFailure error
        }
        ()

    override this.ViewDidDisappear (animated) =
        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()

                  