public class Ps2GameMetadata
{
    public string Title { get; set; } = "Unknown";
    public string Serial { get; set; } = "Unknown";
    public string Region { get; set; } = "Unknown";
    public string ReleaseDate { get; set; } = "Unknown";

    // These may be array OR single string in JSON => store as single string (comma joined)
    public string Genre { get; set; } = "Unknown";
    public string Developer { get; set; } = "Unknown";
    public string Publisher { get; set; } = "Unknown";
    public string Language { get; set; } = "Unknown";
}
