using CaseInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseInterview.Infrastructure.Persistence.Configurations;

public class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("chapters");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(c => c.RawText)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.Order)
            .IsRequired();

        builder.HasOne(c => c.Book)
            .WithMany(b => b.Chapters)
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.BookId, c.Order });
    }
}
