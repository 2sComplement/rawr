namespace Rawr

module Result =

    let unzip results =
        results
        |> List.choose (fun r ->
            match r with
            | Ok v -> Some v
            | _ -> None
        ),
        results
        |> List.choose (fun r ->
            match r with
            | Error e -> Some e
            | _ -> None
        )

    let zip3 x1 x2 x3 =
        match x1, x2, x3 with
        | Ok x1res, Ok x2res, Ok x3res -> Ok(x1res, x2res, x3res)
        | Error e, _, _ -> Error e
        | _, Error e, _ -> Error e
        | _, _, Error e -> Error e

    let concat rs =
        let oks, errs =
            rs
            |> Seq.fold
                (fun (os, es) r ->
                    match r with
                    | Error e -> os, es @ [ e ]
                    | Ok o -> os @ [ o ], es
                )
                ([], [])

        if errs.IsEmpty then
            Ok oks
        else
            Error errs