namespace Wireclub.iOS

open System
open System.Drawing
open System.Linq
open System.Collections.Concurrent
open System.Collections.Generic
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Models
open Wireclub.Boundary 
open Wireclub.Boundary.Chat
open ChannelEvent
open Newtonsoft.Json

type ChatMessage = {
    Line: string
}

[<Register("PrivateChatSessionViewController")>]
type PrivateChatSessionViewController (user:Entity) as this =
    inherit UIViewController ()

    let identity = match Api.userIdentity with | Some id -> id | None -> failwith "User must be logged in"
    let events = System.Collections.Generic.HashSet<int64>()
    let mutable session: SessionResponse option = None
    let nameplateImageSize = 50

    let nameplate slug image = 
        let userUrl = sprintf "%s/users/%s" Api.baseUrl slug
        sprintf
            "<a class=icon href=%s><img src=%s width=%i height=%i /></a> <a class=name href=%s></a>"
            userUrl
            (App.imageUrl image nameplateImageSize)
            nameplateImageSize
            nameplateImageSize
            userUrl

    let message color font payload = sprintf "<span style='color: #%s; font-family: %s;'>%s</span>" color font payload
    let line css nameplate message = sprintf "<div class='message %s'>%s <div class=body-wrap><div class=body>%s</div></div></div>" css nameplate message 
    let partnerLine payload color font = line "partner" (nameplate user.Slug user.Image) (message color font payload) 
    let viewerLine payload color font = line "viewer" (nameplate identity.Slug identity.Avatar) (message color font payload) 

    let addLine line =
        this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLine(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject { Line = line })) |> ignore
        this.WebView.ScrollToBottom()

    let addLines lines =
        this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLines(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject (lines |> Array.map (fun e -> { Line = e })))) |> ignore
        this.WebView.ScrollToBottom()

    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        this.ResizeViewToKeyboard args
        this.WebView.ScrollToBottom()
        
    let showObserver = UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
    let hideObserver = UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))

    let sendMessage text =
        match text with
        | "" -> ()
        | _ ->
            Async.startWithContinuation
                (PrivateChat.send session.Value.UserId text)
                (function
                    | Api.ApiOk response -> 
                        this.Text.Text <- ""
                        if events.Add response.Sequence then
                            addLine (viewerLine response.Feedback response.Color (fontFamily response.Font))
                    | error -> this.HandleApiFailure error)

    let processEvent event addLine = 
        if events.Add event.Sequence then
            match event with
            | { Event = PrivateMessage (color, font, message) } -> 
                addLine (partnerLine message color (fontFamily font))
            | { Event = PrivateMessageSent (color, font, message) } -> 
                addLine (viewerLine message color (fontFamily font))
            | _ -> ()

    let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
        let rec loop () = async {
            let! event = inbox.Receive()
            this.InvokeOnMainThread (fun _ -> processEvent event addLine)
            return! loop ()
        }

        loop ()
    )

    let webViewDelegate = {
        new UIWebViewDelegate() with
        override this.ShouldStartLoad (view, request, navigationType) =
            let uri = new Uri(request.Url.AbsoluteString)
            match uri.Segments with
            | [| _; "users/"; slug |] when slug = user.Slug -> Navigation.navigate (sprintf "/users/%s" slug) (Some user)
            | [| _; "users/"; slug |] when slug = identity.Slug -> Navigation.navigate (sprintf "/users/%s" slug) (Some { Id = identity.Id; Label = identity.Name; Slug = identity.Slug; Image = identity.Avatar })
            | segments -> printfn "%A" segments
            false
    }

    static member val buttonImage = Image.resize (new SizeF(22.0f, 22.0f)) (UIImage.FromFile "UIButtonBarProfile.png") with get

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    override this.ViewDidLoad () =
        
        this.NavigationItem.RightBarButtonItem <- new UIBarButtonItem(PrivateChatSessionViewController.buttonImage, UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            Navigation.navigate (sprintf "/users/%s" user.Slug) (Some user)
        ))
        // Prevents a 64px offset on a webviews scrollview
        this.AutomaticallyAdjustsScrollViewInsets <- false
        this.WebView.BackgroundColor <- Utility.grayLightAccent

        this.NavigationItem.Title <- user.Label
        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.baseUrl + "/mobile/privateChat")))
        this.WebView.LoadFinished.Add(fun _ ->
            this.WebView.Delegate <- webViewDelegate
            this.WebView.SetBodyBackgroundColor (colorToCss Utility.grayLightAccent)

            Async.startWithContinuation
                (async {
                    let! session = PrivateChat.session user.Id
                    let! history = DB.fetchChatEventHistoryByEntity user.Id
                    return session, history
                })
                (function
                    | Api.ApiOk newSession, history ->
                        session <- Some newSession

                        let lines = new List<string>()
                        for event in history do
                            if String.IsNullOrEmpty event.EventJson = false then
                                try
                                    let event = JsonConvert.DeserializeObject<ChannelEvent>(event.EventJson)
                                    processEvent event lines.Add
                                with
                                | ex -> printfn "%A %s" event ex.Message 

                        addLines (lines.ToArray())
                        processor.Start()

                        // Send message
                        this.Text.ShouldReturn <- (fun _ -> sendMessage this.Text.Text; false)
                        this.SendButton.TouchUpInside.Add(fun args -> sendMessage this.Text.Text )

                    | error, _ -> this.HandleApiFailure error
                )
        )

    
    override this.ViewDidDisappear animated =

        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()

    member this.HandleChannelEvent = processor.Post

    member this.User = user

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
                    let user =
                        {
                            Id = newSession.UserId
                            Slug = newSession.Url // FIXME
                            Label = newSession.DisplayName
                            Image = newSession.PartnerAvatar
                        } 
                    continuation user (start user)
                    
                | error -> printfn "Failed to start PM session: %A" error
            )
        ()

    let touch (user:Wireclub.Boundary.Chat.PrivateChatFriend) =
        ()

    let leave slug =
        match sessions.TryRemove slug with
        | _ -> ()
    

                  