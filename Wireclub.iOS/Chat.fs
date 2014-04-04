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


[<Register ("ChatRoomUsersViewController")>]
type ChatRoomUsersViewController (users:ChatUser[]) =
    inherit UIViewController ("ChatRoomUsersViewController", null)

    let users = users.OrderBy(fun e -> e.Name).ToArray()

    let source = {
        new UITableViewSource() with
            override this.GetCell(tableView, indexPath) =
                let user = users.[indexPath.Row]
                let cell = 
                    match tableView.DequeueReusableCell "room-user-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "room-user-cell")
                    | c -> c
                
                cell.Tag <- indexPath.Row
                cell.TextLabel.Text <- user.Name
                Image.loadImageForCell (App.imageUrl user.Avatar 100) Image.placeholder cell tableView
                cell

            override this.RowsInSection(tableView, section) =
                users.Length

            override this.RowSelected(tableView, indexPath) =
                tableView.DeselectRow (indexPath, false)
                let user = users.[indexPath.Row]
                Navigation.navigate ("/users/" + user.Slug) (Some { Id = user.Id; Label = user.Name; Slug = user.Slug; Image = user.Avatar })
                ()
        }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- source
        controller.Table.ReloadData ()
        ()

[<Register ("ChatRoomViewController")>]
type ChatRoomViewController (room:Entity) as this =
    inherit UIViewController ("PrivateChatSessionViewController", null)

    let identity = match Api.userIdentity with | Some id -> id | None -> failwith "User must be logged in"
    let events = System.Collections.Generic.HashSet<int64>()
    let users = ConcurrentDictionary<string, ChatUser>()
    let mutable startSequence = 0L
    let nameplateImageSize = 21
        
    let nameplate (user:ChatUser) =     
        sprintf
            "<a class=icon href=%s/users/%s><img src=%s width=%i height=%i /></a> <a class=name href=%s/users/%s>%s</a>"
            Api.baseUrl
            user.Slug
            (App.imageUrl user.Avatar nameplateImageSize)
            nameplateImageSize
            nameplateImageSize
            Api.baseUrl
            user.Slug
            user.Name

    let line = sprintf "<div class=message>%s <div class=body>%s</div></div>" 
    let userMessageLine payload user color font = line (nameplate user) (sprintf "<span style='color: #%s; font-family: %s;'>%s</span>" color font payload)
    let notificationLine payload = line String.Empty payload
    let userFeedbackLine payload user  = line (nameplate user) payload

    let addLine line forceScroll =
        this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLine(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject { Line = line })) |> ignore
        this.WebView.EvaluateJavascript (sprintf "wireclub.Mobile.scrollToEnd(%b);" forceScroll) |> ignore

    let addLines lines =
        this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLines(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject (lines |> Array.map (fun e -> { Line = e })))) |> ignore
        this.WebView.EvaluateJavascript "wireclub.Mobile.scrollToEnd(true);" |> ignore

    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        this.ResizeViewToKeyboard args
        this.WebView.EvaluateJavascript "wireclub.Mobile.scrollToEnd(true);" |> ignore
        
    let showObserver = UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
    let hideObserver = UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))

    let webViewDelegate = {
        new UIWebViewDelegate() with
        override this.ShouldStartLoad (view, request, navigationType) =
            let uri = new Uri(request.Url.AbsoluteString)
            match uri.Segments with
            | [|_; "users/"; slug |] ->
                let user = users.Values.Single(fun e -> e.Slug = slug)
                Navigation.navigate (sprintf "/users/%s" slug) (Some { Id = user.Id; Label = user.Name; Slug = user.Slug; Image = user.Avatar })
            | segments ->  Navigation.navigate (uri.ToString()) None
            false
    }

    let sendMessage (identity:Models.User) text =
        match text with
        | "" -> ()
        | _ ->
            Async.startNetworkWithContinuation
                (Chat.send room.Slug text)
                (this.HandleApiResult >> function
                    | Api.ApiOk response -> 
                        this.Text.Text <- ""
                        match users.TryGetValue identity.Id with
                        | true, user -> addLine (userFeedbackLine response.Payload user) true
                        | _ -> ()
                    | error -> this.HandleApiFailure error
                )

    let addUser = (fun (user:ChatUser) -> users.AddOrUpdate (user.Id, user, System.Func<string,ChatUser,ChatUser>(fun _ _ -> user)) |> ignore)

    let processEvent event addLine =
        let historic = event.Sequence < startSequence
        if events.Add event.Sequence then
            match event with
            | { Event = Notification message } -> 
                addLine (notificationLine message) false

            | { Event = Message (color, font, message); User = user } when (user <> identity.Id || historic) -> 
                match users.TryGetValue event.User with
                | true, user -> addLine (userMessageLine message user color (fontFamily font)) false
                | _ -> ()

            | { Event = Join user } -> 
                if historic = false then
                    addUser user
                if users.Count < 25 then
                    addLine (userMessageLine "joined the room" user "#000" (fontFamily 0)) false

            | { Event = Leave user } -> 
                match users.TryGetValue event.User with
                | true, user -> 
                    if users.Count < 25 then
                        addLine (userMessageLine "left the room" user "#000" (fontFamily 0)) false
                    if historic = false then
                        users.TryRemove user.Id |> ignore
                | _ -> ()
            
            | { Event = DisposableMessage } -> () //messages from anon chat rooms
            | { Event = AddedModerator; User = user } when historic = false -> ()
            | { Event = RemovedModerator; User = user } when historic = false -> ()
            | { Event = Drink } -> ()
            | { Event = AcceptDrink } -> ()
            | { Event = Modifier } -> ()
            | _ -> ()


    let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
        let rec loop () = async {
            let! event = inbox.Receive()
            this.InvokeOnMainThread(fun _ -> processEvent event addLine)
            return! loop ()
        }

        loop ()
    ) 

    let memberTypes = [  MembershipTypePublic.Member; MembershipTypePublic.Moderator; MembershipTypePublic.Admin ]

    let cancelPoll = new Threading.CancellationTokenSource()

    let keepAlive () =
        this.InvokeOnMainThread(fun _ -> 
            if this.NavigationController <> null && this.NavigationController.VisibleViewController = upcast this then
                Async.startWithContinuation
                    (Chat.keepAlive room.Slug)
                    (fun _ -> ())
                ()
        )

    static member val buttonImage = Image.resize (new SizeF(22.0f, 22.0f)) (UIImage.FromFile "UIButtonBarProfile.png") with get

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- room.Label
        this.NavigationItem.LeftItemsSupplementBackButton <- true

        // Prevents a 64px offset on a webviews scrollview
        this.AutomaticallyAdjustsScrollViewInsets <- false
        this.WebView.BackgroundColor <- UIColor.White

        this.NavigationItem.RightBarButtonItem <- new UIBarButtonItem(ChatRoomViewController.buttonImage, UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            this.NavigationController.PushViewController(new ChatRoomUsersViewController(users.Values |> Seq.toArray), true)
        ))

        // Send message
        this.Text.ShouldReturn <- (fun _ ->
            sendMessage identity this.Text.Text
            false
        )
        this.SendButton.TouchUpInside.Add(fun args ->
            sendMessage identity this.Text.Text
        )

        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.baseUrl + "/api/chat/chatRoomTemplate")))
        this.WebView.LoadFinished.Add(fun _ ->
            this.WebView.Delegate <- webViewDelegate
            this.WebView.SetBodyBackgroundColor (colorToCss UIColor.White)

            Async.startNetworkWithContinuation
                (Chat.join room.Slug)
                (function
                    | Api.ApiOk (result, events) ->
                        startSequence <- result.Sequence

                        result.Members |> Array.filter (fun e -> memberTypes.Contains e.Membership) |> Array.iter addUser
                        result.HistoricMembers |> Array.filter (fun e -> memberTypes.Contains e.Membership) |> Array.iter addUser
                        this.WebView.PreloadImages [ for user in users.Values do yield App.imageUrl user.Avatar nameplateImageSize ]
                        let lines = new List<string>()
                        for event in events do processEvent event (fun l _ -> lines.Add l)
                        addLines (lines.ToArray())
                        processor.Start()

                        //remove this for know
                        //Async.Start(Timer.ticker keepAlive (30 * 1000), cancelPoll.Token)

                    | error -> this.HandleApiFailure error 
            )
        )

    override this.ViewDidAppear animated = 
        Async.startWithContinuation 
            (async {
                do! DB.updateChatHistoryReadById room.Id
                let! unread = DB.fetchChatHistoryUnreadCount ()
                return unread
            })
            (function
                | 0 -> this.NavigationItem.LeftBarButtonItem <- null
                | unread ->
                    this.NavigationItem.LeftBarButtonItem <- new UIBarButtonItem((sprintf "(%i)" unread), UIBarButtonItemStyle.Plain, new EventHandler(fun _ _ -> 
                        this.NavigationController.PopViewControllerAnimated true |> ignore
                    ))
            )
    
    override this.ViewDidDisappear animated =
        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()

    member this.HandleChannelEvent = processor.Post

    member this.Room = room

    override this.Dispose (bool) =
        cancelPoll.Cancel()

module ChatRooms =
    let rooms = ConcurrentDictionary<string, Entity * ChatRoomViewController>()

    let join (room:Entity) =
        let _, controller =
            rooms.AddOrUpdate (
                    room.Id,
                    (fun _ -> room, new ChatRoomViewController (room)),
                    (fun _ room -> room)
                )
        Async.Start (DB.createChatHistory (room, DB.ChatHistoryType.ChatRoom, None))
        controller

    let joinById id continuation =
        Async.startWithContinuation
            (DB.fetchChatHistoryById id)
            (function
                | null -> ()
                | room ->
                    let room = {
                        Id = room.EntityId
                        Label = room.Label
                        Slug = room.Slug
                        Image = room.Image
                    }
                    continuation room (join room)

            )
        ()

    let leave slug =
        match rooms.TryRemove slug with
        | _ -> ()
    
                  