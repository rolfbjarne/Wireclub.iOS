namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat

type ChatsViewController () =
    inherit UIViewController ()

type ChatDirectoryViewController () =
    inherit UIViewController ()

type ChatRoomViewController (slug) =
    inherit UIViewController ()

type ChatRoomUsersViewController () =
    inherit UIViewController ()