namespace Wireclub.iOS

open System
open System.Linq
open System.Drawing
open System.Globalization
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
                //cell.DetailTextLabel.Text <- room
                Image.loadImageForCell (App.imageUrl friend.Avatar 100) Image.placeholder cell tableView

                cell

        override this.RowsInSection(tableView, section) =
            match friends with
            | [||] when loaded -> 1
            | _ -> friends.Length

        override this.GetHeightForRow(tableView, index) =
            match friends with
            | [||] when loaded -> controller.Table.Frame.Height
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

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- tableSource
        Async.startNetworkWithContinuation
            (PrivateChat.online())
            (function
                | Api.ApiOk response ->
                    friends <- response.Friends
                    loaded <- true
                    controller.Table.ReloadData ()

                | error -> controller.HandleApiFailure error
            )

        base.ViewDidLoad ()

[<Register ("SettingsMenuViewController")>]
type SettingsMenuViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
            

[<Register ("MegaMenuViewController")>]
type MegaMenuViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

[<Register ("HomeViewController")>]
type HomeViewController () as controller =
    inherit UIViewController ()

    let controllers =
        lazy
            ([|
                new ChatsViewController () :> UIViewController
                new FriendsViewController () :> UIViewController
                new ChatDirectoryViewController () :> UIViewController
                Resources.menuStoryboard.Value.InstantiateInitialViewController() :?> UIViewController
            |])

    let changeTab index =
        for controller in controllers.Value do
            controller.View.Hidden <- true

        controllers.Value.[index].View.Hidden <- false
        controller.NavigationItem.Title <-
            match index with
            | 3 -> "More"
            | item -> controller.TabBar.Items.[index].Title 


    let tabBarDelegate =
        { 
            new UITabBarDelegate() with
            override this.ItemSelected (bar, item) = changeTab (item.Tag)
        }

    
    [<Outlet>]
    member val TabBar: UITabBar = null with get, set

    [<Outlet>]
    member val ContentView: UIView = null with get, set

    member val ChatsController = controllers.Value.[0] :?> ChatsViewController

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
        printfn "[Home:Load]" 
        this.NavigationItem.HidesBackButton <- true
            
        this.AutomaticallyAdjustsScrollViewInsets <- false

        // Set up the child controllers
        let frame = System.Drawing.RectangleF(0.0f, 0.0f, this.ContentView.Bounds.Width, this.ContentView.Bounds.Height)       
        for controller in controllers.Value do
            this.AddChildViewController controller
            this.ContentView.AddSubview controller.View
            controller.View.Frame <- frame

        
        this.TabBar.Delegate <- tabBarDelegate
        this.TabBar.SelectedItem <- this.TabBar.Items.First()
        changeTab 0

