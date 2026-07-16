using LearnBridge.Domain.Entities;
using LearnBridge.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LearnBridge.Data;

public sealed class LearnBridgeDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public LearnBridgeDbContext(DbContextOptions<LearnBridgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Learner> Learners => Set<Learner>();

    public DbSet<ParentalConsent> ParentalConsents => Set<ParentalConsent>();

    public DbSet<LearningProfile> LearningProfiles => Set<LearningProfile>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<JourneyMemory> JourneyMemories => Set<JourneyMemory>();

    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();

    public DbSet<AccessAuditLog> AccessAuditLogs => Set<AccessAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Learner>(entity =>
        {
            entity.HasIndex(l => l.ParentId);
            entity.Property(l => l.DisplayName).HasMaxLength(200);

            entity
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(l => l.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParentalConsent>(entity =>
        {
            entity.HasIndex(c => c.LearnerId);

            entity
                .HasOne<Learner>()
                .WithMany()
                .HasForeignKey(c => c.LearnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LearningProfile>(entity =>
        {
            entity.HasIndex(p => p.LearnerId).IsUnique();

            entity
                .HasOne<Learner>()
                .WithMany()
                .HasForeignKey(p => p.LearnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasIndex(g => g.LearnerId);
            entity.Property(g => g.Status).HasConversion<string>().HasMaxLength(32);

            entity
                .HasOne<Learner>()
                .WithMany()
                .HasForeignKey(g => g.LearnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationSession>(entity =>
        {
            entity.HasIndex(s => s.LearnerId);

            entity
                .HasOne<Learner>()
                .WithMany()
                .HasForeignKey(s => s.LearnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JourneyMemory>(entity =>
        {
            entity.HasIndex(m => m.LearnerId);

            // Stored as the enum's string name, not an int, so the column
            // stays self-describing and a DB-level CHECK/rename mistake is
            // obvious — the closed set itself is still enforced by the C#
            // enum type, this is just for readability in the data.
            entity.Property(m => m.Category).HasConversion<string>().HasMaxLength(32);

            entity
                .HasOne<Learner>()
                .WithMany()
                .HasForeignKey(m => m.LearnerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not SetNull: JourneyMemory already cascades directly
            // from Learner above. Learner -> ConversationSession -> (SetNull)
            // JourneyMemory would be a second, conflicting cascade path to
            // the same rows during a Learner delete, which SQL Server
            // refuses to create ("may cause cycles or multiple cascade
            // paths"). Deleting a session while memories still reference it
            // needs to null/reassign those memories explicitly first.
            entity
                .HasOne<ConversationSession>()
                .WithMany()
                .HasForeignKey(m => m.ConversationSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccessAuditLog>(entity =>
        {
            entity.HasIndex(a => a.LearnerId);
            entity.HasIndex(a => a.OccurredAt);
            entity.Property(a => a.Action).HasConversion<string>().HasMaxLength(16);
            entity.Property(a => a.Resource).HasMaxLength(64);
            entity.Property(a => a.RequestPath).HasMaxLength(512);

            // Deliberately no FK to Learner: audit rows must survive even if
            // the learner row itself is ever deleted — an audit trail that
            // disappears alongside the data it was auditing defeats the point.
        });
    }
}
