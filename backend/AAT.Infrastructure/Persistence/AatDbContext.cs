using AAT.Domain;
using Microsoft.EntityFrameworkCore;

namespace AAT.Infrastructure.Persistence;

public sealed class AatDbContext(DbContextOptions<AatDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<TenantEnvironment> Environments => Set<TenantEnvironment>();
    public DbSet<ObserverAgent> Agents => Set<ObserverAgent>();
    public DbSet<EnrollmentToken> EnrollmentTokens => Set<EnrollmentToken>();
    public DbSet<AgentHeartbeat> Heartbeats => Set<AgentHeartbeat>();
    public DbSet<AiApplication> Applications => Set<AiApplication>();
    public DbSet<ModelCatalogEntry> Models => Set<ModelCatalogEntry>();
    public DbSet<ExecutionTrace> ExecutionTraces => Set<ExecutionTrace>();
    public DbSet<HardwareMetric> HardwareMetrics => Set<HardwareMetric>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(120);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne<Organization>().WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TenantEnvironment>(entity =>
        {
            entity.ToTable("environments");
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => new { e.OrganizationId, e.Name }).IsUnique();
            entity.HasOne<Organization>().WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ObserverAgent>(entity =>
        {
            entity.ToTable("agents");
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Version).HasMaxLength(64);
            entity.Property(e => e.CredentialHash).HasMaxLength(64);
            entity.Property(e => e.HealthState).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => e.CredentialHash).IsUnique();
            entity.HasIndex(e => new { e.OrganizationId, e.LastSeenUtc });
            entity.HasOne<Organization>().WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantEnvironment>().WithMany().HasForeignKey(e => e.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EnrollmentToken>(entity =>
        {
            entity.ToTable("enrollment_tokens");
            entity.Property(e => e.AgentName).HasMaxLength(120);
            entity.Property(e => e.TokenHash).HasMaxLength(64);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasOne<Organization>().WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantEnvironment>().WithMany().HasForeignKey(e => e.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentHeartbeat>(entity =>
        {
            entity.ToTable("agent_heartbeats");
            entity.Property(e => e.AgentVersion).HasMaxLength(64);
            entity.Property(e => e.HealthState).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => new { e.AgentId, e.ReportedAtUtc });
            entity.HasOne<ObserverAgent>().WithMany().HasForeignKey(e => e.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiApplication>(entity =>
        {
            entity.ToTable("ai_applications");
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.IntegrationType).HasMaxLength(64);
            entity.HasOne<ObserverAgent>().WithMany().HasForeignKey(e => e.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ModelCatalogEntry>(entity =>
        {
            entity.ToTable("models");
            entity.Property(e => e.Provider).HasMaxLength(64);
            entity.Property(e => e.ModelName).HasMaxLength(128);
            entity.Property(e => e.Runtime).HasMaxLength(64);
            entity.HasIndex(e => new { e.OrganizationId, e.Provider, e.ModelName }).IsUnique();
        });

        modelBuilder.Entity<ExecutionTrace>(entity =>
        {
            entity.ToTable("execution_traces");
            entity.Property(e => e.RequestId).HasMaxLength(128);
            entity.Property(e => e.ErrorCode).HasMaxLength(64);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => new { e.OrganizationId, e.EventId }).IsUnique();
            entity.HasIndex(e => new { e.OrganizationId, e.StartedAtUtc });
            entity.HasIndex(e => new { e.OrganizationId, e.AgentId, e.StartedAtUtc });
            entity.HasIndex(e => e.RequestId);
            entity.HasOne<Organization>().WithMany().HasForeignKey(e => e.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ObserverAgent>().WithMany().HasForeignKey(e => e.AgentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ModelCatalogEntry>().WithMany().HasForeignKey(e => e.ModelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AiApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HardwareMetric>(entity =>
        {
            entity.ToTable("hardware_metrics");
            entity.Property(e => e.GpuName).HasMaxLength(128);
            entity.Property(e => e.CollectionSource).HasMaxLength(64);
            entity.HasIndex(e => new { e.OrganizationId, e.EventId }).IsUnique();
            entity.HasIndex(e => new { e.OrganizationId, e.AgentId, e.SampledAtUtc });
            entity.HasOne<ObserverAgent>().WithMany().HasForeignKey(e => e.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.Property(e => e.Action).HasMaxLength(64);
            entity.Property(e => e.Target).HasMaxLength(200);
            entity.HasIndex(e => new { e.OrganizationId, e.TimestampUtc });
        });
    }
}
