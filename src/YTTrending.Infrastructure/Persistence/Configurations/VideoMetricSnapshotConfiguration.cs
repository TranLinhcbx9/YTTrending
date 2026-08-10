namespace YTTrending.Infrastructure.Persistence.Configurations;

public class VideoMetricSnapshotConfiguration : IEntityTypeConfiguration<VideoMetricSnapshot>
{
    public void Configure(EntityTypeBuilder<VideoMetricSnapshot> builder)
    {
        // BẮT BUỘC khai tay: entity này không có navigation property ở CẢ HAI chiều
        // nên EF Core không nhận ra đây là quan hệ — xem A26 mục b.
        builder.HasOne<Video>()
               .WithMany()
               .HasForeignKey(s => s.VideoId);
    }
}
