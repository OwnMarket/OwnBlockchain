namespace Own.Common

type AsyncResult<'TSuccess, 'TFailure> = Async<Result<'TSuccess, 'TFailure>>
