// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

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
open Wireclub.Boundary.Models
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
    let mutable loaded = false
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

    let addLine line forceScroll =
        this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLine(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject { Line = line })) |> ignore
        this.WebView.EvaluateJavascript (sprintf "wireclub.Mobile.scrollToEnd(%b);" forceScroll) |> ignore

    let addLines lines =
        this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLines(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject (lines |> Array.map (fun e -> { Line = e })))) |> ignore
        this.WebView.EvaluateJavascript "wireclub.Mobile.scrollToEnd(true);" |> ignore

    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        this.ResizeViewToKeyboard args
        this.WebView.EvaluateJavascript "wireclub.Mobile.scrollToEnd(true);" |> ignore
        
    let mutable showObserver:NSObject = null
    let mutable hideObserver:NSObject = null

    let sendMessage text =
        match text with
        | "" -> ()
        | _ ->
            this.SendButton.Enabled <- false
            Async.startNetworkWithContinuation
                (PrivateChat.send session.Value.UserId text)
                (fun result ->
                    this.SendButton.Enabled <- true
                    match result with
                    | Api.ApiOk response -> 
                        this.Text.Text <- ""
                        if events.Add response.Sequence then
                            addLine (viewerLine response.Feedback response.Color (fontFamily response.Font)) true
                    | error -> this.HandleApiFailure error)

    let processEvent event addLine = 
        if events.Add event.Sequence then
            match event with
            | { Event = PrivateMessage (color, font, message) } -> 
                addLine (partnerLine message color (fontFamily font)) false
            | { Event = PrivateMessageSent (color, font, message) } -> 
                addLine (viewerLine message color (fontFamily font)) true
            | _ -> ()

            if events.Count > 50 then
                events.Clear()

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
            | segments -> Navigation.navigate (uri.ToString()) None
            false
    }

    let showMore () =
        let sheet = new UIActionSheet (Title = "Private Message Options")
        sheet.AddButton "Close Chat" |> ignore
        sheet.AddButton "Cancel" |> ignore
        sheet.DestructiveButtonIndex <- 0
        sheet.CancelButtonIndex <- 1

        sheet.ShowInView (this.View)
        sheet.Dismissed.Add(fun args ->
            match args.ButtonIndex with
            | 0 -> 
                Async.startInBackgroundWithContinuation
                    (fun _ -> DB.removeChatHistoryById user.Id)
                    (fun _ ->
                        this.Leave user.Id
                        Navigation.navigate "/home" None
                    )
            | _ -> ()
        )

    static member val buttonImage = UIImage.FromFile "UIButtonBarProfile.png" with get

    member val Leave:(string -> unit) = (fun (slug:string) -> ()) with get, set

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    [<Outlet>]
    member val Progress: UIActivityIndicatorView = null with get, set

    member this.UserButton:UIBarButtonItem =
        new UIBarButtonItem(PrivateChatSessionViewController.buttonImage, UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            Navigation.navigate (sprintf "/users/%s" user.Slug) (Some user)
        ))

    member this.MoreButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UITabBarMoreTemplateSelected.png", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> showMore()))

    override this.ViewDidLoad () =
        this.NavigationItem.LeftItemsSupplementBackButton <- true
        this.NavigationItem.RightBarButtonItems <-
            [|
                this.MoreButton
                this.UserButton
            |]
        // Prevents a 64px offset on a webviews scrollview
        this.AutomaticallyAdjustsScrollViewInsets <- false
        this.WebView.BackgroundColor <- Utility.grayLightAccent

        this.NavigationItem.Title <- user.Label

    override this.ViewDidAppear animated = 
        if loaded = false then
            this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.webUrl + "/api/chat/privateChatTemplate")))
            this.WebView.LoadError.Add(fun error -> showSimpleAlert "Error" error.Error.Description "Close")
            this.WebView.LoadFinished.Add(fun _ ->
                this.WebView.Delegate <- webViewDelegate
                this.WebView.SetBodyBackgroundColor (colorToCss Utility.grayLightAccent)

                Async.startNetworkWithContinuation
                    (async {
                        let! session = PrivateChat.session user.Id
                        let history = DB.fetchChatEventHistoryByEntity user.Id
                        return session, history
                    })
                    (function
                        | Api.ApiOk newSession, history ->
                            session <- Some newSession
                            loaded <- true

                            let lines = new List<string>()
                            for event in history.OrderBy(fun e -> e.LastStamp) do
                                if String.IsNullOrEmpty event.EventJson = false then
                                    try
                                        let event = JsonConvert.DeserializeObject<ChannelEvent>(event.EventJson)
                                        processEvent event (fun l _ -> lines.Add l)
                                    with
                                    | ex -> printfn "%A %s" event ex.Message 

                            addLines (lines.ToArray())
                            processor.Start()

                            // Send message
                            this.Text.ShouldReturn <- (fun _ -> sendMessage this.Text.Text; false)
                            this.SendButton.TouchUpInside.Add(fun args -> sendMessage this.Text.Text )
                            this.Progress.StopAnimating ()

                        | error, _ -> this.HandleApiFailure error
                    )
            )

        Async.startInBackgroundWithContinuation 
            (fun _ ->
                DB.updateChatHistoryReadById user.Id
                DB.fetchChatHistoryUnreadCount ()
            )
            (function
                | 0 -> this.NavigationItem.LeftBarButtonItem <- null
                | unread ->
                    this.NavigationItem.LeftBarButtonItem <- new UIBarButtonItem((sprintf "(%i)" unread), UIBarButtonItemStyle.Plain, new EventHandler(fun _ _ -> 
                        this.NavigationController.PopViewControllerAnimated true |> ignore
                    ))
            )

        showObserver <- UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
        hideObserver <- UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
        this.Text.BecomeFirstResponder () |> ignore
    
    override this.ViewDidDisappear animated =
        showObserver.Dispose ()
        hideObserver.Dispose ()

    member this.HandleChannelEvent = processor.Post

    member this.User = user

module ChatSessions =
    open SQLite
    open System.Linq

    let sessions = ConcurrentDictionary<string, Entity * PrivateChatSessionViewController>()

    let leave id =
        match sessions.TryRemove id with
        | _ -> ()

    let start (user:Entity) =
        let _, controller =
            let controllerAdd = new PrivateChatSessionViewController (user) 
            controllerAdd.Leave <- leave

            sessions.AddOrUpdate (
                user.Id,
                (fun _ -> user, controllerAdd ),
                (fun _ x -> x)
            )

        Async.Start (DB.createChatHistory (user, DB.ChatHistoryType.PrivateChat, None))
        controller

    let startById id continuation =
        if id = Api.userId then failwith "Can't chat with yourself"

        Async.startNetworkWithContinuation
            (PrivateChat.session id)
            (function
                | Api.ApiOk newSession ->
                    let user =
                        {
                            Id = newSession.UserId
                            Slug = newSession.Slug // FIXME
                            Label = newSession.DisplayName
                            Image = newSession.PartnerAvatar
                        } 
                    continuation user (start user)
                    
                | error -> printfn "Failed to start PM session: %A" error
            )
        ()
