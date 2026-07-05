using FluentValidation;

namespace Nrs.Api.Features.Enrollments.Validation;

/// <summary>
/// Minimal-API endpoint filter that runs the registered <see cref="IValidator{T}"/> for a
/// request body and short-circuits with an RFC-7807 validation problem (400) when it fails.
/// This is the vertical-slice equivalent of the controller pipeline's automatic
/// DataAnnotations validation.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var input = context.Arguments.OfType<T>().FirstOrDefault();
        if (input is not null)
        {
            var result = await validator.ValidateAsync(input, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                return Results.ValidationProblem(result.ToDictionary());
            }
        }

        return await next(context);
    }
}
