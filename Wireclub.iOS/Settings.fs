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

open Toast

[<Register ("ChatOptionsViewController")>]
type ChatOptionsViewController(handle:nativeint) as controller =
    inherit UITableViewController (handle)

    let joinLeave =
        [
            0, "Show Join/Leave Messages"
            1, "Hide Join/Leave In Busy Rooms"
            2, "Hide Join/Leave Messages"
        ]

    let joinLeaveById id = 
        match joinLeave |> List.filter (fun (i, _) -> i = id) with
        | [ id, message ] -> message
        | _-> snd joinLeave.Head

    let pickerFont = new UIPickerView() 
    let accessoryFont =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.Color.BecomeFirstResponder() |> ignore),
            (fun _ -> ()),
            (fun _ -> controller.Font.ResignFirstResponder() |> ignore)
        )
    let sourceFont =
        { 
            new UIPickerViewModel() with
            override this.GetRowsInComponent(pickerView, comp) = Utility.fonts.Length
            override this.GetComponentCount(pickerView) = 1
            override this.GetTitle(pickerView, row, comp) = snd Utility.fonts.[row]
            override this.Selected(pickerView, row, comp) =
                controller.Font.Text <- snd Utility.fonts.[row]
        }
                
    let pickerColor = new UIPickerView() 
    let accessoryColor =
        new NavigateInputAccessoryViewController(
            (fun _ -> controller.ShowJoinLeave.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Font.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.Color.ResignFirstResponder() |> ignore)
        )

    let sourceColor =
        { 
            new UIPickerViewModel() with
            override this.GetRowsInComponent(pickerView, comp) = Utility.colors.Length
            override this.GetComponentCount(pickerView) = 1
            override this.GetView(pickerView, row, comp, view) =
                let colors = Utility.colors.[row]
                let label = new UILabel()
                label.TextColor <- colors.Color
                label.Text <- colors.Name
                label.TextAlignment <- UITextAlignment.Center
                upcast label
            override this.Selected(pickerView, row, comp) = controller.Color.Text <- Utility.colors.[row].Name
        }

    let pickerJoinLeave = new UIPickerView() 
    let accessoryJoinLeave =
        new NavigateInputAccessoryViewController(
            (fun _ -> ()),
            (fun _ -> controller.Color.BecomeFirstResponder() |> ignore),
            (fun _ -> controller.ShowJoinLeave.ResignFirstResponder() |> ignore)
        )

    let sourceJoinLeave =
        { 
            new UIPickerViewModel() with
            override this.GetRowsInComponent(pickerView, comp) = joinLeave.Length
            override this.GetComponentCount(pickerView) = 1
            override this.GetTitle(pickerView, row, comp) = snd joinLeave.[row]
            override this.Selected(pickerView, row, comp) = controller.ShowJoinLeave.Text <- snd joinLeave.[row]
        }

    [<Outlet>]
    member val Font:UITextField = null with get, set

    [<Outlet>]
    member val Color:UITextField = null with get, set

    [<Outlet>]
    member val ShowJoinLeave:UITextField = null with get, set

    [<Outlet>]
    member val PlaySounds:UISwitch = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    [<Outlet>]
    member val ShowAdultContent:UISwitch = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        Async.startNetworkWithContinuation
            (Settings.chat ())
            (function
                | Api.ApiOk data -> 
                    this.Font.Text <- Utility.fontFamily data.Font
                    this.Font.TintColor <- UIColor.Clear
                    this.Font.InputView <- keyboardFrom pickerFont accessoryFont.View
                    pickerFont.Model <- sourceFont
                    pickerFont.Select(Utility.fonts |> List.findIndex (fun (i, _) -> i = data.Font), 0, false)
                    this.Font.EditingDidBegin.Add(fun _ -> pickerFont.ReloadAllComponents())

                    pickerColor.BackgroundColor <- UIColor.White

                    this.Color.Text <- (Utility.customColor data.ColorId).Name
                    this.Color.TintColor <- UIColor.Clear
                    this.Color.InputView <- keyboardFrom pickerColor accessoryColor.View
                    pickerColor.Model <- sourceColor
                    pickerColor.Select(Utility.colors |> List.findIndex (fun c -> c.Id = data.ColorId), 0, false)
                    this.Color.EditingDidBegin.Add(fun _ -> pickerColor.ReloadAllComponents())

                    this.ShowJoinLeave.Text <- joinLeaveById data.JoinLeaveMessages
                    this.ShowJoinLeave.TintColor <- UIColor.Clear
                    this.ShowJoinLeave.InputView <- keyboardFrom pickerJoinLeave accessoryJoinLeave.View
                    pickerJoinLeave.Model <- sourceJoinLeave
                    pickerJoinLeave.Select(joinLeave |> List.findIndex (fun (i, _) -> i = data.JoinLeaveMessages), 0, false)
                    this.ShowJoinLeave.EditingDidBegin.Add(fun _ -> pickerJoinLeave.ReloadAllComponents())

                    this.PlaySounds.On <- data.PlaySounds
                    this.ShowAdultContent.On <- data.ShowRatedR

                    this.ShowAdultContent.ValueChanged.Add(fun args ->
                        Async.startNetworkWithContinuation
                            (Settings.updateContentOptions this.ShowAdultContent.On)
                            (function
                                | Api.ApiOk _ -> ()
                                | error -> this.HandleApiFailure error
                            )
                    )

                    this.Save.TouchUpInside.Add(fun _ ->
                        this.View.EndEditing(true) |> ignore
                        Async.startNetworkWithContinuation
                            (Settings.updateChat
                                (
                                    match Utility.fonts |> List.filter (fun (_, m) -> m = this.Font.Text) with
                                    | [ id, _ ] -> id
                                    | _-> fst fonts.Head 
                                )
                                (
                                    match Utility.colors |> List.filter (fun c -> c.Name = this.Color.Text) with
                                    | [ color ] -> color.Id
                                    | _-> Utility.colors.Head.Id
                                )
                                this.PlaySounds.On
                                (
                                    match joinLeave |> List.filter (fun (_, m) -> m = this.ShowJoinLeave.Text) with
                                    | [ id, _ ] -> id
                                    | _-> fst joinLeave.Head 
                                )
                            )
                            (function
                                | Api.ApiOk data ->
                                    this.Color.Text <- (Utility.customColor data.ColorId).Name
                                    this.Font.Text <- Utility.fontFamily data.Font
                                    this.ShowJoinLeave.Text <- joinLeaveById data.JoinLeaveMessages
                                    this.PlaySounds.On <- data.PlaySounds

                                    this.View.MakeToast("Saved", 2.0, "center")
                                | error -> this.HandleApiFailure error
                            )
                        )

                | error -> this.HandleApiFailure error
            )

