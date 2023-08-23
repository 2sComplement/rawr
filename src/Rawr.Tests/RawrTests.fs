namespace SeaMonster.Domain.Extensions.Tests

open NUnit.Framework
open Rawr
open Swensen.Unquote
// open FsToolkit.ErrorHandling
// open FSharpx

module RawrBuilderTests =

    type TestEnv =
        { Prop: int }

        static member prop_ e = e.Prop

    let private run (api: Rawr<TestEnv, 'a, 'err>) : Result<_, _> =
        let env = { Prop = 40 }
        async { return! api env } |> Async.RunSynchronously

    [<Test>]
    let ``rawr wraps value in Ok`` () =
        let result = rawr { return 42 } |> run

        result =! Ok 42

    [<Test>]
    let ``rawr returns Error`` () =
        let mutable expected = 42

        let result =
            rawr {
                if true then
                    return! "Something bad happened" |> Rawr.ofError
                else
                    expected <- 2
                    return 2
            }
            |> run

        result =! Error "Something bad happened"
        expected =! 42

    [<Test>]
    let ``rawr returns traced Error`` () =
        let mutable expected = 42

        let result =
            rawr {
                if true then
                    return! "Something bad happened" |> Rawr.ofError
                else
                    expected <- 2
                    return 2
            }
            |> run

        match result with
        | Error t -> t =! "Something bad happened"
        | Ok _ -> failwith "Expected traced error"

        expected =! 42

    [<Test>]
    let ``rawr binds async`` () =

        let add (n, m) = async { return n + m }

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                let! x = add (prop, 2)
                return x
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr returns async`` () =

        let add (n, m) = async { return n + m }

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return! add (prop, 2)
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr binds task`` () =

        let add (n, m) = task { return n + m }

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                let! x = add (prop, 2)
                return x
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr returns task`` () =

        let add (n, m) = task { return n + m }

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return! add (prop, 2)
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr binds asyncResult`` () =

        let taskOk = async { return Ok 42 }

        let results =
            rawr {
                let! x = taskOk |> Rawr.ofAsyncResult
                return x
            }
            |> run

        results =! Ok 42

    [<Test>]
    let ``rawr returns asyncResult`` () =

        let taskOk = async { return Ok 42 }

        let result = rawr { return! taskOk |> Rawr.ofAsyncResult } |> run

        result =! Ok 42

    [<Test>]
    let ``rawr binds reader`` () =

        let fun1 () =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return prop
            }

        let result =
            rawr {
                let! x = fun1 ()
                return x + 2
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr binds Ok result`` () =

        let add (n, m) = Ok(n + m)

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                let! x = add (prop, 2)
                return x
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr returns Ok result`` () =

        let add (n, m) = Ok(n + m)

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return! add (prop, 2)
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr binds Error result`` () =

        let add (n, m) = Error(n + m)

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                let! x = add (prop, 2)
                return x
            }
            |> run

        result =! Error 42

    [<Test>]
    let ``rawr returns Error result`` () =

        let add (n, m) = Error(n + m)

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return! add (prop, 2)
            }
            |> run

        result =! Error 42

    [<Test>]
    let ``rawr returns reader`` () =

        let fun1 () =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return prop
            }

        let result = rawr { return! fun1 () } |> run

        result =! Ok 40

    [<Test>]
    let ``rawr maps reader`` () =

        let fun1 () =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return prop
            }

        let result =
            rawr {
                let! x = fun1 () |> Rawr.map (fun n -> n + 2)
                return x
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr returns error from async`` () =

        let shouldNotExecute () = failwith "Should not execute"

        let returnError e =
            async { if e then return Error 40 else return Ok 0 }

        let result =
            rawr {
                let! x = returnError true

                match x with
                | Ok _ ->
                    shouldNotExecute ()
                    return 0
                | Error n -> return n + 2
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr returns error from asyncResult`` () =

        let shouldNotExecute () = failwith "Should not execute"

        let returnError e =
            async { if e then return Error 0 else return Ok 42 }

        let result =
            rawr {
                let! x = returnError true |> Rawr.ofAsyncResult
                shouldNotExecute ()
                return x + 0
            }
            |> run

        result =! Error 0

    [<Test>]
    let ``rawr executes try finally block in async`` () =

        let mutable modified = 0

        let returnError e =
            async { if e then return Error 0 else return Ok 42 }

        let result =
            rawr {
                try
                    let! x = returnError true |> Rawr.ofAsyncResult
                    return x + 10
                finally
                    modified <- 42
            }
            |> run

        result =! Error 0
        modified =! 42

    [<Test>]
    let ``rawr executes try finally block in asyncResult`` () =

        let mutable modified = 0

        let returnError e =
            async { if e then return Error 0 else return Ok 42 }

        let result =
            rawr {
                try
                    let! x = returnError true |> Rawr.ofAsyncResult
                    return x + 0
                finally
                    modified <- 42
            }
            |> run

        result =! Error 0
        modified =! 42

    [<Test>]
    let ``rawr executes try with block`` () =

        let mutable modified = 0

        let throw e =
            async {
                if e then
                    failwith "Catch"
                    return 0
                else
                    return 42
            }

        let result =
            rawr {
                try
                    let! x = throw true
                    failwith "Forbidden code"
                    return x + 0
                with ex ->
                    ex.Message =! "Catch"
                    modified <- 42
                    let! x = Rawr.asks TestEnv.prop_
                    return x + 2
            }
            |> run

        result =! Ok 42
        modified =! 42

    [<Test>]
    let ``rawr delays execution`` () =
        
        let mutable executed = false

        let inline execute (delayed: 'args -> Rawr<_, int, _>) args =
            rawr {
                let! executable = delayed args

                return
                    async {
                        if false then
                            let! result = executable
                            return result
                        else
                            return Ok 42
                    }
            }

        let delayed =
            fun () -> rawr { return 0 }
            |> execute

        let result = delayed () |> run
        result =! Ok 42

    [<Test>]
    let ``rawr executes lazily`` () =
        let mutable executed = false

        let defer = async { executed <- true }

        let result =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                let _ = defer // Regular let binding so the task won't execute
                return prop
            }
            |> run

        result =! Ok 40
        executed =! false

    [<Test>]
    let ``rawr binds async with map`` () =

        let getNum () = async { return 2 }

        let result =
            rawr {
                let! x = getNum ()
                let! prop = Rawr.asks TestEnv.prop_

                return Some(x + prop)
            }
            |> run

        result =! Ok(Some 42)

    [<Test>]
    let ``rawr combines return`` () =

        let result =
            rawr {
                if true then
                    return 42
                else
                    let! prop = Rawr.asks TestEnv.prop_

                    return prop
            }
            |> run

        result =! Ok 42

    [<Test>]
    let ``rawr binds another rawr`` () =

        let rawr1 n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return prop + n
            }

        let rawr0 n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return prop + n * 2
            }

        let result = rawr { return! rawr0 1 |> Rawr.bind rawr1 } |> run

        result =! Ok 82

    [<Test>]
    let ``rawr binds another rawr with error`` () =

        let mutable wasCalled = false

        let rawr1 n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_

                if true then
                    return! "Something bad happened" |> Rawr.ofError

                wasCalled <- true
                return prop + n
            }

        let rawr0 n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return prop + n * 2
            }

        let result = rawr { return! rawr0 1 |> Rawr.bind rawr1 } |> run

        result =! Error "Something bad happened"
        wasCalled =! false

    [<Test>]
    let ``rawr binds another rawr with exception`` () =

        let mutable wasCalled = false

        let rawr1 n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_

                if true then
                    failwith "Something bad happened"

                wasCalled <- true
                return prop + n
            }

        let rawr0 n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return prop + n * 2
            }

        let result =
            rawr {
                try
                    return! rawr0 1 |> Rawr.bind rawr1
                with _ ->
                    return! "Exception" |> Rawr.ofError
            }
            |> run

        result =! Error "Exception"
        wasCalled =! false

    [<Test>]
    let ``rawr mapError throws an exception and flows through try with`` () =

        let mutable wasCalled = false
        let mutable withExecuted = false

        let throwErrorAsExn err = failwith $"Error %s{err}"

        let operation n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_

                return! "as exn" |> Rawr.ofError

                wasCalled <- true
                return prop + n * 2
            }

        let result =
            rawr {
                try
                    return! operation 1 |> Rawr.mapError throwErrorAsExn
                with ex ->
                    withExecuted <- true
                    return! ex.Message |> Rawr.ofError
            }
            |> run

        result =! Error "Error as exn"
        wasCalled =! false
        withExecuted =! true

    [<Test>]
    let ``rawr mapError throws an exception and flows through try with during sequential processing`` () =

        let n = 5
        let mutable wasCalled = 0
        let mutable withExecuted = false

        let throwErrorAsExn errs =
            let err = errs |> String.concat ", "
            failwith $"Error %s{err}"

        let operation n : Rawr<_, int, _> =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_

                if n = 1 then
                    return! "as exn" |> Rawr.ofError

                wasCalled <- wasCalled + 1
                return prop + n * 2
            }

        let result =
            rawr {
                try
                    let! _ =
                        [ 1..n ]
                        |> List.map operation
                        |> Rawr.sequential
                        |> Rawr.mapError throwErrorAsExn

                    ()
                with ex ->
                    withExecuted <- true
                    return! ex.Message |> Rawr.ofError
            }
            |> run

        result =! Error "Error as exn"
        wasCalled =! 4
        withExecuted =! true

    [<Test>]
    let ``rawr fold works`` () =

        let processItem acc i =
            rawr {
                let! prop = Rawr.asks TestEnv.prop_
                return acc + prop + i
            }

        let result = [ 0..9 ] |> Rawr.fold processItem 0 |> run

        result =! Ok 445
