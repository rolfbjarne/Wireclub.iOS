module Wireclub.iOS.DB

open System
open System.IO
open SQLite
open Wireclub.Models
open Wireclub.Boundary
open Wireclub.Boundary.Chat
open Wireclub.Boundary.Models

type ChatHistoryType =
| None = 0
| PrivateChat = 1
| ChatRoom = 2

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

[<AllowNullLiteral>]
type ChatHistoryEvent() =
    [<PrimaryKey; AutoIncrement>]
    member val Id = 0 with get, set
    member val EntityId = "" with get, set
    member val LastStamp = DateTime.UtcNow with get, set
    member val Type = ChatHistoryType.PrivateChat with get, set
    [<MaxLength(Int32.MaxValue)>]
    member val EventJson = "" with get, set

let db = new SQLiteAsyncConnection(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "db"))
let dbChatHistory = db.CreateTableAsync<ChatHistory> () |> Async.AwaitTask |> Async.RunSynchronously
let dbChatEventHistory = db.CreateTableAsync<ChatHistoryEvent> () |> Async.AwaitTask |> Async.RunSynchronously



let createChatHistoryEvent (entity:Entity) historyType eventJson = async {
    do!
        db.InsertAsync 
            (ChatHistoryEvent(
                EntityId = entity.Id,
                Type = historyType,
                EventJson = eventJson
            )) 
        |> Async.AwaitTask 
        |> Async.Ignore

    let! count = db.Table<ChatHistoryEvent>().CountAsync() |> Async.AwaitTask
    if count > 500 then
        let! toClear = db.Table<ChatHistoryEvent>().OrderBy(fun s -> s.LastStamp).Take(100).ToListAsync() |> Async.AwaitTask 

        //TODO batch this
        for event in toClear do
            do! db.DeleteAsync(event) |> Async.AwaitTask |> Async.Ignore
}

let createChatHistory = 
    let processor = MailboxProcessor<(Entity * ChatHistoryType * (string * bool) option) * AsyncReplyChannel<unit>>.Start(fun inbox ->
        let rec loop () = async {
            let! (entity, historyType, last), channel = inbox.Receive()
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
                    | Some (_, read) when existing.Type = ChatHistoryType.ChatRoom && existing.Read = false && read = false -> () //if it's a chatoom and unread leave it on the first unread
                    | Some (last, read) ->
                        existing.Last <- last
                        existing.LastStamp <- DateTime.UtcNow
                        existing.Read <- read
                    | _ -> ()

                    db.UpdateAsync existing
                    |> Async.AwaitTask
                    |> Async.Ignore
                )

            channel.Reply(())

            return! loop ()
        }

        loop ()
    )

    fun message -> processor.PostAndAsyncReply(fun replyChannel -> message, replyChannel)

let fetchChatHistoryById (id) =
    db.Table<ChatHistory>().Where(fun x -> x.EntityId = id).FirstOrDefaultAsync()
    |> Async.AwaitTask

let updateChatHistoryReadById (id) = async {
    let! history = fetchChatHistoryById id
    match history with
    | null -> ()
    | history ->
        history.Read <- true
        do! db.UpdateAsync history |> Async.AwaitTask |> Async.Ignore
}

let fetchChatHistoryUnreadCount () =
    db.Table<ChatHistory>().Where(fun s -> s.Read = false).CountAsync() 
    |> Async.AwaitTask

let fetchChatHistory () =
    db.Table<ChatHistory>().OrderByDescending(fun s -> s.LastStamp).Take(100).ToListAsync() 
    |> Async.AwaitTask


let fetchChatEventHistoryByEntity entityId =
    db.Table<ChatHistoryEvent>().Where(fun e -> e.EntityId = entityId).OrderByDescending(fun s -> s.LastStamp).Take(100).ToListAsync() 
    |> Async.AwaitTask
