namespace Wireclub.iOS

open System
open System.Linq
open System.Drawing
open System.Globalization
open MonoTouch.Foundation
open MonoTouch.UIKit
open MonoTouch.CoreLocation
open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Models
open Wireclub.Boundary.Chat

open Xamarin.Media 

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
            


[<Register ("AsyncPickerViewController")>]
type AsyncPickerViewController () =
    inherit UIViewController ("AsyncPickerViewController", null)

    [<Outlet>]
    member val Picker: UIPickerView = null with get, set

    [<Outlet>]
    member val Progress:UIActivityIndicatorView  = null with get, set

[<Register ("NavigateInputAccessoryViewController")>]
type NavigateInputAccessoryViewController (next, prev, ``done``) =
    inherit UIViewController ("NavigateInputAccessoryViewController", null)

    [<Outlet>]
    member val NextButton: UIButton = null with get, set

    [<Outlet>]
    member val PrevButton: UIButton = null with get, set

    [<Outlet>]
    member val DoneButton: UIButton = null with get, set

    override this.ViewDidLoad () = 
        this.NextButton.TouchUpInside.Add (next)
        this.PrevButton.TouchUpInside.Add (prev)
        this.DoneButton.TouchUpInside.Add (``done``)

[<Register ("EditProfileViewController")>]
type EditProfileViewController (handle:nativeint) as controller =
    inherit UITableViewController (handle)

    let location = new CLLocationManager()

    let pickerMedia = new MediaPicker()
    let pickerDate = new UIDatePicker(new RectangleF(0.0f,0.0f,320.0f,216.0f))
    let accessoryBirthday =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.Country.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Username.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Birthday.ResignFirstResponder() |> ignore)
        )

    let pickerCountry = new AsyncPickerViewController() //new RectangleF(0.0f,0.0f,320.0f,216.0f)
    let accessoryCountry =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.Region.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Birthday.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Country.ResignFirstResponder() |> ignore)
        )

    let pickerRegion = new AsyncPickerViewController()
    let accessoryRegion =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.City.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Country.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Region.ResignFirstResponder() |> ignore)
        )

    let mutable country:LocationCountry option = None
    let mutable countries:LocationCountry [] = [||]

    let mutable region:LocationRegion option = None
    let mutable regions:LocationRegion [] = [||]

    [<Outlet>]
    member val Birthday:UITextField = null with get, set

    [<Outlet>]
    member val About:UITextView = null with get, set

    [<Outlet>]
    member val GenderSelect:UISegmentedControl = null with get, set

    [<Outlet>]
    member val Location:UITextField   = null with get, set

    [<Outlet>]
    member val ProfileImage:UIImageView  = null with get, set

    [<Outlet>]
    member val SaveButton: UIButton = null with get, set

    [<Outlet>]
    member val Username:UITextField  = null with get, set

    [<Outlet>]
    member val Country:UITextField  = null with get, set

    [<Outlet>]
    member val Region:UITextField  = null with get, set

    [<Outlet>]
    member val City:UITextField  = null with get, set

    [<Outlet>]
    member val ProfileImageProgress:UIActivityIndicatorView  = null with get, set



    override this.ViewDidLoad () =
        this.NavigationItem.Title <- "Create Profile"
        this.NavigationItem.HidesBackButton <- true


        // Birthday picker
        pickerDate.MinimumDate <-  NSDate.op_Implicit (DateTime.UtcNow.AddYears(-120))
        pickerDate.MaximumDate <- NSDate.op_Implicit (DateTime.UtcNow.AddYears(-13))
        pickerDate.Date <- NSDate.op_Implicit (DateTime.UtcNow.AddYears(-100))
        pickerDate.Mode <- UIDatePickerMode.Date
        pickerDate.ValueChanged.Add(fun _ ->
            let value = NSDate.op_Implicit pickerDate.Date
            this.Birthday.Text <- value.ToString("M/d/yyyy")
        )
        this.Birthday.InputView <- pickerDate
        this.Birthday.InputAccessoryView <- accessoryBirthday.View

        // Country Picker
        this.Country.InputView <- pickerCountry.View
        this.Country.InputAccessoryView <- accessoryCountry.View
        pickerCountry.Picker.Source <- { 
            new UIPickerViewModel() with
            override this.GetRowsInComponent(pickerView, comp) = countries.Length
            override this.GetComponentCount(pickerView) = 1
            override this.GetTitle(pickerView, row, comp) = countries.[row].Name
            override this.Selected(pickerView, row, comp) =
                country <- Some countries.[row]
                controller.Country.Text <- country.Value.Name
        }
        this.Country.EditingDidBegin.Add(fun _ ->
            Async.startWithContinuation
                (Settings.countries ())
                (function
                    | Api.ApiOk result -> 
                        countries <- result
                        regions <- [||]
                        pickerCountry.Progress.StopAnimating()
                        pickerCountry.Picker.ReloadAllComponents()
                    | error ->
                        pickerCountry.Progress.StopAnimating()
                        this.HandleApiFailure error
                )         
        )

        // Region Picker
        this.Region.InputView <- pickerRegion.View
        this.Region.InputAccessoryView <- accessoryRegion.View
        pickerRegion.Picker.Source <- { 
            new UIPickerViewModel() with
            override this.GetRowsInComponent(pickerView, comp) = regions.Length
            override this.GetComponentCount(pickerView) = 1
            override this.GetTitle(pickerView, row, comp) = regions.[row].Name
            override this.Selected(pickerView, row, comp) =
                region <- Some regions.[row]
                controller.Region.Text <- region.Value.Name
        }
        this.Region.EditingDidBegin.Add(fun _ ->
            match country with
            | Some country ->
                pickerRegion.Progress.StartAnimating()
                Async.startWithContinuation
                    (Settings.regions country.Id)
                    (function
                        | Api.ApiOk rs -> 
                            regions <- rs
                            pickerRegion.Progress.StopAnimating()
                            pickerRegion.Picker.ReloadAllComponents()
                        | error ->
                            pickerRegion.Progress.StopAnimating()
                            this.HandleApiFailure error
                    )
            | None -> showSimpleAlert "Error" "Please pick a country" "Close" // Can't resign first responder at this point causes horrible exception
        )

        // Next input
        for input, next in List.nextTuple [ this.Username; this.Birthday; this.Country; this.Region; this.City ] do
            input.ReturnKeyType <- UIReturnKeyType.Next
            input.EditingDidEndOnExit.Add (fun _ -> next.BecomeFirstResponder() |> ignore)

        this.City.EditingDidEndOnExit.Add (fun _ -> this.About.BecomeFirstResponder() |> ignore)
        this.City.ReturnKeyType <- UIReturnKeyType.Next

        // Profile image
        match Api.userIdentity with
        | Some identity -> Image.loadImageForView (App.imageUrl identity.Avatar 100) Image.placeholder this.ProfileImage
        | None -> ()

        this.SaveButton.TouchUpInside.Add(fun _ ->
            Async.startWithContinuation
                (Settings.profile
                    (this.Username.Text.Trim())
                    (match this.GenderSelect.SelectedSegment with | 1 -> GenderType.Male | 2 -> GenderType.Female | _ -> GenderType.Undefined)
                    (DateTime.ParseExact(this.Birthday.Text, "M/d/yyyy", CultureInfo.CurrentCulture))
                    (match country with | Some country -> country.Id | None _ -> String.Empty)
                    (match region with | Some region -> region.Name | None _ -> String.Empty)
                    (this.City.Text.Trim())
                    this.About.Text
                    )
                (function
                    | Api.ApiOk identity -> 
                        Api.userIdentity <- Some identity
                        Navigation.navigate "/home" None
                    | error -> this.HandleApiFailure error
                )
        )

    override this.RowSelected (tableView, indexPath) =
        tableView.DeselectRow (indexPath, false)
        match indexPath.Section, indexPath.Row with
        | 0, 0 -> 
            let alert = new UIAlertView (Title="Send Friend Request?", Message="")
            alert.AddButton "Choose Existing" |> ignore
            alert.AddButton "Take Photo" |> ignore
            alert.AddButton "Cancel" |> ignore
            alert.Show ()
            alert.Dismissed.Add(fun args ->
                let updateAvatar (controller:MediaPickerController) = 
                    this.PresentViewController (controller, true, null)
                    Async.StartWithContinuations (
                        (Async.AwaitTask (controller.GetResultAsync())),
                        (fun result ->
                            this.ProfileImageProgress.StartAnimating()
                            controller.DismissViewController (true, fun _ -> 
                                let imageOriginal = UIImage.FromFile result.Path
                                let data = 
                                    match imageOriginal.Orientation with
                                    | UIImageOrientation.Up | UIImageOrientation.UpMirrored -> imageOriginal.AsJPEG()
                                    | _ ->
                                        //redraw the raw image without the orientation
                                        UIGraphics.BeginImageContext(imageOriginal.Size)
                                        imageOriginal.Draw(new RectangleF(0.0f, 0.0f, imageOriginal.Size.Width, imageOriginal.Size.Height))
                                        let data = UIGraphics.GetImageFromCurrentImageContext().AsJPEG()
                                        UIGraphics.EndImageContext()
                                        data

                                let dataBuffer = Array.zeroCreate (int data.Length)
                                System.Runtime.InteropServices.Marshal.Copy(data.Bytes, (dataBuffer:byte []), 0, int data.Length)
                                Async.startWithContinuation
                                    (Settings.avatar dataBuffer)
                                    (function 
                                        | Api.ApiOk image ->
                                            match Api.userIdentity with
                                            | Some identity ->
                                                Api.userIdentity <- Some { identity with Avatar = image.Token }
                                                Image.loadImageForView (App.imageUrl image.Token 100) this.ProfileImage.Image this.ProfileImage
                                                this.ProfileImageProgress.StopAnimating()
                                            | None -> printfn "identity not set"
                                        | error ->
                                            this.ProfileImageProgress.StopAnimating()
                                            this.HandleApiFailure error
                                    )
                            )
                        ),
                        (fun ex -> controller.DismissViewController (true, fun _ -> showSimpleAlert "Error" ex.Message "Close")),
                        (fun exCancel -> controller.DismissViewController (true, fun _ -> ()))
                    )

                match args.ButtonIndex with
                | 0 -> pickerMedia.GetPickPhotoUI() |> updateAvatar
                | 1 -> pickerMedia.GetTakePhotoUI (new StoreCameraMediaOptions(Name = sprintf "%s.jpg" (System.IO.Path.GetTempFileName()), Directory = "Wireclub")) |> updateAvatar
                | _ -> ()
            )
        | _, _ -> ()


