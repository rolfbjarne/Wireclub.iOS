// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open System.Text.RegularExpressions
open System.Linq
open System.Drawing
open System.Globalization

open Foundation
open UIKit

open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models

module Error =
    let report () = async {
        try
            let errors = DB.fetchErrors ()
            if errors.Any() then
                let! result = App.reportErrors [ for error in errors do yield error.Error ]
                match result with
                | Api.ApiOk _ -> DB.deleteAll<DB.Error>()
                | _ -> ()
        with
        | ex -> ()
    }
        
