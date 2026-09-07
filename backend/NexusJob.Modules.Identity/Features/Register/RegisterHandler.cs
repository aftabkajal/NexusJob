using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Identity.Auth;
using NexusJob.Modules.Identity.Persistence;
using Npgsql;

namespace NexusJob.Modules.Identity.Features.Register;

/// <summary>
/// Registers an account and signs it in (FR-1). The request body carries
/// <c>accountType</c>; a <c>company</c> and a <c>job_seeker</c> branch write to
/// their own independent table (AD-11) - the same email may register in both.
/// One module, one <c>SaveChanges</c> in <c>identity</c> (AD-8). On success the
/// response carries the auth cookie (AD-13) and the account summary - <c>200</c>,
/// not <c>201</c>, so the SPA renders the signed-in state immediately (spec
/// Design Notes).
/// </summary>
internal sealed class RegisterHandler(
    IdentityDbContext db,
    IPasswordHasher<CompanyAccount> companyPasswordHasher,
    IPasswordHasher<JobSeekerAccount> jobSeekerPasswordHasher)
{
    public async Task<IResult> HandleAsync(RegisterRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return AccountType.Classify(request.AccountType) switch
        {
            AccountType.Company => await RegisterCompanyAsync(request, httpContext, cancellationToken),
            AccountType.JobSeeker => await RegisterJobSeekerAsync(request, httpContext, cancellationToken),
            _ => Results.Problem(
                title: "Unsupported account type.",
                statusCode: StatusCodes.Status400BadRequest),
        };
    }

    private async Task<IResult> RegisterCompanyAsync(RegisterRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
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
        account.PasswordHash = companyPasswordHasher.HashPassword(account, request.Password);

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

    private async Task<IResult> RegisterJobSeekerAsync(RegisterRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);

        // Duplicate checks are scoped to job_seeker_account only: an email already
        // held by a company_account (and absent here) still registers (AD-11).
        if (await db.JobSeekerAccounts.AnyAsync(a => a.Email == email, cancellationToken))
        {
            return DuplicateEmail();
        }

        var account = new JobSeekerAccount
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            FullName = request.Name.Trim(),
            PasswordHash = string.Empty,
        };
        account.PasswordHash = jobSeekerPasswordHasher.HashPassword(account, request.Password);

        db.JobSeekerAccounts.Add(account);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return DuplicateEmail();
        }

        await httpContext.SignInAsync(AuthCookie.Scheme, ClaimsPrincipalFactory.ForJobSeeker(account.Id));

        return Results.Ok(new AuthAccountResponse(account.Id.ToString(), AccountType.JobSeeker, account.FullName));
    }

    private static IResult DuplicateEmail() => Results.Problem(
        title: "Email is already registered.",
        statusCode: StatusCodes.Status409Conflict);
}
