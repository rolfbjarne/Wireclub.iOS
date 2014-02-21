module Resources

open MonoTouch.Foundation
open MonoTouch.UIKit

// Storyboards
let menuStoryboard = lazy (UIStoryboard.FromName ("MegaMenu", null))
let loginStoryboard = lazy (UIStoryboard.FromName ("Login", null))
let userStoryboard = lazy (UIStoryboard.FromName ("User", null))
let editProfileStoryboard = lazy(UIStoryboard.FromName ("EditProfile", null))