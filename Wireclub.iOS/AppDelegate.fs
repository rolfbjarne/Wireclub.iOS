// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open MonoTouch.UIKit
open MonoTouch.Foundation

open Newtonsoft.Json

open Utility

open Wireclub.iOS.DB


[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let window = new UIWindow (UIScreen.MainScreen.Bounds)
    let entryController = new EntryViewController()
    let navigationController = new UINavigationController(entryController)

    override this.FinishedLaunching (app, options) =
        Api.agent <- "wireclub-app-ios/" + NSBundle.MainBundle.InfoDictionary.["CFBundleVersion"].ToString()

        NSUserDefaults.StandardUserDefaults.RegisterDefaults(
            NSDictionary.FromObjectAndKey(NSObject.FromObject(Api.agent), NSObject.FromObject("UserAgent")))

        Logger.log <-
            (fun ex -> 
                let error = sprintf "%s\n%s" ex.Message ex.StackTrace 
                printfn "%s" error
                Async.startInBackgroundWithContinuation 
                    (fun _ -> DB.createError (Error(Error = error )))
                    (fun _ -> ())
            )
                    
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

        override this.RegisteredForRemoteNotifications(app, deviceToken) =
            match deviceToken.Description with
            | null | "" -> ()
            | token -> () //TODO: Post to server token.Trim('<').Trim('>')

        override this.ReceivedRemoteNotification(app, userInfo) =
            ()

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
