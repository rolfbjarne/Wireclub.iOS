// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.
//
// Adapted from: Toast by Charles Scalesse to F#
// https://github.com/scalessec/Toast
//
// ORIGINAL LICENSE:
//
//    Permission is hereby granted, free of charge, to any person obtaining a
//    copy of this software and associated documentation files (the
//    "Software"), to deal in the Software without restriction, including
//    without limitation the rights to use, copy, modify, merge, publish,
//    distribute, sublicense, and/or sell copies of the Software, and to
//    permit persons to whom the Software is furnished to do so, subject to
//    the following conditions:
//
//    The above copyright notice and this permission notice shall be included
//    in all copies or substantial portions of the Software.
//
//    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
//    OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//    IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//    CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//    TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//    SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

module Toast

open System
open System.Collections.Generic
open System.Linq
open Foundation
open UIKit
open CoreGraphics
open System.Drawing

//CONFIGURE THESE VALUES TO ADJUST LOOK & FEEL,
//DISPLAY DURATION, ETC.

// general appearance
let toastMaxWidth = nfloat 0.8f // 80% of parent view width
let toastMaxHeight = nfloat 0.8f // 80% of parent view height
let toastHorizontalPadding = nfloat 10.0f
let toastVerticalPadding = nfloat 10.0f
let toastCornerRadius = nfloat 10.0f
let toastOpacity = nfloat 0.8f
let toastFontSize = nfloat 16.0f
let toastMaxTitleLines  = nint 0
let toastMaxMessageLines = nint 0
let toastFadeDuration = 0.2

// shadow appearance
let toastShadowOpacity = 0.8f
let toastShadowRadius = nfloat 6.0f
let toastShadowOffset = new CGSize(nfloat 4.0f, nfloat 4.0f)
let toastDisplayShadow = true

// display duration and position
let toastDefaultPosition = "bottom"
let toastDefaultDuration  = 3.0

// image view size
let toastImageViewWidth = nfloat 80.0f
let toastImageViewHeight = nfloat 80.0f

// activity
let toastActivityWidth = 100.0f
let toastActivityHeight = 100.0f
let toastActivityDefaultPosition = "center"

// interaction
let toastHidesOnTap = true // excludes activity views

// associative reference keys
let toastTimerKey = "toastTimerKey"
let toastActivityViewKey = "toastActivityViewKey"

let sizeForString (str:string) (font:UIFont) (constrainedSize:CGSize) (lineBreakMode:UILineBreakMode) =
    let str = NSString.op_Explicit str 
    str.StringSize(font, constrainedSize, lineBreakMode)
//    if ([str respondsToSelector:@selector(boundingRectWithSize:options:attributes:context:)]) {
//        NSMutableParagraphStyle *paragraphStyle <- [[NSMutableParagraphStyle alloc] init]
//        paragraphStyle.lineBreakMode <- lineBreakMode
//        NSDictionary *attributes <- @{NSFontAttributeName:font, NSParagraphStyleAttributeName:paragraphStyle}
//        CGRect boundingRect <- [str boundingRectWithSize:constrainedSize options:NSStringDrawingUsesLineFragmentOrigin attributes:attributes context:null]
//        return CGSizeMake(ceilf(boundingRect.Size.Width), ceilf(boundingRect.Size.Height))
//    }

// Toast Methods

