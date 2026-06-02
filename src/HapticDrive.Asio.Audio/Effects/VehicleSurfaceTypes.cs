namespace HapticDrive.Asio.Audio.Effects;

internal static class VehicleSurfaceTypes
{
    public const byte Tarmac = 0;
    public const byte RumbleStrip = 1;
    public const byte Concrete = 2;
    public const byte Rock = 3;
    public const byte Gravel = 4;
    public const byte Mud = 5;
    public const byte Sand = 6;
    public const byte Grass = 7;
    public const byte Water = 8;
    public const byte Cobblestone = 9;
    public const byte Metal = 10;
    public const byte Ridged = 11;

    public static string GetName(byte surfaceTypeId)
    {
        return surfaceTypeId switch
        {
            Tarmac => "Tarmac",
            RumbleStrip => "Rumble strip",
            Concrete => "Concrete",
            Rock => "Rock",
            Gravel => "Gravel",
            Mud => "Mud",
            Sand => "Sand",
            Grass => "Grass",
            Water => "Water",
            Cobblestone => "Cobblestone",
            Metal => "Metal",
            Ridged => "Ridged",
            _ => $"Unknown {surfaceTypeId}"
        };
    }
}
