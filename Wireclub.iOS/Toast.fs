module Toast

open System
open System.Collections.Generic
open System.Linq
open MonoTouch.Foundation
open MonoTouch.UIKit
open MonoTouch.CoreGraphics
open System.Drawing

//CONFIGURE THESE VALUES TO ADJUST LOOK & FEEL,
//DISPLAY DURATION, ETC.

// general appearance
let toastMaxWidth = 0.8f // 80% of parent view width
let toastMaxHeight = 0.8f // 80% of parent view height
let toastHorizontalPadding = 10.0f
let toastVerticalPadding = 10.0f
let toastCornerRadius = 10.0f
let toastOpacity = 0.8f
let toastFontSize = 16.0f
let toastMaxTitleLines  = 0
let toastMaxMessageLines = 0
let toastFadeDuration = 0.2f

// shadow appearance
let toastShadowOpacity = 0.8f
let toastShadowRadius = 6.0f
let toastShadowOffset = new SizeF(4.0f, 4.0f)
let toastDisplayShadow = true

// display duration and position
let toastDefaultPosition = "bottom"
let int toastDefaultDuration  = 3.0f

// image view size
let toastImageViewWidth = 80.0f
let toastImageViewHeight = 80.0f

// activity
let toastActivityWidth = 100.0f
let toastActivityHeight = 100.0f
let toastActivityDefaultPosition = "center"

// interaction
let toastHidesOnTap = true // excludes activity views

// associative reference keys
let toastTimerKey = "toastTimerKey"
let toastActivityViewKey = "toastActivityViewKey"


