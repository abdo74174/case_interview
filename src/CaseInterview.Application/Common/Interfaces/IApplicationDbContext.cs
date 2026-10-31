using CaseInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaseInterview.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Book> Books { get; }
    DbSet<Chapter> Chapters { get; }
    DbSet<ChunkEmbedding> ChunkEmbeddings { get; }
    DbSet<InterviewCase> InterviewCases { get; }
    DbSet<InterviewSession> InterviewSessions { get; }
    DbSet<SessionMessage> SessionMessages { get; }
    DbSet<SessionFeedback> SessionFeedbacks { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
