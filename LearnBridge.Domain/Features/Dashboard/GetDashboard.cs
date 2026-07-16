using LearnBridge.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Domain.Features.Dashboard;

/// <summary>
/// Aggregated read for the parent dashboard and the learner-home streak/badges
/// strip. Reads several learner-linked tables in one request and marks one
/// audit access per resource touched (constraint 5). A read, so it audits but
/// does not consent-gate (constraint 2). Returns null when the learner does
/// not exist, which the endpoint maps to 404.
/// </summary>
public sealed record GetDashboardQuery(Guid LearnerId) : IRequest<DashboardResponse?>;

internal sealed class GetDashboardQueryHandler
    : IRequestHandler<GetDashboardQuery, DashboardResponse?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditContext _auditContext;

    public GetDashboardQueryHandler(IApplicationDbContext dbContext, IAuditContext auditContext)
    {
        _dbContext = dbContext;
        _auditContext = auditContext;
    }

    public async Task<DashboardResponse?> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        bool exists = await _dbContext.Learners
            .AnyAsync(l => l.Id == request.LearnerId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        DateTime utcNow = DateTime.UtcNow;
        DateTime windowStart = utcNow.AddDays(-90);

        var sessions = await _dbContext.ConversationSessions
            .Where(s => s.LearnerId == request.LearnerId && s.StartedAt >= windowStart)
            .ToListAsync(cancellationToken);

        var goals = await _dbContext.Goals
            .Where(g => g.LearnerId == request.LearnerId)
            .ToListAsync(cancellationToken);

        var memories = await _dbContext.JourneyMemories
            .Where(m => m.LearnerId == request.LearnerId)
            .ToListAsync(cancellationToken);

        _auditContext.MarkLearnerAccess(request.LearnerId, "conversation_sessions");
        _auditContext.MarkLearnerAccess(request.LearnerId, "goals");
        _auditContext.MarkLearnerAccess(request.LearnerId, "journey_memory");

        return new DashboardResponse(
            DashboardCalculator.Compute(utcNow, sessions, goals, memories),
            DashboardCalculator.BuildTimeline(utcNow, sessions, goals, memories));
    }
}