// Toast Methods
//
//let makeToast (message:string) = makeToast message toastDefaultDuration toastDefaultPosition
//
//let makeToast (message:string) (duration:int) (position:string) =
//    let toast = viewForMessage message None None
//    showToast toast duration position
//
//let makeToast (message:string) (duration:int) (position:string) (title:string) =
//    let toast = viewForMessage message title None
//    showToast toast duration position
//
//let makeToast:(NSString *)message duration:(NSTimeInterval)duration position:(id)position image:(UIImage *)image {
//    let toast = viewForMessage message title None
//    showToast toast duration position
//}
//
//let makeToast:(NSString *)message duration:(NSTimeInterval)duration  position:(id)position title:(NSString *)title image:(UIImage *)image {
//    UIView *toast = [self viewForMessage:message title:title image:image]
//    [self showToast:toast duration:duration position:position]  
//}
//
//let showToast (toast:UIView) =
//    [self showToast:toast duration:toastDefaultDuration position:toastDefaultPosition]
//
//
//let showToast:(UIView *)toast duration:(NSTimeInterval)duration position:(id)point {
//    toast.center <- [self centerPointForPosition:point withToast:toast]
//    toast.alpha <- 0.0f
//    
//    if (toastHidesOnTap) {
//        UITapGestureRecognizer *recognizer <- [[UITapGestureRecognizer alloc] initWithTarget:toast action:@selector(handleToastTapped:)]
//        [toast addGestureRecognizer:recognizer]
//        toast.userInteractionEnabled <- true
//        toast.exclusiveTouch <- true
//    }
//    
//    [self addSubview:toast]
//    
//    [UIView animateWithDuration:toastFadeDuration
//                          delay:0.0f
//                        options:(UIViewAnimationOptionCurveEaseOut | UIViewAnimationOptionAllowUserInteraction)
//                     animations:^{
//                         toast.alpha <- 1.0f
//                     } completion:^(BOOL finished) {
//                         NSTimer *timer <- [NSTimer scheduledTimerWithTimeInterval:duration target:self selector:@selector(toastTimerDidFinish:) userInfo:toast repeats:NO]
//                         // associate the timer with the toast view
//                         objc_setAssociatedObject (toast, &toastTimerKey, timer, OBJC_ASSOCIATION_RETAIN_NONATOMIC)
//                     }]
//    
//}
//
//let hideToast:(UIView *)toast {
//    [UIView animateWithDuration:toastFadeDuration
//                          delay:0.0f
//                        options:(UIViewAnimationOptionCurveEaseIn | UIViewAnimationOptionBeginFromCurrentState)
//                     animations:^{
//                         toast.alpha <- 0.0f
//                     } completion:^(BOOL finished) {
//                         [toast removeFromSuperview]
//                     }]
//}
//
//#pragma mark - Events
//
//let toastTimerDidFinish:(NSTimer *)timer {
//    [self hideToast:(UIView *)timer.userInfo]
//}
//
//let handleToastTapped:(UITapGestureRecognizer *)recognizer {
//    NSTimer *timer <- (NSTimer *)objc_getAssociatedObject(self, &toastTimerKey)
//    [timer invalidate]
//    
//    [self hideToast:recognizer.view]
//}
//
//#pragma mark - Toast Activity Methods
//
//let makeToastActivity {
//    [self makeToastActivity:toastActivityDefaultPosition]
//}
//
//let makeToastActivity:(id)position {
//    // sanity
//    UIView *existingActivityView <- (UIView *)objc_getAssociatedObject(self, &toastActivityViewKey)
//    if (existingActivityView <> null) return
//    
//    UIView *activityView <- [[UIView alloc] initWithFrame:new RectangleF(0, 0, toastActivityWidth, toastActivityHeight)]
//    activityView.center <- [self centerPointForPosition:position withToast:activityView]
//    activityView.backgroundColor <- [[UIColor blackColor] colorWithAlphaComponent:toastOpacity]
//    activityView.alpha <- 0.0f
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
//    activityIndicatorView.center <- CGPointMake(activityView.bounds.size.width / 2, activityView.bounds.size.height / 2)
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
//                         activityView.alpha <- 1.0f
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
//                             existingActivityView.alpha <- 0.0f
//                         } completion:^(BOOL finished) {
//                             [existingActivityView removeFromSuperview]
//                             objc_setAssociatedObject (self, &toastActivityViewKey, null, OBJC_ASSOCIATION_RETAIN_NONATOMIC)
//                         }]
//    }
//}
//
//#pragma mark - Helpers
//
//- (CGPoint)centerPointForPosition:(id)point withToast:(UIView *)toast {
//    if([point isKindOfClass:[NSString class]]) {
//        // convert string literals "top", "bottom", "center", or any point wrapped in an NSValue object into a CGPoint
//        if([point caseInsensitiveCompare:"top"] == NSOrderedSame) {
//            return CGPointMake(self.bounds.size.width/2, (toast.frame.size.height / 2) + toastVerticalPadding)
//        } else if([point caseInsensitiveCompare:"bottom"] == NSOrderedSame) {
//            return CGPointMake(self.bounds.size.width/2, (self.bounds.size.height - (toast.frame.size.height / 2)) - toastVerticalPadding)
//        } else if([point caseInsensitiveCompare:"center"] == NSOrderedSame) {
//            return CGPointMake(self.bounds.size.width / 2, self.bounds.size.height / 2)
//        }
//    } else if ([point isKindOfClass:[NSValue class]]) {
//        return [point CGPointValue]
//    }
//    
//    NSLog("Warning: Invalid position for toast.")
//    return [self centerPointForPosition:toastDefaultPosition withToast:toast]
//}

let sizeForString (str:string) (font:UIFont) (constrainedSize:SizeF) (lineBreakMode:UILineBreakMode) =
    let str = NSString.op_Explicit str 
//    if ([str respondsToSelector:@selector(boundingRectWithSize:options:attributes:context:)]) {
//        NSMutableParagraphStyle *paragraphStyle <- [[NSMutableParagraphStyle alloc] init]
//        paragraphStyle.lineBreakMode <- lineBreakMode
//        NSDictionary *attributes <- @{NSFontAttributeName:font, NSParagraphStyleAttributeName:paragraphStyle}
//        CGRect boundingRect <- [str boundingRectWithSize:constrainedSize options:NSStringDrawingUsesLineFragmentOrigin attributes:attributes context:null]
//        return CGSizeMake(ceilf(boundingRect.size.width), ceilf(boundingRect.size.height))
//    }

    str.StringSize(font, constrainedSize, lineBreakMode)

