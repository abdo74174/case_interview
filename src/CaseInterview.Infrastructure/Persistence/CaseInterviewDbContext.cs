using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaseInterview.Infrastructure.Persistence;

public class CaseInterviewDbContext : DbContext, IApplicationDbContext
{
    public CaseInterviewDbContext(DbContextOptions<CaseInterviewDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<ChunkEmbedding> ChunkEmbeddings => Set<ChunkEmbedding>();
    public DbSet<InterviewCase> InterviewCases => Set<InterviewCase>();
    public DbSet<InterviewSession> InterviewSessions => Set<InterviewSession>();
    public DbSet<SessionMessage> SessionMessages => Set<SessionMessage>();
    public DbSet<SessionFeedback> SessionFeedbacks => Set<SessionFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all Fluent API entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CaseInterviewDbContext).Assembly);

        if (Database.IsNpgsql())
        {
            // Enable pgvector extension in PostgreSQL
            modelBuilder.HasPostgresExtension("vector");
        }
        else
        {
            // For InMemory test provider which does not natively map Pgvector.Vector
            modelBuilder.Entity<ChunkEmbedding>()
                .Property(c => c.Embedding)
                .HasConversion(
                    v => v != null ? string.Join(",", v.ToArray()) : "",
                    s => new Pgvector.Vector(s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(float.Parse).ToArray())
                );
        }
    }
}
