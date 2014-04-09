// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open System.Text.RegularExpressions
open System.Linq
open System.Drawing
open System.Globalization
open System.Web

open MonoTouch.Foundation
open MonoTouch.UIKit

open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models

open Newtonsoft.Json

open ChannelEvent


[<RequireQualifiedAccess>]
type ChatSession =
| User of Entity
| ChatRoom of Entity

[<Register("ChatsViewController")>]
type ChatsViewController () as controller =
    inherit UIViewController ()
         
    let mutable (chats:DB.ChatHistory[]) = [| |]

    let readColor = new UIColor(0.3f, 0.3f, 0.3f, 1.0f)
    let unreadColor = new UIColor(0.0f, 0.0f, 0.0f, 1.0f)
    let mutable loaded = false

    let tableSource = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
            match chats with
            | [||] when loaded -> 
                let cell = 
                    match tableView.DequeueReusableCell "no-chat-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "no-chat-cell")
                    | c -> c

                let label = new UILabel(tableView.Frame)
                label.TextAlignment <- UITextAlignment.Center
                label.Text <- "No chats yet."
                cell.ContentView.AddSubview(label)
                cell.SelectionStyle <- UITableViewCellSelectionStyle.None
                cell
            | _ -> 
                let chat = chats.[indexPath.Row]
                let cell = 
                    match tableView.DequeueReusableCell "chat-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "chat-cell")
                    | c -> c
                
                cell.Tag <- indexPath.Row
                match chat.Type with
                | DB.ChatHistoryType.PrivateChat
                | DB.ChatHistoryType.ChatRoom
                | _ ->
                    let color = if chat.Read then readColor else unreadColor
                    cell.TextLabel.Text <- chat.Label
                    cell.TextLabel.TextColor <- color
                    cell.DetailTextLabel.Text <- chat.Last
                    cell.DetailTextLabel.TextColor <- color
                    Image.loadImageForCell (App.imageUrl chat.Image 100) Image.placeholder cell tableView
                
                cell

        override this.RowsInSection(tableView, section) =
            match chats with
            | [||] when loaded -> 1
            | _ -> chats.Length

        override this.GetHeightForRow(tableView, index) =
            match chats with
            | [||] when loaded -> controller.Table.Frame.Height
            | _ -> tableView.RowHeight
    
        override this.RowSelected(tableView, indexPath) =
            match chats with
            | [||] when loaded -> ()
            | _ -> 
                tableView.DeselectRow (indexPath, false)
                let session = chats.[indexPath.Row]
                let entity = {
                    Id = session.EntityId
                    Slug = session.Slug
                    Label = session.Label
                    Image = session.Image
                }
                match session.Type with
                | DB.ChatHistoryType.PrivateChat ->
                    let newController = ChatSessions.start entity
                    controller.NavigationController.PushViewController(newController, true)
                | DB.ChatHistoryType.ChatRoom
                | _ ->
                    let newController = ChatRooms.join entity
                    controller.NavigationController.PushViewController(newController, true)
    }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    member this.Reload () =
        Async.startWithContinuation
            (DB.fetchChatHistory ())
            (fun sessions ->
                chats <- Seq.toArray sessions
                loaded <- true
                this.Table.ReloadData()
            )

    override controller.ViewDidLoad () =
        base.ViewDidLoad ()
        controller.Table.Source <- tableSource

    override this.ViewDidAppear (animated) =
        base.ViewDidAppear (animated)
        this.Reload()


[<Register ("ChatDirectoryViewController")>]
type ChatDirectoryViewController() as controller =
    inherit UIViewController ()

    let mutable directory:ChatDirectoryViewModel option = None
    let rooms () = 
        match directory, controller.RoomFilter.SelectedSegment with
        | Some directory, 0 -> directory.Official 
        | Some directory, 1 -> directory.Member
        | Some directory, 2 -> directory.Personal
        | _, _ -> [||]
   
    let tableSource = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
            let room = rooms().[indexPath.Row]
            let cell = 
                match tableView.DequeueReusableCell "room-cell" with
                | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "room-cell")
                | c -> c

            cell.Tag <- indexPath.Row
            cell.TextLabel.Text <- room.Name
            cell.DetailTextLabel.Text <- room.Description
            cell.DetailTextLabel.TextColor <- UIColor.Gray
            Image.loadImageForCell (App.imageUrl room.Image 100) Image.placeholderChat cell tableView
            cell

        override this.RowsInSection(tableView, section) =
            rooms().Length

        override this.RowSelected(tableView, indexPath) =
            tableView.DeselectRow (indexPath, false)
            let room = rooms().[indexPath.Row]
            let roomController = ChatRooms.join { Id=room.Id; Slug=room.Slug; Label=room.Name; Image=room.Image }
            controller.NavigationController.PushViewController(roomController, true)
    }

    let refresh (tableController:UITableViewController) =
        Async.startNetworkWithContinuation
            (Chat.directory ())
            (function
                | Api.ApiOk d ->
                    directory <- Some d
                    tableController.TableView.ReloadData ()
                    tableController.RefreshControl.EndRefreshing()
                | error -> controller.HandleApiFailure error
            )

    [<Outlet>]
    member val ContentView: UIView = null with get, set

    [<Outlet>]
    member val RoomFilter: UISegmentedControl = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        let tableController = new UITableViewController()
        this.AddChildViewController tableController
        this.ContentView.AddSubview tableController.View
        this.RoomFilter.ValueChanged.Add(fun args -> tableController.TableView.ReloadData () )

        tableController.View.Frame <- new RectangleF(0.f, 0.f, this.ContentView.Frame.Width, this.ContentView.Frame.Height)
        tableController.TableView.Source <- tableSource
        tableController.RefreshControl <- new UIRefreshControl()
        tableController.RefreshControl.ValueChanged.Add(fun _ -> refresh tableController)
        refresh tableController

