namespace NexusJob.Modules.Applications.Features.CreateApplication;

/// <summary>
/// The representation of an application returned by <c>POST /api/applications</c> -
/// one shape, no envelope (AD-15). Serialised as
/// <c>{ id, jobPostingId, submittedAt }</c>; <c>id</c> and <c>jobPostingId</c> are
/// <see cref="System.Guid"/> values as strings (AD-15 conventions) and
/// <c>submittedAt</c> is the UTC submit instant as ISO-8601. A repeat / concurrent
/// apply returns the <em>existing</em> application's values (AD-9 / AD-20).
/// </summary>
public sealed record ApplicationResponse(string Id, string JobPostingId, DateTimeOffset SubmittedAt);
