// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open System.Drawing
open System.Linq
open System.Collections.Concurrent
open System.Collections.Generic
open System.Text.RegularExpressions

open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models
open ChannelEvent

open Utility


[<Register ("CreditsViewController")>]
type CreditsViewController () =
    inherit UIViewController ("CreditsViewController", null)

    [<Outlet>]
    member val Table: UITableView = null with get, set