using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace NexusJob.Modules.JobPostings.Auth;

/// <summary>
/// Validates the bound <typeparamref name="TRequest"/> body against its
/// <see cref="System.ComponentModel.DataAnnotations"/> attributes (AD-15: built-in
/// .NET validation, no FluentValidation). A failure is a <c>400</c> validation
/// ProblemDetails whose <c>errors</c> map names each invalid field; the handler
/// never runs, so no row is written.
///
/// A thin <c>internal</c> copy of
/// <c>NexusJob.Modules.Identity.Auth.DataAnnotationsValidationFilter&lt;T&gt;</c>:
/// .NET 10's <c>AddValidation()</c> source generator only scans the web-SDK Host
/// project, so a module request type needs this explicit filter (spec Design
/// Notes).
/// </summary>
internal sealed class DataAnnotationsValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            // A missing / empty / null JSON body binds to null; stop here with a
            // 400 rather than letting the handler dereference it into a 500.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = ["A request body is required."],
            });
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(
                    result => result.MemberNames.DefaultIfEmpty(string.Empty),
                    (result, member) => (Member: member, Message: result.ErrorMessage ?? "The value is invalid."))
                .GroupBy(entry => entry.Member, entry => entry.Message)
                .ToDictionary(group => group.Key, group => group.ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
