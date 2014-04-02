namespace Wireclub.iOS

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Newtonsoft.Json
open Wireclub.iOS.DB


[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let window = new UIWindow (UIScreen.MainScreen.Bounds)
    let entryController = new EntryViewController()
    let navigationController = new UINavigationController(entryController)

    override this.FinishedLaunching (app, options) =
        NSUserDefaults.StandardUserDefaults.RegisterDefaults(
            NSDictionary.FromObjectAndKey(NSObject.FromObject(Api.agent), NSObject.FromObject("UserAgent")))

        Logger.log <-
            (fun ex -> 
                Async.StartWithContinuations (
                    (DB.createError (Error(Error = ex.Message))),
                    (fun _ -> ()),
                    (fun _ -> ()),
                    (fun _ -> ()))
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

module Main =
    [<EntryPoint>]
    let main args =
        try
            UIApplication.Main(args, null, "AppDelegate")
        with
        | ex ->
            let reportError = DB.createError (Error(Error = ex.Message)) |> Async.StartAsTask
            reportError.Wait()
        0

