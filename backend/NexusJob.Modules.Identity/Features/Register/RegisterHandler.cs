using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Identity.Auth;
using NexusJob.Modules.Identity.Persistence;
using Npgsql;

namespace NexusJob.Modules.Identity.Features.Register;

/// <summary>
/// Registers a Company account and signs it in (FR-1). One module, one
/// <c>SaveChanges</c> in <c>identity</c> (AD-8). On success the response carries
/// the auth cookie (AD-13) and the account summary - <c>200</c>, not <c>201</c>,
/// so the SPA renders the signed-in state immediately (spec Design Notes).
/// </summary>
internal sealed class RegisterHandler(
    IdentityDbContext db,
    IPasswordHasher<CompanyAccount> passwordHasher)
{
    public async Task<IResult> HandleAsync(RegisterRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!AccountType.IsCompany(request.AccountType))
        {
            return Results.Problem(
                title: "Registration is not available for this account type.",
                detail: "Job seeker registration is not available yet.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var email = EmailNormalizer.Normalize(request.Email);

        if (await db.CompanyAccounts.AnyAsync(a => a.Email == email, cancellationToken))
        {
            return DuplicateEmail();
        }

        var account = new CompanyAccount
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            DisplayName = request.Name.Trim(),
            PasswordHash = string.Empty,
        };
        account.PasswordHash = passwordHasher.HashPassword(account, request.Password);

        db.CompanyAccounts.Add(account);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Lost the race with a concurrent registration of the same email.
            // The unique index (AD-11) is the guard; no second row exists.
            return DuplicateEmail();
        }

        await httpContext.SignInAsync(AuthCookie.Scheme, ClaimsPrincipalFactory.ForCompany(account.Id));

        return Results.Ok(new AuthAccountResponse(account.Id.ToString(), AccountType.Company, account.DisplayName));
    }

    private static IResult DuplicateEmail() => Results.Problem(
        title: "Email is already registered.",
        statusCode: StatusCodes.Status409Conflict);
}
