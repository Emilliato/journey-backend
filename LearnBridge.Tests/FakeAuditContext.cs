using LearnBridge.Domain.Abstractions;

namespace LearnBridge.Tests;

/// <summary>Captures marked accesses in-memory so handler/service tests can assert them.</summary>
internal sealed class FakeAuditContext : IAuditContext
{
    public List<(Guid LearnerId, string Resource)> Marked { get; } = [];

    public void MarkLearnerAccess(Guid learnerId, string resource) => Marked.Add((learnerId, resource));
}
