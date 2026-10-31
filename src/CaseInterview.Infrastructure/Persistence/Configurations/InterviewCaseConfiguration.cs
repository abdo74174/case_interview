using System.Text.Json;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseInterview.Infrastructure.Persistence.Configurations;

public class InterviewCaseConfiguration : IEntityTypeConfiguration<InterviewCase>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<InterviewCase> builder)
    {
        builder.ToTable("interview_cases");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(c => c.Scenario)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.Difficulty)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.HiddenRequirements)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>()
            )
            .HasColumnType("jsonb");

        builder.Property(c => c.Numbers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions) ?? new Dictionary<string, string>()
            )
            .HasColumnType("jsonb");

        builder.Property(c => c.Twists)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>()
            )
            .HasColumnType("jsonb");

        builder.Property(c => c.Rubric)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<RubricItem>>(v, JsonOptions) ?? new List<RubricItem>()
            )
            .HasColumnType("jsonb");

        builder.HasOne(c => c.Chapter)
            .WithMany(ch => ch.Cases)
            .HasForeignKey(c => c.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ChapterId);
    }
}
