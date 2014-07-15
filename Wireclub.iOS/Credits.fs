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
open MonoTouch.StoreKit

open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models

open ChannelEvent

open Utility


[<Register ("CreditsViewController")>]
type CreditsViewController () =
    inherit UIViewController ("CreditsViewController", null)

    let localizedPrice (product:SKProduct) =
        let formatter = new NSNumberFormatter()
        formatter.FormatterBehavior <- NSNumberFormatterBehavior.Version_10_4
        formatter.NumberStyle <- NSNumberFormatterStyle.Currency
        formatter.Locale <- product.PriceLocale
        formatter.StringFromNumber(product.Price)


    let productsRequestDelegate = { 
        new SKProductsRequestDelegate () with
            override this.ReceivedResponse (request, response) =
                for product in response.Products do
                    printfn "LocalizedDescription: %s" product.LocalizedDescription
                    printfn "LocalizedTitle: %s" product.LocalizedTitle
                    printfn "ProductIdentifier: %s" product.ProductIdentifier
                    printfn "ProductIdentifier: %s" (localizedPrice product)

                for product in response.InvalidProducts do
                    Logger.log (Exception(sprintf "[StoreKit] InvalidProduct - %s" product))

            override this.RequestFailed (request, error) = Logger.log (Exception( error.Description))
        }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override this.ViewDidLoad () =
        Async.startNetworkWithContinuation
            (async { return Api.ApiOk [| "wiredev.credits.1075";"wiredev.credits.12000"; "wiredev.credits.2200";"wiredev.credits.500";"wiredev.credits.5750";"wiredev.credits.24500" |]  })
            (function 
                | Api.ApiOk result ->
                    
                    let request = new SKProductsRequest(NSSet.MakeNSObjectSet<NSString>(result.Select(fun s -> new NSString(s)).ToArray()), Delegate = productsRequestDelegate)
                    request.Start()


                | error -> this.HandleApiFailure error
            )



