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

    let mutable rooms: ChatDirectoryRoomViewModel[] = [| |]
   
    let tableSource = { 
        new UITableViewSource() with
        override this.GetCell(tableView, indexPath) =
            let room = rooms.[indexPath.Row]
            let cell = 
                match tableView.DequeueReusableCell "room-cell" with
                | null -> new UITableViewCell (UITableViewCellStyle.Subtitle, "room-cell")
                | c -> c

            cell.Tag <- indexPath.Row
            cell.TextLabel.Text <- room.Name
            cell.DetailTextLabel.Text <- room.Description
            Image.loadImageForCell (App.imageUrl room.Image 100) Image.placeholderChat cell tableView
            cell

        override this.RowsInSection(tableView, section) =
            rooms.Length

        override this.RowSelected(tableView, indexPath) =
            tableView.DeselectRow (indexPath, false)
            let room = rooms.[indexPath.Row]
            let roomController = ChatRooms.join { Id=room.Id; Slug=room.Slug; Label=room.Name; Image=room.Image }
            controller.NavigationController.PushViewController(roomController, true)
    }

    let updateDirectory () = 
        Async.startWithContinuation
            (Chat.directory ())
            (function
                | Api.ApiOk directory ->
                    rooms <- directory.Official
                    controller.Table.ReloadData ()
                | er -> 
                    // ## Handle "soft" errors (no alertview, maybe a toast if the table is empty?)
                    printfn "Api Error: %A" er
            )
    

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- tableSource
        updateDirectory ()
        base.ViewDidLoad ()
