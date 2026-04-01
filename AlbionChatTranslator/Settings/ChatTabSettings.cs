namespace AlbionChatTranslator.Settings;

public class ChatTabSettings
{
    public string Name { get; set; } = string.Empty;
    public List<int> ChannelIds { get; set; } = new();
    public bool Enabled { get; set; } = true;

}
