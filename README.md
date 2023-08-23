# Reader-Async With Result
a.k.a. `rawr`

This library provides a computation expression that combines the following in attempt to streamline the developer experience while retaining functional purity and inversion of control:
* Reader - a [dependency injection pattern](https://fsharpforfunandprofit.com/posts/dependencies-3/)
* Async
* Result

## Motivation

There are many strategies for [dependency injection](https://martinfowler.com/articles/injection.html), and error handling, most of which end up introducing sometimes hefty boilerplate. `rawr` attempts to reduce this boilerplate while still providing solutions to these concepts.

### Inspiration and Further Reading

* [Reader monad](https://github.com/fsprojects/FSharpx.Extras/blob/master/src/FSharpx.Extras/ComputationExpressions/Reader.fs) from [FSharpx](https://github.com/fsprojects/FSharpx.Extras)
* [FsToolkit.ErrorHandling](https://github.com/demystifyfp/FsToolkit.ErrorHandling)
* [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)


## Examples

You have a function that needs to validate and then write a value to the database. You could just implement this function naively:

```F#
let write value =
    async {
        let validationResult = ValidationFunctions.validate value
        match validationResult with
        | Ok () ->
            let dbConnection = DB.connect StaticConfig.connectionString
            let! writtenValue = DBFunctions.writeValue dbConnection value
            return Ok writtenValue
        | Error e ->
            return Error e
    }
```

This function will not be unit-testable, due to the hard-coded dependency on `StaticConfig.connectionString` and `DBFunctions.writeValue`. Here is an alternate implementation that is more testable:

```F#
let write writeValue value =
    async {
        let validationResult = ValidationFunctions.validate value
        match validationResult with
        | Ok () ->
            let! writtenValue = writeValue value
            return Ok writtenValue
        | Error e ->
            return Error e
    }
```

In this version, we pass in the `writeValue` function, which in a non-test context could just be a curried function:

```F#
let writeValue = DB.connect StaticConfig.connectionString |> DBFunctions.writeValue
```

In a test context, `writeValue` could just be a function that asserts that the correct value is being written.

We are now left with the pattern match on the `validate` function, which is likely to appear throughout our code. With `asyncResult` from [FsToolkit.ErrorHandling](https://demystifyfp.gitbook.io/fstoolkit-errorhandling), this would be implicit:

```F#
let write writeValue value =
    asyncResult {
        // An error would be returned after this if applicable
        do! ValidationFunctions.validate value

        let! writtenValue = writeValue value
        return writtenValue // Returns Ok implicitly
    }
```

This seems more palatable. However, you can imagine that function signatures could get unruly when a function contains many dependencies. This is where a Reader environment comes in:

```F#
let write value =
    reader {
        let! (writeValue: 'value -> Async<'value>) = Reader.ask

        return
            asyncResult {
                // An error would be returned after this if applicable
                do! ValidationFunctions.validate value

                let! writtenValue = writeValue value
                return writtenValue
            }
    }
}
```

Reducing dependency-bloat of the function signature is taken care of, but we introduced a bunch more boilerplate. This is where `rawr` comes in handy:

```F#
let write value =
    rawr {
        let! (writeValue: 'value -> Async<'value>) = Reader.ask
        // An error would be returned after this if applicable
        do! ValidationFunctions.validate value

        let! writtenValue = writeValue value
        return writtenValue
    }
```

Here, we pull what we need out of our dependencies (which can be a record containing multiple dependencies if needed), and error handling is still handled implicitly for us.