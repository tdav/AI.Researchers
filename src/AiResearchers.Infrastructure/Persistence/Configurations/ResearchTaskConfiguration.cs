using AiResearchers.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiResearchers.Infrastructure.Persistence.Configurations;

public class ResearchTaskConfiguration : IEntityTypeConfiguration<ResearchTask>
{
    public void Configure(EntityTypeBuilder<ResearchTask> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Topic).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Language).HasMaxLength(16).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Depth).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(t => t.Status);

        builder.HasMany(t => t.InterviewAnswers).WithOne(a => a.ResearchTask)
            .HasForeignKey(a => a.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.OutlineSections).WithOne(s => s.ResearchTask)
            .HasForeignKey(s => s.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.FocusAreas).WithOne(f => f.ResearchTask)
            .HasForeignKey(f => f.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Sources).WithOne(s => s.ResearchTask)
            .HasForeignKey(s => s.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Findings).WithOne(f => f.ResearchTask)
            .HasForeignKey(f => f.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.ProgressEvents).WithOne(e => e.ResearchTask)
            .HasForeignKey(e => e.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.Report).WithOne(r => r.ResearchTask)
            .HasForeignKey<Report>(r => r.ResearchTaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
