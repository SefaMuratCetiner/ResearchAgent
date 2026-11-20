using Microsoft.AspNetCore.Http;
using Research.SharedKernel.Results;

namespace Research.ServiceDefaults.Problems;

/// <summary>The one place an <see cref="ErrorKind"/> becomes an HTTP status.</summary>
public static class ErrorStatus
{
    public static int From(ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.Rule => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };
}