type UIView with
    member this.MakeToast (message:string) =
        this.MakeToast(message, toastDefaultDuration, toastDefaultPosition)

    member this.MakeToast (message:string, duration:float, position:string) =
        this.ShowToast ((this.ViewForMessage message null null), duration, position)

    member this.MakeToast (message:string, duration:float, position:string, title:string) =
        this.ShowToast ((this.ViewForMessage message title null), duration, position)

    member this.MakeToast (message:string, duration:float, position:string, image:UIImage) =
        this.ShowToast ((this.ViewForMessage message null image), duration, position)

    member this.MakeToast (message:string, duration:float, position:string, title:string, image:UIImage) =
        this.ShowToast ((this.ViewForMessage message title image), duration, position)

    member this.CenterPointForPosition (point:string) (toast:UIView) =
        match point with
        | "top" -> new CGPoint(this.Bounds.Size.Width / nfloat 2.0f, (toast.Frame.Size.Height / nfloat 2.0f) + toastVerticalPadding)
        | "bottom" -> new CGPoint(this.Bounds.Size.Width / nfloat 2.0f, (this.Bounds.Size.Height - (toast.Frame.Size.Height / nfloat 2.0f)) - toastVerticalPadding)
        | "center" -> new CGPoint(this.Bounds.Size.Width / nfloat 2.0f, this.Bounds.Size.Height / nfloat 2.0f)
        | _-> this.CenterPointForPosition toastDefaultPosition toast

//    if([point isKindOfClass:[NSString class]]) {
//        // convert string literals "top", "bottom", "center", or any point wrapped in an NSValue object into a CGPoint
//        if([point caseInsensitiveCompare:"top"] == NSOrderedSame) {        }
//        else if([point caseInsensitiveCompare:"bottom"] == NSOrderedSame) {        }
//        else if([point caseInsensitiveCompare:"center"] == NSOrderedSame) {        }
//    } else if ([point isKindOfClass:[NSValue class]]) {
//        return [point CGPointValue]
//    }

    member this.ShowToast(toast:UIView) =
        this.ShowToast (toast, toastDefaultDuration, toastDefaultPosition)

    member this.ShowToast(toast:UIView, duration:float, position:string) =
        toast.Center <- this.CenterPointForPosition position toast
        toast.Alpha <- nfloat 0.0f

        let timer:NSTimer ref = ref null
        if toastHidesOnTap then
            let recognizer =  new UITapGestureRecognizer(new Action<_>(fun _ -> timer.Value.Invalidate(); this.HideToast toast ))
            toast.AddGestureRecognizer recognizer
            toast.UserInteractionEnabled <- true
            toast.ExclusiveTouch <- true
        
        this.AddSubview(toast)

        UIView.Animate(
            toastFadeDuration,
            0.0,
            UIViewAnimationOptions.CurveEaseOut ||| UIViewAnimationOptions.AllowUserInteraction,
            (fun _ -> toast.Alpha <- nfloat 1.0f),
            (fun _ -> timer := NSTimer.CreateScheduledTimer(duration, (fun _ ->  this.HideToast toast ))))

    member this.HideToast (toast:UIView) =
        UIView.Animate(
            toastFadeDuration,
            0.0,
            UIViewAnimationOptions.CurveEaseIn ||| UIViewAnimationOptions.BeginFromCurrentState,
            (fun _ -> toast.Alpha <- nfloat 0.0f),
            (fun _ -> toast.RemoveFromSuperview()))


