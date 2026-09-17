namespace YTTrending.Infrastructure.Persistence.Configurations;

public class SavedIdeaConfiguration : IEntityTypeConfiguration<SavedIdea>
{
    public void Configure(EntityTypeBuilder<SavedIdea> builder)
    {
        // 1 video chỉ bookmark được 1 lần
        builder.HasIndex(s => s.VideoId).IsUnique();

        builder.HasOne<Video>()
               .WithOne(v => v.SavedIdea)
               .HasForeignKey<SavedIdea>(s => s.VideoId);

        // Note cố tình không khai HasMaxLength -> map thành text
    }
}
