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

        
[<Register ("PrivacyOptionsViewController")>]
type PrivacyOptionsViewController(handle:nativeint) =
    inherit UITableViewController (handle)

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

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

[<Register ("BlockedUsersViewController")>]
type BlockedUsersViewController (handle:nativeint) =
    inherit UITableViewController (handle)

    override this.ViewDidLoad () =
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