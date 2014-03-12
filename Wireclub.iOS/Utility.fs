namespace Wireclub.iOS

open System
open System.Drawing
open System.Globalization

open MonoTouch.Foundation
open MonoTouch.UIKit

type AlertDelegate (action: int -> unit) =
    inherit UIAlertViewDelegate ()
    override this.Dismissed (view, index) = 
        action index

module Navigation =
    let mutable navigate: (string -> (Entity option) -> unit) = (fun _ _ -> failwith "No navigation handler attached")

module Async =
    let startWithContinuation (computation: Async<'T>) (continuation: 'T -> unit) =
        Async.StartWithContinuations (computation, continuation, (fun ex -> raise ex), (fun _ -> ()))

module List =
    let rec nextTuple list =
        match list with
        | [] | [_] -> []
        | head::tail ->
            [
                yield head, tail.Head
                yield! nextTuple tail
            ]

    let nextPrevTuple list =
        let rec nextPrevTuple prev list =
            match list with
            | [] -> []
            | [current] -> [prev, current, None]
            | head::tail ->
                [
                    yield prev, head, Some tail.Head
                    yield! nextPrevTuple (Some head) tail
                ]

        nextPrevTuple None list

[<AutoOpen>]
module Utility =
    let showSimpleAlert title message button =
        let alert = new UIAlertView (Title=title, Message=message)
        alert.AddButton button |> ignore
        alert.Show ()
        ()

    //make a custom mapping for each OS based on supported fonts
    let fontFamily id =
        match id with
        | 1 -> "Arial"
        | 2 -> "Courier New"
        | 3 -> "Georgia"
        | 4 -> "Times New Roman"
        | 5 -> "Trebuchet"
        | 6 -> "Lucida Sans"
        | 7 -> "Comic Sans MS"
        | _ -> "Arial"

    let cssToColor (color:string) =
        let color = 
            match color.ToCharArray() with 
            | [| '#'; r; g; b; |] -> new String([| r; r; g; g; b; b; |])
            | _ -> color.Substring(1, 6)

        let color = Int32.Parse(color, NumberStyles.HexNumber)
            
        UIColor.FromRGBA(
            (byte)((color >>> 0x10) &&& 0xff),
            (byte)((color >>> 8) &&& 0xff),
            (byte)(color &&& 0xff),
            (byte)0xff)

    let colorToCss (color:UIColor) = 
        match color.GetRGBA () with
        | red, green, blue, _ -> sprintf "#%02x%02x%02x" (int (red * 255.0f)) (int (green * 255.0f)) (int (blue * 255.0f))

    let grayBackground = cssToColor "#e9eaef"
    let grayLightAccent = cssToColor "#f0f1f6"
    let grayDarkAccent = cssToColor "#dcdde1"
    let wireclubBlue = cssToColor "#3287D6"
    let lighterText = cssToColor "#999"
    let dialogBorder = cssToColor "#666"

    type UIViewController with
        member this.HandleApiFailure<'A> (result:Api.ApiResult<'A>) =
            match result with
            | Api.ApiOk _ -> ()
            | Api.BadRequest [] -> showSimpleAlert "Error" "An error occured submitting your request" "Close"
            | Api.BadRequest ({Key=key; Value=value}::_) -> showSimpleAlert key value "Close"
            | _ -> printfn "Api Failure: %A" result

        member this.HandleApiResult<'A> (result:Api.ApiResult<'A>) =
            match result with
            | Api.ApiOk result -> ()
            | failure -> this.HandleApiFailure failure
            result

        member this.ResizeViewToKeyboard (args:UIKeyboardEventArgs) =
            let view = this.View.Subviews.[0]
            UIView.BeginAnimations ("")
            UIView.SetAnimationCurve (args.AnimationCurve);
            UIView.SetAnimationDuration (args.AnimationDuration);
            let mutable viewFrame = view.Frame;
            let endRelative = view.ConvertRectFromView (args.FrameEnd, null);
            viewFrame.Height <- endRelative.Y;
            view.Frame <- viewFrame;
            UIView.CommitAnimations ()

    type UIWebView with
        member this.ScrollToBottom () =
            this.EvaluateJavascript 
                (sprintf "window.scrollBy(0, %i);" (int (this.EvaluateJavascript "document.body.offsetHeight;"))) |> ignore

        member this.SetBodyBackgroundColor (color:string) =
            this.EvaluateJavascript 
                (sprintf "document.body.style.backgroundColor = '%s';" color) |> ignore

        member this.PreloadImages urls =
            this.EvaluateJavascript 
                (String.concat ";" [ yield "var preload = new Image()"; for url in urls do yield sprintf "preload.src = '%s'" url ]) |> ignore


