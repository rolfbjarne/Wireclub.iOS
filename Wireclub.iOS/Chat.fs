// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open System.Linq
open System.Collections.Concurrent
open System.Collections.Generic
open System.Text.RegularExpressions

open Foundation
open UIKit
open CoreGraphics

open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models
open ChannelEvent

open Utility


[<Register ("GameViewController")>]
type GameViewController (entity:Entity, name:string) =
    inherit UIViewController ("GameViewController", null)

    let events = new List<ChannelEvent * string> ()

    let cancelPoll = new Threading.CancellationTokenSource()

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        this.Title <- name

        this.WebView.ShouldStartLoad <- UIWebLoaderControl(fun webView request navigationType ->
            let uri = new Uri(request.Url.AbsoluteString)
            match uri.Segments with
            | [|_; "credits"; |] ->
                Navigation.navigate  "/credits" None
                false
            | _ -> 
                if request.Headers.Keys |> Array.exists ((=) (NSObject.FromObject "x-auth-token")) = false then

                    printfn "[Game] loading %s" (request.Url.ToString())

                    let headers = new NSMutableDictionary (request.Headers)
                    headers.SetValueForKey(NSObject.FromObject (Api.userToken), new NSString("x-auth-token"))
                    let request = request.MutableCopy () :?> NSMutableUrlRequest

                    request.Headers <- headers
                    webView.LoadRequest request
                    false
                else
                    true
        )

        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.webUrl + "/mobile/chat/game/" + entity.Slug)))

        #if DEBUG
        let refresh = new UIRefreshControl()
        refresh.ValueChanged.Add(fun e -> 
            this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.webUrl + "/mobile/chat/game/" + entity.Slug)))
            refresh.EndRefreshing()
        )
        this.WebView.ScrollView.AddSubview refresh
        #endif

        Async.Start(Utility.Timer.ticker (fun _ -> 
            if events.Count > 0 then
                let eventsBuilder = new System.Text.StringBuilder()
                for (event, json) in events do
                    let json = String.Format( "[{0},{1},{2},{3},'{4}']",event.Sequence, event.EventType, event.Stamp, json, event.User)
                    eventsBuilder.AppendFormat ("wireclub.Channel.processChannelEvents('{0}', {1});", entity.Id, json) |> ignore

                this.InvokeOnMainThread(fun _-> this.WebView.EvaluateJavascript (eventsBuilder.ToString()) |> ignore)
                events.Clear()

        ) 500, cancelPoll.Token)

    override this.Dispose (bool) =
        cancelPoll.Cancel()

    member this.ProcessEvent(event:ChannelEvent) =
        match event.Event with
        | BingoRoundChanged json
        | BingoRoundDraw json
        | BingoRoundWon json
        | AppEvent (_, json)
        | CustomAppEvent json -> events.Add (event, json)
        | _ -> ()

     