[<Register ("EmailViewController")>]
type EmailViewController(handle:nativeint) =
    inherit UITableViewController (handle)

    let identity = match Api.userIdentity with | Some id -> id | None -> failwith "User must be logged in"

    [<Outlet>]
    member val Confirm:UITextField = null with get, set

    [<Outlet>]
    member val New:UITextField = null with get, set

    [<Outlet>]
    member val Password:UITextField = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        this.Save.TouchUpInside.Add(fun _ ->
            this.View.EndEditing(true) |> ignore
            Async.startNetworkWithContinuation
                (Settings.email this.New.Text this.Confirm.Text this.Password.Text)
                (function
                    | Api.ApiOk data -> this.View.MakeToast("Saved", 2.0, "center")
                    | error -> this.HandleApiFailure error
                )
            
        )

[<Register ("MessagingOptionsViewController")>]
type MessagingOptionsViewController(handle:nativeint) =
    inherit UITableViewController (handle)

    [<Outlet>]
    member val Picture:UISwitch = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    [<Outlet>]
    member val Verified:UISwitch = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        Async.startNetworkWithContinuation
            (Settings.messaging ())
            (function
                | Api.ApiOk data -> 
                    this.Picture.On <- data.BlockWithoutPicture
                    this.Verified.On <- data.BlockWithoutVerifiedEmail

                    this.Save.TouchUpInside.Add(fun _ ->
                        this.View.EndEditing(true) |> ignore
                        Async.startNetworkWithContinuation
                            (Settings.updateMessaging this.Picture.On false this.Verified.On)
                            (function
                                | Api.ApiOk data ->
                                    this.Picture.On <- data.BlockWithoutPicture
                                    this.Verified.On <- data.BlockWithoutVerifiedEmail
                                    this.View.MakeToast("Saved", 2.0, "center")
                                | error -> this.HandleApiFailure error
                            )
                        )

                | error -> this.HandleApiFailure error
            )
    
[<Register ("NotificationsViewController")>]
type NotificationsViewController(handle:nativeint) =
    inherit UITableViewController (handle)

    [<Outlet>]
    member val ClubActivity:UISwitch = null with get, set

    [<Outlet>]
    member val Invitations:UISwitch = null with get, set

    [<Outlet>]
    member val NewMessages:UISwitch = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        Async.startNetworkWithContinuation
            (Settings.notifications ())
            (function
                | Api.ApiOk data -> 
                    this.ClubActivity.On <- data.SurpressedEmailTypes.Contains("AlertClubMembership") = false
                    this.NewMessages.On <- data.SurpressedEmailTypes.Contains("AlertMessage") = false
                    this.Invitations.On <- data.SurpressedEmailTypes.Contains("AlertInvitedToEntity") = false

                    this.Save.TouchUpInside.Add(fun _ ->
                        this.View.EndEditing(true) |> ignore
                        let suppressedNotifications = 
                            [
                                if this.ClubActivity.On then yield "AlertClubMembership"
                                if this.NewMessages.On then yield "AlertMessage"
                                if this.Invitations.On then yield "AlertInvitedToEntity"
                            ]

                        Async.startNetworkWithContinuation
                            (Settings.suppressNotifications suppressedNotifications)
                            (function
                                | Api.ApiOk data ->
                                    this.ClubActivity.On <- data.SurpressedEmailTypes.Contains("AlertClubMembership") = false
                                    this.NewMessages.On <- data.SurpressedEmailTypes.Contains("AlertMessage") = false
                                    this.Invitations.On <- data.SurpressedEmailTypes.Contains("AlertInvitedToEntity") = false
                                    this.View.MakeToast("Saved", 2.0, "center")
                                | error -> this.HandleApiFailure error
                            )
                        )

                | error -> this.HandleApiFailure error
            )

