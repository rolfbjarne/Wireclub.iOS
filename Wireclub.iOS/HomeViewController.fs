namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit



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
            NSUserDefaults.StandardUserDefaults.SetString(this.Email.Text, "email")
            NSUserDefaults.StandardUserDefaults.SetString(this.Password.Text, "password")
            NSUserDefaults.StandardUserDefaults.Synchronize() |> ignore
            this.DismissViewController(true, null)

            (*
            // ## Disable UI
            // ## Progress
            Async.StartImmediate <| async {
                let! result = Account.login this.Email.Text this.Password.Text
                match result with
                | Api.ApiOk result ->
                    NSUserDefaults.StandardUserDefaults.SetString("email", this.Email.Text)
                    NSUserDefaults.StandardUserDefaults.SetString("password", this.Password.Text)
                    this.DismissViewController(true, null)
                | _ -> ()
            }*)

        )

[<Register ("HomeViewController")>]
type HomeViewController () =
    inherit UIViewController ()
    
    [<Outlet>]
    member val Button: UIButton = null with get, set

    [<Outlet>]
    member val Button2: UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()


type ChatMessage = {
    Current:string
    Id:string
    Slug:string
    Avatar:string
    Message: string
    Sequence: int64
    Color: string
    Font: string

    NavigateUrl:string
    ContentUrl:string
}


[<Register("PrivateChatSessionViewController")>]
type PrivateChatSessionViewController (user:Wireclub.Boundary.Chat.PrivateChatFriend) =
    inherit UIViewController ()

    [<Outlet>]
    member val WebView: UIWebView = null with get, set

    override this.ViewDidLoad () =
        this.NavigationItem.Title <- user.Name
        this.WebView.LoadRequest(new NSUrlRequest(new NSUrl("http://dev.wireclub.com/mobile/chat")))
        this.WebView.LoadFinished.Add(fun _ ->
            this.WebView.EvaluateJavascript(sprintf "wireclub.Mobile.addMessage(%s)" (Newtonsoft.Json.JsonConvert.SerializeObject {
                        Current = "ahh"
                        NavigateUrl = Api.baseUrl
                        ContentUrl = Api.baseUrl

                        Id = "ahhh"
                        Slug = "ahh"
                        Avatar = "ahhAHHh"
                        Color = "ahhhH"
                        Font = string 1
                        Message = "WHAT UP"
                        Sequence = 0L
                    })) |> ignore
            ()
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

[<Register ("EntryViewController")>]
type EntryViewController () =
    inherit UIViewController ()

    let navigationController = new UINavigationController()
    let rootController = new FriendsViewController()

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

    override this.ViewDidAppear (animated) =
        match NSUserDefaults.StandardUserDefaults.StringForKey "password" with
        | null -> 
            this.PresentViewController (new LoginViewController(), false, null)
        | _ ->
            this.PresentViewController (new DialogViewController("http://www.wireclub.com/account/login"), false, null)
            (*
            Async.StartImmediate <| async {
                // Check login is valid....
                do! Async.Sleep (1000) // ### REMOVE
                this.PresentViewController (rootController, true, null)
                navigationController.PushViewController (rootController, false)
            }*)
        