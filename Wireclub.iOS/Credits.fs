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
        let ids =
            NSSet.MakeNSObjectSet<NSString>(
                [|
                    new NSString("wiredev.credits.1075")
                    new NSString("wiredev.credits.12000")
                    new NSString("wiredev.credits.2200")
                    new NSString("wiredev.credits.500")
                    new NSString("wiredev.credits.5750")
                    new NSString("wiredev.credits.24500")
                |]
            )
        let request = new SKProductsRequest(ids, Delegate = productsRequestDelegate)
        request.Start()



