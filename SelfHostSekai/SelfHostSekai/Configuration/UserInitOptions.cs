namespace SelfHostSekai.Configuration;

public class UserInitOptions
{
    public string UserName { get; set; } = "Player";
    
    public int[] MusicVocalIds { get; set; } = [];

    public int[] CardIds { get; set; } = [];
    
    public int[] ReleaseConditions { get; set; } = [];

    public string Costume3dUnlockDesc { get; set; } = "最初から所持";
    
    public int[] StampIds { get; set; } = [];
}