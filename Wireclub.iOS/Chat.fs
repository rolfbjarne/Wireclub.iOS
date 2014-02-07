namespace Wireclub.iOS

open System
open System.Collections.Concurrent
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat

[<Register ("ChatRoomUsersViewController")>]
type ChatRoomUsersViewController (users:ChatUser[]) =
    inherit UIViewController ("ChatRoomUsersViewController", null)

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- 
            { new UITableViewSource() with
                override this.GetCell(tableView, indexPath) =
                    let cell = 
                        match tableView.DequeueReusableCell "room-user-cell" with
                        | null -> new UITableViewCell()
                        | c -> c
                    
                    cell.TextLabel.Text <- users.[indexPath.Row].Name
                    cell

                override this.RowsInSection(tableView, section) =
                    users.Length

                override this.RowSelected(tableView, indexPath) =
                    controller.NavigationController.PushViewController(new DialogViewController ("http://dev.wireclub.com/users/" + users.[indexPath.Row].Slug + "?mobile=true"), true)
                    ()

            }

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

    let sendMessage (identity:Models.User) context text =
        match text with
        | "" -> ()
        | _ ->
            Async.StartImmediate (async {
                let! response = Chat.send room.Slug text
                do! Async.SwitchToContext context
                match this.HandleApiResult response with
                | Api.ApiOk response -> 
                    this.Text.Text <- ""
                    addMessage identity.Id identity.Slug identity.Avatar "" 0 response.Payload 0L
                | error -> this.HandleApiFailure error
            })

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
            Async.StartImmediate <| async {
                let context = System.Threading.SynchronizationContext.Current
                let! result = Chat.join room.Slug

                // HAX HAX HAX
                let! identity = Account.identity ()
                let identity = 
                    match identity with
                    | Api.ApiOk identity -> identity
                    | _ -> failwith "API ERROR"


                let users = ConcurrentDictionary<string, ChatUser>()
                let addUser = (fun (user:ChatUser) -> users.AddOrUpdate (user.Id, user, System.Func<string,ChatUser,ChatUser>(fun _ _ -> user)) |> ignore)

                // HAX
                this.NavigationItem.RightBarButtonItem <- new UIBarButtonItem("...", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
                    this.NavigationController.PushViewController(new ChatRoomUsersViewController(users.Values |> Seq.toArray), true)
                ))


                match result with
                | Api.ApiOk (result, events) ->
                    let processor = new MailboxProcessor<ChannelEvent>(fun inbox ->
                        let rec loop () = async {
                            let! event = inbox.Receive()
                            let historic = event.Sequence < result.Sequence
                            do! Async.SwitchToContext context
                            match event with
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
                    ChannelClient.watching.Add (room.Id)
                    ChannelClient.reset ()

                    // Send message
                    this.Text.ShouldReturn <- (fun _ ->
                        sendMessage identity context this.Text.Text
                        false
                    )
                    this.SendButton.TouchUpInside.Add(fun args ->
                        sendMessage identity context this.Text.Text
                    )
                    
                | Api.BadRequest errors -> ()
                | error -> this.HandleApiFailure error 

            })
        ()

    override this.ViewDidDisappear (animated) =
        if this.IsMovingToParentViewController then
            showObserver.Dispose ()
            hideObserver.Dispose ()

                  