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
            Async.StartImmediate <| async {
                
                // ## Send / Toast ...
                this.DismissViewController (true, null)
            }
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
            Async.StartImmediate <| async {
                let! result = Account.signup this.Email.Text this.Password.Text
                //result |> this.
                ()
            }
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
            Async.StartImmediate <| async {
                let! result = Account.login this.Email.Text this.Password.Text
                match result with
                | Api.ApiOk result ->
                    NSUserDefaults.StandardUserDefaults.SetString (result.Token, "auth-token")
                    NSUserDefaults.StandardUserDefaults.Synchronize () |> ignore
                    this.NavigationController.PopViewControllerAnimated true |> ignore
                | error -> this.HandleApiFailure error
            }
        )

        this.ForgotPasswordButton.TouchUpInside.Add(fun _ ->
            this.NavigationController.PushViewController (forgotPasswordStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
        )

        this.CreateAccountButton.TouchUpInside.Add(fun _ ->
            this.NavigationController.PushViewController (signupStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
        )


[<Register("FriendsViewController")>]
type FriendsViewController () as controller =
    inherit UIViewController ()
     
    let mutable (friends:PrivateChatFriend[]) = [| |]
    let placeholder = UIImage.FromFile "Images/Placeholder.png"

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
            Image.loadImageForCell (App.imageUrl friend.Avatar 100) placeholder cell tableView

            cell

        override this.RowsInSection(tableView, section) =
            friends.Length

        override this.RowSelected(tableView, indexPath) =
            tableView.DeselectRow (indexPath, false)
            let newController = ChatSessions.start friends.[indexPath.Row]
            controller.NavigationController.PushViewController(newController, true)
    }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- tableSource
        Async.StartImmediate <| async {
            let! response = PrivateChat.online()
            match response with
            | Api.ApiOk response ->
                friends <- response.Friends
                controller.Table.ReloadData ()

            | er -> printfn "Api Error: %A" er
        }

        base.ViewDidLoad ()

[<Register ("MegaMenuViewController")>]
type MegaMenuViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    override this.ViewDidLoad () =
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
        printfn "[Home:Load]" 
        this.NavigationItem.HidesBackButton <- true
        this.NavigationItem.Title <- "Wireclub"

        this.NavigationItem.LeftBarButtonItem <- new UIBarButtonItem("...", UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> 
            this.NavigationController.PushViewController (Resources.menuStoryboard.Value.InstantiateInitialViewController() :?> UIViewController, true)
        ))

        // Set up the child controllers
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

    let rootController = new HomeViewController()

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        let navigate url (data:obj) =
            match url with
            | Routes.User id -> 
                let controller = (Resources.userStoryboard.Value.InstantiateInitialViewController () :?> UITabBarController).ChildViewControllers.[0] :?> UserViewController
                controller.User <- Some (data :?> Wireclub.Boundary.Chat.PrivateChatFriend)
                this.NavigationController.PushViewController (controller, true)

            | Routes.ChatSession id ->             
                this.NavigationController.PopToViewController (rootController, false) |> ignore // Straight yolo
                let controller = ChatSessions.start (data :?> Wireclub.Boundary.Chat.PrivateChatFriend)
                this.NavigationController.PushViewController (controller, true)

            | Routes.ChatRoom id ->
                this.NavigationController.PopToViewController (rootController, false) |> ignore
                let controller = ChatRooms.join (data :?> Wireclub.Boundary.Chat.ChatDirectoryRoomViewModel)
                this.NavigationController.PushViewController (controller, true)

            | url -> 
                this.NavigationController.PushViewController (new DialogViewController (url), true)

        Navigation.navigate <- navigate


        printfn "[Entry:Load]"

    override this.ViewDidAppear (animated) =
        // When the user is authenticated start the channel client and push the main app controller
        let proceed animated =
            ChannelClient.init ()
            this.NavigationController.PushViewController(rootController, animated)

        let defaults = NSUserDefaults.StandardUserDefaults
        match defaults.StringForKey "auth-token", System.String.IsNullOrEmpty Api.userId with
        // User has not entered an account
        | null, _ -> 
            this.NavigationController.PushViewController (Resources.loginStoryboard.Value.InstantiateInitialViewController() :?> UIViewController, false)

        // User has an account but has not authenticated with the api
        | token, true -> 
            Async.StartImmediate <| async {
                let! loginResult = Account.loginToken token
                match loginResult with
                | Api.ApiOk identity -> proceed true
                | _ -> 
                    this.NavigationController.PushViewController (Resources.loginStoryboard.Value.InstantiateInitialViewController() :?> UIViewController, true)
            }
        // User is fully authenticated already
        | _, false -> proceed false
            
       