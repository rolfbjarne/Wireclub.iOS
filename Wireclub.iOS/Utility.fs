namespace Wireclub.iOS

open MonoTouch.Foundation
open MonoTouch.UIKit

type AlertDelegate (action: int -> unit) =
    inherit UIAlertViewDelegate ()
    override this.Dismissed (view, index) = 
        action index

[<AutoOpen>]
module Utility =
    let showSimpleAlert title message button =
        let alert = new UIAlertView (Title=title, Message=message)
        alert.AddButton button |> ignore
        alert.Show ()
        ()
        
    type UIViewController with
        member this.HandleApiResult<'A> (result:Api.ApiResult<'A>) =
            match result with
            | Api.ApiOk result -> ()
            | _ -> ()
            
            result

        member this.HandleApiFailure<'A> (result:Api.ApiResult<'A>) =
            match result with
            | Api.ApiOk _ -> ()
            | Api.BadRequest [] -> showSimpleAlert "Error" "An error occured submitting your request" "Close"
            | Api.BadRequest ({Key=key; Value=value}::_) -> showSimpleAlert key value "Close"
            | _ -> printfn "Api Failure: %A" result


        member this.ResizeViewToKeyboard (args:UIKeyboardEventArgs) =
            UIView.BeginAnimations ("")
            UIView.SetAnimationCurve (args.AnimationCurve);
            UIView.SetAnimationDuration (args.AnimationDuration);
            let mutable viewFrame = this.View.Frame;
            let endRelative = this.View.ConvertRectFromView (args.FrameEnd, null);
            viewFrame.Height <- endRelative.Y;
            this.View.Frame <- viewFrame;
            UIView.CommitAnimations ()

