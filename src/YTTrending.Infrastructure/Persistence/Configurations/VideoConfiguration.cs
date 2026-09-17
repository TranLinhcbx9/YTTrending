namespace YTTrending.Infrastructure.Persistence.Configurations;

public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.Property(v => v.YoutubeVideoId).HasMaxLength(32);
        builder.Property(v => v.Title).HasMaxLength(512);
        builder.Property(v => v.ThumbnailUrl).HasMaxLength(512);
        builder.Property(v => v.Category).HasMaxLength(64);
        // Description cố tình KHÔNG khai HasMaxLength -> map thành text

        // A11: varchar + HasConversion<string>(), không dùng native Postgres ENUM
        builder.Property(v => v.Status)
               .HasConversion<string>()
               .HasMaxLength(16);

        // Duplicate-check của Discovery Engine dựa vào index này
        builder.HasIndex(v => v.YoutubeVideoId).IsUnique();

        // Soft-delete tự ẩn khỏi mọi query; cần xem cả bản đã xóa thì .IgnoreQueryFilters()
        builder.HasQueryFilter(v => v.DeletedAt == null);

        builder.HasOne(v => v.Channel)
               .WithMany()                        // Channel cố tình không có collection Videos
               .HasForeignKey(v => v.ChannelId);
    }
}
