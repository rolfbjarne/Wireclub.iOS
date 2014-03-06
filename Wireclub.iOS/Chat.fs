namespace Wireclub.iOS

open System
open System.Collections.Concurrent
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open ChannelEvent

type ChatRoomMessage = {
    Line: string
}

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

    // ## FACTOR
    let scrollToBottom () =
        this.WebView.EvaluateJavascript 
            (sprintf "window.scrollBy(0, %i);" (int (this.WebView.EvaluateJavascript "document.body.offsetHeight;"))) |> ignore
        
    let addLine line =
        this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLine(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject { Line = line })) |> ignore

    let nameplate (user:ChatUser) =     
        sprintf
            "<a class=icon href=%s/users/%s><img src=%s width=%i height=%i /></a> <a class=name href=%s/users/%s>%s</a>"
            Api.baseUrl
            user.Slug
            (App.imageUrl user.Avatar 32)
            32
            32
            Api.baseUrl
            user.Slug
            user.Name

    let line = sprintf "<div class=message>%s <div class=body>%s</div></div>" 

    let addUserMessage sequence user color font payload =
        if events.Add sequence then
            let line = line (nameplate user) (sprintf "<span style='color: #%s; font-family: %s;'>%s</span>" color font payload)
            addLine line
            scrollToBottom()

    let addNotification sequence payload =
        if events.Add sequence then
            addLine (line String.Empty payload)
            scrollToBottom()

    let addUserFeedback user payload =
        addLine (line (nameplate user) payload)
        scrollToBottom()


    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        this.ResizeViewToKeyboard args
        scrollToBottom()
        
    let showObserver = UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
    let hideObserver = UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))

    let sendMessage (identity:Models.User) text =
        match text with
        | "" -> ()
        | _ ->
            Async.startWithContinuation
                (Chat.send room.Slug text)
                (fun response -> 
                    match this.HandleApiResult response with
                    | Api.ApiOk response -> 
                        this.Text.Text <- ""
                        match users.TryGetValue identity.Id with
                        | true, user -> addUserFeedback user response.Payload 
                        | _ -> ()
                    | error -> this.HandleApiFailure error
                )

    let addUser = (fun (user:ChatUser) -> users.AddOrUpdate (user.Id, user, System.Func<string,ChatUser,ChatUser>(fun _ _ -> user)) |> ignore)

    let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
        let rec loop () = async {
            let! event = inbox.Receive()
            let historic = event.Sequence < startSequence

            this.InvokeOnMainThread(fun _ ->
                match event with
                | { Event = Notification message } -> 
                    addNotification event.Sequence message

                | { Event = Message (color, font, message); User = user } when (user <> identity.Id || historic) -> 
                    match users.TryGetValue event.User with
                    | true, user -> addUserMessage event.Sequence user color (fontFamily font) message
                    | _ -> ()

                | { Event = Join user } -> 
                    if historic = false then
                        addUser user
                    addUserMessage event.Sequence user "#000" (fontFamily 0) "joined the room"

                | { Event = Leave user } -> 
                    match users.TryGetValue event.User with
                    | true, user -> 
                        addUserMessage event.Sequence user "#000" (fontFamily 0) "left the room"
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
        this.NavigationItem.Title <- room.Label


        // Prevents a 64px offset on a webviews scrollview
        this.AutomaticallyAdjustsScrollViewInsets <- false
        this.WebView.BackgroundColor <- Utility.grayLightAccent

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
            Async.startWithContinuation
                (Chat.join room.Slug)
                (fun result ->
                    match result with
                    | Api.ApiOk (result, events) ->
                        startSequence <- result.Sequence
                        processor.Start()
                        events |> Array.iter processor.Post
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
    
                  