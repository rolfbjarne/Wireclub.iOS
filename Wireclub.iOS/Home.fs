namespace Wireclub.iOS

open System
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat

[<Register ("ForgotPasswordViewController")>]
type ForgotPasswordViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    [<Outlet>]
    member val Email: UITextField = null with get, set
    [<Outlet>]
    member val SubmitButton: UIButton = null with get, set

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- "Forgot Password"
        this.NavigationItem.LeftBarButtonItem <- new UIBarButtonItem("Cancel", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            this.NavigationController.PopViewControllerAnimated (true) |> ignore
        ))
        this.SubmitButton.TouchUpInside.Add(fun _ ->
            // ## Send / Toast ...
            this.DismissViewController (true, null)
        )


[<Register ("SignupViewController")>]
type SignupViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    [<Outlet>]
    member val Email: UITextField = null with get, set
    [<Outlet>]
    member val Password: UITextField = null with get, set
    [<Outlet>]
    member val SignupButton: UIButton = null with get, set

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- "Join"
        this.NavigationItem.LeftBarButtonItem <- new UIBarButtonItem("Cancel", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            this.NavigationController.PopViewControllerAnimated (true) |> ignore
        ))

        this.SignupButton.TouchUpInside.Add(fun _ ->
            Async.startWithContinuation
                (Account.signup this.Email.Text this.Password.Text)
                (fun _ -> ())
        )

