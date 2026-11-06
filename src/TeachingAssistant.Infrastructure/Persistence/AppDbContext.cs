using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the app's own data in Postgres. Entities are the Domain classes;
/// how they map to tables lives in Configurations/, so Domain stays free of EF.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionVersion> SubmissionVersions => Set<SubmissionVersion>();
    public DbSet<VersionContent> VersionContents => Set<VersionContent>();
    public DbSet<AssignmentInstructions> AssignmentInstructions => Set<AssignmentInstructions>();
    public DbSet<InstructionsVersion> InstructionsVersions => Set<InstructionsVersion>();
    public DbSet<InstructionsContent> InstructionsContents => Set<InstructionsContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
