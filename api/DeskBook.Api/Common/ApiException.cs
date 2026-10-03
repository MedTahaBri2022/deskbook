using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DeskBook.Api.Common;

/// <summary>A business error with its HTTP status, rendered as an RFC 9457 problem.</summary>
public class ApiException(int status, string detail, IDictionary<string, string>? errors = null,
    object? conflict = null) : Exception(detail)
{
    public int Status { get; } = status;
    public IDictionary<string, string> Errors { get; } = errors ?? new Dictionary<string, string>();

    /// <summary>For a 409: the booking in the way, so the client can show it.</summary>
    public object? Conflict { get; } = conflict;

    public static ApiException NotFound(string what) => new(StatusCodes.Status404NotFound, $"{what} not found");
    public static ApiException Forbidden(string detail) => new(StatusCodes.Status403Forbidden, detail);

    public static ApiException Invalid(string detail, IDictionary<string, string> errors) =>
        new(StatusCodes.Status422UnprocessableEntity, detail, errors);
}

public class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        if (exception is not ApiException error)
            return false;

        context.Response.StatusCode = error.Status;
        var problem = new ProblemDetails { Status = error.Status, Detail = error.Message };
        if (error.Errors.Count > 0)
            problem.Extensions["errors"] = error.Errors;
        if (error.Conflict is not null)
            problem.Extensions["conflict"] = error.Conflict;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
