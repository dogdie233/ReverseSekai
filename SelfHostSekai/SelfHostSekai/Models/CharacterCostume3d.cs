namespace SelfHostSekai.Models;

public class CharacterCostume3D
{
    public enum UnitType
    {
        LightSound,
        Idol,
        Street,
        ThemePark,
        SchoolRefusal,
        Piapro
    }
    
    public required UnitType Unit { get; set; }
    public required int HeadId { get; set; }
    public required int HairId { get; set; }
    public required int BodyId { get; set; }
}