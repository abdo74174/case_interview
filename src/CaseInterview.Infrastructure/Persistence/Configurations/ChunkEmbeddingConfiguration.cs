using CaseInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector.EntityFrameworkCore;

namespace CaseInterview.Infrastructure.Persistence.Configurations;

public class ChunkEmbeddingConfiguration : IEntityTypeConfiguration<ChunkEmbedding>
{
    public void Configure(EntityTypeBuilder<ChunkEmbedding> builder)
    {
        builder.ToTable("chunk_embeddings");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Text)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.Embedding)
            .IsRequired()
            .HasColumnType("vector(1536)");

        builder.Property(c => c.TokenCount)
            .IsRequired();

        builder.Property(c => c.ChunkIndex)
            .IsRequired();

        builder.HasOne(c => c.Chapter)
            .WithMany(ch => ch.Chunks)
            .HasForeignKey(c => c.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ChapterId);

        // HNSW vector index with cosine distance
        builder.HasIndex(c => c.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
