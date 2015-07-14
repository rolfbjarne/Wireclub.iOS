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

type WireclubNavigationController =
    inherit UINavigationController

    new (controller:UIViewController) = {
        inherit UINavigationController(controller)
    }

    override this.ShouldAutorotate () = true

    override this.GetSupportedInterfaceOrientations () =
        match this.VisibleViewController with
        | :? GameViewController -> UIInterfaceOrientationMask.All
        | _ -> UIInterfaceOrientationMask.Portrait

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let window = new UIWindow (UIScreen.MainScreen.Bounds)
    let entryController = new EntryViewController()
    let navigationController = new WireclubNavigationController(entryController)

    let transactionObserver = {
        new SKPaymentTransactionObserver() with
            override this.UpdatedTransactions(queue, transactions) =
                printfn "UpdatedTransactions"
                for transaction in transactions do
                    if Api.userIdentity <> None then
                        Credits.postTransaction transaction
                    else
                        Credits.transactionsAdd transaction
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

        Api.client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Api.agent) |> ignore
        NSUserDefaults.StandardUserDefaults.RegisterDefaults(
            NSDictionary.FromObjectAndKey(NSObject.FromObject(Api.agent), NSObject.FromObject("UserAgent")))

        Api.client.DefaultRequestHeaders.TryAddWithoutValidation("x-user-agent-name", UIDevice.CurrentDevice.SystemName) |> ignore
        Api.client.DefaultRequestHeaders.TryAddWithoutValidation("x-user-agent-version", UIDevice.CurrentDevice.SystemVersion) |> ignore

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
                let task = UIApplication.SharedApplication.BeginBackgroundTask(fun _ -> ())
                Async.startWithContinuation
                    (PrivateChat.setMobile ())
                    (fun _ -> UIApplication.SharedApplication.EndBackgroundTask(task))

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
