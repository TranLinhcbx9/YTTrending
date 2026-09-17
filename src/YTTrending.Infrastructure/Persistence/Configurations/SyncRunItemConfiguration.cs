namespace YTTrending.Infrastructure.Persistence.Configurations;

public sealed class SyncRunItemConfiguration : IEntityTypeConfiguration<SyncRunItem>
{
    public void Configure(EntityTypeBuilder<SyncRunItem> builder)
    {
        builder.Property(x => x.ChannelId).IsRequired();
        builder.Property(x => x.ChannelName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ErrorCode).HasMaxLength(128);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1024);

        // FK duy nhất của item; xóa run thì xóa history item của chính run đó.
        builder.HasOne(x => x.SyncRun)
            .WithMany()
            .HasForeignKey(x => x.SyncRunId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SyncRunId, x.ChannelId }).IsUnique();
        builder.HasIndex(x => new { x.SyncRunId, x.Status });
    }
}