[<Register ("SignupViewController")>]
type SignupViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    let editProfileStoryboard = UIStoryboard.FromName ("EditProfile", null)

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
                (function
                    | Api.ApiOk result ->
                        NSUserDefaults.StandardUserDefaults.SetString (result.Token, "auth-token")
                        NSUserDefaults.StandardUserDefaults.Synchronize () |> ignore
                        this.NavigationController.PushViewController (editProfileStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
                    | error -> this.HandleApiFailure error
                )
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

    let changeFilter index =
        chatsController.View.Hidden <- true
        friendsController.View.Hidden <- true
        directoryController.View.Hidden <- true
        match index with
        | 0 -> chatsController.View.Hidden <- false
        | 1 -> friendsController.View.Hidden <- false
        | _ -> directoryController.View.Hidden <- false

    let tabBarDelegate =
        {
            new UITabBarDelegate() with
            override this.ItemSelected (bar, item) = changeFilter (item.Tag)
        }

    
    [<Outlet>]
    member val TabBar: UITabBar = null with get, set

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
            
        this.AutomaticallyAdjustsScrollViewInsets <- false

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

        changeFilter 0
        
        this.TabBar.Delegate <- tabBarDelegate
        this.TabBar.SelectedItem <- this.TabBar.Items.First()

[<Register ("EntryViewController")>]
type EntryViewController () =
    inherit UIViewController ()

    let rootController = new HomeViewController()
    let loginController = lazy (Resources.loginStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)
    let editProfileController = lazy (Resources.editProfileStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)
    

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
            Async.startWithContinuation
                (Account.loginToken token)
                (function
                    | Api.ApiOk identity -> proceed true
                    | _ -> this.NavigationController.PushViewController (loginController.Value, true)
                )

        // User is fully authenticated already
        | _, false -> proceed false
            
       