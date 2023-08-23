namespace Rawr

type Reader<'env, 'a> = 'env -> 'a

type Rawr<'env, 'a, 'err> = Reader<'env, Async<Result<'a, 'err>>>

[<RequireQualifiedAccess>]
module Rawr =

    let async = async

    let inline retn a = fun _ -> async { return Ok a }

    let inline ofError (e: 'err) : Rawr<'a, 'b, 'err> = fun _ -> async { return Error e }

    let inline asks q =
        fun env ->
            let a = q env
            async { return Ok a }

    let inline map (f: 'a -> 'b) (a: Rawr<'env, 'a, 'err>) : Rawr<'env, 'b, 'err> =
        fun env ->
            async {
                let! x = a env
                let y = x |> Result.map f
                return y
            }

    let inline bind (f: 'a -> Rawr<'env, 'b, 'err>) (a: Rawr<'env, 'a, 'err>) : Rawr<'env, 'b, 'err> =
        fun env ->
            async {
                let! x = a env

                let y =
                    match x with
                    | Ok a' -> f a'
                    | Error e -> fun _ -> async { return Error e }

                return! y env
            }


    let inline mapEnv (f: 'enva -> 'envb) (a: Rawr<'envb, 'a, 'err>) : Rawr<'enva, 'a, 'err> =
        fun envA ->
            async {
                let envB = f envA
                let! x = a envB
                return x
            }

    let inline mapError (f: 'err1 -> 'err2) (a: Rawr<'env, 'a, 'err1>) : Rawr<'env, 'a, 'err2> =
        fun env ->
            async {
                let! x = a env
                return x |> Result.mapError f
            }


    let inline Parallel (rs: Rawr<'env, 'a, 'err> seq) : Rawr<'env, 'a list, 'err list> =
        fun env ->
            async {
                let! rs = rs |> Seq.map (fun r -> r env) |> Async.Parallel
                return Result.concat <| List.ofArray rs
            }

    let inline sequential (rs: Rawr<'env, 'a, 'err> seq) : Rawr<'env, 'a list, 'err list> =
        fun env ->
            let rs = List.ofSeq rs
            let results = Array.zeroCreate rs.Length

            async {
                for i in 0 .. rs.Length - 1 do
                    let r = rs[i]
                    let! rs = r env
                    results[i] <- rs

                return Result.concat results
            }

    let inline ignore (r: Rawr<'env, 'a, 'err>) : Rawr<'env, unit, 'err> =
        fun env ->
            async {
                let! result = (r |> map ignore) env

                match result with
                | Ok _ -> return Ok()
                | Error e -> return Error e
            }

    let inline ofAsyncResult (t: Async<Result<'a, 'err>>) =
        fun _ ->
            async {
                let! x = t
                return x
            }

    let inline fold
        (folder: 'state -> 'a -> Rawr<'env, 'state, 'err>)
        (initial: 'state)
        (items: 'a seq)
        : Rawr<'env, 'state, 'err> =
        fun (env: 'env) ->
            async {
                let mutable state = Ok initial

                for i in 0 .. Seq.length items - 1 do
                    let item = Seq.item i items

                    match state with
                    | Ok st ->
                        let! newState = folder st item env
                        state <- newState
                    | _ -> ()

                return state
            }
