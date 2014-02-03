namespace Wireclub.iOS

open System
open MonoTouch.UIKit
open MonoTouch.Foundation

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit UIApplicationDelegate ()

    let window = new UIWindow (UIScreen.MainScreen.Bounds)
    let storyboard = UIStoryboard.FromName ("Main", null)

    

    // This method is invoked when the application is ready to run.
    override this.FinishedLaunching (app, options) =
        //window.RootViewController <- new Wireclub_iOSViewController ()
        let initialViewController = storyboard.InstantiateInitialViewController () :?> Views.MainViewController
        window.RootViewController <- initialViewController
        window.MakeKeyAndVisible ()

        

        true

module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main (args, null, "AppDelegate")
        0

