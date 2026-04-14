using Microsoft.EntityFrameworkCore;

namespace BrokerFlow.Api.Models;

public class BrokerFlowDbContext : DbContext
{
    public BrokerFlowDbContext(DbContextOptions<BrokerFlowDbContext> options) : base(options) { }

    public DbSet<Source> Sources => Set<Source>();
    public DbSet<XmlTemplate> XmlTemplates => Set<XmlTemplate>();
    public DbSet<MappingConfig> MappingConfigs => Set<MappingConfig>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Source>(e =>
        {
            e.ToTable("Sources");
            e.HasIndex(s => s.Name);
        });

        modelBuilder.Entity<XmlTemplate>(e =>
        {
            e.ToTable("XmlTemplates");
        });

        modelBuilder.Entity<MappingConfig>(e =>
        {
            e.ToTable("MappingConfigs");
        });

        modelBuilder.Entity<Schedule>(e =>
        {
            e.ToTable("Schedules");
        });

        modelBuilder.Entity<ProcessingJob>(e =>
        {
            e.ToTable("ProcessingJobs");
            e.HasIndex(j => j.Status);
            e.HasIndex(j => j.CreatedAt);
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("AuditEntries");
            e.Property(a => a.Id).UseIdentityColumn();
            e.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<AppConfig>(e =>
        {
            e.ToTable("AppConfigs");
        });
    }
}
