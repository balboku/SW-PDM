namespace SWPdm.Api.Endpoints;

public static class EndpointHelpers
{
    public static IResult ValidationError(string fieldName, string message)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [fieldName] = new[] { message }
        });
    }

    public static IResult ToProblem(Exception ex)
    {
        return ex switch
        {
            ArgumentException argumentException => Results.Problem(
                title: "Invalid request",
                detail: argumentException.Message,
                statusCode: StatusCodes.Status400BadRequest),
            FileNotFoundException fileNotFoundException => Results.Problem(
                title: "File not found",
                detail: fileNotFoundException.Message,
                statusCode: StatusCodes.Status404NotFound),
            UnauthorizedAccessException unauthorizedAccessException => Results.Problem(
                title: "Access denied",
                detail: unauthorizedAccessException.Message,
                statusCode: StatusCodes.Status403Forbidden),
            TimeoutException timeoutException => Results.Problem(
                title: "Operation timed out",
                detail: timeoutException.Message,
                statusCode: StatusCodes.Status504GatewayTimeout),
            InvalidOperationException invalidOperationException => Results.Problem(
                title: "Operation failed",
                detail: invalidOperationException.Message,
                statusCode: StatusCodes.Status500InternalServerError),
            NotSupportedException notSupportedException => Results.Problem(
                title: "Not supported",
                detail: notSupportedException.Message,
                statusCode: StatusCodes.Status501NotImplemented),
            _ => Results.Problem(
                title: "Unexpected error",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
