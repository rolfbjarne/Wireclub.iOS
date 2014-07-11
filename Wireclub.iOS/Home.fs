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
    inherit RootViewContoller ()

    let controllers =
        lazy
            ([|
                new ChatsViewController (controller) :> UIViewController
                new FriendsViewController (controller) :> UIViewController
                new ChatDirectoryViewController (controller) :> UIViewController
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
            controller.View.Frame <- frame
            this.AddChildViewController controller
            this.ContentView.AddSubview controller.View

        this.TabBar.Delegate <- tabBarDelegate
        this.TabBar.SelectedItem <- this.TabBar.Items.First()
        changeTab 0

    override this.Tabs with get () = this.TabBar

    override this.SetOnlineStatus(status:OnlineStateType) =
        let statusDescription =
            match status with
            | OnlineStateType.Visible -> "Online"
            | OnlineStateType.Idle -> "Away"
            | _ -> "Offline"

        let showStatus () =
            let alert = new UIAlertView (Title = "Change Online Status", Message = sprintf "Currently: %s" statusDescription)
            alert.AddButton "Online" |> ignore
            alert.AddButton "Away" |> ignore
            alert.AddButton "Offline" |> ignore
            alert.AddButton "Cancel" |> ignore
            alert.Show ()
            alert.Dismissed.Add(fun args ->
                let status =
                    match args.ButtonIndex with
                        | 0 -> Some OnlineStateType.Visible
                        | 1 -> Some OnlineStateType.Idle
                        | 2 -> Some OnlineStateType.Invisible
                        | _ -> None

                match status with
                | Some status ->
                    this.SetOnlineStatus status
                    Async.startNetworkWithContinuation
                        (PrivateChat.changeOnlineState status)
                        (function
                            | Api.ApiOk _ -> ()
                            | error -> this.HandleApiFailure error
                        )
                | _-> ()
            )

        match status with
        | OnlineStateType.Visible
        | OnlineStateType.Idle
        | OnlineStateType.Invisible
        | OnlineStateType.Offline ->
            this.NavigationItem.RightBarButtonItem <- new UIBarButtonItem(statusDescription, UIBarButtonItemStyle.Bordered, new EventHandler(fun (s:obj) (e:EventArgs) -> showStatus()))
        | _->
            this.NavigationItem.RightBarButtonItem <- null

        

[<Register ("EntryViewController")>]
type EntryViewController () as controller =
    inherit UIViewController ()

    let mutable rootController = new HomeViewController()
    let loginController = lazy (Resources.loginStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)
    let editProfileController = lazy (Resources.editProfileStoryboard.Value.InstantiateInitialViewController() :?> UIViewController)

    let reloadNavigationItem (controller:UIViewController) =
        Async.startInBackgroundWithContinuation 
            (fun _ -> DB.fetchChatHistoryUnreadCount())
            (function
                | 0 ->
                    rootController.TabBar.Items.[0].BadgeValue <- null
                    controller.NavigationItem.LeftBarButtonItem <- null
                | unread ->
                    rootController.TabBar.Items.[0].BadgeValue <- string unread
                    controller.NavigationItem.LeftBarButtonItem <-
                        new UIBarButtonItem((sprintf "(%i)" unread), UIBarButtonItemStyle.Plain, new EventHandler(fun _ _ -> 
                            controller.NavigationController.PopViewControllerAnimated true |> ignore
                        ))
            )

    let handleNotifications (event:ChannelEvent.ChannelEvent) (entity:Entity) preview =
        let read, historyType =
            match controller.NavigationController.VisibleViewController with
            | :? PrivateChatSessionViewController as controller -> controller.User.Id = entity.Id, DB.ChatHistoryType.PrivateChat
            | :? ChatRoomViewController as controller -> controller.Room.Id = entity.Id, DB.ChatHistoryType.ChatRoom
            | _ -> false, DB.ChatHistoryType.None
        
        Async.startWithContinuation
            (async {
                do! DB.updateChatHistory (entity, historyType, (Some (preview, read)))
                if historyType = DB.ChatHistoryType.PrivateChat then
                    DB.createChatHistoryEvent entity historyType (JsonConvert.SerializeObject(event))
            })
            (fun _ -> 
                match controller.NavigationController.VisibleViewController with
                    | :? PrivateChatSessionViewController as controller -> reloadNavigationItem (controller :> UIViewController)
                    | :? ChatRoomViewController as controller -> reloadNavigationItem (controller :> UIViewController)
                    | _ -> ()
            )

    let handleAppEvent (event) =
        match event with
        | { Event = AppEvent (event, json) } ->
            match event with
            | UserRelationshipChanged (id, blocked)-> 
                for room, controller in ChatRooms.rooms.Values do
                    controller.SetBlocked (id, blocked)
            | _ -> ()
        | _ -> ()

        event

    let handleEvent channel (event:ChannelEvent.ChannelEvent) =
        controller.InvokeOnMainThread (fun _ ->             
            let stripHtml html = html |> String.stripHtml |> HttpUtility.HtmlDecode
    
            match event |> handleAppEvent with
            //Private chat event
            | { Event = PrivateMessage (color, font, message) }
            | { Event = PrivateMessageSent (color, font, message) } ->
                match ChatSessions.sessions.TryGetValue event.User with
                | true, (user, controller) ->
                    controller.HandleChannelEvent event
                    handleNotifications event user (stripHtml message)
                | _ -> ChatSessions.startById event.User (fun user controller -> 
                    controller.HandleChannelEvent event
                    handleNotifications event user (stripHtml message)
                )

            | { Event = Notification message }
            | { Event = Message (_, _, message)} -> 
                match ChatRooms.rooms.TryGetValue channel with
                | true, (room, controller) ->
                    controller.HandleChannelEvent event
                    handleNotifications event room (stripHtml message)
                | _ -> ()
                    //TODO: There is race when leaving the chat room and recieving events of the left chat room it needs to be figured out
                    //ChatRooms.joinById channel (fun room controller ->
                    //    controller.HandleChannelEvent event
                    //    handleNotifications event room (stripHtml message)
                    //)
            | _ ->
                match ChatRooms.rooms.TryGetValue channel with
                | true, (room, controller) ->
                    controller.HandleChannelEvent event
                | _ -> ()
                    //TODO: There is race when leaving the chat room and recieving events of the left chat room it needs to be figured out
                    //ChatRooms.joinById channel (fun room controller ->
                    //    controller.HandleChannelEvent event
                    //)
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

    member val NavigateOnLoad:(string * (Entity option)) option = None with get, set

    override this.ViewDidLoad () =
        base.ViewDidLoad ()

        let objOrFail = function | Some o -> o | _ -> failwith "Expected Some"
        let navigate url (data:Entity option) =
            let uri = new Uri(url)

            printfn "[Navigate] %s" (uri.ToString())
            match url, data with
            | Routes.User id, data
            | Routes.AddFriend id, data -> 
                let pushUser user =
                    let userController = Resources.userStoryboard.Value.InstantiateInitialViewController () :?> UserViewController
                    userController.Entity <- Some user
                    controller.NavigationController.PushViewController (userController, true)

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

            | Routes.ChatSession id, None ->
                ChatSessions.startById id (fun user controller -> 
                    this.NavigationController.PopToViewController (rootController, false) |> ignore // Straight yolo
                    this.NavigationController.PushViewController (controller, true)
                )

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
                Async.startInBackgroundWithContinuation
                    (fun _ ->
                        DB.deleteAll<DB.ChatHistory>()
                        DB.deleteAll<DB.ChatHistoryEvent>()
                    )
                    (fun _ -> ())

                match NSUserDefaults.StandardUserDefaults.StringForKey "device-token" with
                | null | "" -> ()
                | deviceToken ->
                    Async.startWithContinuation
                        (Settings.deleteDevice deviceToken)
                        (fun _ -> NSUserDefaults.StandardUserDefaults.RemoveObject("device-token"))
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

            UIApplication.SharedApplication.RegisterForRemoteNotificationTypes(UIRemoteNotificationType.Alert ||| UIRemoteNotificationType.Sound ||| UIRemoteNotificationType.Badge)

            Async.Start(Utility.Timer.ticker (fun _ -> Async.Start (Error.report ()) ) (60 * 1000))

            match Api.userIdentity.Value.Membership with
            | MembershipTypePublic.Pending -> this.NavigationController.PushViewController (editProfileController.Value, true)
            | _ ->
                match this.NavigateOnLoad with
                | Some (url, entity) ->
                    this.NavigationController.PushViewController(rootController, false)
                    Navigation.navigate url entity
                | _ ->
                    this.NavigationController.PushViewController(rootController, animated)
                

        match NSUserDefaults.StandardUserDefaults.StringForKey "auth-token", System.String.IsNullOrEmpty Api.userId with
        // User has not entered an account
        | null, _ -> 
            this.NavigationController.PushViewController (loginController.Value, false)

        // User has an account but has not authenticated with the api
        | token, true -> 
            Async.startNetworkWithContinuation
                (Account.loginToken token)
                (function
                    | Api.ApiOk identity -> proceed true
                    | _ -> this.NavigationController.PushViewController (loginController.Value, true)
                )

        // User is fully authenticated already
        | _, false -> proceed false
            
       