[<Register("FriendsViewController")>]
type FriendsViewController () as controller =
    inherit UIViewController ()
     
    let mutable (friends:PrivateChatFriend[]) = [| |]
    let mutable loaded = false

    let tableSource = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
            match friends with
            | [||] when loaded -> 
                let cell = 
                    match tableView.DequeueReusableCell "no-friend-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "no-friends-cell")
                    | c -> c

                let label = new UILabel(tableView.Frame)
                label.TextAlignment <- UITextAlignment.Center
                label.Text <- "No friends yet."
                cell.ContentView.AddSubview(label)
                cell.SelectionStyle <- UITableViewCellSelectionStyle.None
                cell
            | _ -> 
                let friend = friends.[indexPath.Row]
                let cell = 
                    match tableView.DequeueReusableCell "friend-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "friend-cell")
                    | c -> c
                
                cell.Tag <- indexPath.Row
                cell.TextLabel.Text <- friend.Name
                match friend.State with
                | OnlineStateType.Idle -> 
                    cell.DetailTextLabel.Text <- "Away"
                    cell.DetailTextLabel.TextColor <- UIColor.DarkTextColor
                | OnlineStateType.Visible ->
                    cell.DetailTextLabel.Text <- "Online"
                    cell.DetailTextLabel.TextColor <- UIColor.DarkTextColor
                | _ -> 
                    cell.DetailTextLabel.Text <- "Offline"
                    cell.DetailTextLabel.TextColor <- UIColor.Gray

                Image.loadImageForCell (App.imageUrl friend.Avatar 100) Image.placeholder cell tableView

                cell

        override this.RowsInSection(tableView, section) =
            match friends with
            | [||] when loaded -> 1
            | _ -> friends.Length

        override this.GetHeightForRow(tableView, index) =
            match friends with
            | [||] when loaded -> tableView.Frame.Height
            | _ -> tableView.RowHeight
    
        override this.RowSelected(tableView, indexPath) =
            match friends with
            | [||] when loaded -> ()
            | _ ->
                tableView.DeselectRow (indexPath, false)
                let friend = friends.[indexPath.Row]
                let newController = ChatSessions.start {
                        Id = friend.Id
                        Slug = friend.Slug
                        Label = friend.Name
                        Image = friend.Avatar
                    }
                controller.NavigationController.PushViewController(newController, true)
    }

    let refresh (tableController:UITableViewController) =
        Async.startNetworkWithContinuation
            (PrivateChat.online())
            (function
                | Api.ApiOk response ->
                    friends <- response.Friends.OrderBy(fun e -> e.State).ToArray()
                    loaded <- true
                    tableController.TableView.ReloadData ()
                    tableController.RefreshControl.EndRefreshing()
                | error -> controller.HandleApiFailure error
            )

    [<Outlet>]
    member val ContentView: UIView = null with get, set

    [<Outlet>]
    member val OnlineState: UISegmentedControl = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        let tableController = new UITableViewController()
        this.AddChildViewController tableController
        this.ContentView.AddSubview tableController.View
        this.OnlineState.ValueChanged.Add(fun _ -> 
            let state =
                match this.OnlineState.SelectedSegment with
                | 1 -> OnlineStateType.Idle
                | 2 -> OnlineStateType.Invisible
                | _ -> OnlineStateType.Visible

            Async.startNetworkWithContinuation
                (PrivateChat.changeOnlineState state)
                (function
                    | Api.ApiOk _ -> ()
                    | error -> this.HandleApiFailure error
                )
        )

        tableController.View.Frame <- new RectangleF(0.f, 0.f, this.ContentView.Frame.Width, this.ContentView.Frame.Height)
        tableController.TableView.Source <- tableSource
        tableController.RefreshControl <- new UIRefreshControl()
        tableController.RefreshControl.ValueChanged.Add(fun _ -> refresh tableController)
        refresh tableController


        



