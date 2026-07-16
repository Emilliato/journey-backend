using LearnBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Abstractions;

/// <summary>
/// The data-access port the application (handler) layer depends on. The
/// concrete EF Core <c>LearnBridgeDbContext</c> in LearnBridge.Data implements
/// it, so handlers stay free of a hard dependency on the persistence project —
/// the DbContext is itself the unit-of-work and repository here, which keeps
/// the layering clean without a repository class per entity.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Learner> Learners { get; }

    DbSet<ParentalConsent> ParentalConsents { get; }

    DbSet<LearningProfile> LearningProfiles { get; }

    DbSet<Goal> Goals { get; }

    DbSet<JourneyMemory> JourneyMemories { get; }

    DbSet<ConversationSession> ConversationSessions { get; }

    DbSet<AccessAuditLog> AccessAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
