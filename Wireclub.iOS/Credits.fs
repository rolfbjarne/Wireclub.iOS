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
type CreditsViewController () as controller =
    inherit UIViewController ("CreditsViewController", null)

    let mutable products:(string * string * string * string * SKProduct) list = []

    let localizedPrice (product:SKProduct) =
        let formatter = new NSNumberFormatter()
        formatter.FormatterBehavior <- NSNumberFormatterBehavior.Version_10_4
        formatter.NumberStyle <- NSNumberFormatterStyle.Currency
        formatter.Locale <- product.PriceLocale
        formatter.StringFromNumber(product.Price)

    let font = UIFont.SystemFontOfSize UIFont.ButtonFontSize
    let source = {
        new UITableViewSource() with

            override this.GetCell(tableView, indexPath) =
                let (id, name, desc, price, product) = products.[indexPath.Row]
                let cell = 
                    match tableView.DequeueReusableCell "credits-product-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "credits-product-cell")
                    | c -> c
                
                cell.Tag <- indexPath.Row
                cell.TextLabel.Text <- name
                cell.DetailTextLabel.Text <- desc

                let size = (new NSString(price)).StringSize(font)
                cell.AccessoryView <- new UILabel(new RectangleF(0.f, 0.f, size.Width, controller.Table.RowHeight), Text = price, Font = font)
                //TODO: load credits images
                //Image.loadImageForCell (App.imageUrl user.Avatar 100) Image.placeholder cell tableView
                cell

            override this.RowsInSection(tableView, section) =
                products.Length

            override this.RowSelected(tableView, indexPath) =
                tableView.DeselectRow (indexPath, false)
                let (id, name, desc, price, product) = products.[indexPath.Row]
                let alert = new UIAlertView (Title = sprintf "Purchase %s" name, Message = sprintf "Would you like to purchase %s for %s" name price)
                alert.AddButton "Confirm" |> ignore
                alert.AddButton "Cancel" |> ignore
                alert.Clicked.Add(fun args ->
                    match args.ButtonIndex with
                    | 0 ->
                        let payment = SKMutablePayment.PaymentWithProduct product
                        payment.Quantity <- 1
                        SKPaymentQueue.DefaultQueue.AddPayment(payment)
                    | _ -> ()
                )
                alert.Show ()
    }

    let products = { 
        new SKProductsRequestDelegate () with
            override this.ReceivedResponse (request, response) =
                products <-
                    [
                        for product in response.Products do
                            yield (
                                product.ProductIdentifier,
                                product.LocalizedTitle,
                                product.LocalizedDescription,
                                (localizedPrice product),
                                product
                            )
                    ]

                for product in response.InvalidProducts do
                    Logger.log (Exception(sprintf "[StoreKit] InvalidProduct - %s" product))

                controller.Table.ReloadData()

            override this.RequestFailed (request, error) = Logger.log (Exception( error.Description))
        }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override this.ViewDidLoad () =
        this.Table.Source <- source

        Async.startNetworkWithContinuation
            (async { return Api.ApiOk [| "wiredev.credits.1075";"wiredev.credits.12000"; "wiredev.credits.2200";"wiredev.credits.500";"wiredev.credits.5750";"wiredev.credits.24500" |]  })
            (function 
                | Api.ApiOk result ->                    
                    let request = new SKProductsRequest(NSSet.MakeNSObjectSet<NSString>(result.Select(fun s -> new NSString(s)).ToArray()), Delegate = products)
                    request.Start()
                | error -> this.HandleApiFailure error
            )



