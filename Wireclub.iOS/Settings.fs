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

    [<Outlet>]
    member val Confirm:UITextField = null with get, set

    [<Outlet>]
    member val New:UITextField = null with get, set

    [<Outlet>]
    member val Old:UITextField = null with get, set

    [<Outlet>]
    member val Password:UITextField = null with get, set

    [<Outlet>]
    member val Save:UIButton = null with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

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

[<Register ("PasswordViewController")>]
type PasswordViewController(handle:nativeint) =
    inherit UITableViewController (handle)

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