[<Register ("EntryViewController")>]
type EntryViewController () as controller =
    inherit UIViewController ()

    let rootController = new HomeViewController()
    let loginController = lazy (Resources.loginStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)
    let editProfileController = lazy (Resources.editProfileStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)
    
    let updateNavigationNotification = ()

    let handleEvent channel (event:ChannelEvent.ChannelEvent) =
        controller.InvokeOnMainThread (fun _ -> 
            match event with
            //Private chat event
            | { Event = PrivateMessage (color, font, message) }
            | { Event = PrivateMessageSent (color, font, message) } ->
                let handlePrivateMessageEvent (user:Entity) =
                    let read =
                        match controller.NavigationController.VisibleViewController with
                        | :? PrivateChatSessionViewController as controller when controller.User.Id = user.Id -> true
                        | _ -> false
                    let message = String.stripHtml message

                    Async.startWithContinuation
                        (async {
                            do! DB.createChatHistory user DB.ChatHistoryType.PrivateChat (Some (message, read))
                            do! DB.createChatHistoryEvent user DB.ChatHistoryType.PrivateChat (JsonConvert.SerializeObject(event))
                         })
                        (fun _ -> rootController.ChatsController.Reload ())

                match ChatSessions.sessions.TryGetValue event.User with
                | true, (user, controller) ->
                    controller.HandleChannelEvent event
                    handlePrivateMessageEvent user
                | _ -> ChatSessions.startById event.User (fun user controller -> 
                    controller.HandleChannelEvent event
                    handlePrivateMessageEvent user
                )
            | _ ->
                match ChatRooms.rooms.TryGetValue channel with
                | true, (_, controller) ->
                    controller.HandleChannelEvent event
                | _ -> ChatRooms.joinById channel
        )

    let resolvableHosts =
        [
            "www.wireclub.com"
            "dev.wireclub.com"
            (new Uri(Api.baseUrl)).Host
        ]

    let openExternal (uri:Uri) = 
        let supportsChrome = UIApplication.SharedApplication.CanOpenUrl (NSUrl.FromString "googlechrome://")
        let openExternal (builder:UriBuilder) =
            let uri = builder.Uri
            UIApplication.SharedApplication.OpenUrl (new NSUrl (uri.GetComponents (UriComponents.HttpRequestUrl, UriFormat.UriEscaped))) |> ignore

        match uri.Scheme with
        | "http" when supportsChrome -> openExternal (new UriBuilder(uri, Scheme = "googlechrome"))
        | "https" when supportsChrome -> openExternal (new UriBuilder(uri, Scheme = "googlechromes"))
        | _ -> openExternal (new UriBuilder(uri))

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        let objOrFail = function | Some o -> o | _ -> failwith "Expected Some"
        let navigate url (data:Entity option) =
            let uri = new Uri(url)

            printfn "[Navigate] %s" (uri.ToString())
            match url, data with
            | Routes.User id, data -> 
                let pushUser user =
                    let controller = Resources.userStoryboard.Value.InstantiateInitialViewController () :?> UITabBarController
                    (controller.ChildViewControllers.[0] :?> UserViewController).User <- Some user
                    this.NavigationController.PushViewController (controller, true)

                match data with
                | Some data -> pushUser data
                | None ->
                     Async.startNetworkWithContinuation
                        (User.entityBySlug id)
                        (this.HandleApiResult >> function
                            | Api.ApiOk data -> pushUser data
                            | error -> this.HandleApiFailure error
                        )

            | Routes.ChatRoom id, data ->
                let pushRoom room =
                    this.NavigationController.PopToViewController (rootController, false) |> ignore
                    let controller = ChatRooms.join room
                    this.NavigationController.PushViewController (controller, true)

                match data with
                | Some data -> pushRoom data
                | None ->
                     Async.startNetworkWithContinuation
                        (Chat.entityBySlug id)
                        (this.HandleApiResult >> function
                            | Api.ApiOk data -> pushRoom data
                            | error -> this.HandleApiFailure error
                        )

            | Routes.ChatSession id, Some data ->
                this.NavigationController.PopToViewController (rootController, false) |> ignore // Straight yolo
                let controller = ChatSessions.start data
                this.NavigationController.PushViewController (controller, true)

            | Routes.YouTube video, _ ->
                new Uri (sprintf "https://www.youtube.com/watch?v=%s" video) |> openExternal
            | Routes.ExternalRedirect _, _ -> uri |> openExternal
            | "/home", _ ->
                if this.NavigationController.ViewControllers.Contains rootController then
                    this.NavigationController.PopToViewController (rootController, true) |> ignore
                else
                    this.NavigationController.PopToViewController (this, false) |> ignore
                    this.NavigationController.PushViewController(rootController, true)
            | url, _ -> 
                this.NavigationController.PushViewController (new DialogViewController (url), true)

        Navigation.navigate <- navigate

        printfn "[Entry:Load]"

    override this.ViewDidAppear (animated) =
        // When the user is authenticated start the channel client and push the main app controller
        let proceed animated =
            ChannelClient.init handleEvent

            match Api.userIdentity.Value.Membership with
            | MembershipTypePublic.Pending -> this.NavigationController.PushViewController (editProfileController.Value, true)
            | _ -> this.NavigationController.PushViewController(rootController, animated)

        let defaults = NSUserDefaults.StandardUserDefaults
        match defaults.StringForKey "auth-token", System.String.IsNullOrEmpty Api.userId with
        // User has not entered an account
        | null, _ -> 
            this.NavigationController.PushViewController (loginController.Value, false)

        // User has an account but has not authenticated with the api
        | token, true -> 
            Async.startNetworkWithContinuation
                (Account.loginToken token)
                (function
                    | Api.ApiOk identity -> proceed true
                    | _ -> this.NavigationController.PushViewController (loginController.Value, true)
                )

        // User is fully authenticated already
        | _, false -> proceed false
            
       