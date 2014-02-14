module Wireclub.iOS.DB

open System
open SQLite
open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat

type ChatHistoryType =
| PrivateChat = 0
| ChatRoom = 1

[<AllowNullLiteral>]
type ChatHistory() =
    [<PrimaryKey; AutoIncrement>]
    member val Id = 0 with get, set
    member val EntityId = "" with get, set
    member val Label = "" with get, set
    member val Slug = "" with get, set
    member val Image = "" with get, set
    member val Last = "" with get, set
    member val LastStamp = DateTime.UtcNow with get, set
    member val Read = true with get, set
    member val Type = ChatHistoryType.PrivateChat with get, set

let db = new SQLiteAsyncConnection("db")
let dbChatHistory = db.CreateTableAsync<ChatHistory> () |> Async.AwaitTask |> Async.RunSynchronously

let createChatHistory (entity:Entity) historyType (last:(string * bool) option) = async {
    let! existing = db.Table<ChatHistory>().Where(fun x -> x.EntityId = entity.Id).FirstOrDefaultAsync() |> Async.AwaitTask

    do!
        (match existing with
        | null -> 
            db.InsertAsync 
                (ChatHistory(
                    EntityId = entity.Id,
                    Label = entity.Label,
                    Slug = entity.Slug,
                    Image = entity.Image,
                    Last = "",
                    Read = true,
                    Type = historyType
                )) 
            |> Async.AwaitTask 
            |> Async.Ignore

        | existing -> 
            match last with
            | Some (last, read) ->
                existing.Last <- last
                existing.LastStamp <- DateTime.UtcNow
                existing.Read <- read
            | None -> ()

            db.UpdateAsync existing
            |> Async.AwaitTask
            |> Async.Ignore
        )
}

let fetchChatHistory () =
    db.Table<ChatHistory>().Where(fun _ -> true).OrderByDescending(fun s -> s.LastStamp).Take(100).ToListAsync() 
    |> Async.AwaitTask
