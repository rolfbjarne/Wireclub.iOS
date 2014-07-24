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

let db = new SQLiteConnection(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "db"))
let dbLock computation =  
    lock db (fun _ -> computation db) 

let init () =
    dbLock (fun db ->
        db.CreateTable<ChatHistory> () |> ignore
        db.CreateTable<ChatHistoryEvent> () |> ignore
        db.CreateTable<Error> () |> ignore
    )

init ()

let deleteAll<'T when 'T : (new : unit -> 'T)>() = 
    dbLock (fun db ->
        let toDelete = db.Table<'T>().ToList()
        for event in toDelete do db.Delete(event) |> ignore
    )
    
let fetchErrors () =
    dbLock (fun db ->
        db.Table<Error>().ToList()
    )

let createError (error:Error) =
    dbLock (fun db ->
        db.Insert (error) |> ignore
        #if DEBUG
        let errors = db.Table<Error>().ToList()
        printfn "Errors: %A" (errors.Select(fun (e:Error) -> e.Error).ToArray())
        #endif
    )

let clearErrors () =
    dbLock (fun db ->
        let errors = db.Table<Error>().ToList()
        for error in errors.Take(100) do db.Delete(error) |> ignore
    )

let createChatHistoryEvent (entity:Entity) historyType eventJson =
    dbLock (fun db ->
        db.Insert
            (ChatHistoryEvent(
                EntityId = entity.Id,
                Type = historyType,
                EventJson = eventJson
            )) |> ignore

        let count = db.Table<ChatHistoryEvent>().Count()
        if count > 500 then
            let toClear = db.Table<ChatHistoryEvent>().OrderBy(fun s -> s.LastStamp).Take(100).ToList()
            for event in toClear do db.Delete(event) |> ignore
        )


let createChatHistory (entity:Entity) (historyType:ChatHistoryType) = 
    dbLock (fun db ->
        match db.Table<ChatHistory>().Where(fun x -> x.EntityId = entity.Id).FirstOrDefault() with
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
                    )) |> ignore
        | existing -> ()
    )

let updateChatHistory = 
    let processor = MailboxProcessor<(Entity * ChatHistoryType * (string * bool) option) * AsyncReplyChannel<unit>>.Start(fun inbox ->
        let rec loop () = async {
            let! (entity, historyType, last), channel = inbox.Receive()
            dbLock (fun db ->
                match db.Table<ChatHistory>().Where(fun x -> x.EntityId = entity.Id).FirstOrDefault() with
                | null -> ()
                | existing -> 
                    match last with
                    | Some (_, read) when existing.Type = ChatHistoryType.ChatRoom && existing.Read = false && read = false -> () //if it's a chatoom and unread leave it on the first unread
                    | Some (last, read) ->
                        existing.Last <- last
                        existing.LastStamp <- DateTime.UtcNow
                        existing.Read <- read
                    | _ -> ()

                    db.Update existing |> ignore
                )

            channel.Reply(())

            return! loop ()
        }

        loop ()
    )

    fun message -> processor.PostAndAsyncReply(fun replyChannel -> message, replyChannel)

let fetchChatHistoryById (id) =
    dbLock(fun db ->
        db.Table<ChatHistory>().Where(fun x -> x.EntityId = id).FirstOrDefault()
    )

let updateChatHistoryReadById (id) =
    dbLock (fun db -> 
        let history = db.Table<ChatHistory>().Where(fun x -> x.EntityId = id).FirstOrDefault()
        match history with
        | null -> ()
        | history ->
            history.Read <- true
            db.Update history |> ignore
    )

let fetchChatHistoryUnreadCount () =
    dbLock (fun db -> 
        db.Table<ChatHistory>().Where(fun s -> s.Read = false).Count()
    )

let fetchChatHistory () = 
    dbLock (fun db -> 
        db.Table<ChatHistory>().OrderByDescending(fun s -> s.LastStamp).Take(100).ToList()
    )

let fetchChatEventHistoryByEntity entityId =
    dbLock (fun db -> 
        db.Table<ChatHistoryEvent>().Where(fun e -> e.EntityId = entityId).OrderByDescending(fun s -> s.LastStamp).Take(100).ToList() 
    )

let removeChatHistoryById (id) =
    dbLock (fun db -> 
        match db.Table<ChatHistory>().Where(fun x -> x.EntityId = id).FirstOrDefault() with
        | null -> ()
        | history -> db.Delete(history) |> ignore
    )
