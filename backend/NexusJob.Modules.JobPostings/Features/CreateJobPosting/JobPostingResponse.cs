namespace NexusJob.Modules.JobPostings.Features.CreateJobPosting;

/// <summary>
/// The representation of a created posting returned by
/// <c>POST /api/job-postings</c> - one shape, no envelope (AD-15). Serialised as
/// <c>{ id, title, description, createdAt }</c>; <c>id</c> is the posting
/// <see cref="System.Guid"/> as a string (AD-15 conventions) and
/// <c>createdAt</c> is the UTC creation instant as ISO-8601.
/// </summary>
public sealed record JobPostingResponse(string Id, string Title, string Description, DateTimeOffset CreatedAt);
