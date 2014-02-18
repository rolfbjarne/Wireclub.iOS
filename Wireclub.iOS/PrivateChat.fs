namespace Wireclub.iOS

open System
open System.Collections.Concurrent
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open ChannelEvent

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
type PrivateChatSessionViewController (user:Entity) as this =
    inherit UIViewController ()

    let identity = match Api.userIdentity with | Some id -> id | None -> failwith "User must be logged in"
    let events = System.Collections.Generic.HashSet<int64>()
    let mutable session: SessionResponse option = None

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

    let sendMessage text =
        match text with
        | "" -> ()
        | _ ->
            Async.startWithContinuation
                (PrivateChat.send session.Value.UserId text)
                (function
                    | Api.ApiOk response -> 
                        this.Text.Text <- ""
                        addMessage identity.Id identity.Slug identity.Avatar "" 0 response.Feedback response.Sequence
                    | error -> this.HandleApiFailure error)

    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        this.ResizeViewToKeyboard args
        scrollToBottom()
        
    let showObserver = UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
    let hideObserver = UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))

    let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
        let rec loop () = async {
            let! event = inbox.Receive()
            this.InvokeOnMainThread (fun _ ->
                match event with
                | { Event = PrivateMessage (color, font, message) } ->
                    addMessage session.Value.UserId "??SLUG??" session.Value.PartnerAvatarId color font message event.Sequence
                    Async.Start (DB.createChatHistory user DB.ChatHistoryType.PrivateChat (Some (message, false)))

                | { Event = PrivateMessageSent (color, font, message) } ->
                    addMessage Api.userId identity.Slug identity.Avatar color font message event.Sequence
                    Async.Start (DB.createChatHistory user DB.ChatHistoryType.PrivateChat (Some ("You: " + message, false)))
                
                | _ -> ()
            )
            return! loop ()
        }

        loop ()
    )

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    override this.ViewDidLoad () =
        
        this.NavigationItem.RightBarButtonItem <- new UIBarButtonItem("...", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            Navigation.navigate (sprintf "/users/%s" user.Slug) (Some user)
        ))

        this.NavigationItem.Title <- user.Label
        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.baseUrl + "/mobile/chat")))
        this.WebView.LoadFinished.Add(fun _ ->
            Async.StartImmediate <| async {
                let context = System.Threading.SynchronizationContext.Current

                let! sessionResponse = PrivateChat.session user.Id

                match sessionResponse with
                | Api.ApiOk newSession ->
                    session <- Some newSession
                    processor.Start ()

                    // Send message
                    this.Text.ShouldReturn <- (fun _ ->
                        sendMessage this.Text.Text
                        false
                    )
                    this.SendButton.TouchUpInside.Add(fun args ->
                        sendMessage this.Text.Text
                    )

                | error -> this.HandleApiFailure error // TODO: In case of an error kill the controller?
            }
        )

    override this.ViewDidDisappear (animated) =
        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()

    member this.HandleChannelEvent = processor.Post

module ChatSessions =
    open SQLite
    open System.Linq

    let sessions = ConcurrentDictionary<string, Entity * PrivateChatSessionViewController>()

    let start (user:Entity) =
        let _, controller =
            sessions.AddOrUpdate (
                    user.Id,
                    (fun _ -> user, new PrivateChatSessionViewController (user) ),
                    (fun _ x -> x)
                )

        Async.Start (DB.createChatHistory user DB.ChatHistoryType.PrivateChat None)
        controller

    let startById id continuation =
        if id = Api.userId then failwith "Can't chat with yourself"

        Async.startWithContinuation
            (PrivateChat.session id)
            (function
                | Api.ApiOk newSession ->
                    start
                        {
                            Id = newSession.UserId
                            Slug = newSession.Url // FIXME
                            Label = newSession.DisplayName
                            Image = newSession.PartnerAvatar
                        } 
                    |> continuation
                    
                | error -> printfn "Failed to start PM session: %A" error
            )
        ()

    let touch (user:Wireclub.Boundary.Chat.PrivateChatFriend) =
        ()

    let leave slug =
        match sessions.TryRemove slug with
        | _ -> ()
    

                  