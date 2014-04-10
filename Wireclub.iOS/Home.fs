// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

namespace Wireclub.iOS

open System
open System.Text.RegularExpressions
open System.Linq
open System.Drawing
open System.Globalization
open System.Web

open MonoTouch.Foundation
open MonoTouch.UIKit

open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models

open Newtonsoft.Json

open ChannelEvent

[<Register ("HomeViewController")>]
type HomeViewController () as controller =
    inherit UIViewController ()

    let controllers =
        lazy
            ([|
                new ChatsViewController () :> UIViewController
                new FriendsViewController () :> UIViewController
                new ChatDirectoryViewController () :> UIViewController
                Resources.menuStoryboard.Value.InstantiateInitialViewController() :?> UIViewController
            |])

    let changeTab index =
        for controller in controllers.Value do
            controller.View.Hidden <- true

        controllers.Value.[index].View.Hidden <- false
        controller.NavigationItem.Title <-
            match index with
            | 3 -> "More"
            | item -> controller.TabBar.Items.[index].Title 


    let tabBarDelegate =
        { 
            new UITabBarDelegate() with
            override this.ItemSelected (bar, item) = changeTab (item.Tag)
        }

    
    [<Outlet>]
    member val TabBar: UITabBar = null with get, set

    [<Outlet>]
    member val ContentView: UIView = null with get, set

    member val ChatsController = controllers.Value.[0] :?> ChatsViewController

    override this.ViewDidLoad () =
        base.ViewDidLoad ()
        printfn "[Home:Load]" 
        this.NavigationItem.HidesBackButton <- true
            
        this.AutomaticallyAdjustsScrollViewInsets <- false

        // Set up the child controllers
        let frame = System.Drawing.RectangleF(0.0f, 0.0f, this.ContentView.Bounds.Width, this.ContentView.Bounds.Height)       
        for controller in controllers.Value do
            this.AddChildViewController controller
            this.ContentView.AddSubview controller.View
            controller.View.Frame <- frame

        
        this.TabBar.Delegate <- tabBarDelegate
        this.TabBar.SelectedItem <- this.TabBar.Items.First()
        changeTab 0

[<Register ("EntryViewController")>]
type EntryViewController () as controller =
    inherit UIViewController ()

    let mutable rootController = new HomeViewController()
    let loginController = lazy (Resources.loginStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)
    let editProfileController = lazy (Resources.editProfileStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)

    let handleEvent channel (event:ChannelEvent.ChannelEvent) =
        controller.InvokeOnMainThread (fun _ -> 
            let handleNotifications (entity:Entity) preview =
                let read, historyType =
                    match controller.NavigationController.VisibleViewController with
                    | :? PrivateChatSessionViewController as controller ->
                        controller.User.Id = entity.Id, DB.ChatHistoryType.PrivateChat
                    | :? ChatRoomViewController as controller ->
                        controller.Room.Id = entity.Id, DB.ChatHistoryType.ChatRoom
                    | _ -> false, DB.ChatHistoryType.None
                
                Async.startWithContinuation
                    (async {
                        do! DB.createChatHistory (entity, historyType, (Some (preview, read)))
                        if historyType = DB.ChatHistoryType.PrivateChat then
                            do! DB.createChatHistoryEvent entity historyType (JsonConvert.SerializeObject(event))
                     })
                    (fun _ -> 
                        rootController.ChatsController.Reload ()

                        let reloadNavigationItem (controller:UIViewController) =
                            Async.startWithContinuation 
                                (DB.fetchChatHistoryUnreadCount())
                                (function
                                    | 0 ->
                                        controller.NavigationItem.LeftBarButtonItem <- null
                                    | unread ->
                                        controller.NavigationItem.LeftBarButtonItem <-
                                            new UIBarButtonItem((sprintf "(%i)" unread), UIBarButtonItemStyle.Plain, new EventHandler(fun _ _ -> 
                                                controller.NavigationController.PopViewControllerAnimated true |> ignore
                                            ))
                                )
                        match controller.NavigationController.VisibleViewController with
                            | :? PrivateChatSessionViewController as controller -> reloadNavigationItem (controller :> UIViewController)
                            | :? ChatRoomViewController as controller -> reloadNavigationItem (controller :> UIViewController)
                            | _ -> ()
                    )

            let stripHtml html = 
                html |> String.stripHtml |> HttpUtility.HtmlDecode
    

            match event with
            //Private chat event
            | { Event = PrivateMessage (color, font, message) }
            | { Event = PrivateMessageSent (color, font, message) } ->
                match ChatSessions.sessions.TryGetValue event.User with
                | true, (user, controller) ->
                    controller.HandleChannelEvent event
                    handleNotifications user (stripHtml message)
                | _ -> ChatSessions.startById event.User (fun user controller -> 
                    controller.HandleChannelEvent event
                    handleNotifications user (stripHtml message)
                )

            | { Event = Notification message }
            | { Event = Message (_, _, message)} -> 
                match ChatRooms.rooms.TryGetValue channel with
                | true, (room, controller) ->
                    controller.HandleChannelEvent event
                    handleNotifications room (stripHtml message)
                | _ -> ChatRooms.joinById channel (fun room controller ->
                    controller.HandleChannelEvent event
                    handleNotifications room (stripHtml message)
                )
            | _ ->
                match ChatRooms.rooms.TryGetValue channel with
                | true, (room, controller) ->
                    controller.HandleChannelEvent event
                | _ -> ChatRooms.joinById channel (fun room controller ->
                    controller.HandleChannelEvent event
                )
        )

    let resolvableHosts =
        [
            "www.wireclub.com"
            "dev.wireclub.com"
            (new Uri(Api.baseUrl)).Host
        ]

    let openExternal (uri:Uri) = 
        let supportsChrome = UIApplication.SharedApplication.CanOpenUrl (NSUrl.FromString "googlechrome://")
        let openExternal (builder:UriBuilder) =
            let uri = builder.Uri
            UIApplication.SharedApplication.OpenUrl (new NSUrl (uri.GetComponents (UriComponents.HttpRequestUrl, UriFormat.UriEscaped))) |> ignore

        match uri.Scheme with
        | "http" when supportsChrome -> openExternal (new UriBuilder(uri, Scheme = "googlechrome"))
        | "https" when supportsChrome -> openExternal (new UriBuilder(uri, Scheme = "googlechromes"))
        | _ -> openExternal (new UriBuilder(uri))

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        let objOrFail = function | Some o -> o | _ -> failwith "Expected Some"
        let navigate url (data:Entity option) =
            let uri = new Uri(url)

            printfn "[Navigate] %s" (uri.ToString())
            match url, data with
            | Routes.User id, data -> 
                let pushUser user =
