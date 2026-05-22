using AiResearchers.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiResearchers.Infrastructure.Persistence.Configurations;

public class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Text).IsRequired();

        // Finding -> Source: Restrict, to avoid multiple cascade paths to ResearchTask
        builder.HasOne(f => f.Source).WithMany(s => s.Findings)
            .HasForeignKey(f => f.SourceId).OnDelete(DeleteBehavior.Restrict);

        // Finding -> OutlineSection: optional (nullable FK), set null on section delete
        builder.HasOne(f => f.OutlineSection).WithMany(o => o.Findings)
            .HasForeignKey(f => f.OutlineSectionId).OnDelete(DeleteBehavior.SetNull);
    }
}
