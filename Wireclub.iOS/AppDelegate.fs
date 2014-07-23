// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open System.Collections.Generic
open MonoTouch.UIKit
open MonoTouch.StoreKit
open MonoTouch.Foundation

open Newtonsoft.Json

open Utility

open Wireclub.iOS.DB

type PushNotification =
| Unknown
| PrivateChat of string

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let window = new UIWindow (UIScreen.MainScreen.Bounds)
    let entryController = new EntryViewController()
    let navigationController = new UINavigationController(entryController)

    let transactionObserver = {
        new SKPaymentTransactionObserver() with
            override this.UpdatedTransactions(queue, transactions) =
                printfn "UpdatedTransactions"
                for transaction in transactions do
                    match transaction.TransactionState with
                    | SKPaymentTransactionState.Purchasing -> printfn "[StoreKit] Purchasing"
                    | SKPaymentTransactionState.Purchased ->
                        printfn "[StoreKit] Purchased"

                        if Api.userIdentity <> None then
                            let data = NSData.FromUrl(NSBundle.MainBundle.AppStoreReceiptUrl)
                            if data <> null then
                                Async.startNetworkWithContinuation
                                    (Credits.appStorePurchase (transaction.TransactionIdentifier) (data.GetBase64EncodedString NSDataBase64EncodingOptions.None))
                                    (function
                                        | Api.ApiOk bundle ->
                                            SKPaymentQueue.DefaultQueue.FinishTransaction(transaction)

                                            let alert =
                                                new UIAlertView (
                                                    Title = sprintf "Purchase Complete",
                                                    Message = sprintf "%i credits have been added to your account." bundle.CurrentUserCredits
                                                )
                                            alert.AddButton "Awesome!" |> ignore
                                            alert.Show ()
                                        | error -> ()
                                    )
                        else
                            Credits.transactionsAdd transaction

                    | SKPaymentTransactionState.Failed ->
                        printfn "[StoreKit] Failed %s" transaction.Error.LocalizedDescription
                        SKPaymentQueue.DefaultQueue.FinishTransaction(transaction)
                    | SKPaymentTransactionState.Restored ->
                        //TODO: in theory this should never happen since we are only dealing with consumables
                        printfn "[StoreKit] Restored"
                        SKPaymentQueue.DefaultQueue.FinishTransaction(transaction)
                    | state -> Logger.log(Exception (sprintf "[StoreKit] Uknown Transaction type: %A" state))

                ()
            }

    let parsePushNotification (info:NSDictionary) =
        match info.TryGetValue(NSObject.FromObject "aps") with
        | true, null -> Unknown
        | true, aps when aps.GetType() = typeof<NSDictionary> ->
            let aps = aps :?> NSDictionary
            match aps.TryGetValue(NSObject.FromObject "u") with
            | true, null -> Unknown
            | true, u -> PrivateChat (u.ToString())
            | _ -> Unknown
        | _ -> Unknown


    override this.FinishedLaunching (app, options) =
        Api.agent <- "wireclub-app-ios/" + NSBundle.MainBundle.InfoDictionary.["CFBundleVersion"].ToString()

        NSUserDefaults.StandardUserDefaults.RegisterDefaults(
            NSDictionary.FromObjectAndKey(NSObject.FromObject(Api.agent), NSObject.FromObject("UserAgent")))

        if options <> null then
            match options.TryGetValue(NSObject.FromObject UIApplication.LaunchOptionsRemoteNotificationKey) with
            | true, userInfo ->
                match parsePushNotification (userInfo :?> NSDictionary) with
                | PrivateChat id -> entryController.NavigateOnLoad <- Some (("/privateChat/session/" + id), None)
                | _ -> ()
            | _ -> ()

        Logger.log <-
            (fun ex -> 
                let error = sprintf "%s\n%s" ex.Message ex.StackTrace 
                printfn "%s" error
                Async.startInBackgroundWithContinuation 
                    (fun _ -> DB.createError (Error(Error = error )))
                    (fun _ -> ())
            )


        SKPaymentQueue.DefaultQueue.AddTransactionObserver(transactionObserver)
                    
        window.RootViewController <- navigationController
        window.MakeKeyAndVisible ()

#if DEBUG
        let rec tick () = async {
            System.GC.Collect ()
            do! Async.Sleep (100)
            return! tick()
        }    
        Async.Start(tick())

        Reachability.onReachabilityChanged.Publish |> Event.add (fun _ ->
            printfn "[Reachability] %A" (Reachability.internetConnectionStatus ())
            (*
            match Reachability.internetConnectionStatus () with
            | Reachability.NetworkStatus.ReachableViaCarrierDataNetwork -> printfn "[Reachability] Carrier"
            | Reachability.NetworkStatus.ReachableViaWiFiNetwork -> printfn "[Reachability] Wifi"
            | _ -> printfn "[Reachability] None"*)
            )
#endif

        true

        override this.RegisteredForRemoteNotifications(app, pushToken) =
            match pushToken.Description with
            | null | "" -> ()
            | pushToken -> 
                let pushToken = pushToken.Trim('<').Trim('>')
                match NSUserDefaults.StandardUserDefaults.StringForKey "device-token" with
                | null | "" -> 
                    Async.startWithContinuation
                        (Settings.registerDevice pushToken)
                        (function
                            | Api.ApiOk result ->
                                NSUserDefaults.StandardUserDefaults.SetString(result, "device-token")
                                NSUserDefaults.StandardUserDefaults.Synchronize() |> ignore
                            | _ -> ()
                        )
                | deviceToken ->
                    Async.startWithContinuation
                        (Settings.updateDevicePushToken deviceToken pushToken)
                        (fun _ -> ())

        override this.ReceivedRemoteNotification(app, userInfo) =
            match app.ApplicationState with 
            | UIApplicationState.Active -> () 
            | UIApplicationState.Background
            | UIApplicationState.Inactive -> 
                match parsePushNotification userInfo with
                | PrivateChat id -> Navigation.navigate ("/privateChat/session/" + id) None
                | _ -> ()
            | _ -> ()
         

         override this.OnResignActivation (app) =
            if Api.userIdentity <> None then
                Async.startWithContinuation
                    (PrivateChat.setMobile ())
                    (fun _ -> ())

         override this.OnActivated (app) =
            if Api.userIdentity <> None then
                Async.startWithContinuation
                    (PrivateChat.updatePresence ())
                    (fun result -> printfn "%A" result)

module Main =
    [<EntryPoint>]
    let main args =
        #if DEBUG
            UIApplication.Main(args, null, "AppDelegate")
            0
        #else
            try
                UIApplication.Main(args, null, "AppDelegate")
            with
            | ex ->
                DB.createError (Error(Error = ex.Message))
            0
        #endif
