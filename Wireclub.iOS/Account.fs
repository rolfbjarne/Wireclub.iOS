// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

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

open Toast

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
        
    let identity = match Api.userIdentity with | Some id -> id | None -> failwith "User must be logged in"

    let pickerMedia = new MediaPicker()
    let pickerDate = new UIDatePicker(new RectangleF(0.0f,0.0f,320.0f,216.0f))
    let accessoryBirthday =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.Country.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Username.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Birthday.ResignFirstResponder() |> ignore)
        )

    let mutable country:LocationCountry option = None
    let mutable countries:LocationCountry [] = [||]
    let pickerCountry = new AsyncPickerViewController() //new RectangleF(0.0f,0.0f,320.0f,216.0f)
    let accessoryCountry =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.Region.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Birthday.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Country.ResignFirstResponder() |> ignore)
        )
    let sourceCountry =
        { 
            new UIPickerViewModel() with
            override this.GetRowsInComponent(pickerView, comp) = countries.Length
            override this.GetComponentCount(pickerView) = 1
            override this.GetTitle(pickerView, row, comp) = countries.[row].Name
            override this.Selected(pickerView, row, comp) =
                country <- Some countries.[row]
                controller.Country.Text <- country.Value.Name
        }


    let mutable region:LocationRegion option = None
    let mutable regions:LocationRegion [] = [||]
    let pickerRegion = new AsyncPickerViewController()
    let accessoryRegion =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.City.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Country.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Region.ResignFirstResponder() |> ignore)
        )
    let sourceRegion =
        { 
            new UIPickerViewModel() with
            override this.GetRowsInComponent(pickerView, comp) = regions.Length
            override this.GetComponentCount(pickerView) = 1
            override this.GetTitle(pickerView, row, comp) = regions.[row].Name
            override this.Selected(pickerView, row, comp) =
                region <- Some regions.[row]
                controller.Region.Text <- region.Value.Name
        }


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
        this.NavigationItem.Title <- if identity.Membership = MembershipTypePublic.Pending then "Create Profile" else "Edit Profile"
        this.NavigationItem.HidesBackButton <- identity.Membership = MembershipTypePublic.Pending

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
        this.Birthday.TintColor <- UIColor.Clear
        this.Birthday.InputAccessoryView <- accessoryBirthday.View
        this.Birthday.EditingDidBegin.Add(fun _ ->
            if String.IsNullOrEmpty(this.Birthday.Text.Trim()) = false then
                pickerDate.Date <- NSDate.op_Implicit (DateTime.ParseExact(this.Birthday.Text, "M/d/yyyy", CultureInfo.CurrentCulture))
        )

        // Country Picker
        this.Country.InputView <- pickerCountry.View
        this.Country.InputAccessoryView <- accessoryCountry.View
        this.Country.TintColor <- UIColor.Clear
        pickerCountry.Picker.Source <- sourceCountry
        this.Country.EditingDidBegin.Add(fun _ ->
            Async.startNetworkWithContinuation
                (Settings.countries ())
                (function
                    | Api.ApiOk c -> 
                        countries <- c
                        regions <- [||]
                        pickerCountry.Progress.StopAnimating()
                        pickerCountry.Picker.ReloadAllComponents()
                        match country with
                        | Some country -> pickerCountry.Picker.Select(countries |> Array.findIndex (fun c -> c.Id = country.Id), 0, false)
                        | _ -> ()
                    | error ->
                        pickerCountry.Progress.StopAnimating()
                        this.HandleApiFailure error
                )         
        )

        // Region Picker
        this.Region.InputView <- pickerRegion.View
        this.Region.InputAccessoryView <- accessoryRegion.View
        this.Region.TintColor <- UIColor.Clear
        pickerRegion.Picker.Source <- sourceRegion
        this.Region.EditingDidBegin.Add(fun _ ->
            match country with
            | Some country ->
                pickerRegion.Progress.StartAnimating()
                Async.startNetworkWithContinuation
                    (Settings.regions country.Id)
                    (function
                        | Api.ApiOk r -> 
                            regions <- r
                            pickerRegion.Progress.StopAnimating()
                            pickerRegion.Picker.ReloadAllComponents()
                            match region, regions with
                            | _, [||] -> ()
                            | Some region, regions -> pickerCountry.Picker.Select(regions |> Array.findIndex (fun r -> r.Id = region.Id), 0, false)
                            | _ -> ()
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
            this.SaveButton.Enabled <- false
            Async.startNetworkWithContinuation
                (Settings.updateProfile
                    (this.Username.Text.Trim())
                    (match this.GenderSelect.SelectedSegment with | 0 -> GenderType.Male | 1 -> GenderType.Female | _ -> GenderType.Undefined)
                    (DateTime.ParseExact(this.Birthday.Text, "M/d/yyyy", CultureInfo.CurrentCulture))
                    (match country with | Some country -> country.Id | None _ -> String.Empty)
                    (match region with | Some region -> region.Name | None _ -> String.Empty)
                    (this.City.Text.Trim())
                    (this.About.Text.Trim())
                    )
                (fun result ->
                    this.SaveButton.Enabled <- true
                    match result with
                    | Api.ApiOk i -> 
                        Api.userIdentity <- Some i
                        if identity.Membership = MembershipTypePublic.Pending then
                            Navigation.navigate "/home" None
                        else
                            this.View.EndEditing true |> ignore
                            this.View.MakeToast("Saved", 2.0, "bottom")

                    | error -> this.HandleApiFailure error
                )
        )

        Async.startNetworkWithContinuation
            (async {
                let! result = Settings.profile ()
                match result with
                | Api.ApiOk profile -> 
                    let! countries = Settings.countries ()
                    let! regions = Settings.regions profile.CountryId
                    return result, countries, regions
                | _ -> 
                    return result, Api.ApiResult.Exception (Exception "No profile loaded"), Api.ApiResult.Exception (Exception "No profile loaded")
            })
            (function
                | Api.ApiOk profile, Api.ApiOk c, Api.ApiOk r ->
                    countries <- c
                    regions <- r
                    this.Username.Text <- identity.Name
                    this.GenderSelect.SelectedSegment <-
                        match profile.Gender with
                        | GenderType.Female -> 1
                        | _ -> 0

                    this.Birthday.Text <- profile.Birthday.ToString("M/d/yyyy")

                    match countries |> Array.filter (fun e -> e.Id = profile.CountryId) with
                    | [| c |] ->
                        country <- Some c 
                        this.Country.Text <- c.Name                       
                    |  _ -> ()

                    match regions |> Array.filter (fun e -> e.Id = profile.RegionId) with
                    | [| r |] ->
                        region <- Some r 
                        this.Region.Text <- r.Name                       
                    |  _ -> ()

                    this.City.Text <- profile.CityName
                    this.About.Text <- profile.Bio

                | _ -> ()
            )

    override this.GetHeightForRow (tableView, indexPath) =
        match indexPath.Section, indexPath.Row with
        | 0, 0 -> 74.0f
        | 1, 0 -> if identity.Membership = MembershipTypePublic.Pending then tableView.RowHeight else 0.0f
        | 3, 0 -> 74.0f
        | _ -> tableView.RowHeight


    override this.WillDisplay (tableView, cell, indexPath) = 
        match indexPath.Section, indexPath.Row with
        | 1, 0 ->
            if identity.Membership <> MembershipTypePublic.Pending then cell.Hidden <- true
        | _ -> ()


    override this.RowSelected (tableView, indexPath) =
        tableView.DeselectRow (indexPath, false)
        match indexPath.Section, indexPath.Row with
        | 0, 0 -> 
            let alert = new UIAlertView (Title="Upload a picture", Message="")
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
                                Async.startNetworkWithContinuation
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
type SignupViewController (handle:nativeint) as controller =
    inherit UITableViewController (handle)

    let editProfileStoryboard = UIStoryboard.FromName ("EditProfile", null)

    let signup () =
        controller.SignupButton.Enabled <- false
        Async.startNetworkWithContinuation
            (Account.signup controller.Email.Text controller.Password.Text)
            (fun result ->
                controller.SignupButton.Enabled <- true
                match result with
                | Api.ApiOk result ->
                    NSUserDefaults.StandardUserDefaults.SetString (result.Token, "auth-token")
                    NSUserDefaults.StandardUserDefaults.Synchronize () |> ignore
                    controller.NavigationController.PushViewController (editProfileStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
                | error -> controller.HandleApiFailure error
            )

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

        this.Email.EditingDidEndOnExit.Add(fun _-> this.Password.BecomeFirstResponder() |> ignore )

        this.Password.EditingDidEndOnExit.Add(fun _ ->
            this.Password.ResignFirstResponder() |> ignore
            signup()
        )

        this.SignupButton.TouchUpInside.Add(fun _ -> signup() )

[<Register ("LoginViewController")>]
type LoginViewController (handle:nativeint) as controller =
    inherit UITableViewController (handle)

    let signupStoryboard = UIStoryboard.FromName ("Signup", null)
    let forgotPasswordStoryboard = UIStoryboard.FromName ("ForgotPassword", null)

    let login () = 
        controller.LoginButton.Enabled <- false
        Async.startNetworkWithContinuation
            (Account.login controller.Email.Text controller.Password.Text)
            (fun result ->
                controller.LoginButton.Enabled <- true
                match result with
                | Api.ApiOk result ->
                    NSUserDefaults.StandardUserDefaults.SetString (result.Token, "auth-token")
                    NSUserDefaults.StandardUserDefaults.Synchronize () |> ignore
                    controller.NavigationController.PopViewControllerAnimated true |> ignore
                | error -> controller.HandleApiFailure error
            )

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

        this.Email.EditingDidEndOnExit.Add(fun _-> this.Password.BecomeFirstResponder() |> ignore )

        this.Password.EditingDidEndOnExit.Add(fun _ ->
            this.Password.ResignFirstResponder() |> ignore
            login()
        )

        this.LoginButton.TouchUpInside.Add(fun _ -> login ())

        this.ForgotPasswordButton.TouchUpInside.Add(fun _ ->
            this.NavigationController.PushViewController (forgotPasswordStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
        )

        this.CreateAccountButton.TouchUpInside.Add(fun _ ->
            this.NavigationController.PushViewController (signupStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
        )
