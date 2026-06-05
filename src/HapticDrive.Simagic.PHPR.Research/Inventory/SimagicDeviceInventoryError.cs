namespace HapticDrive.Simagic.PHPR.Research.Inventory;

public sealed record SimagicDeviceInventoryError
{
    public SimagicDeviceInventoryError()
    {
    }

    public SimagicDeviceInventoryError(SimagicDeviceInventoryMethod method, string message)
    {
        Method = method;
        Message = message;
    }

    public SimagicDeviceInventoryMethod Method { get; init; } = SimagicDeviceInventoryMethod.Unknown;

    public string Message { get; init; } = "";
}
