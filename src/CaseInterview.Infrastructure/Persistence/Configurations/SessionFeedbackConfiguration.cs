using System.Text.Json;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseInterview.Infrastructure.Persistence.Configurations;

public class SessionFeedbackConfiguration : IEntityTypeConfiguration<SessionFeedback>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<SessionFeedback> builder)
    {
        builder.ToTable("session_feedbacks");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.OverallScore)
            .IsRequired();

        builder.Property(f => f.RubricScores)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<RubricScore>>(v, JsonOptions) ?? new List<RubricScore>()
            )
            .HasColumnType("jsonb");

        builder.Property(f => f.Strengths)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>()
            )
            .HasColumnType("jsonb");

        builder.Property(f => f.Gaps)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>()
            )
            .HasColumnType("jsonb");

        builder.Property(f => f.ChapterLinks)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<ChapterLink>>(v, JsonOptions) ?? new List<ChapterLink>()
            )
            .HasColumnType("jsonb");

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.HasOne(f => f.Session)
            .WithOne(s => s.Feedback)
            .HasForeignKey<SessionFeedback>(f => f.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.SessionId)
            .IsUnique();
    }
}
