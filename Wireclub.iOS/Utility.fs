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

module Image =
    open System
    open System.IO
    open System.Collections.Concurrent
    open System.Threading

    let images = ConcurrentDictionary<string, UIImage>()

    let cachePath url =
        // ## TODO Android tmp directory
        let documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments)
        let cache = Path.Combine (documents, "..", "Library", "Caches")
        Path.Combine (cache, System.Text.RegularExpressions.Regex.Replace(url, "[^\w]", "_"))

    let tryAcquireFromCache url =
        match images.TryGetValue url with
        | true, image -> Some image
        | false, _ -> None

    let tryAcquireFromDisk context url = async {
        let file = cachePath url
        match File.Exists file with
        | true -> 
            // Must be on the ui thread to create images
            do! Async.SwitchToContext context
            let image = UIImage.FromFile  file
            return Some (images.AddOrUpdate (url, image, (fun _ i -> i)))
        | false -> return None
    }

    let tryAcquireFromServer context url = async {
        let! image = Api.req<byte[]> url "get" []
        match image with
        | Api.ApiOk data ->             
            use stream = new FileStream(cachePath url, FileMode.Create)
            do! stream.AsyncWrite data
            //return! tryAcquireFromDisk context url
            let image = UIImage.LoadFromData (NSData.FromArray data)
            return Some (images.AddOrUpdate (url, image, (fun _ i -> i)))
        | error -> 
            printfn "Failed to load image: %A" error
            return None
    }

    // Agent handles loading remote images, requests will be processed serially
    type Request = (string * SynchronizationContext) * AsyncReplyChannel<UIImage option>
    let agent = MailboxProcessor<Request>.Start(fun inbox ->
        let rec loop () = async {
            let! ((url, context), replyChannel) = inbox.Receive ()
            let! image = tryAcquireFromServer context url
            replyChannel.Reply image

            return! loop ()
        }

        loop ()
    )

    let tryAcquire context url = async {
        let image = tryAcquireFromCache url
        if image <> None then 
            return image
        else
            let! image = tryAcquireFromDisk context url
            if image <> None then 
                return image
            else
                return! agent.PostAndAsyncReply (fun replyChannel -> (url, context), replyChannel)
    }
