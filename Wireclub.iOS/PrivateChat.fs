namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat


type ChatMessage = {
    Current:string
    Id:string
    Slug:string
    Avatar:string
    Message: string
    Sequence: int64
    Color: string
    Font: string

    NavigateUrl:string
    ContentUrl:string
}


[<Register("PrivateChatSessionViewController")>]
type PrivateChatSessionViewController (user:Wireclub.Boundary.Chat.PrivateChatFriend) as this =
    inherit UIViewController ()

    let events = System.Collections.Generic.HashSet<int64>()

    let scrollToBottom () =
        this.WebView.EvaluateJavascript 
            (sprintf "window.scrollBy(0, %i);" 
                (int (this.WebView.EvaluateJavascript "document.body.offsetHeight;"))) |> ignore

    let addMessage id slug avatar color font message sequence =
        if events.Add sequence then
            this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addMessage(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject {
                Current = Api.userId
                NavigateUrl = Api.baseUrl
                ContentUrl = Api.baseUrl
                Id = id
                Slug = slug
                Avatar = avatar
                Color = color
                Font = string font
                Message = message
                Sequence = sequence
            })) |> ignore
            scrollToBottom ()

    let sendMessage (identity:Models.User) session context text =
        match text with
        | "" -> ()
        | _ ->
            Async.StartImmediate (async {
                let! response = PrivateChat.send session.UserId text
                do! Async.SwitchToContext context
                match this.HandleApiResult response with
                | Api.ApiOk response -> 
                    this.Text.Text <- ""
                    addMessage identity.Id identity.Slug identity.Avatar "" 0 response.Feedback response.Sequence
                | error -> this.HandleApiFailure error
            })

    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        this.ResizeViewToKeyboard args
        scrollToBottom()
        

    let showObserver = UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
    let hideObserver = UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    override this.ViewDidLoad () =
        //this.NavigationItem.BackBarButtonItem.Title <- "Chats"
        this.NavigationItem.Title <- user.Name
        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.baseUrl + "/mobile/chat")))
        this.WebView.LoadFinished.Add(fun _ ->
            Async.StartImmediate <| async {
                let context = System.Threading.SynchronizationContext.Current

                // HAX HAX HAX
                let! identity = Account.identity ()
                let identity = 
                    match identity with
                    | Api.ApiOk identity -> identity
                    | _ -> failwith "API ERROR"

                let! session = PrivateChat.session user.Id
                match session with
                | Api.ApiOk session ->
                    let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
                        let rec loop () = async {
                            let! event = inbox.Receive()
                            do! Async.SwitchToContext context
                            match event with
                            | { User = user } when user <> session.UserId  -> ()
                            | { Event = PrivateMessage (color, font, message) } ->
                                addMessage session.UserId "??SLUG??" session.PartnerAvatarId color font message event.Sequence
                            | { Event = PrivateMessageSent (color, font, message) } ->
                                addMessage Api.userId identity.Slug identity.Avatar color font message event.Sequence
                            | _ -> ()

                            return! loop ()
                        }

                        loop ()
                    )

                    processor.Start()
                    ChannelClient.handlers.TryAdd(Api.userId, processor) |> ignore

                    // Send message
                    this.Text.ShouldReturn <- (fun _ ->
                        sendMessage identity session context this.Text.Text
                        false
                    )
                    this.SendButton.TouchUpInside.Add(fun args ->
                        sendMessage identity session context this.Text.Text
                    )

                | _ -> failwith "API FAIL"
            }
        )

    override this.ViewDidDisappear (animated) =
        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()

