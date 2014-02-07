namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat

[<Register ("LoginViewController")>]
type LoginViewController () =
    inherit UIViewController ()

    [<Outlet>]
    member val Email: UITextField = null with get, set
    [<Outlet>]
    member val Password: UITextField = null with get, set
    //[<Outlet>]
    //member val ForgotPasswordButton: UIButton = null with get, set
    [<Outlet>]
    member val LoginButton: UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        this.LoginButton.TouchUpInside.Add(fun _ ->

            // ## Disable UI
            // ## Progress
            Async.StartImmediate <| async {
                let! result = Account.login this.Email.Text this.Password.Text
                match result with
                | Api.ApiOk result ->
                    NSUserDefaults.StandardUserDefaults.SetString (this.Email.Text, "email")
                    NSUserDefaults.StandardUserDefaults.SetString (this.Password.Text, "password")
                    NSUserDefaults.StandardUserDefaults.Synchronize () |> ignore
                    this.DismissViewController(true, null)
                | _ -> ()
            }
        )


[<Register("FriendsViewController")>]
type FriendsViewController () =
    inherit UIViewController ()
     
    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        Async.StartImmediate <| async {
            let! friends = PrivateChat.online()
            match friends with
            | Api.ApiOk friends ->
                controller.Table.Source <- 
                    { new UITableViewSource() with
                        override this.GetCell(tableView, indexPath) =
                            let cell = 
                                match tableView.DequeueReusableCell "friend-cell" with
                                | null -> new UITableViewCell()
                                | c -> c
                            
                            cell.TextLabel.Text <- friends.Friends.[indexPath.Row].Name
                            cell

                        override this.RowsInSection(tableView, section) =
                            friends.Friends.Length

                        override this.RowSelected(tableView, indexPath) =
                            controller.NavigationController.PushViewController(new PrivateChatSessionViewController(friends.Friends.[indexPath.Row]), true)
                            
                    }

                controller.Table.ReloadData ()

            | er -> printfn "Api Error: %A" er
        }

        base.ViewDidLoad ()


[<Register ("ChatDirectoryViewController")>]
type ChatDirectoryViewController() =
    inherit UIViewController ()

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        Async.StartImmediate <| async {
            let! directory = Chat.directory ()
            match directory with
            | Api.ApiOk directory ->
                controller.Table.Source <- 
                    { new UITableViewSource() with
                        override this.GetCell(tableView, indexPath) =
                            let cell = 
                                match tableView.DequeueReusableCell "room-cell" with
                                | null -> new UITableViewCell()
                                | c -> c
                            
                            cell.TextLabel.Text <- directory.Official.[indexPath.Row].Name
                            cell

                        override this.RowsInSection(tableView, section) =
                            directory.Official.Length

                        override this.RowSelected(tableView, indexPath) =
                            controller.NavigationController.PushViewController(new ChatRoomViewController(directory.Official .[indexPath.Row]), true)
                            ()
                            
                    }

                controller.Table.ReloadData ()

            | er -> printfn "Api Error: %A" er
        }

        base.ViewDidLoad ()


[<Register ("HomeViewController")>]
type HomeViewController () =
    inherit UIViewController ()

    let friendsController = new FriendsViewController()
    let chatController = new ChatDirectoryViewController()
    
    [<Outlet>]
    member val Filter: UISegmentedControl = null with get, set

    [<Outlet>]
    member val ContentView: UIView = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
        this.AddChildViewController friendsController
        this.AddChildViewController chatController
        this.ContentView.AddSubview friendsController.View
        this.ContentView.AddSubview chatController.View
        let frame = System.Drawing.RectangleF(0.0f, 0.0f, this.ContentView.Bounds.Width, this.ContentView.Bounds.Height)            
        chatController.View.Frame <- frame
        friendsController.View.Frame <- frame
        chatController.View.Hidden <- true

        this.Filter.ValueChanged.Add (fun _ ->
            match this.Filter.SelectedSegment with
            | 0 ->
                friendsController.View.Hidden <- false
                chatController.View.Hidden <- true
            | _ ->
                friendsController.View.Hidden <- true
                chatController.View.Hidden <- false
        )

[<Register ("EntryViewController")>]
type EntryViewController () =
    inherit UIViewController ()

    let navigationController = new UINavigationController()
    //let rootController = new FriendsViewController()
    let rootController = new HomeViewController()

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

    override this.ViewDidAppear (animated) =
        // When the user is authenticated start the channel client and push the main app controller
        let proceed () =
            ChannelClient.init ()
            navigationController.PushViewController(rootController, false)
            this.PresentViewController (navigationController, true, null)

        let defaults = NSUserDefaults.StandardUserDefaults

        match defaults.StringForKey "email", defaults.StringForKey "password", System.String.IsNullOrEmpty Api.userId with
        // User has not entered an account
        | null, null, _ -> 
            this.PresentViewController (new LoginViewController(), false, null)
        // User has an account but has not authenticated with the api
        | email, password, true -> 
            Async.StartImmediate <| async {
                let! loginResult = Account.login email password
                match loginResult with
                | Api.ApiOk identity -> proceed ()
                | _ -> this.PresentViewController (new LoginViewController(), true, null)
            }
        // User is fully authenticated already
        | _, _, false -> proceed ()
            
       