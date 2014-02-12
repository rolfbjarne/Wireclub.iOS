namespace Wireclub.iOS

open System
open MonoTouch.UIKit
open MonoTouch.Foundation
open Newtonsoft.Json

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let window = new UIWindow (UIScreen.MainScreen.Bounds)
    let entryController = new EntryViewController()
    let navigationController = new UINavigationController(entryController)

    override this.FinishedLaunching (app, options) =
        NSUserDefaults.StandardUserDefaults.RegisterDefaults(
            NSDictionary.FromObjectAndKey(NSObject.FromObject(Api.agent), NSObject.FromObject("UserAgent")))

        window.RootViewController <- navigationController
        window.MakeKeyAndVisible ()
        true

module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main (args, null, "AppDelegate")
        0

