using CaseInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseInterview.Infrastructure.Persistence.Configurations;

public class InterviewSessionConfiguration : IEntityTypeConfiguration<InterviewSession>
{
    public void Configure(EntityTypeBuilder<InterviewSession> builder)
    {
        builder.ToTable("interview_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Stage)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired();

        builder.Property(s => s.StartedAt)
            .IsRequired();

        builder.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Case)
            .WithMany(c => c.Sessions)
            .HasForeignKey(s => s.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.CaseId);
    }
}
