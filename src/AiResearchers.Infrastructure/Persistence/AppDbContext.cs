using AiResearchers.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiResearchers.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ResearchTask> ResearchTasks => this.Set<ResearchTask>();
    public DbSet<InterviewAnswer> InterviewAnswers => this.Set<InterviewAnswer>();
    public DbSet<OutlineSection> OutlineSections => this.Set<OutlineSection>();
    public DbSet<FocusArea> FocusAreas => this.Set<FocusArea>();
    public DbSet<Source> Sources => this.Set<Source>();
    public DbSet<Finding> Findings => this.Set<Finding>();
    public DbSet<ProgressEvent> ProgressEvents => this.Set<ProgressEvent>();
    public DbSet<Report> Reports => this.Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
