using Microsoft.AspNetCore.Mvc;
using Gigs.Exceptions;

namespace Gigs.Types;

public class Result<T>
{
    protected Result(T data)
    {
        Data = data;
    }

    protected Result(Exception error, ErrorType type)
    {
        Error = error;
        ErrorType = type;
    }

    public T? Data { get; }
    public Exception? Error { get; }
    private ErrorType ErrorType { get; }
    public bool IsSuccess => Error == null;

    public ActionResult ToResponse()
    {
        return IsSuccess ? ToSuccessResponse() : ToErrorResponse();
    }

    private ActionResult ToSuccessResponse()
    {
        return Data != null ? new OkObjectResult(Data) : new NoContentResult();
    }

    private ActionResult ToErrorResponse()
    {
        if (Error == null) return new BadRequestObjectResult("An error occurred.");

        return ErrorType switch
        {
            ErrorType.NotFound => new NotFoundObjectResult(Error.Message),
            ErrorType.Conflict => new ConflictObjectResult(Error.Message),
            ErrorType.BadRequest => new BadRequestObjectResult(Error.Message),
            ErrorType.Unauthorized => new UnauthorizedObjectResult(Error.Message),
            ErrorType.Forbidden => new ForbidResult(Error.Message),
            _ => new BadRequestObjectResult(Error.Message)
        };
    }
}

public class Success<T>(T data) : Result<T>(data);

public class Failure<T>(string message) : Result<T>(new Exception(message), ErrorType.None);

public class NotFoundFailure<T>(string message = "Item not found.") : Result<T>(new NotFoundException(message), ErrorType.NotFound);

public class ConflictFailure<T>(string message) : Result<T>(new ConflictException(message), ErrorType.Conflict);

public class BadRequestFailure<T>(string message) : Result<T>(new Exception(message), ErrorType.BadRequest);

public class UnauthorizedFailure<T>(string message) : Result<T>(new Exception(message), ErrorType.Unauthorized);

public class ForbiddenFailure<T>(string message) : Result<T>(new Exception(message), ErrorType.Forbidden);

public enum ErrorType
{
    None,
    NotFound,
    Conflict,
    BadRequest,
    Unauthorized,
    Forbidden
}

public static class Result
{
    public static Result<T> Ok<T>(T data) => new Success<T>(data);
    public static Result<T> Fail<T>(string message) => new Failure<T>(message);
    public static Result<T> NotFound<T>(string message = "Not found") => new NotFoundFailure<T>(message);
    public static Result<T> Conflict<T>(string message) => new ConflictFailure<T>(message);
    public static Result<T> Unauthorized<T>(string message = "Unauthorized") => new UnauthorizedFailure<T>(message);
    public static Result<T> Forbidden<T>(string message = "Forbidden") => new ForbiddenFailure<T>(message);
}

public static class ResultHelpers
{
    public static Result<T> ToSuccess<T>(this T data) => new Success<T>(data);
    public static Result<T> ToFailure<T>(this string message) => new Failure<T>(message);
    public static Result<T> ToNotFound<T>(this string message) => new NotFoundFailure<T>(message);
    public static Result<T> ToConflict<T>(this string message) => new ConflictFailure<T>(message);
}
