namespace Rawr

open System
open System.Threading.Tasks

type RawrBuilder() =

    member inline _.Return(value: 'a) : Rawr<'env, 'a, 'err> = Rawr.retn value

    member inline _.ReturnFrom(m: Rawr<'env, 'a, 'err>) : Rawr<'env, 'a, 'err> = m

    member _.ReturnFrom(m: Async<'a>) : Rawr<'env, 'a, 'err> =
        fun _ ->
            async {
                let! x = m
                return Ok x
            }

    member _.ReturnFrom(m: Task<'a>) : Rawr<'env, 'a, 'err> =
        fun _ ->
            async {
                let! x = m |> Async.AwaitTask
                return Ok x
            }

    member _.ReturnFrom(m: Result<'a, 'err>) : Rawr<'env, 'a, 'err> = fun _ -> async { return m }

    // Binds another Rawr
    member inline _.Bind
        (
            r: Rawr<'env, 'a, 'err>,
            [<InlineIfLambda>] binder: 'a -> Rawr<'env, 'b, 'err>
        ) : Rawr<'env, 'b, 'err> =
        Rawr.bind binder r

    // Binds an async
    member inline this.Bind
        (
            a: Async<'a>,
            [<InlineIfLambda>] binder: 'a -> Rawr<'env, 'b, 'err>
        ) : Rawr<'env, 'b, 'err> =
        fun env ->
            async {
                let! x = a
                return! binder x env
            }

    // Binds a task
    member inline this.Bind(t: Task<'a>, [<InlineIfLambda>] binder: 'a -> Rawr<'env, 'b, 'err>) : Rawr<'env, 'b, 'err> =
        fun env ->
            async {
                let! x = t |> Async.AwaitTask
                return! binder x env
            }

    // Binds a result
    member inline this.Bind
        (
            t: Result<'a, 'err>,
            [<InlineIfLambda>] binder: 'a -> Rawr<'env, 'b, 'err>
        ) : Rawr<'env, 'b, 'err> =
        fun env ->
            async {
                match t with
                | Ok x -> return! binder x env
                | Error e -> return Error e
            }

    member inline _.Zero() : Rawr<'env, unit, 'err> = fun _ -> Ok() |> async.Return

    member inline this.TryWith
        (
            [<InlineIfLambda>] computation: Rawr<'env, 'a, 'err>,
            [<InlineIfLambda>] handler: exn -> Rawr<'env, 'a, 'err>
        ) : Rawr<'env, 'a, 'err> =
        fun env ->
            let envAppliedComputation = computation env
            async.TryWith(envAppliedComputation, (fun ex -> handler ex env))

    member inline this.TryFinally
        (
            [<InlineIfLambda>] computation: Rawr<'env, 'a, 'err>,
            [<InlineIfLambda>] compensation: unit -> unit
        ) : Rawr<'env, 'a, 'err> =
        fun env ->
            let tr = computation env
            async.TryFinally(tr, compensation)

    member inline this.Using(res: #IDisposable, [<InlineIfLambda>] binder: 'a -> Rawr<'env, 'b, 'err>) =
        this.TryFinally(
            binder res,
            fun () ->
                if not (isNull (box res)) then
                    res.Dispose()
        )

    member inline this.Delay(f: unit -> Rawr<'env, 'a, 'err>) = this.Bind(this.Return(), f)

    member inline this.While
        (
            [<InlineIfLambda>] guard: unit -> bool,
            [<InlineIfLambda>] computation: Rawr<'env, unit, 'err>
        ) : Rawr<'env, unit, 'err> =
        fun env ->
            async {
                let mutable fin = false
                let mutable result = Ok()

                while not fin && guard () do
                    match! computation env with
                    | Ok x -> x
                    | Error _ as e ->
                        result <- e
                        fin <- true

                return result
            }

    member inline this.For
        (
            sequence: #seq<'a>,
            [<InlineIfLambda>] binder: 'a -> Rawr<'env, unit, 'err>
        ) : Rawr<'env, unit, 'err> =
        fun env ->
            async {
                use enumerator = sequence.GetEnumerator()
                let mutable fin = false
                let mutable result = Ok()

                while not fin && enumerator.MoveNext() do
                    match! binder enumerator.Current env with
                    | Ok x -> x
                    | Error _ as e ->
                        result <- e
                        fin <- true

                return result
            }

    member inline this.Combine(computation1: Rawr<'env, unit, 'err>, computation2: Rawr<'env, 'a, 'err>) =
        this.Bind(computation1, (fun () -> computation2))

    member inline this.Combine
        (
            computation1: Rawr<'env, unit, 'err>,
            [<InlineIfLambda>] computation2: unit -> Rawr<'env, unit, 'err>
        ) =
        this.Bind(computation1, computation2)

[<AutoOpen>]
module RawrBuilder =
    let rawr = RawrBuilder()
