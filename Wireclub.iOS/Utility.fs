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
