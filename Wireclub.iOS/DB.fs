// Copyright (c) Wireclub Media Inc. All Rights Reserved. See License.md in the project root for license information.

module Wireclub.iOS.DB

open System
open System.Linq
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
type Error() =
    [<PrimaryKey; AutoIncrement>]
    member val Id = 0 with get, set
    [<MaxLength(Int32.MaxValue)>]
    member val Error = "" with get, set
    member val LastStamp = DateTime.UtcNow with get, set

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

SQLite3.Config (SQLite3.ConfigOption.Serialized) |> ignore

let db () =
    new SQLiteConnection(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "db"))

let init () =
    use db = db ()
    db.CreateTable<ChatHistory> () |> ignore
    db.CreateTable<ChatHistoryEvent> () |> ignore
    db.CreateTable<Error> () |> ignore

init ()

let deleteAll<'T when 'T : (new : unit -> 'T)>() = 
    use db = db ()
    let toDelete = db.Table<'T>().ToList()
    for event in toDelete do
        db.Delete(event) |> ignore
    
let fetchErrors () =
    use db = db ()
    db.Table<Error>().ToList() 

let createError (error:Error) =
    use db = db ()
    db.Insert (error) |> ignore

    #if DEBUG
    let errors = fetchErrors()
    printfn "Errors: %A" (errors.Select(fun (e:Error) -> e.Error).ToArray())
    #endif

let clearErrors () =
    use db = db ()
    let errors = db.Table<Error>().ToList()
    for error in errors.Take(100) do
        db.Delete(error) |> ignore

let createChatHistoryEvent (entity:Entity) historyType eventJson =
    use db = db ()
    db.Insert
        (ChatHistoryEvent(
            EntityId = entity.Id,
            Type = historyType,
            EventJson = eventJson
        )) 
    |> ignore

    let count = db.Table<ChatHistoryEvent>().Count()
    if count > 500 then
        let toClear = db.Table<ChatHistoryEvent>().OrderBy(fun s -> s.LastStamp).Take(100).ToList()

        //TODO batch this
        for event in toClear do
            db.Delete(event) |> ignore

let createChatHistory = 
    let processor = MailboxProcessor<(Entity * ChatHistoryType * (string * bool) option) * AsyncReplyChannel<unit>>.Start(fun inbox ->
        let rec loop () = async {
            let! (entity, historyType, last), channel = inbox.Receive()
            use db = db ()
            let existing = db.Table<ChatHistory>().Where(fun x -> x.EntityId = entity.Id).FirstOrDefault()

            match existing with
            | null -> 
                db.Insert
                    (ChatHistory(
                        EntityId = entity.Id,
                        Label = entity.Label,

                        Slug = entity.Slug,
                        Image = entity.Image,
                        Last = "",
                        Read = true,
                        Type = historyType
                    )) 
                |> ignore

            | existing -> 
                match last with
                | Some (_, read) when existing.Type = ChatHistoryType.ChatRoom && existing.Read = false && read = false -> () //if it's a chatoom and unread leave it on the first unread
                | Some (last, read) ->
                    existing.Last <- last
                    existing.LastStamp <- DateTime.UtcNow
                    existing.Read <- read
                | _ -> ()

                db.Update existing
                |> ignore
            

            channel.Reply(())

            return! loop ()
        }

        loop ()
    )

    fun message -> processor.PostAndAsyncReply(fun replyChannel -> message, replyChannel)

let fetchChatHistoryById (id) =
    use db = db ()
    db.Table<ChatHistory>().Where(fun x -> x.EntityId = id).FirstOrDefault()

let updateChatHistoryReadById (id) =
    use db = db ()
    let history = fetchChatHistoryById id
    match history with
    | null -> ()
    | history ->
        history.Read <- true
        db.Update history |> ignore

let fetchChatHistoryUnreadCount () =
    use db = db ()
    db.Table<ChatHistory>().Where(fun s -> s.Read = false).Count()

let fetchChatHistory () = 
    use db = db ()
    db.Table<ChatHistory>().OrderByDescending(fun s -> s.LastStamp).Take(100).ToList() 

let fetchChatEventHistoryByEntity entityId =
    use db = db ()
    db.Table<ChatHistoryEvent>().Where(fun e -> e.EntityId = entityId).OrderByDescending(fun s -> s.LastStamp).Take(100).ToList() 

let removeChatHistoryById (id) =
    use db = db ()
    let history = fetchChatHistoryById id
    db.Delete(history) |> ignore
