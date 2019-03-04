namespace Own.Common.FSharp

type AsyncResult<'TSuccess, 'TFailure> = Async<Result<'TSuccess, 'TFailure>>
