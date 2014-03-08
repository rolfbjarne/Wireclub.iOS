namespace Wireclub.iOS

open System
open System.Linq
open System.Drawing
open System.Globalization
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
                                        let image = imageOriginal |> Image.resize imageOriginal.Size
                                        image.AsJPEG()

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
