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
    let images = ConcurrentDictionary<string, UIImage>()
    let placeholder = UIImage.FromFile "Images/PlaceholderChat.png"

    let loadImageForCell room (cell:UITableViewCell) =
        let context = SynchronizationContext.Current
        Async.StartImmediate <| async {
            let! image = Image.tryAcquire context room.Image
            match image with
            | Some image ->
                do! Async.SwitchToContext context
                match controller.Table.VisibleCells |> Array.tryFind (fun c -> c.Tag = cell.Tag) with
                | Some cell -> cell.ImageView.Image <- image
                | None -> ()
            | None -> ()
        }

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
            cell.ImageView.Image <-
                match images.TryGetValue room.Image with
                | true, image -> image
                | false, _ -> 
                    loadImageForCell room cell
                    placeholder
            cell

        override this.RowsInSection(tableView, section) =
            rooms.Length

        override this.RowSelected(tableView, indexPath) =
            controller.NavigationController.PushViewController(new ChatRoomViewController(rooms.[indexPath.Row]), true)
            ()
    }

    let updateDirectory () = async {
        let! directory = Chat.directory ()
        match directory with
        | Api.ApiOk directory ->
            rooms <- directory.Official
            controller.Table.ReloadData ()
        | er -> 
            // ## Handle "soft" errors (no alertview, maybe a toast if the table is empty?)
            printfn "Api Error: %A" er
    }

    [<Outlet>]
    member val Table: UITableView = null with get, set

    override controller.ViewDidLoad () =
        controller.Table.Source <- tableSource
        updateDirectory () |> Async.StartImmediate
        base.ViewDidLoad ()


