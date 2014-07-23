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

module Credits =
    let transactions = new List<SKPaymentTransaction>()

    let transactionsFetch () =
        lock transactions (fun _ -> new List<SKPaymentTransaction>(transactions))

    let transactionsAdd item =
        lock transactions (fun _ -> transactions.Add item)

    let transactionsClear () =
        lock transactions (fun _ -> transactions.Clear() )

[<Register ("CreditsViewController")>]
type CreditsViewController () as controller =
    inherit UIViewController ("CreditsViewController", null)

    let mutable products:(string * string * string * string * SKProduct * CreditBundle) list = []
    let mutable bundles:CreditBundle [] = [||]

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
                let (id, name, desc, price, product, bundle) = products.[indexPath.Row]
                let cell = 
                    match tableView.DequeueReusableCell "credits-product-cell" with
                    | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "credits-product-cell")
                    | c -> c
                
                cell.Tag <- indexPath.Row
                cell.TextLabel.Text <- name
                cell.DetailTextLabel.Text <- desc

                let size = (new NSString(price)).StringSize(font)
                cell.AccessoryView <- new UILabel(new RectangleF(0.f, 0.f, size.Width, controller.Table.RowHeight), Text = price, Font = font)
                cell.ImageView.Image <- UIImage.FromFile(sprintf "purchase-%i.png" (int bundle.Price))
                cell

            override this.RowsInSection(tableView, section) = products.Length

            override this.RowSelected(tableView, indexPath) =
                tableView.DeselectRow (indexPath, false)
                let (id, name, desc, price, product, bundle) = products.[indexPath.Row]
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
                            match bundles |> Seq.tryFind(fun p -> p.AppStoreId = product.ProductIdentifier) with
                            | Some bundle ->
                                yield (
                                    product.ProductIdentifier,
                                    product.LocalizedTitle,
                                    product.LocalizedDescription,
                                    (localizedPrice product),
                                    product,
                                    bundle
                                )
                            | _ -> ()
                    ] |> List.sortBy(fun (_, _, _, _, _, bundle) -> bundle.RegularCredits)

                for product in response.InvalidProducts do
                    Logger.log (Exception(sprintf "[StoreKit] InvalidProduct - %s" product))

                controller.Table.ReloadData()

            override this.RequestFailed (request, error) = Logger.log (Exception( error.Description))
        }

    let mutable appEventObserver:NSObject = null

    member this.OnAppEvent (notification:NSNotification) =
        notification.HandleAppEvent
            (function
                | CreditsBalanceChanged (balance) -> printfn "balance %i" balance
                | _ -> ()
            )


    [<Outlet>]
    member val Table: UITableView = null with get, set

    override this.ViewDidLoad () =
        this.Table.Source <- source
        appEventObserver <- NSNotificationCenter.DefaultCenter.AddObserver("Wireclub.AppEvent", this.OnAppEvent)

        Async.startNetworkWithContinuation
            (Credits.bundles())
            (function 
                | Api.ApiOk result ->     
                    bundles <- result.Bundles
                    let ids = 
                        [
                            for bundle in bundles do
                                yield bundle.AppStoreId
                        ]
                               
                    let request = new SKProductsRequest(NSSet.MakeNSObjectSet<NSString>(ids.Select(fun s -> new NSString(s)).ToArray()), Delegate = products)
                    request.Start()
                | error -> this.HandleApiFailure error
            )



