using Gigs.Types;

namespace Gigs.Utils;

public static class ResultExtensions
{
    public static Result<T> OnSuccess<T>(this Result<T> result, Action<T> action)
    {
        if (!result.IsSuccess) return result;
        if (result.Data != null)
            action(result.Data);
        return result;
    }

    public static Result<T> OnFailure<T>(this Result<T> result, Action<Exception> action)
    {
        if (result.IsSuccess) return result;
        if (result.Error != null)
            action(result.Error);
        return result;
    }

    public static T ExpectSuccess<T>(this Result<T> result)
    {
        if (result.IsSuccess && result.Data is not null)
            return result.Data;

        throw new InvalidOperationException(result.IsSuccess
            ? "Expected success with data but was null."
            : "Expected success but was failure");
    }

    public static Exception ExpectFailure<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Expected failure but got success.");

        return result.Error ?? new Exception("An error occurred.");
    }
}
