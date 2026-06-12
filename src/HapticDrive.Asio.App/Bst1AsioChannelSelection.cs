using HapticDrive.Asio.Core.Audio;

namespace HapticDrive.Asio.App;

internal static class Bst1AsioChannelSelection
{
    public static Bst1AsioChannelSelectionResult Select(int channel, AudioOutputDeviceKind outputKind)
    {
        var selectedChannel = Math.Max(0, channel);
        var asioSelected = outputKind == AudioOutputDeviceKind.Asio;
        return new Bst1AsioChannelSelectionResult(
            selectedChannel,
            ShouldRebuildPipeline: asioSelected,
            ShouldStartPulse: false,
            asioSelected
                ? $"Manual ASIO Hardware Test selected channel {selectedChannel}; haptics are stopped until started explicitly."
                : $"Manual ASIO Hardware Test selected channel {selectedChannel}; switch Output mode to ASIO Output before testing.");
    }
}

internal sealed record Bst1AsioChannelSelectionResult(
    int SelectedChannel,
    bool ShouldRebuildPipeline,
    bool ShouldStartPulse,
    string Message);
