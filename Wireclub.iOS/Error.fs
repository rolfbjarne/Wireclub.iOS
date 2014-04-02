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

module Error =
    let report () = async {
        try
            let! errors = DB.fetchErrors ()
            if errors.Any() then
                let! result = App.reportErrors [ for error in errors do yield error.Error ]
                match result with
                | Api.ApiOk _ -> do! DB.clearErrors()
                | _ -> ()
        with
        | ex -> ()
    }
        