[<Register ("PasswordViewController")>]
type PasswordViewController(handle:nativeint) =
    inherit UITableViewController (handle)

    let forgotPasswordStoryboard = UIStoryboard.FromName ("ForgotPassword", null)

    [<Outlet>]
    member val Confirm:UITextField = null with get, set

    [<Outlet>]
    member val Forgot:UIButton = null with get, set

    [<Outlet>]
    member val New:UITextField = null with get, set

    [<Outlet>]
    member val Old:UITextField = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        this.Save.TouchUpInside.Add(fun _ ->
            this.View.EndEditing(true) |> ignore
            Async.startNetworkWithContinuation
                (Settings.password this.Old.Text this.New.Text this.Confirm.Text)
                (function
                    | Api.ApiOk data -> this.View.MakeToast("Saved", 2.0, "center")
                    | error -> this.HandleApiFailure error
                )
        )

        this.Forgot.TouchUpInside.Add(fun _ ->
            this.NavigationController.PushViewController (forgotPasswordStoryboard.InstantiateInitialViewController() :?> UIViewController, true)
        )


 type UIPrivacyPickerViewModel (field:UITextField) =
    inherit UIPickerViewModel()

    let options = [
        RelationshipRequiredType.Anyone
        RelationshipRequiredType.Friends
        RelationshipRequiredType.NoOne
    ]


    override this.GetRowsInComponent(pickerView, comp) = options.Length
    override this.GetComponentCount(pickerView) = 1
    override this.GetTitle(pickerView, row, comp) = options.[row].ToString()
    override this.Selected(pickerView, row, comp) =
        field.Text <- options.[row].ToString()



[<Register ("PrivacyOptionsViewController")>]
type PrivacyOptionsViewController(handle:nativeint) =
    inherit UITableViewController (handle)

    let mutable pickers:(UIPickerView * NavigateInputAccessoryViewController * UIPrivacyPickerViewModel) list = []

    [<Outlet>]
    member val Contact:UITextField = null with get, set

    [<Outlet>]
    member val InviteGames:UITextField = null with get, set

    [<Outlet>]
    member val PictureRankings:UISwitch = null with get, set

    [<Outlet>]
    member val PrivateChat:UITextField = null with get, set

    [<Outlet>]
    member val ViewBlog:UITextField = null with get, set

    [<Outlet>]
    member val ViewPictures:UITextField = null with get, set

    [<Outlet>]
    member val ViewProfile:UITextField = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    [<Outlet>]
    member val OptOut:UISwitch = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        Async.startNetworkWithContinuation
            (Settings.privacy ())
            (function
                | Api.ApiOk data -> 
                    pickers <-
                        [
                            this.Contact, data.RequiredRelationshipToContact
                            this.PrivateChat, data.RequiredRelationshipToPrivateChat
                            this.ViewProfile, data.RequiredRelationshipToViewProfile
                            this.ViewBlog, data.RequiredRelationshipToViewBlog
                            this.ViewPictures, data.RequiredRelationshipToViewPictures
                            this.InviteGames, data.RequiredRelationshipToGameChallenge
                        ]
                        |> List.nextPrevTuple
                        |> List.map (fun fields ->
                            let prev, field, next = fields
                            let prev = match prev with | Some prev -> Some (fst prev) | _ -> None
                            let next = match next with | Some next -> Some (fst next) | _ -> None
                            let field, value = field

                            let pickerNext _ = match next with | Some next -> next.BecomeFirstResponder() |> ignore | _ -> ()
                            let pickerPrev _ = match prev with | Some prev -> prev.BecomeFirstResponder() |> ignore | _ -> ()
                            let pickerDone _ = field.ResignFirstResponder() |> ignore


                            let picker = new UIPickerView()
                            let accessory = new NavigateInputAccessoryViewController( pickerNext, pickerPrev, pickerDone )
                            let source =  new UIPrivacyPickerViewModel(field)

                            field.TintColor <- UIColor.Clear
                            field.Text <- value.ToString()
                            field.InputView <- keyboardFrom picker accessory.View
                            picker.Model <- source
                            let row = 
                                function
                                | RelationshipRequiredType.NoOne -> 2
                                | RelationshipRequiredType.Friends -> 1
                                | _ -> 0

                            picker.Select(row value, 0, false)
                            field.EditingDidBegin.Add(fun _ -> picker.ReloadAllComponents())
                            picker, accessory, source
                        )

                    this.OptOut.On <- data.OptOutFromRatePictures

                    let relationship =
                        function
                        | "NoOne" -> RelationshipRequiredType.NoOne
                        | "Friends" -> RelationshipRequiredType.Friends
                        | _ -> RelationshipRequiredType.Anyone

                    this.Save.TouchUpInside.Add(fun _ ->
                        this.View.EndEditing(true) |> ignore
                        Async.startNetworkWithContinuation
                            (Settings.updatePrivacy
                                (this.Contact.Text |> relationship)
                                (this.PrivateChat.Text |> relationship)
                                (this.ViewPictures.Text |> relationship)
                                (this.ViewBlog.Text |> relationship)
                                (this.ViewPictures.Text |> relationship)
                                RelationshipRequiredType.Anyone
                                RelationshipRequiredType.Anyone
                                RelationshipRequiredType.Anyone
                                (this.InviteGames.Text |> relationship)
                                this.OptOut.On
                            )
                            (function
                                | Api.ApiOk data ->
                                    this.Contact.Text <- data.RequiredRelationshipToContact.ToString()
                                    this.PrivateChat.Text <- data.RequiredRelationshipToPrivateChat.ToString()
                                    this.ViewProfile.Text <- data.RequiredRelationshipToViewProfile.ToString()
                                    this.ViewBlog.Text <- data.RequiredRelationshipToViewBlog.ToString()
                                    this.ViewPictures.Text <- data.RequiredRelationshipToViewPictures.ToString()
                                    this.InviteGames.Text <- data.RequiredRelationshipToGameChallenge.ToString()
                                    this.View.MakeToast("Saved", 2.0, "center")
                                | error -> this.HandleApiFailure error
                            )
                        )

                | error -> this.HandleApiFailure error
            )