//                    let controller = Resources.userStoryboard.Value.InstantiateInitialViewController () :?> UITabBarController
//
//                    for controller in controller.ChildViewControllers do
//                        let controller = controller :?> UserBaseViewController
//                        controller.User <- Some user

                    let controller = Resources.userStoryboard.Value.InstantiateInitialViewController () :?> UserViewController
                    controller.Entity <- Some user
                    this.NavigationController.PushViewController (controller, true)

                match data with
                | Some data -> pushUser data
                | None ->
                     Async.startNetworkWithContinuation
                        (User.entityBySlug id)
                        (this.HandleApiResult >> function
                            | Api.ApiOk data -> pushUser data
                            | error -> this.HandleApiFailure error
                        )

            | Routes.ChatRoom id, data ->
                let pushRoom room =
                    this.NavigationController.PopToViewController (rootController, false) |> ignore
                    let controller = ChatRooms.join room
                    this.NavigationController.PushViewController (controller, true)

                match data with
                | Some data -> pushRoom data
                | None ->
                     Async.startNetworkWithContinuation
                        (Chat.entityBySlug id)
                        (this.HandleApiResult >> function
                            | Api.ApiOk data -> pushRoom data
                            | error -> this.HandleApiFailure error
                        )

            | Routes.ChatSession id, Some data ->
                this.NavigationController.PopToViewController (rootController, false) |> ignore // Straight yolo
                let controller = ChatSessions.start data
                this.NavigationController.PushViewController (controller, true)

            | Routes.YouTube video, _ ->
                new Uri (sprintf "https://www.youtube.com/watch?v=%s" video) |> openExternal
            | Routes.ExternalRedirect _, _ -> uri |> openExternal
            | "/home", _ ->
                if this.NavigationController.ViewControllers.Contains rootController then
                    this.NavigationController.PopToViewController (rootController, true) |> ignore
                else
                    this.NavigationController.PopToViewController (this, false) |> ignore
                    this.NavigationController.PushViewController(rootController, true)
            | "/logout", _ ->
                NSUserDefaults.StandardUserDefaults.RemoveObject("auth-token")
                NSUserDefaults.StandardUserDefaults.Synchronize () |> ignore
                Async.startWithContinuation
                    (async {
                        do! DB.deleteAll<DB.ChatHistory>()
                        do! DB.deleteAll<DB.ChatHistoryEvent>()
                    })
                    (fun _ -> ())
                Account.logout ()
                ChannelClient.close()

                this.NavigationController.PopToRootViewController(true) |> ignore
                rootController <- new HomeViewController()
            | url, _ -> 
                this.NavigationController.PushViewController (new DialogViewController (url), true)

        Navigation.navigate <- navigate

        printfn "[Entry:Load]"

    override this.ViewDidAppear (animated) =
        // When the user is authenticated start the channel client and push the main app controller
        let proceed animated =
            ChannelClient.init handleEvent

            match Api.userIdentity.Value.Membership with
            | MembershipTypePublic.Pending -> this.NavigationController.PushViewController (editProfileController.Value, true)
            | _ -> this.NavigationController.PushViewController(rootController, animated)

        let defaults = NSUserDefaults.StandardUserDefaults
        match defaults.StringForKey "auth-token", System.String.IsNullOrEmpty Api.userId with
        // User has not entered an account
        | null, _ -> 
            this.NavigationController.PushViewController (loginController.Value, false)

        // User has an account but has not authenticated with the api
        | token, true -> 
            Async.startNetworkWithContinuation
                (async {
                    let! login = Account.loginToken token
                    do! Error.report ()
                    return login
                })
                (function
                    | Api.ApiOk identity -> proceed true
                    | _ -> this.NavigationController.PushViewController (loginController.Value, true)
                )

        // User is fully authenticated already
        | _, false -> proceed false
            
       
