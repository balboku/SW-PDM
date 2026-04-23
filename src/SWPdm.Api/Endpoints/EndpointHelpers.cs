namespace SWPdm.Api.Endpoints;

public static class EndpointHelpers
{
    private static string BuildErrorDetail(Exception ex)
    {
        Exception current = ex;

        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current == ex ? ex.Message : $"{ex.Message} Inner: {current.Message}";
    }

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
                detail: BuildErrorDetail(argumentException),
                statusCode: StatusCodes.Status400BadRequest),
            FileNotFoundException fileNotFoundException => Results.Problem(
                title: "File not found",
                detail: BuildErrorDetail(fileNotFoundException),
                statusCode: StatusCodes.Status404NotFound),
            UnauthorizedAccessException unauthorizedAccessException => Results.Problem(
                title: "Access denied",
                detail: BuildErrorDetail(unauthorizedAccessException),
                statusCode: StatusCodes.Status403Forbidden),
            TimeoutException timeoutException => Results.Problem(
                title: "Operation timed out",
                detail: BuildErrorDetail(timeoutException),
                statusCode: StatusCodes.Status504GatewayTimeout),
            InvalidOperationException invalidOperationException => Results.Problem(
                title: "Operation failed",
                detail: BuildErrorDetail(invalidOperationException),
                statusCode: StatusCodes.Status500InternalServerError),
            NotSupportedException notSupportedException => Results.Problem(
                title: "Not supported",
                detail: BuildErrorDetail(notSupportedException),
                statusCode: StatusCodes.Status501NotImplemented),
            _ => Results.Problem(
                title: "Unexpected error",
                detail: BuildErrorDetail(ex),
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