[<Register ("LoginViewController")>]
type LoginViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    let signupStoryboard = UIStoryboard.FromName ("Signup", null)
    let forgotPasswordStoryboard = UIStoryboard.FromName ("ForgotPassword", null)

    [<Outlet>]
    member val Email: UITextField = null with get, set
    [<Outlet>]
    member val Password: UITextField = null with get, set
    [<Outlet>]
    member val ForgotPasswordButton: UIButton = null with get, set
    [<Outlet>]
    member val CreateAccountButton: UIButton = null with get, set
    [<Outlet>]
    member val LoginButton: UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        //this.NavigationItem.LeftBarButtonItem.Title <- "Cancel"
        this.NavigationItem.Title <- "Login"
        this.NavigationItem.HidesBackButton <- true

        this.LoginButton.TouchUpInside.Add(fun _ ->
            
            // ## Disable UI
            // ## Progress
            Async.startWithContinuation
                (Account.login this.Email.Text this.Password.Text)
                (function
                    | Api.ApiOk result ->
                        NSUserDefaults.StandardUserDefaults.SetString (result.Token, "auth-token")
                        NSUserDefaults.StandardUserDefaults.Synchronize () |> ignore
                        this.NavigationController.PopViewControllerAnimated true |> ignore
                    | error -> this.HandleApiFailure error
                )
        )

        this.ForgotPasswordButton.TouchUpInside.Add(fun _ ->
            this.NavigationController.PushViewController (forgotPasswordStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
        )

        this.CreateAccountButton.TouchUpInside.Add(fun _ ->
            this.NavigationController.PushViewController (signupStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
        )

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

    let tableSource = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
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
            chats.Length

        override this.RowSelected(tableView, indexPath) =
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

    override controller.ViewDidLoad () =
        controller.Table.Source <- tableSource
        base.ViewDidLoad ()

    override this.ViewDidAppear (animated) =
        base.ViewDidAppear (animated)

        Async.startWithContinuation
            (async {
                return! DB.fetchChatHistory ()
            })
            (fun sessions ->
                chats <- sessions |> Seq.toArray
                this.Table.ReloadData()
            )

[<Register("FriendsViewController")>]
type FriendsViewController () as controller =
    inherit UIViewController ()
     
    let mutable (friends:PrivateChatFriend[]) = [| |]

    let tableSource = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
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
            friends.Length

        override this.RowSelected(tableView, indexPath) =
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
        Async.startWithContinuation
            (PrivateChat.online())
            (function
                | Api.ApiOk response ->
                    friends <- response.Friends
                    controller.Table.ReloadData ()

                | er -> printfn "Api Error: %A" er
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
type HomeViewController () =
    inherit UIViewController ()

    let chatsController = new ChatsViewController ()
    let friendsController = new FriendsViewController ()
    let directoryController = new ChatDirectoryViewController ()
    
    [<Outlet>]
    member val Filter: UISegmentedControl = null with get, set

    [<Outlet>]
    member val ContentView: UIView = null with get, set

    member val ChatsController = chatsController

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
        printfn "[Home:Load]" 
        this.NavigationItem.HidesBackButton <- true
        this.NavigationItem.Title <- "Wireclub"

        this.NavigationItem.LeftBarButtonItem <- new UIBarButtonItem("...", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            this.NavigationController.PushViewController (Resources.menuStoryboard.Value.InstantiateInitialViewController() :?> UIViewController, true)
        ))

        // Set up the child controllers
        this.AddChildViewController chatsController
        this.AddChildViewController friendsController
        this.AddChildViewController directoryController
        this.ContentView.AddSubview chatsController.View
        this.ContentView.AddSubview friendsController.View
        this.ContentView.AddSubview directoryController.View
        let frame = System.Drawing.RectangleF(0.0f, 0.0f, this.ContentView.Bounds.Width, this.ContentView.Bounds.Height)            
        chatsController.View.Frame <- frame
        friendsController.View.Frame <- frame
        directoryController.View.Frame <- frame

        let changeFilter index =
            chatsController.View.Hidden <- true
            friendsController.View.Hidden <- true
            directoryController.View.Hidden <- true
            match index with
            | 0 -> chatsController.View.Hidden <- false
            | 1 -> friendsController.View.Hidden <- false
            | _ -> directoryController.View.Hidden <- false

        changeFilter 0
        
        this.Filter.ValueChanged.Add (fun _ ->
            changeFilter (this.Filter.SelectedSegment)
        )


[<Register ("EntryViewController")>]
type EntryViewController () =
    inherit UIViewController ()

    let rootController = new HomeViewController()
    let loginController = lazy (Resources.loginStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)

    let handleEvent channel (event:ChannelEvent.ChannelEvent) =        
        if channel = Api.userId then
            match ChatSessions.sessions.TryGetValue event.User with
            | true, (_, controller) -> controller.HandleChannelEvent event
            | _ -> ChatSessions.startById event.User (fun controller -> 
                    controller.HandleChannelEvent event
                    if rootController.ChatsController.IsViewLoaded then
                        rootController.ChatsController.Table.ReloadData ()
                )
        else
            match ChatRooms.rooms.TryGetValue channel with
            | true, (_, controller) -> controller.HandleChannelEvent event
            | _ -> ChatRooms.joinById channel


    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        let objOrFail = function | Some o -> o | _ -> failwith "Expected Some"
            
        let navigate url (data:Entity option) =
            match url, data with
            | Routes.User id, _ -> 
                let controller = Resources.userStoryboard.Value.InstantiateInitialViewController () :?> UITabBarController
                (controller.ChildViewControllers.[0] :?> UserViewController).User <- data
                this.NavigationController.PushViewController (controller, true)

            | Routes.ChatSession id, Some data ->
                this.NavigationController.PopToViewController (rootController, false) |> ignore // Straight yolo
                let controller = ChatSessions.start data
                this.NavigationController.PushViewController (controller, true)

            | Routes.ChatRoom id, Some data ->
                this.NavigationController.PopToViewController (rootController, false) |> ignore
                let controller = ChatRooms.join data
                this.NavigationController.PushViewController (controller, true)

            | url, _ -> 
                this.NavigationController.PushViewController (new DialogViewController (url), true)

        Navigation.navigate <- navigate

        printfn "[Entry:Load]"

    override this.ViewDidAppear (animated) =
        // When the user is authenticated start the channel client and push the main app controller
        let proceed animated =
            ChannelClient.init handleEvent
            this.NavigationController.PushViewController(rootController, animated)

        let defaults = NSUserDefaults.StandardUserDefaults
        match defaults.StringForKey "auth-token", System.String.IsNullOrEmpty Api.userId with
        // User has not entered an account
        | null, _ -> 
            this.NavigationController.PushViewController (loginController.Value, false)

        // User has an account but has not authenticated with the api
        | token, true -> 
            Async.startWithContinuation
                (Account.loginToken token)
                (function
                    | Api.ApiOk identity -> proceed true
                    | _ -> this.NavigationController.PushViewController (loginController.Value, true)
                )

        // User is fully authenticated already
        | _, false -> proceed false
            
       