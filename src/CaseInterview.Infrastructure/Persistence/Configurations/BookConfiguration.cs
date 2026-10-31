using CaseInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseInterview.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(b => b.Author)
            .HasMaxLength(200);

        builder.Property(b => b.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.Status)
            .IsRequired();

        builder.Property(b => b.FailureReason)
            .HasMaxLength(2000);

        builder.HasOne(b => b.Owner)
            .WithMany(u => u.Books)
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.OwnerId);
    }
}
