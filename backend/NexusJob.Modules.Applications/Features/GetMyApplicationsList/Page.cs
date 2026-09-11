using System.Text.Json.Serialization;

namespace NexusJob.Modules.Applications.Features.GetMyApplicationsList;

/// <summary>
/// The uniform paginated-list shape (AD-15): <c>{ items, page, pageSize, total }</c>,
/// offset pagination, <c>page</c> 1-based. Module-local - no shared kernel exists
/// (spec Boundaries &amp; Constraints): this is a verbatim copy of
/// <c>JobPostings.Features.SearchJobPostings.Page&lt;T&gt;</c>'s shape, duplicated
/// here rather than extracted, since <c>JobPostings</c>'s <c>Page&lt;T&gt;</c> lives
/// in an implementation assembly Applications cannot reference (AD-1).
///
/// The positional parameter is named <see cref="PageNumber"/>, not <c>Page</c>:
/// C# forbids a member sharing its enclosing type's name (<c>Page&lt;T&gt;</c>),
/// so <see cref="JsonPropertyNameAttribute"/> pins the wire shape to AD-15's
/// exact <c>page</c> key regardless of the C# member name.
/// </summary>
/// <param name="Items">The page's rows.</param>
/// <param name="PageNumber">The 1-based page number, after clamping. Serialised as <c>page</c>.</param>
/// <param name="PageSize">The page size, after clamping.</param>
/// <param name="Total">The total number of matching rows across every page.</param>
public sealed record Page<T>(
    IReadOnlyList<T> Items,
    [property: JsonPropertyName("page")] int PageNumber,
    int PageSize,
    int Total);
