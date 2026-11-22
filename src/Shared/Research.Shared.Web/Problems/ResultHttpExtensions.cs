using Microsoft.AspNetCore.Http;
using Research.SharedKernel.Results;

namespace Research.Shared.Web.Problems;

/// <summary>Maps domain/application <see cref="Result"/> and <see cref="Result{T}"/> outcomes to HTTP <see cref="IResult"/>s.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(
        this Result<T> result,
        HttpContext context,
        Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess
            ? onSuccess(result.Value)
            : ProblemResults.From(result.Error, context);
    }

    public static IResult ToOkOrProblem<T, TResponse>(
        this Result<T> result,
        HttpContext context,
        Func<T, TResponse> map) =>
        result.ToHttpResult(context, value =>
        {
            ArgumentNullException.ThrowIfNull(map);
            return TypedResults.Ok(map(value));
        });

    public static IResult ToHttpResult(
        this Result result,
        HttpContext context,
        Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess
            ? onSuccess()
            : ProblemResults.From(result.Error, context);
    }
}
