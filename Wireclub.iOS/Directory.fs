namespace Wireclub.iOS

open System
open System.Threading
open System.Collections.Concurrent
open MonoTouch.Foundation
open MonoTouch.UIKit
open Wireclub.Boundary
open Wireclub.Boundary.Chat

[<Register ("ChatDirectoryViewController")>]
type ChatDirectoryViewController() as controller =
    inherit UIViewController ()

    let mutable directory:ChatDirectoryViewModel option = None
    let rooms () = 
        match directory, controller.RoomFilter.SelectedSegment with
        | Some directory, 0 -> directory.Official 
        | Some directory, 1 -> directory.Member
        | Some directory, 2 -> directory.Personal
        | _, _ -> [||]
   
    let tableSource = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
            let room = rooms().[indexPath.Row]
            let cell = 
                match tableView.DequeueReusableCell "room-cell" with
                | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "room-cell")
                | c -> c

            cell.Tag <- indexPath.Row
            cell.TextLabel.Text <- room.Name
            cell.DetailTextLabel.Text <- room.Description
            cell.DetailTextLabel.TextColor <- UIColor.Gray
            Image.loadImageForCell (App.imageUrl room.Image 100) Image.placeholderChat cell tableView
            cell

        override this.RowsInSection(tableView, section) =
            rooms().Length

        override this.RowSelected(tableView, indexPath) =
            tableView.DeselectRow (indexPath, false)
            let room = rooms().[indexPath.Row]
            let roomController = ChatRooms.join { Id=room.Id; Slug=room.Slug; Label=room.Name; Image=room.Image }
            controller.NavigationController.PushViewController(roomController, true)
    }

    let updateDirectory () = 
        Async.startNetworkWithContinuation
            (Chat.directory ())
            (function
                | Api.ApiOk d ->
                    directory <- Some d
                    controller.Table.ReloadData ()
                    controller.RoomFilter.ValueChanged.Add(fun args ->
                        controller.Table.ReloadData ()
                    )
                | er -> 
                    // ## Handle "soft" errors (no alertview, maybe a toast if the table is empty?)
                    printfn "Api Error: %A" er
            )
    

    [<Outlet>]
    member val Table: UITableView = null with get, set

    [<Outlet>]
    member val RoomFilter: UISegmentedControl = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- tableSource
        updateDirectory ()
        base.ViewDidLoad ()