[<Register ("BlockedUsersViewController")>]
type BlockedUsersViewController (handle:nativeint) as controller =
    inherit UITableViewController (handle)

    let mutable users:Entity [] = [||]

    let source = {
        new UITableViewSource() with
            override this.GetCell(tableView, indexPath) =
                let user = users.[indexPath.Row]
                let cell = 
                    match tableView.DequeueReusableCell "room-user-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "room-user-cell")
                    | c -> c

                cell.SelectionStyle <- UITableViewCellSelectionStyle.None
                cell.Tag <- indexPath.Row
                cell.TextLabel.Text <- user.Label
                Image.loadImageForCell (App.imageUrl user.Image 44) Image.placeholder cell tableView
                cell

            override this.RowsInSection(tableView, section) = users.Length
            override this.RowSelected(tableView, indexPath) = ()
            override this.CanEditRow(tableView, indexPath) = true
            override this.EditingStyleForRow(tableView, indexPath) = UITableViewCellEditingStyle.Delete
            override this.CommitEditingStyle(tableView, style, indexPath) =
                let userId = users.[indexPath.Row].Id
                Async.startNetworkWithContinuation
                    (Settings.unblock [ userId ])
                    (function
                        | Api.ApiOk data -> 
                            users <- [| for user in users do if user.Id <> userId then yield user |]
                            tableView.DeleteRows([| indexPath |], UITableViewRowAnimation.Automatic)
                        | error -> controller.HandleApiFailure error
                    )

           
        }

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        this.TableView.Source <- source
        Async.startNetworkWithContinuation
            (Settings.blocked ())
            (function
                | Api.ApiOk data -> 
                    users <- data.BlockedUsers
                    this.TableView.ReloadData()
                    this.TableView.Editing <- true
                | error -> this.HandleApiFailure error
            )


[<Register ("SettingsMenuViewController")>]
type SettingsMenuViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    let editProfileController = lazy (Resources.editProfileStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)

    override this.ViewDidLoad () =
        base.ViewDidLoad ()


    override this.RowSelected (tableView, indexPath) =
        match indexPath.Section, indexPath.Row with
        | 0, 0 ->  this.NavigationController.PushViewController(editProfileController.Value, true)
        | _ -> ()
            

[<Register ("MegaMenuViewController")>]
type MegaMenuViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

    override this.RowSelected (tableView, indexPath) =
        match indexPath.Section, indexPath.Row with
        | 1, 0 ->  Navigation.navigate "/logout" None
        | 0, 1 ->  Navigation.navigate "/credits" None
        | _ -> ()









