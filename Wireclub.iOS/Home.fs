namespace Wireclub.iOS

open System
open System.Linq
open System.Drawing
open MonoTouch.Foundation
open MonoTouch.UIKit
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

[<Register("CountryPickerViewController")>]
type CountryPickerViewController (itemSelected) =
    inherit UIViewController ("CountryPickerViewController", null)

    let mutable countries:LocationCountry [] = [||]
    let source = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
            let country = countries.[indexPath.Row]
            let cell = 
                match tableView.DequeueReusableCell "country-cell" with
                | null -> new UITableViewCell (UITableViewCellStyle.Default, "country-cell")
                | c -> c

            cell.Tag <- indexPath.Row
            cell.TextLabel.Text <- country.Name
            cell

        override this.RowsInSection(tableView, section) = countries.Length

        override this.RowSelected(tableView, indexPath) =
            tableView.DeselectRow (indexPath, false)
            itemSelected countries.[indexPath.Row]
    }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override this.ViewDidLoad () =
        this.Table.Source <- source

        Async.startWithContinuation
            (Places.countries ())
            (function
                | Api.ApiOk cs -> 
                    countries <- cs
                    this.Table.ReloadData()
                | error -> this.HandleApiFailure error
            )

[<Register("RegionPickerViewController")>]
type RegionPickerViewController (country:LocationCountry, itemSelected) as controller =
    inherit UIViewController ("RegionPickerViewController", null)

    let mutable regions:LocationRegion [] = [||]
    let source = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
            let region = regions.[indexPath.Row]
            let cell = 
                match tableView.DequeueReusableCell "region-cell" with
                | null -> new UITableViewCell (UITableViewCellStyle.Default, "region-cell")
                | c -> c

            cell.Tag <- indexPath.Row
            cell.TextLabel.Text <- region.Name
            cell

        override this.RowsInSection(tableView, section) = regions.Length

        override this.RowSelected(tableView, indexPath) =
            tableView.DeselectRow (indexPath, false)
            itemSelected regions.[indexPath.Row]
    }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override this.ViewDidLoad () =
        controller.Table.Source <- source

        Async.startWithContinuation
            (Places.regions country.Id)
            (function
                | Api.ApiOk rs -> 
                    regions <- rs
                    this.Table.ReloadData()
                | error -> this.HandleApiFailure error
            )

[<Register ("EditProfileViewController")>]
type EditProfileViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    let pickerMedia = new MediaPicker()
    let pickerDate = new UIDatePicker(new RectangleF(0.0f,0.0f,320.0f,216.0f))

    let mutable country:LocationCountry option = None
    let mutable region:LocationRegion option = None

    [<Outlet>]
    member val Birthday:UITextField = null with get, set

    [<Outlet>]
    member val Description:UITextField = null with get, set

    [<Outlet>]
    member val First:UITextField  = null with get, set

    [<Outlet>]
    member val GenderSelect:UISegmentedControl = null with get, set

    [<Outlet>]
    member val Last:UITextField  = null with get, set

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

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- "Create Profile"
        this.NavigationItem.HidesBackButton <- true
        pickerDate.MinimumDate <-  NSDate.op_Implicit (DateTime.UtcNow.AddYears(-120))
        pickerDate.MaximumDate <- NSDate.op_Implicit (DateTime.UtcNow.AddYears(-13))

        pickerDate.Mode <- UIDatePickerMode.Date
        this.Birthday.InputView <- pickerDate

        match Api.userIdentity with
        | Some identity -> Image.loadImageForView (App.imageUrl identity.Avatar 100) Image.placeholder this.ProfileImage
        | None -> ()


        pickerDate.ValueChanged.Add(fun _ ->
            let value = NSDate.op_Implicit pickerDate.Date
            this.Birthday.Text <- value.ToString("M/d/yyyy")
        )

        this.SaveButton.TouchUpInside.Add(fun _ ->
            Async.startWithContinuation
                (async { return Api.ApiOk 1 })
                (function
                    | Api.ApiOk result ->  showSimpleAlert "Hooray" "An awesome occured submitting your request" "Close"
                    | error -> this.HandleApiFailure error
                )
        )

    member this.ChooseProfileImage () =
        let alert = new UIAlertView (Title="Send Friend Request?", Message="")
        alert.AddButton "Choose Existing" |> ignore
        alert.AddButton "Take Photo" |> ignore
        alert.AddButton "Cancel" |> ignore
        alert.Show ()
        alert.Dismissed.Add(fun args ->
            let updateAvatar (controller:MediaPickerController) = 
                this.PresentViewController (controller, true, null)
                Async.startWithContinuation
                    (Async.AwaitTask (controller.GetResultAsync()))
                    (fun result ->
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
                                (Api.upload<Image> "settings/doAvatar" "avatar" "avatar.jpg" dataBuffer)
                                (function 
                                    | Api.ApiOk image ->
                                        match Api.userIdentity with
                                        | Some identity ->
                                            Api.userIdentity <- Some { identity with Avatar = image.Token }
                                            Image.loadImageForView (App.imageUrl image.Token 100) Image.placeholder this.ProfileImage
                                        | None -> printfn "identity not set"
                                    | error -> this.HandleApiFailure error
                                )
                        )
                    )

            match args.ButtonIndex with
            | 0 -> pickerMedia.GetPickPhotoUI() |> updateAvatar
            | 1 -> pickerMedia.GetTakePhotoUI (new StoreCameraMediaOptions(Name = sprintf "%s.jpg" (System.IO.Path.GetTempFileName()), Directory = "Wireclub")) |> updateAvatar
            | _ -> ()
        )

    member this.ChooseCountry () = 
        let picker = new CountryPickerViewController(fun item ->
            country <- Some item
            this.Country.Text <- item.Name
            this.NavigationController.PopViewControllerAnimated true |> ignore
        )

        this.NavigationController.PushViewController (picker, true)


    member this.ChooseRegion () = 
        match country with
        | Some country ->
            let picker = new RegionPickerViewController(country, fun item ->
                region <- Some item
                this.Region.Text <- item.Name
                this.NavigationController.PopViewControllerAnimated true |> ignore
            )
            this.NavigationController.PushViewController (picker, true)
        | None -> showSimpleAlert "Country" "Please pick a country" "Close"

    override this.RowSelected (view, indexPath) =
        match indexPath.Section, indexPath.Row with
        | 0, 0 -> this.ChooseProfileImage () 
        | 2, 0 -> this.ChooseCountry () 
        | 2, 1 -> this.ChooseRegion () 
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
            
       