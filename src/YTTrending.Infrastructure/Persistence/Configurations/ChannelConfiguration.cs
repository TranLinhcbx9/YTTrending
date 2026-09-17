namespace YTTrending.Infrastructure.Persistence.Configurations;

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.Property(c => c.YoutubeChannelId).HasMaxLength(64);
        builder.Property(c => c.Name).HasMaxLength(255);
        builder.Property(c => c.Url).HasMaxLength(512);
        builder.Property(c => c.UploadsPlaylistId).HasMaxLength(64);

        // Chống add trùng channel — Channel Management dựa vào index này
        builder.HasIndex(c => c.YoutubeChannelId).IsUnique();
    }
}