//#pragma mark - Toast Activity Methods
//
//member this.MakeToastActivity {
//    [self makeToastActivity:toastActivityDefaultPosition]
//}
//
//member this.MakeToastActivity:(id)position {
//    // sanity
//    UIView *existingActivityView <- (UIView *)objc_getAssociatedObject(self, &toastActivityViewKey)
//    if (existingActivityView <> null) return
//    
//    UIView *activityView <- [[UIView alloc] initWithFrame:new RectangleF(0, 0, toastActivityWidth, toastActivityHeight)]
//    activityView.center <- [self centerPointForPosition:position withToast:activityView]
//    activityView.backgroundColor <- [[UIColor blackColor] colorWithAlphaComponent:toastOpacity]
//    activityView.Alpha <- 0.0f
//    activityView.autoresizingMask <- (UIViewAutoresizingFlexibleLeftMargin | UIViewAutoresizingFlexibleRightMargin | UIViewAutoresizingFlexibleTopMargin | UIViewAutoresizingFlexibleBottomMargin)
//    activityView.layer.cornerRadius <- toastCornerRadius
//    
//    if (toastDisplayShadow) {
//        activityView.layer.shadowColor <- [UIColor blackColor].CGColor
//        activityView.layer.shadowOpacity <- toastShadowOpacity
//        activityView.layer.shadowRadius <- toastShadowRadius
//        activityView.layer.shadowOffset <- toastShadowOffset
//    }
//    
//    UIActivityIndicatorView *activityIndicatorView <- [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleWhiteLarge]
//    activityIndicatorView.center <- CGPointMake(activityView.Bounds.Size.Width / 2, activityView.Bounds.Size.Height / 2)
//    [activityView addSubview:activityIndicatorView]
//    [activityIndicatorView startAnimating]
//    
//    // associate the activity view with self
//    objc_setAssociatedObject (self, &toastActivityViewKey, activityView, OBJC_ASSOCIATION_RETAIN_NONATOMIC)
//    
//    [self addSubview:activityView]
//    
//    [UIView animateWithDuration:toastFadeDuration
//                          delay:0.0f
//                        options:UIViewAnimationOptionCurveEaseOut
//                     animations:^{
//                         activityView.Alpha <- 1.0f
//                     } completion:null]
//}
//
//let hideToastActivity {
//    UIView *existingActivityView <- (UIView *)objc_getAssociatedObject(self, &toastActivityViewKey)
//    if (existingActivityView <> null) {
//        [UIView animateWithDuration:toastFadeDuration
//                              delay:0.0f
//                            options:(UIViewAnimationOptionCurveEaseIn | UIViewAnimationOptionBeginFromCurrentState)
//                         animations:^{
//                             existingActivityView.Alpha <- 0.0f
//                         } completion:^(BOOL finished) {
//                             [existingActivityView removeFromSuperview]
//                             objc_setAssociatedObject (self, &toastActivityViewKey, null, OBJC_ASSOCIATION_RETAIN_NONATOMIC)
//                         }]
//    }
//}

    member this.ViewForMessage (message:string) (title:string) (image:UIImage) =
        // dynamically build a toast view with any combination of message, title, & image.
        let mutable messageLabel:UILabel = null
        let mutable titleLabel:UILabel = null
        let mutable imageView:UIImageView = null
        
        // create the parent view
        let wrapperView = new UIView()
        wrapperView.AutoresizingMask <- UIViewAutoresizing.FlexibleLeftMargin ||| UIViewAutoresizing.FlexibleRightMargin ||| UIViewAutoresizing.FlexibleTopMargin ||| UIViewAutoresizing.FlexibleBottomMargin
        wrapperView.Layer.CornerRadius <- toastCornerRadius
        
        if toastDisplayShadow then
            wrapperView.Layer.ShadowColor <- UIColor.Black.CGColor
            wrapperView.Layer.ShadowOpacity <- toastShadowOpacity
            wrapperView.Layer.ShadowRadius <- toastShadowRadius
            wrapperView.Layer.ShadowOffset <- toastShadowOffset

        wrapperView.BackgroundColor <- UIColor.Black.ColorWithAlpha toastOpacity
        
        if image <> null then
            imageView <- new UIImageView()
            imageView.ContentMode <- UIViewContentMode.ScaleAspectFit
            imageView.Frame <- new CGRect(toastHorizontalPadding, toastVerticalPadding, toastImageViewWidth, toastImageViewHeight)
        
        
        let imageWidth, imageHeight, imageLeft =
            match imageView with 
            | null -> nfloat 0.0f, nfloat 0.0f, nfloat 0.0f
            | imageView -> imageView.Bounds.Size.Width,imageView.Bounds.Size.Height,toastHorizontalPadding
        
        if title <> null then
            titleLabel <- new UILabel()
            titleLabel.Lines <- toastMaxTitleLines
            titleLabel.Font <-  UIFont.BoldSystemFontOfSize toastFontSize
            titleLabel.TextAlignment <- UITextAlignment.Left
            titleLabel.LineBreakMode <- UILineBreakMode.WordWrap
            titleLabel.TextColor <- UIColor.White
            titleLabel.BackgroundColor <- UIColor.Clear
            titleLabel.Alpha <- nfloat 1.0f
            titleLabel.Text <- title
            
            // size the title label according to the length of the text
            let maxSizeTitle = new CGSize((this.Bounds.Size.Width * toastMaxWidth) - imageWidth, this.Bounds.Size.Height * toastMaxHeight)
            let expectedSizeTitle = sizeForString title titleLabel.Font maxSizeTitle titleLabel.LineBreakMode
            titleLabel.Frame <- new CGRect(nfloat 0.0f, nfloat 0.0f, expectedSizeTitle.Width, expectedSizeTitle.Height)

        if message <> null then
            messageLabel <- new UILabel()
            messageLabel.Lines <- toastMaxMessageLines
            messageLabel.Font <- UIFont.SystemFontOfSize toastFontSize
            messageLabel.LineBreakMode <- UILineBreakMode.WordWrap
            messageLabel.TextColor <- UIColor.White
            messageLabel.BackgroundColor <- UIColor.Clear
            messageLabel.Alpha <- nfloat 1.0f
            messageLabel.Text <- message
            
            // size the message label according to the length of the text
            let maxSizeMessage = new CGSize((this.Bounds.Size.Width * toastMaxWidth) - imageWidth, this.Bounds.Size.Height * toastMaxHeight)
            let expectedSizeMessage = sizeForString message messageLabel.Font maxSizeMessage messageLabel.LineBreakMode
            messageLabel.Frame <- new CGRect(nfloat 0.0f, nfloat 0.0f, expectedSizeMessage.Width, expectedSizeMessage.Height)
        
        
        // titleLabel frame values
        let titleWidth, titleHeight, titleTop, titleLeft =
            match titleLabel with 
            | null -> nfloat 0.0f, nfloat 0.0f, nfloat 0.0f, nfloat 0.0f
            | titleLabel -> titleLabel.Bounds.Size.Width, titleLabel.Bounds.Size.Height, toastVerticalPadding, imageLeft + imageWidth + toastHorizontalPadding
        
        
        // messageLabel frame values
        let messageWidth, messageHeight, messageLeft, messageTop =
            match messageLabel with
            | null -> nfloat 0.0f, nfloat 0.0f, nfloat 0.0f, nfloat 0.0f
            | messageLabel -> messageLabel.Bounds.Size.Width,messageLabel.Bounds.Size.Height,imageLeft + imageWidth + toastHorizontalPadding,titleTop + titleHeight + toastVerticalPadding

        let longerWidth = max titleWidth messageWidth
        let longerLeft = max titleLeft messageLeft
        
        // wrapper width uses the longerWidth or the image width, whatever is larger. same logic applies to the wrapper height
        let wrapperWidth = max (imageWidth + (toastHorizontalPadding * nfloat 2.0f)) (longerLeft + longerWidth + toastHorizontalPadding)
        let wrapperHeight = max (messageTop + messageHeight + toastVerticalPadding) (imageHeight + (toastVerticalPadding * nfloat 2.0f))
                             
        wrapperView.Frame <- new CGRect(nfloat 0.0f, nfloat 0.0f, wrapperWidth, wrapperHeight)
        
        if titleLabel <> null then
            titleLabel.Frame <- new CGRect(titleLeft, titleTop, titleWidth, titleHeight)
            wrapperView.AddSubview titleLabel
        
        if messageLabel <> null then
            messageLabel.Frame <- new CGRect(messageLeft, messageTop, messageWidth, messageHeight)
            wrapperView.AddSubview messageLabel
        
        if imageView <> null then
            wrapperView.AddSubview imageView
        
        wrapperView

