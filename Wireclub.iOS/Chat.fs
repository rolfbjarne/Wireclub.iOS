namespace Wireclub.iOS

open System
open System.Collections.Concurrent
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
                    Navigation.navigate ("/users/" + user.Slug) ({ PrivateChatFriend.Id = user.Id; Name = user.Name; Slug = user.Slug; Avatar = user.Avatar; State = OnlineStateType.Visible  })
                    ()

            }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- source
        controller.Table.ReloadData ()
        ()

[<Register ("ChatRoomViewController")>]
type ChatRoomViewController (room:ChatDirectoryRoomViewModel) as this =
    inherit UIViewController ("PrivateChatSessionViewController", null)

    // ## FACTOR
    let scrollToBottom () =
        this.WebView.EvaluateJavascript 
            (sprintf "window.scrollBy(0, %i);" 
                (int (this.WebView.EvaluateJavascript "document.body.offsetHeight;"))) |> ignore

    
    let addMessage id slug avatar color font message sequence =
        //if events.Add seqce then
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
                        addMessage identity.Id identity.Slug identity.Avatar "" 0 response.Payload 0L
                    | error -> this.HandleApiFailure error
                )

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    [<Outlet>]
    member val SendButton: UIButton = null with get, set

    [<Outlet>]
    member val Text: UITextField = null with get, set

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- room.Name

        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl(Api.baseUrl + "/mobile/chat")))
        this.WebView.LoadFinished.Add(fun _ ->
            Async.startWithContinuation
                (async {
                    let! result = Chat.join room.Slug

                    // HAX HAX HAX
                    let! identity = Account.identity ()
                    let identity = 
                        match identity with
                        | Api.ApiOk identity -> identity
                        | _ -> failwith "API ERROR"

                    return result, identity
                })
                (fun (result, identity) ->

                    let users = ConcurrentDictionary<string, ChatUser>()
                    let addUser = (fun (user:ChatUser) -> users.AddOrUpdate (user.Id, user, System.Func<string,ChatUser,ChatUser>(fun _ _ -> user)) |> ignore)

                    // HAX
                    this.NavigationItem.RightBarButtonItem <- new UIBarButtonItem("...", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                        this.NavigationController.PushViewController(new ChatRoomUsersViewController(users.Values |> Seq.toArray), true)
                    ))

                    match result with
                    | Api.ApiOk (result, events) ->
                        let context = System.Threading.SynchronizationContext.Current
                        let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
                            let rec loop () = async {
                                let! event = inbox.Receive()
                                let historic = event.Sequence < result.Sequence

                                do! Async.SwitchToContext context
                                match event with
                                | { Event = Notification message } -> 
                                    addMessage "id" "slug" "avatar" "#000" 0 message event.Sequence

                                | { Event = Message (color, font, message); User = user } when (user <> identity.Id || historic) -> 
                                    let nameplate = 
                                        match users.TryGetValue event.User with
                                        | true, user -> user.Name
                                        | _ -> sprintf "[%s]" event.User

                                    addMessage "id" "slug" "avatar" color font (sprintf "%s: %s" nameplate message) event.Sequence

                                | { Event = Join user } -> 
                                    if historic = false then
                                        addUser user
                                    addMessage "id" "slug" "avatar" "#000" 0 (sprintf "%s joined the room" user.Name) event.Sequence

                                | { Event = Leave user } -> 
                                    match users.TryGetValue event.User with
                                    | true, user -> 
                                        addMessage "id" "slug" "avatar" "#000" 0 (sprintf "%s left the room" user.Name) event.Sequence
                                        if historic = false then
                                            users.TryRemove user.Id |> ignore
                                    | _ -> ()
                                
                                | { Event = DisposableMessage } -> () // Does the app even need to handle this?
                                | { Event = AddedModerator; User = user } when historic = false -> ()
                                | { Event = RemovedModerator; User = user } when historic = false -> ()
                                | { Event = Drink } -> ()
                                | { Event = AcceptDrink } -> ()
                                | { Event = Modifier } -> ()
                                | _ -> ()

                                return! loop ()
                            }

                            loop ()
                        ) 

                        processor.Start()
                        events |> Array.iter processor.Post
                        result.Members |> Array.iter addUser
                        result.HistoricMembers |> Array.iter addUser

                        ChannelClient.handlers.TryAdd (room.Id, processor) |> ignore

                        // Send message
                        this.Text.ShouldReturn <- (fun _ ->
                            sendMessage identity this.Text.Text
                            false
                        )
                        this.SendButton.TouchUpInside.Add(fun args ->
                            sendMessage identity this.Text.Text
                        )
                        
                    | Api.BadRequest errors -> ()
                    | error -> this.HandleApiFailure error 

            )
        )

    override this.ViewDidDisappear (animated) =
        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()


module ChatRooms =
    let rooms = ConcurrentDictionary<string, ChatDirectoryRoomViewModel * ChatRoomViewController>()

    let join (room:ChatDirectoryRoomViewModel) =
        let _, controller =
            rooms.AddOrUpdate (
                    room.Slug,
                    (fun _ -> room, new ChatRoomViewController (room)),
                    (fun _ room -> room)
                )
        controller

    let leave slug =
        match rooms.TryRemove slug with
        | _ -> ()
    
                  