namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit

[<AutoOpen>]
module Utility =
    type UIViewController with
        member this.HandleApiResult<'A> (result:Api.ApiResult<'A>) =
            match result with
            | Api.ApiOk result -> ()
            | _ -> ()
            
            result

        member this.HandleApiFailure<'A> (result:Api.ApiResult<'A>) =
            match result with
            | Api.ApiOk _ -> ()
            | _ ->
                printfn "Api Failure: %A" result


        member this.ResizeViewToKeyboard (args:UIKeyboardEventArgs) =
            UIView.BeginAnimations ("")
            UIView.SetAnimationCurve (args.AnimationCurve);
            UIView.SetAnimationDuration (args.AnimationDuration);
            let mutable viewFrame = this.View.Frame;
            let endRelative = this.View.ConvertRectFromView (args.FrameEnd, null);
            viewFrame.Height <- endRelative.Y;
            this.View.Frame <- viewFrame;
            UIView.CommitAnimations ()
