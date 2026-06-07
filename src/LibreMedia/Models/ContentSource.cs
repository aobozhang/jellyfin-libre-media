namespace LibreMedia.Models;

public class ContentSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}