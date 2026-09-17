public sealed class YouTubeOptions
{
    public const string SectionName = "YouTube";
    public string ApiKey { get; init; } = string.Empty;
    public bool UseFake { get; init; }
}
