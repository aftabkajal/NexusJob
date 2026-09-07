using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Identity.Auth;
using NexusJob.Modules.Identity.Features.Register;
using NexusJob.Modules.Identity.Persistence;

namespace NexusJob.Modules.Identity.Features.Login;

/// <summary>
/// Verifies credentials and signs the caller in (FR-2). The request body carries
/// <c>accountType</c>; a <c>company</c> and a <c>job_seeker</c> branch look up
/// their own independent table (AD-11). Every failure - unknown email, wrong
/// password, or an unrecognised <c>accountType</c> - returns the one generic
/// <c>401</c> ProblemDetails with no field-level detail (spec Design Notes), so
/// the response never reveals which part was wrong.
/// </summary>
internal sealed class LoginHandler(
    IdentityDbContext db,
    IPasswordHasher<CompanyAccount> companyPasswordHasher,
    IPasswordHasher<JobSeekerAccount> jobSeekerPasswordHasher)
{
    /// <summary>
    /// A throwaway PBKDF2 hash (600k iterations, arbitrary password) verified on
    /// the "no such email" path so it does the same constant work as the
    /// "wrong password" path - no timing side-channel to enumerate accounts.
    /// </summary>
    private const string DummyPasswordHash =
        "AQAAAAIACSfAAAAAEPVOkD7mLkQdJSm7IzwjaBHG76Pmec/fMOEZ2im542hweL2y9+lixwkJ2JvXCi/RUw==";

    public async Task<IResult> HandleAsync(LoginRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return AccountType.Classify(request.AccountType) switch
        {
            AccountType.Company => await LoginCompanyAsync(request, httpContext, cancellationToken),
            AccountType.JobSeeker => await LoginJobSeekerAsync(request, httpContext, cancellationToken),
            _ => InvalidCredentials(),
        };
    }

    private async Task<IResult> LoginCompanyAsync(LoginRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);
        var account = await db.CompanyAccounts.SingleOrDefaultAsync(a => a.Email == email, cancellationToken);
        if (account is null)
        {
            // Constant work on both failure paths: verify against a fixed dummy
            // hash so "unknown email" is not measurably faster than "wrong password".
            _ = companyPasswordHasher.VerifyHashedPassword(new CompanyAccount
            {
                Email = email,
                PasswordHash = DummyPasswordHash,
                DisplayName = string.Empty,
            }, DummyPasswordHash, request.Password);
            return InvalidCredentials();
        }

        var verification = companyPasswordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            try
            {
                account.PasswordHash = companyPasswordHasher.HashPassword(account, request.Password);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A transient failure rehashing must not turn a correct login into
                // a 500; the existing hash still verifies. Rehash on the next login.
            }
        }

        await httpContext.SignInAsync(AuthCookie.Scheme, ClaimsPrincipalFactory.ForCompany(account.Id));

        return Results.Ok(new AuthAccountResponse(account.Id.ToString(), AccountType.Company, account.DisplayName));
    }

    private async Task<IResult> LoginJobSeekerAsync(LoginRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);
        var account = await db.JobSeekerAccounts.SingleOrDefaultAsync(a => a.Email == email, cancellationToken);
        if (account is null)
        {
            _ = jobSeekerPasswordHasher.VerifyHashedPassword(new JobSeekerAccount
            {
                Email = email,
                PasswordHash = DummyPasswordHash,
                FullName = string.Empty,
            }, DummyPasswordHash, request.Password);
            return InvalidCredentials();
        }

        var verification = jobSeekerPasswordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            try
            {
                account.PasswordHash = jobSeekerPasswordHasher.HashPassword(account, request.Password);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A transient failure rehashing must not turn a correct login into
                // a 500; the existing hash still verifies. Rehash on the next login.
            }
        }

        await httpContext.SignInAsync(AuthCookie.Scheme, ClaimsPrincipalFactory.ForJobSeeker(account.Id));

        return Results.Ok(new AuthAccountResponse(account.Id.ToString(), AccountType.JobSeeker, account.FullName));
    }

    private static IResult InvalidCredentials() => Results.Problem(
        title: "Invalid credentials.",
        statusCode: StatusCodes.Status401Unauthorized);
}
