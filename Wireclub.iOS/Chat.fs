namespace Wireclub.iOS

open System
open System.Collections.Concurrent
open System.Collections.Generic
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open ChannelEvent


[<Register ("ChatRoomUsersViewController")>]
type ChatRoomUsersViewController (users:ChatUser[]) =
    inherit UIViewController ("ChatRoomUsersViewController", null)

    let source = { new UITableViewSource() with
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
    let nameplateImageSize = 32
        
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

    let sendMessage (identity:Models.User) text =
        match text with
        | "" -> ()
        | _ ->
            Async.startWithContinuation
                (Chat.send room.Slug text)
                (this.HandleApiResult >> function
                    | Api.ApiOk response -> 
                        this.Text.Text <- ""
                        match users.TryGetValue identity.Id with
                        | true, user -> addLine (userFeedbackLine response.Payload user)
                        | _ -> ()
                    | error -> this.HandleApiFailure error
                )

    let addUser = (fun (user:ChatUser) -> users.AddOrUpdate (user.Id, user, System.Func<string,ChatUser,ChatUser>(fun _ _ -> user)) |> ignore)

    let processEvent event addLine =
        let historic = event.Sequence < startSequence
        if events.Add event.Sequence then
            match event with
            | { Event = Notification message } -> 
                addLine (notificationLine message)

            | { Event = Message (color, font, message); User = user } when (user <> identity.Id || historic) -> 
                match users.TryGetValue event.User with
                | true, user -> addLine (userMessageLine message user color (fontFamily font))
                | _ -> ()

            | { Event = Join user } -> 
                if historic = false then
                    addUser user
                addLine (userMessageLine "joined the room" user "#000" (fontFamily 0))

            | { Event = Leave user } -> 
                match users.TryGetValue event.User with
                | true, user -> 
                    addLine (userMessageLine "left the room" user "#000" (fontFamily 0))
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

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- room.Label


        // Prevents a 64px offset on a webviews scrollview
        this.AutomaticallyAdjustsScrollViewInsets <- false
        this.WebView.BackgroundColor <- UIColor.White

        this.NavigationItem.RightBarButtonItem <- new UIBarButtonItem("...", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
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

        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.baseUrl + "/mobile/chat")))
        this.WebView.LoadFinished.Add(fun _ ->
            this.WebView.SetBodyBackgroundColor (colorToCss UIColor.White)
            Async.startWithContinuation
                (Chat.join room.Slug)
                (function
                    | Api.ApiOk (result, events) ->
                        startSequence <- result.Sequence

                        this.WebView.PreloadImages [ for user in users.Values do yield App.imageUrl user.Avatar nameplateImageSize ]
                        let lines = new List<string>()
                        for event in events do processEvent event lines.Add
                        addLines (lines.ToArray())
                        processor.Start()
                        result.Members |> Array.iter addUser
                        result.HistoricMembers |> Array.iter addUser
                    | Api.BadRequest errors -> ()
                    | error -> this.HandleApiFailure error 
            )
        )

    override this.ViewDidDisappear (animated) =
        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()

    member this.HandleChannelEvent = processor.Post


module ChatRooms =
    let rooms = ConcurrentDictionary<string, Entity * ChatRoomViewController>()

    let join (room:Entity) =
        let _, controller =
            rooms.AddOrUpdate (
                    room.Id,
                    (fun _ -> room, new ChatRoomViewController (room)),
                    (fun _ room -> room)
                )
        Async.Start (DB.createChatHistory room DB.ChatHistoryType.ChatRoom None)
        controller

    let joinById id =
        ()

    let leave slug =
        match rooms.TryRemove slug with
        | _ -> ()
    
                  