[<Register ("ChatRoomUsersViewController")>]
type ChatRoomUsersViewController (users:UserProfile[]) =
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
                
                cell.Tag <- nint indexPath.Row
                cell.TextLabel.Text <- user.Name
                Image.loadImageForCell (App.imageUrl user.Avatar 100) Image.placeholder cell tableView
                cell

            override this.RowsInSection(tableView, section) =
                nint users.Length

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
type ChatRoomViewController (room:Entity) as controller =
    inherit UIViewController ("PrivateChatSessionViewController", null)

    static let nameplateImageSize = 21
    static let appsAllowed = [| "Slots"; "Bingo"; "Blackjack"; "WireSlots" |]

    let identity = match Api.userIdentity with | Some id -> id | None -> failwith "User must be logged in"

    let events = System.Collections.Generic.HashSet<int64>()
    let users = ConcurrentDictionary<string, UserProfile * bool>()
    let eventBuffer = new List<ChannelEvent>()

    let mutable startSequence = 0L
    let mutable loaded = false
    let mutable active = true

    let mutable apps:string[] = [||]
    let mutable starred = false
        
    let mutable showObserver:NSObject = null
    let mutable hideObserver:NSObject = null
    let mutable activeObserver:NSObject = null
    let mutable inactiveObserver:NSObject = null
    let mutable appEventObserver:NSObject = null

    let mutable gameController:GameViewController option = None

    let nameplate (user:UserProfile) =     
        String.Format(
            "<a class=icon href={0}/users/{1}><img src={2} width={3} height={4} /></a> <a class=name href={5}/users/{6}>{7}</a>",
            Api.baseUrl,
            user.Slug,
            (App.imageUrl user.Avatar nameplateImageSize),
            nameplateImageSize,
            nameplateImageSize,
            Api.baseUrl,
            user.Slug,
            user.Name)

    let sanitize payload = Regex.Replace(payload, "src=\"\/\/static.wireclub.com\/", "src=\"http://static.wireclub.com/")

    let line = sprintf "<div class=message>%s <div class=body>%s</div></div>" 
    let userMessageLine payload user color font = line (nameplate user) (sprintf "<span style='color: #%s; font-family: %s;'>%s</span>" color font (sanitize payload))
    let notificationLine payload = line String.Empty payload
    let userFeedbackLine payload user  = line (nameplate user) payload

    let addLine line forceScroll =
        controller.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLine(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject { Line = line })) |> ignore
        controller.WebView.EvaluateJavascript (sprintf "wireclub.Mobile.scrollToEnd(%b);" forceScroll) |> ignore

    let addLines lines =
        controller.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addLines(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject (lines |> Array.map (fun e -> { Line = e })))) |> ignore
        controller.WebView.EvaluateJavascript "wireclub.Mobile.scrollToEnd(true);" |> ignore

    let placeKeyboard (sender:obj) (args:UIKeyboardEventArgs) =
        controller.ResizeViewToKeyboard args
        controller.WebView.EvaluateJavascript "wireclub.Mobile.scrollToEnd(true);" |> ignore

    let sendMessage (identity:Models.User) text =
        match text with
        | "" -> ()
        | _ ->
            controller.SendButton.Enabled <- false
            Async.startNetworkWithContinuation
                (Chat.send room.Slug text)
                (controller.HandleApiResult >> fun result ->
                    controller.SendButton.Enabled <- true
                    match result with 
                    | Api.ApiOk response -> 
                        controller.Text.Text <- ""
                        match users.TryGetValue identity.Id with
                        | true, (user, _) ->
                            addLine (userFeedbackLine response.Payload user) true
                        | _ -> ()
                    | error -> controller.HandleApiFailure error
                )

    let addUser (userProfile:UserProfile * bool) =
        let user, historic = userProfile
        users.AddOrUpdate (user.Id, userProfile, System.Func<string,UserProfile * bool, UserProfile *bool>(fun _ _ -> userProfile)) |> ignore

    let processEvent event addLine =
        if loaded && active then
            let historic = event.Sequence <= startSequence
            if events.Add event.Sequence then
                match gameController with
                | Some gameController -> gameController.ProcessEvent(event);
                | _ -> ()

                match event with
                | { Event = Notification message } -> 
                    addLine (notificationLine message) false

                | { Event = Message (color, font, message); User = user } when (user <> identity.Id || historic) -> 
                    match users.TryGetValue event.User with
                    | true, (user, _) when user.Blocked = false ->
                        addLine (userMessageLine message user color (fontFamily font)) false
                    | _ -> ()

                | { Event = Join user } -> 
                    if historic = false then
                        addUser(user, false)
                    if users.Count < 25 then
                        addLine (userMessageLine "joined the room" user "#000" (fontFamily 0)) false

                | { Event = Leave user } -> 
                    match users.TryGetValue event.User with
                    | true, (user, _) -> 
                        if users.Count < 25 then
                            addLine (userMessageLine "left the room" user "#000" (fontFamily 0)) false
                        if historic = false then
                            users.TryRemove user.Id |> ignore

                            if user.Id = identity.Id then
                                match controller.NavigationController with
                                | null -> ()
                                | navController when navController.VisibleViewController = (controller :> UIViewController) ->
                                    let alert = new UIAlertView (Title = "Room Left", Message = "You have left the room.")
                                    alert.AddButton "Leave" |> ignore
                                    alert.AddButton "Rejoin" |> ignore
                                    alert.Show ()
                                    alert.Clicked.Add(fun e -> 
                                        match int e.ButtonIndex with
                                        | 0 -> controller.Leave()
                                        | 1 -> controller.Join()
                                        | _ -> ()
                                    )
                                | _ -> ()
                    | _ -> ()
                
                | { Event = DisposableMessage } -> () //messages from anon chat rooms
                | { Event = AddedModerator; User = user } when historic = false -> ()
                | { Event = RemovedModerator; User = user } when historic = false -> ()
                | { Event = Drink } -> ()
                | { Event = AcceptDrink } -> ()
                | { Event = Modifier } -> ()
                | _ -> ()

                if events.Count > 50 then
                    events.Clear()
        else
            eventBuffer.Add(event)

    let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
        let rec loop () = async {
            let! event = inbox.Receive()
            controller.InvokeOnMainThread(fun _ -> processEvent event addLine)
            return! loop ()
        }

        loop ()
    ) 

    let memberTypes = [  MembershipTypePublic.Member; MembershipTypePublic.Moderator; MembershipTypePublic.Admin ]

    let barButtons () =
        [|
            yield controller.MoreButton

            yield controller.UserButton

            if apps.Any(fun e -> appsAllowed.Contains(e)) then
                yield controller.GameButton
        |]

    let markRead () =
        Async.startInBackgroundWithContinuation 
            (fun _ ->
                DB.updateChatHistoryReadById room.Id
                DB.fetchChatHistoryUnreadCount ()
            )
            (function
                | 0 -> controller.NavigationItem.LeftBarButtonItem <- null
                | unread ->
                    controller.NavigationItem.LeftBarButtonItem <- new UIBarButtonItem((sprintf "(%i)" unread), UIBarButtonItemStyle.Plain, new EventHandler(fun _ _ -> 
                        controller.NavigationController.PopViewController true |> ignore
                    ))
            )

    let activate () =
        active <- true
        let lines = new List<string>()
        for event in eventBuffer do processEvent event (fun l _ -> lines.Add l)
        eventBuffer.Clear()
        addLines (lines.ToArray())

    let load () =
        loaded <- true
        controller.WebView.PreloadImages [ for user, _ in users.Values do yield App.imageUrl user.Avatar nameplateImageSize ]
        controller.Progress.StopAnimating ()
        controller.NavigationItem.RightBarButtonItems <- barButtons()

        if apps.Any(fun e -> appsAllowed.Contains(e)) then
            gameController <- Some (new GameViewController(room, apps.First()))

        activate ()

        processor.Start()

    let leave () =
        active <- false
        Async.startNetworkWithContinuation
            (Chat.leave controller.Room.Slug)
            (fun _ -> 
                Async.startInBackgroundWithContinuation
                    (fun _ -> DB.removeChatHistoryById room.Id)
                    (fun _ ->
                        controller.OnLeave room.Id
                        Navigation.navigate "/home" None
                    )
            )

    let join () =
        Async.startNetworkWithContinuation
            (Chat.join room.Slug loaded)
            (function
            | Api.ApiOk (result, events) ->
                startSequence <- result.Sequence
                starred <- result.Channel.ViewerHasStarred
                apps <- result.Channel.Apps

                result.Members |> Array.filter (fun e -> memberTypes.Contains e.Membership) |> Array.iter (fun u -> addUser (u,false))
                result.HistoricMembers |> Array.filter (fun e -> memberTypes.Contains e.Membership) |> Array.iter (fun u -> addUser (u,true))

                let lines = new List<string>()
                for event in events do processEvent event (fun l _ -> lines.Add l)
                addLines (lines.ToArray())

                if loaded = false then
                    controller.WebView.LoadHtmlString(result.Html, new NSUrl(Api.webUrl + "/template"))
                
                if active = false then
                    activate ()

            | Api.HttpError (code, _) when code = 404 ->
                let alert = new UIAlertView (Title = "Room Deleted", Message = "This room no longer exists.")
                alert.AddButton "Leave" |> ignore
                alert.Show ()
                alert.Clicked.Add(fun _ -> leave())
            | error -> controller.HandleApiFailure error)


    member val WebViewDelegate = {
        new UIWebViewDelegate() with
        override this.ShouldStartLoad (view, request, navigationType) =
            let uri = new Uri(request.Url.AbsoluteString)
            match uri.Segments with
            | [|_; "template" |] -> true
            | [|_; "users/"; slug |] ->
                match users.Values |> Seq.tryFind (fun (user, _) -> user.Slug = slug) with
                | Some (user, _) -> Navigation.navigate (sprintf "/users/%s" slug) (Some { Id = user.Id; Label = user.Name; Slug = user.Slug; Image = user.Avatar })
                | _ -> Navigation.navigate (sprintf "/users/%s" slug) None
                false
            | _ -> 
                Navigation.navigate (uri.ToString()) None
                false

        override this.LoadFailed (view, error) =
            showSimpleAlert "Error" error.Description "Close"

        override this.LoadingFinished (view) =
            load ()
    }

    member this.UserButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UIButtonBarProfile.png", UIBarButtonItemStyle.Plain, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                let users =
                    [|
                        for (user, historic) in users.Values do
                            if historic = false then
                                yield user

                    |]
                this.NavigationController.PushViewController(new ChatRoomUsersViewController(users), true)
            ))

    member this.MoreButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UITabBarMoreTemplateSelected.png", UIBarButtonItemStyle.Plain, new EventHandler(fun (s:obj) (e:EventArgs) ->
            let sheet = new UIActionSheet (Title = "Chat Room Options")
            sheet.AddButton (if starred then "Unfavorite Room" else "Favorite Room") |> ignore
            sheet.AddButton "Leave Room" |> ignore
            sheet.AddButton "Cancel" |> ignore
            sheet.DestructiveButtonIndex <- nint 1
            sheet.CancelButtonIndex <- nint 2

            sheet.ShowInView (controller.View)
            sheet.Dismissed.Add(fun args ->
                match int args.ButtonIndex with
                | 0 -> 
                    Async.startWithContinuation
                        ((if starred then Chat.unstar else Chat.star) controller.Room.Slug)
                        (function 
                            | Api.ApiOk _ -> starred <- not starred
                            | error -> controller.HandleApiFailure error 
                        )
                | 1 -> leave ()
                | _ -> ()
            )))

    member this.GameButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UIBarButtonGameItem.png", UIBarButtonItemStyle.Plain, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                match gameController with
                | Some controller -> this.NavigationController.PushViewController(controller, true)
                | _ -> ()
            ))

    member val OnLeave:(string -> unit) = (fun (slug:string) -> ()) with get, set

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    [<Outlet>]
    member val Progress: UIActivityIndicatorView = null with get, set

    override this.ViewDidLoad () =
        this.WebView.BackgroundColor <- UIColor.White
        this.WebView.Delegate <- this.WebViewDelegate

        this.NavigationItem.Title <- room.Label
        this.NavigationItem.LeftItemsSupplementBackButton <- true

        // Prevents a 64px offset on a webviews scrollview
        this.AutomaticallyAdjustsScrollViewInsets <- false
        this.NavigationItem.RightBarButtonItems <- barButtons()

        // Send message
        this.Text.ShouldReturn <- (fun _ -> sendMessage identity this.Text.Text; false)
        this.SendButton.TouchUpInside.Add(fun args -> sendMessage identity this.Text.Text)

        appEventObserver <- NSNotificationCenter.DefaultCenter.AddObserver(NSString.op_Explicit "Wireclub.AppEvent", (fun n -> this.OnAppEvent n))

    member this.OnAppEvent (notification:NSNotification) =
        notification.HandleAppEvent
            (function
                | UserRelationshipChanged (id, blocked)-> 
                    match users.TryGetValue id with
                    | true, (user, historic) -> users.[id] <- ({ user with Blocked = blocked }, historic)
                    | _ -> ()
                | _ -> ()
            )

    member this.ViewDidBecomeActive notification =
        join ()

    member this.ViewWillResignActive notification =
        active <- false

    override this.ViewDidAppear animated = 
        join ()
        markRead ()

        // keyboard
        showObserver <- UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
        hideObserver <- UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
        activeObserver <- NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidBecomeActiveNotification, (fun n -> this.ViewDidBecomeActive n))
        inactiveObserver <- NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.WillResignActiveNotification, (fun n -> this.ViewWillResignActive n))

    override this.ViewDidDisappear animated =
        showObserver.Dispose ()
        hideObserver.Dispose ()
        activeObserver.Dispose()
        inactiveObserver.Dispose()

    member this.HandleChannelEvent = processor.Post

    member this.Join () = join ()
    member this.Leave () = leave()
    member this.Room:Entity = room

module ChatRooms =
    let rooms = ConcurrentDictionary<string, Entity * ChatRoomViewController>()

    let leave id =
        match rooms.TryRemove id with
        | false, _ -> printfn "key does not exist for leaving a room"
        | _ -> ()
    
    let join (room:Entity) =
        let _, controller =
            let controllerAdd = new ChatRoomViewController (room)
            controllerAdd.OnLeave <- leave
            rooms.AddOrUpdate (
                    room.Id,
                    (fun _ -> room, controllerAdd),
                    (fun _ room -> room)
                )

        Async.startInBackgroundWithContinuation
            (fun _ -> DB.createChatHistory room DB.ChatHistoryType.ChatRoom)
            (fun _ -> ())
        controller

    let joinById id continuation =
        Async.startInBackgroundWithContinuation
            (fun _ -> DB.fetchChatHistoryById id)
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

                  
