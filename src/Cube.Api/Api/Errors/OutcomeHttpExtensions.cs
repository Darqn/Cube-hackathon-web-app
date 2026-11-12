using Cube.Api.Common.Operations;

namespace Cube.Api;

public static class OutcomeHttpExtensions
{
    public static IResult ToHttpResult<T>(this Outcome<T> outcome, HttpContext context, Func<T, IResult> success)
    {
        if (outcome.Error is not { } error) return success(outcome.Value);
        var status = error.Kind switch
        {
            FailureKind.NotFound => 404,
            FailureKind.Forbidden => 403,
            FailureKind.Conflict => 409,
            _ => 400
        };
        return Results.Problem(statusCode: status, title: error.Code,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code, ["traceId"] = context.TraceIdentifier });
    }
}