module Image =
    open System
    open System.IO
    open System.Collections.Concurrent
    open System.Threading

    let placeholder = UIImage.FromFile "Images/Placeholder.png"
    let placeholderChat = UIImage.FromFile "Images/PlaceholderChat.png"
    let placeholderMale = UIImage.FromFile "Images/PlaceholderMale.png"
    let placeholderFemale = UIImage.FromFile "Images/PlaceholderFemale.png"

    let images = ConcurrentDictionary<string, UIImage>()

    let resize size (image:UIImage) =
        UIGraphics.BeginImageContext(size)
        image.Draw(new RectangleF(0.0f, 0.0f, size.Width, size.Height))
        let image = UIGraphics.GetImageFromCurrentImageContext()
        UIGraphics.EndImageContext()
        image

    let cachePath url =
        let documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments)
        let cache = Path.Combine (documents, "..", "Library", "Caches")
        Path.Combine (cache.Replace("/Documents/..", String.Empty), System.Text.RegularExpressions.Regex.Replace(url, "[^\w]", "_"))

    let tryAcquireFromCache url =
        match images.TryGetValue url with
        | true, image -> Some image
        | false, _ -> None

    let tryAcquireFromDisk url =
        let file = cachePath url
        match File.Exists file with
        | true -> Some (images.AddOrUpdate (url, (UIImage.FromFile  file), (fun _ i -> i)))
        | false -> None

    let tryAcquireFromServer url = async {
        let! image = Api.req<byte[]> url "get" []
        match image with
        | Api.ApiOk data ->             
            use stream = new FileStream(cachePath url, FileMode.Create)
            do! stream.AsyncWrite data
            return Some data
        | error -> 
            printfn "Failed to load image: %A" error
            return None
    }

    // Agent handles loading remote images, requests will be processed serially
    type Request = (string) * AsyncReplyChannel<byte[] option>
    let agent = MailboxProcessor<Request>.Start(fun inbox ->
        let rec loop () = async {
            let! (url, replyChannel) = inbox.Receive ()
            let! image = tryAcquireFromServer url
            replyChannel.Reply image
            return! loop ()
        }
        loop ()
    )

    // Continuation may be called twice, to set the placeholder and then later to set the final image
    let loadImageWithContinuation url placeholder (continuation: UIImage -> bool -> unit) =
        match tryAcquireFromCache url with
        | Some image -> continuation image true
        | None ->
            match tryAcquireFromDisk url with
            | Some image -> continuation image true
            | None ->
                continuation placeholder true // Set the placeholder while we load
                Async.startWithContinuation
                    (agent.PostAndAsyncReply (fun replyChannel -> url, replyChannel))
                    (function
                        | Some data ->
                            let image = UIImage.LoadFromData (NSData.FromArray data)
                            continuation (images.AddOrUpdate (url, image, (fun _ i -> i))) false
                        | None -> ()
                    )

    let loadImageForView url placeholder (imageView:UIImageView) =
        loadImageWithContinuation url placeholder (fun image _ -> imageView.Image <- image)

    let loadImageForCell url placeholder (cell:UITableViewCell) (table:UITableView) =
        let tag = cell.Tag // Cache the cell id, from this point on just assume it is no longer valid (due to cell reuse)
        loadImageWithContinuation url placeholder (fun image fromCache ->
            if fromCache then
                cell.ImageView.Image <- image
            else
                // Find the cell if it is still visible in the table
                match table.VisibleCells |> Array.tryFind (fun c -> c.Tag = tag) with
                | Some cell -> cell.ImageView.Image <- image
                | None -> ()
        )


