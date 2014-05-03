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


[<Register ("GameViewController")>]
type GameViewController (entity:Entity, name:string) =
    inherit UIViewController ("GameViewController", null)

    let events = new List<ChannelEvent * string> ()

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        this.Title <- name

        this.WebView.ShouldStartLoad <- UIWebLoaderControl(fun webView request navigationType ->
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

        Async.Start(Utility.Timer.ticker (fun _ -> this.InvokeOnMainThread(fun _->  
            if events.Count > 0 then
                let eventsBuilder = new System.Text.StringBuilder()
                for (event, json) in events do
                    let json = sprintf "[%i,%i,%i,%s,'%s']" event.Sequence event.EventType event.Stamp json event.User
                    eventsBuilder.AppendLine (sprintf "wireclub.Channel.processChannelEvents('%s', %s);" entity.Id json) |> ignore

                this.WebView.EvaluateJavascript (eventsBuilder.ToString()) |> ignore
                events.Clear()
        )) 500)

    member this.ProcessEvent(event:ChannelEvent) =
        match event.Event with
        | BingoRoundChanged json
        | BingoRoundDraw json
        | BingoRoundWon json
        | AppEvent json
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
    let users = ConcurrentDictionary<string, UserProfile>()
    let mutable startSequence = 0L
    let nameplateImageSize = 21
    let appsAllowed = [| "Slots"; "Bingo"; "Blackjack" |]

    let mutable gameController:GameViewController option = None

    let mutable apps:string[] = [||]
    let mutable starred = false
        
    let nameplate (user:UserProfile) =     
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

    let mutable showObserver:NSObject = null
    let mutable hideObserver:NSObject = null

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

    let addUser = (fun (user:UserProfile) -> users.AddOrUpdate (user.Id, user, System.Func<string,UserProfile,UserProfile>(fun _ _ -> user)) |> ignore)

    let processEvent event addLine =
        let historic = event.Sequence < startSequence
        if events.Add event.Sequence then
            match gameController with
            | Some gameController -> gameController.ProcessEvent(event);
            | _ -> ()

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

    let barButtons () =
        [|
            yield this.UserButton

            if apps.Any(fun e -> appsAllowed.Contains(e)) then
                yield this.GameButton

            if starred then
                yield this.UnstarButton
            else
                yield this.StarButton
        |]


    member this.UserButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UIButtonBarProfile.png", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                this.NavigationController.PushViewController(new ChatRoomUsersViewController(users.Values |> Seq.toArray), true)
            ))

    member this.UnstarButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UIBarButtonFavoriteActive.png", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                Async.startWithContinuation
                    (Chat.unstar this.Room.Slug)
                    (function 
                        | Api.ApiOk _ -> 
                            starred <- not starred
                            this.NavigationItem.RightBarButtonItems <- barButtons()
                        | error -> this.HandleApiFailure error 
                    )
            ))

    member this.StarButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UIBarButtonFavoriteInactive.png", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                Async.startWithContinuation
                    (Chat.star this.Room.Slug)
                    (function 
                        | Api.ApiOk _ -> 
                            starred <- not starred
                            this.NavigationItem.RightBarButtonItems <- barButtons()
                        | error -> this.HandleApiFailure error 
                    )
            ))

    member this.GameButton:UIBarButtonItem =
        new UIBarButtonItem(UIImage.FromFile "UIBarButtonGameItem.png", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                match gameController with
                | Some controller -> this.NavigationController.PushViewController(controller, true)
                | _ -> ()
            ))


    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    [<Outlet>]
    member val Progress: UIActivityIndicatorView = null with get, set

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- room.Label
        this.NavigationItem.LeftItemsSupplementBackButton <- true

        // Prevents a 64px offset on a webviews scrollview
        this.AutomaticallyAdjustsScrollViewInsets <- false
        this.WebView.BackgroundColor <- UIColor.White

        this.NavigationItem.RightBarButtonItems <- barButtons()

        // Send message
        this.Text.ShouldReturn <- (fun _ ->
            sendMessage identity this.Text.Text
            false
        )
        this.SendButton.TouchUpInside.Add(fun args ->
            sendMessage identity this.Text.Text
        )

        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.webUrl + "/api/chat/chatRoomTemplate")))

        this.WebView.LoadFinished.Add(fun _ ->
            this.WebView.Delegate <- webViewDelegate
            this.WebView.SetBodyBackgroundColor (colorToCss UIColor.White)

            Async.startNetworkWithContinuation
                (Chat.join room.Slug)
                (function
                    | Api.ApiOk (result, events) ->
                        startSequence <- result.Sequence

                        starred <- result.Channel.ViewerHasStarred
                        apps <- result.Channel.Apps

                        if apps.Any(fun e -> appsAllowed.Contains(e)) then
                            gameController <- Some (new GameViewController(room, apps.First()))

                        this.NavigationItem.RightBarButtonItems <- barButtons()

                        result.Members |> Array.filter (fun e -> memberTypes.Contains e.Membership) |> Array.iter addUser
                        result.HistoricMembers |> Array.filter (fun e -> memberTypes.Contains e.Membership) |> Array.iter addUser
                        this.WebView.PreloadImages [ for user in users.Values do yield App.imageUrl user.Avatar nameplateImageSize ]
                        let lines = new List<string>()
                        for event in events do processEvent event (fun l _ -> lines.Add l)
                        addLines (lines.ToArray())
                        processor.Start()

                        this.Progress.StopAnimating ()

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
    
        showObserver <- UIKeyboard.Notifications.ObserveWillShow(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
        hideObserver <- UIKeyboard.Notifications.ObserveWillHide(System.EventHandler<UIKeyboardEventArgs>(placeKeyboard))
        this.Text.BecomeFirstResponder () |> ignore
    
    override this.ViewDidDisappear animated =
        showObserver.Dispose ()
        hideObserver.Dispose ()

    member this.HandleChannelEvent = processor.Post

    member this.Room:Entity = room

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
    
                  
