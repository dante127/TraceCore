using FluentValidation.Results;
using TraceCore.Application.Common.Exceptions;

namespace TraceCore.Application.Common.Models;

public static class ConcurrencyToken
{
    public static byte[]? DecodeOrNull(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            return null;

        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("RowVersion", "The concurrency token is malformed. Please reload and try again.")
            });
        }
    }
}
