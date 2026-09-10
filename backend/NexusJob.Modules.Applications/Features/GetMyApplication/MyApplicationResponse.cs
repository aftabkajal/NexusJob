namespace NexusJob.Modules.Applications.Features.GetMyApplication;

/// <summary>
/// The on-load apply-button state for the calling Job Seeker on one posting
/// (AD-20), returned by <c>GET /api/applications/mine?jobPostingId=</c>. One
/// shape, no envelope (AD-15): <c>{ applied, appliedAt }</c>.
/// <see cref="AppliedAt"/> is the application's <c>submitted_at</c> when
/// <see cref="Applied"/> is <see langword="true"/>, and <see langword="null"/>
/// otherwise. The Host serialises with <c>JsonIgnoreCondition.Never</c>, so
/// <c>"appliedAt": null</c> is always present on the wire (spec Design Notes).
/// </summary>
public sealed record MyApplicationResponse(bool Applied, DateTimeOffset? AppliedAt);
