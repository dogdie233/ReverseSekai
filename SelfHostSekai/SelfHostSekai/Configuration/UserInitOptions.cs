namespace SelfHostSekai.Configuration;

public class UserInitOptions
{
    public string UserName { get; set; } = "Player";
    public int[] MusicVocalIds { get; set; } = [];

    public int[] CardIds { get; set; } = [];
}