using CaseInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseInterview.Infrastructure.Persistence.Configurations;

public class SessionMessageConfiguration : IEntityTypeConfiguration<SessionMessage>
{
    public void Configure(EntityTypeBuilder<SessionMessage> builder)
    {
        builder.ToTable("session_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(m => m.Stage)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.SessionId, m.CreatedAt });
    }
}
