﻿namespace Wireclub.iOS

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
type ChatOptionsViewController(handle:nativeint) =
    inherit UITableViewController (handle)

    [<Outlet>]
    member val Color:UITextField = null with get, set

    [<Outlet>]
    member val Font:UITextField = null with get, set

    [<Outlet>]
    member val PlaySounds:UISwitch = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    [<Outlet>]
    member val ShowJoinLeave:UITextField = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

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
                            field.InputView <- picker
                            field.InputAccessoryView <- accessory.View
                            picker.Source <- source
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

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
            

[<Register ("MegaMenuViewController")>]
type MegaMenuViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    override this.ViewDidLoad () =
        base.ViewDidLoad ()