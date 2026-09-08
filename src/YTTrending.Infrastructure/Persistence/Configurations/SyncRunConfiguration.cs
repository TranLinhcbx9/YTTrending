namespace YTTrending.Infrastructure.Persistence.Configurations;

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.Property(x => x.TriggerType).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TotalCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(128);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1024);
        builder.HasIndex(x => x.Status);
    }
}
