using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// Shares one <see cref="IdentityApiFixture"/> (one Postgres container + one
/// hosted app) across every test in the collection, so the container is started
/// and migrated once.
/// </summary>
[CollectionDefinition(nameof(IdentityApiCollection))]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFixture>;
