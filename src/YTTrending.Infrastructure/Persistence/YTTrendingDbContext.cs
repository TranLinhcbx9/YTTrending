namespace YTTrending.Infrastructure.Persistence;

public class YTTrendingDbContext(DbContextOptions<YTTrendingDbContext> options, TimeProvider clock)
    : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<VideoMetricSnapshot> VideoMetricSnapshots => Set<VideoMetricSnapshot>();
    public DbSet<TrendingScore> TrendingScores => Set<TrendingScore>();
    public DbSet<SavedIdea> SavedIdeas => Set<SavedIdea>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tự nạp 5 file trong Persistence/Configurations, không phải khai từng cái
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YTTrendingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(ct);
    }

    // BẮT BUỘC override cả bản sync — chỗ nào lỡ gọi SaveChanges() thì audit vẫn chạy
    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    private void ApplyAuditFields()
    {
        var now = clock.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Property(nameof(AuditableEntity.CreatedAt)).CurrentValue = now;

            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Property(nameof(AuditableEntity.UpdatedAt)).CurrentValue = now;
        }
    }
}
