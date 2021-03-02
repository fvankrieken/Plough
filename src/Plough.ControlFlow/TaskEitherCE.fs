﻿namespace Plough.ControlFlow

open System
#if !FABLE_COMPILER
open FSharp.Control.Tasks.Affine.Unsafe
open FSharp.Control.Tasks.Affine
#endif
open Ply

[<AutoOpen>]
module TaskEitherCE =

    type TaskEitherBuilder() =

        member inline _.MergeSources(t1 : Task<Either<'a>>, t2 : Task<Either<'b>>) = TaskEither.zip t1 t2
        
        /// <summary>
        /// Method lets us transform data types into our internal representation. This is the identity method to recognize the self type.
        ///
        /// See https://stackoverflow.com/questions/35286541/why-would-you-use-builder-source-in-a-custom-computation-expression-builder
        /// </summary>
        member inline _.Source(task : Task<Either<'a>>) : Task<Either<'a>> = task
        
        #if !FABLE_COMPILER
        member inline _.Return(value : 'a) : Ply<Either<'a>> =
            uply.Return(either.Return value)

        member inline _.ReturnFrom(taskEither : TaskEither<'a>) : Ply<Either<'a>> =
            uply.ReturnFrom taskEither

        member inline _.Zero() : Ply<Either<unit>> = uply.Return <| either.Zero()

        member inline _.Bind(taskEither : TaskEither<'a>, binder : 'a -> Ply<Either<'b>>) : Ply<Either<'b>> =
            uply {
                match! taskEither with
                | Error e ->
                    return Error e
                | Ok { Data = d; Warnings = w1 } ->
                    match! binder d with
                    | Error e -> return Error e
                    | Ok { Data = d2; Warnings = w2 } -> return Ok { Data = d2; Warnings = w1 @ w2 }
            }

        member inline _.Delay(generator : unit -> Ply<Either<'a>>) : unit -> Ply<Either<'a>> =
            uply.Delay(generator)

        member inline _.Combine(computation1 : Ply<Either<unit>>, computation2 : unit -> Ply<Either<'a>>) : Ply<Either<'a>> =
            uply {
                match! computation1 with
                | Error e -> return Error e
                | Ok  { Data = _; Warnings = w1 } ->
                    match! computation2 () with
                    | Error e -> return Error e
                    | Ok { Data = d2; Warnings = w2 } -> return Ok { Data = d2; Warnings = w1 @ w2 }
            }

        member inline _.TryWith(computation : unit -> Ply<Either<'a>>, handler : exn -> Ply<Either<'a>>) : Ply<Either<'a>> =
            uply.TryWith(computation, handler)

        member inline _.TryFinally(computation : unit -> Ply<Either<'a>>, compensation : unit -> unit) : Ply<Either<'a>> =
            uply.TryFinally(computation, compensation)

        member inline _.Using(resource : 'a :> IDisposable, binder : 'a -> Ply<Either<'b>>) : Ply<Either<'b>> =
            uply.Using(resource, binder)

        member _.While(guard : unit -> bool, computation : unit -> Ply<Either<unit>>) : Ply<Either<unit>> =
            uply {
                let mutable fin, result = false, Either.succeed ()

                while not fin && guard () do
                    match! computation () with
                    | Ok { Data = (); Warnings = w1 } ->
                        match result with
                        | Error _ -> ()
                        | Ok { Data = (); Warnings = w2 } ->
                            result <- Ok { Data = (); Warnings = w1 @ w2 }
                    | Error _ as e ->
                        result <- e
                        fin <- true

                return result
            }

        member _.For(sequence : #seq<'a>, binder : 'a -> Ply<Either<unit>>) : Ply<Either<unit>> =
            uply {
                use enumerator = sequence.GetEnumerator()
                let mutable fin, result = false, Either.succeed ()

                while not fin && enumerator.MoveNext() do
                    match! binder enumerator.Current with
                    | Ok { Data = (); Warnings = w1 } ->
                        match result with
                        | Error _ -> ()
                        | Ok { Data = (); Warnings = w2 } ->
                            result <- Ok { Data = (); Warnings = w1 @ w2 }
                    | Error _ as e ->
                        result <- e
                        fin <- true

                return result
            }

        member inline this.BindReturn(x : Task<Either<'a>>, f) =
            this.Bind(x, (fun x -> this.Return(f x)))
            
        member inline _.Run(f : unit -> Ply<'m>) = task.Run f
        
        /// <summary>
        /// Method lets us transform data types into our internal representation.
        /// </summary>
        member inline _.Source(result : Async<Either<'a>>) : Task<Either<'a>> = result |> Async.StartAsTask
        
        #else
        member inline _.Return(value : 'a) : Async<Either<'a>> =
            async.Return(either.Return value)

        member inline _.ReturnFrom(taskEither : TaskEither<'a>) : Async<Either<'a>> =
            async.ReturnFrom taskEither

        member inline _.Zero() : Async<Either<unit>> = async.Return <| either.Zero()

        member inline _.Bind(taskEither : TaskEither<'a>, binder : 'a -> Async<Either<'b>>) : Async<Either<'b>> =
            async {
                match! taskEither with
                | Error e ->
                    return Error e
                | Ok { Data = d; Warnings = w1 } ->
                    match! binder d with
                    | Error e -> return Error e
                    | Ok { Data = d2; Warnings = w2 } -> return Ok { Data = d2; Warnings = w1 @ w2 }
            }

        member inline _.Delay(generator : unit -> Async<Either<'a>>) : unit -> Async<Either<'a>> =
            fun () -> async.Delay(generator)

        member inline _.Combine(computation1 : Async<Either<unit>>, computation2 : unit -> Async<Either<'a>>) : Async<Either<'a>> =
            async {
                match! computation1 with
                | Error e -> return Error e
                | Ok  { Data = _; Warnings = w1 } ->
                    match! computation2 () with
                    | Error e -> return Error e
                    | Ok { Data = d2; Warnings = w2 } -> return Ok { Data = d2; Warnings = w1 @ w2 }
            }

        member inline _.TryWith(computation : unit -> Async<Either<'a>>, handler : exn -> Async<Either<'a>>) : Async<Either<'a>> =
            async.TryWith(computation(), handler)

        member inline _.TryFinally(computation : unit -> Async<Either<'a>>, compensation : unit -> unit) : Async<Either<'a>> =
            async.TryFinally(computation(), compensation)

        member inline _.Using(resource : 'a :> IDisposable, binder : 'a -> Async<Either<'b>>) : Async<Either<'b>> =
            async.Using(resource, binder)

        member _.While(guard : unit -> bool, computation : unit -> Async<Either<unit>>) : Async<Either<unit>> =
            async {
                let mutable fin, result = false, Either.succeed ()

                while not fin && guard () do
                    match! computation () with
                    | Ok { Data = (); Warnings = w1 } ->
                        match result with
                        | Error _ -> ()
                        | Ok { Data = (); Warnings = w2 } ->
                            result <- Ok { Data = (); Warnings = w1 @ w2 }
                    | Error _ as e ->
                        result <- e
                        fin <- true

                return result
            }

        member _.For(sequence : #seq<'a>, binder : 'a -> Async<Either<unit>>) : Async<Either<unit>> =
            async {
                use enumerator = sequence.GetEnumerator()
                let mutable fin, result = false, Either.succeed ()

                while not fin && enumerator.MoveNext() do
                    match! binder enumerator.Current with
                    | Ok { Data = (); Warnings = w1 } ->
                        match result with
                        | Error _ -> ()
                        | Ok { Data = (); Warnings = w2 } ->
                            result <- Ok { Data = (); Warnings = w1 @ w2 }
                    | Error _ as e ->
                        result <- e
                        fin <- true

                return result
            }

        member inline this.BindReturn(x : Task<Either<'a>>, f) =
            this.Bind(x, (fun x -> this.Return(f x)))
            
        member inline _.Run(f : unit -> Async<'m>) = async.ReturnFrom (f())
        #endif
        
    let taskEither = TaskEitherBuilder()

// Having members as extensions gives them lower priority in
// overload resolution between Task<'a> and Task<Either<'a>>.
[<AutoOpen>]
module TaskEitherCEExtensions =
    type TaskEitherBuilder with
        /// <summary>
        /// Needed to allow `for..in` and `for..do` functionality
        /// </summary>
        member inline _.Source(s : #seq<'a>) = s

        /// <summary>
        /// Method lets us transform data types into our internal representation.
        /// </summary>
        member inline _.Source(result : Either<'a>) : Task<Either<'a>> = Task.singleton result

        /// <summary>
        /// Method lets us transform data types into our internal representation.
        /// </summary>
        member inline _.Source(choice : Choice<'a, FailureMessage>) : Task<Either<'a>> =
            choice |> Either.ofChoice id |> Task.singleton

        #if !FABLE_COMPILER
        /// <summary>
        /// Method lets us transform data types into our internal representation.
        /// </summary>
        member inline _.Source(asyncComputation : Async<'a>) : Task<Either<'a>> =
            asyncComputation
            |> Async.StartAsTask
            |> Task.map Either.succeed
        #endif

        /// <summary>
        /// Method lets us transform data types into our internal representation.
        /// </summary>
        member inline _.Source(task : Task<'a>) : Task<Either<'a>> =
            task |> Task.map Either.succeed

        #if !FABLE_COMPILER
        /// <summary>
        /// Method lets us transform data types into our internal representation.
        /// </summary>
        member inline _.Source(t : System.Threading.Tasks.Task) : Task<Either<unit>> =
            task { return! t } |> Task.map Either.succeed
        #endif
        
    open System.Collections.Generic
    open System.Linq
            
    [<AbstractClass>]
    type TaskEither() =
        static member inline collect (source : (unit -> TaskEither<'a>) seq) : TaskEither<'a seq> =
            taskEither {
                let mutable results = Enumerable.Empty()
                
                for item in source do
                    let! result = item ()
                    results <- results.Append(result)
                
                return results
            }
            
        static member inline collectMany (source : (unit -> TaskEither<'a seq>) seq) : TaskEither<'a seq> =
            taskEither {
                let mutable results = Enumerable.Empty()
                
                for item in source do
                    let! result = item ()
                    results <- results.Concat(result)
                
                return results
            }
        
        static member inline collect (source : (unit -> TaskEither<'a>) list) : TaskEither<'a list> =
            taskEither {
                let mutable results = List.empty
                
                for item in source do
                    let! result = item ()
                    results <- results @ [result]
                
                return results
            }
            
        static member inline collectMany (source : (unit -> TaskEither<'a list>) list) : TaskEither<'a list> =
            taskEither {
                let mutable results = List.empty
                
                for item in source do
                    let! result = item ()
                    results <- results @ result
                
                return results
            }

        static member inline collect (source : (unit -> TaskEither<'a>) []) : TaskEither<'a []> = 
            taskEither {
                let results = Array.init source.Length (fun _ -> Unchecked.defaultof<'a>)
                
                for i in 0 .. source.Length - 1 do
                    let! result = source.[i] ()
                    results.[i] <- result
                
                return results
            }
            
        static member inline collectMany (source : (unit -> TaskEither<'a []>) []) : TaskEither<'a []> = 
            taskEither {
                let results = List()
                
                for item in source do
                    let! result = item ()
                    results.AddRange(result)
                
                return results.ToArray()
            }