type UIView with

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
            imageView.Frame <- new RectangleF(toastHorizontalPadding, toastVerticalPadding, toastImageViewWidth, toastImageViewHeight)
        
        
        let imageWidth, imageHeight, imageLeft =
            match imageView with 
            | null -> 0.0f,0.0f,0.0f
            | imageView -> imageView.Bounds.Size.Width,imageView.Bounds.Size.Height,toastHorizontalPadding
        
        if title <> null then
            titleLabel <- new UILabel()
            titleLabel.Lines <- toastMaxTitleLines
            titleLabel.Font <-  UIFont.BoldSystemFontOfSize toastFontSize
            titleLabel.TextAlignment <- UITextAlignment.Left
            titleLabel.LineBreakMode <- UILineBreakMode.WordWrap
            titleLabel.TextColor <- UIColor.White
            titleLabel.BackgroundColor <- UIColor.Clear
            titleLabel.Alpha <- 1.0f
            titleLabel.Text <- title
            
            // size the title label according to the length of the text
            let maxSizeTitle = new SizeF((this.Bounds.Size.Width * toastMaxWidth) - imageWidth, this.Bounds.Size.Height * toastMaxHeight)
            let expectedSizeTitle = sizeForString title titleLabel.Font maxSizeTitle titleLabel.LineBreakMode
            titleLabel.Frame <- new RectangleF(0.0f, 0.0f, expectedSizeTitle.Width, expectedSizeTitle.Height)

        if message <> null then
            messageLabel <- new UILabel()
            messageLabel.Lines <- toastMaxMessageLines
            messageLabel.Font <- UIFont.SystemFontOfSize toastFontSize
            messageLabel.LineBreakMode <- UILineBreakMode.WordWrap
            messageLabel.TextColor <- UIColor.White
            messageLabel.BackgroundColor <- UIColor.Clear
            messageLabel.Alpha <- 1.0f
            messageLabel.Text <- message
            
            // size the message label according to the length of the text
            let maxSizeMessage = new SizeF((this.Bounds.Size.Width * toastMaxWidth) - imageWidth, this.Bounds.Size.Height * toastMaxHeight)
            let expectedSizeMessage = sizeForString message messageLabel.Font maxSizeMessage messageLabel.LineBreakMode
            messageLabel.Frame <- new RectangleF(0.0f, 0.0f, expectedSizeMessage.Width, expectedSizeMessage.Height)
        
        
        // titleLabel frame values
        let titleWidth, titleHeight, titleTop, titleLeft =
            match titleLabel with 
            | null -> 0.0f, 0.0f, 0.0f, 0.0f
            | titleLabel -> titleLabel.Bounds.Size.Width, titleLabel.Bounds.Size.Height, toastVerticalPadding, imageLeft + imageWidth + toastHorizontalPadding
        
        
        // messageLabel frame values
        let messageWidth, messageHeight, messageLeft, messageTop =
            match messageLabel with
            | null -> 0.0f,0.0f,0.0f,0.0f
            | messageLabel -> messageLabel.Bounds.Size.Width,messageLabel.Bounds.Size.Height,imageLeft + imageWidth + toastHorizontalPadding,titleTop + titleHeight + toastVerticalPadding

        let longerWidth = Math.Max(titleWidth, messageWidth)
        let longerLeft = Math.Max(titleLeft, messageLeft)
        
        // wrapper width uses the longerWidth or the image width, whatever is larger. same logic applies to the wrapper height
        let wrapperWidth = Math.Max((imageWidth + (toastHorizontalPadding * 2.0f)), (longerLeft + longerWidth + toastHorizontalPadding))
        let wrapperHeight = Math.Max((messageTop + messageHeight + toastVerticalPadding), (imageHeight + (toastVerticalPadding * 2.0f)))
                             
        wrapperView.Frame <- new RectangleF(0.0f, 0.0f, wrapperWidth, wrapperHeight)
        
        if titleLabel <> null then
            titleLabel.Frame <- new RectangleF(titleLeft, titleTop, titleWidth, titleHeight)
            wrapperView.AddSubview titleLabel
        
        if messageLabel <> null then
            messageLabel.Frame <- new RectangleF(messageLeft, messageTop, messageWidth, messageHeight)
            wrapperView.AddSubview messageLabel
        
        if imageView <> null then
            wrapperView.AddSubview imageView
        
        wrapperView

