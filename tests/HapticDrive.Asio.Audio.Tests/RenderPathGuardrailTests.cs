using System.IO;

namespace HapticDrive.Asio.Audio.Tests;

public sealed class RenderPathGuardrailTests
{
    [Fact]
    public void RenderPath_DoesNotPublishDiagnostics()
    {
        var renderPath = ReadSlice(
            "src/HapticDrive.Asio.Runtime/Pipeline/HapticPipelineCoordinator.cs",
            "    private AudioOutputRenderCallbackResult RenderOutputBuffer(",
            "    private async ValueTask RenderManualAsioHardwarePulseAsync(");

        Assert.False(renderPath.Contains("SetPipelineError(", StringComparison.Ordinal));
        Assert.False(renderPath.Contains("lock (_diagnosticsGate)", StringComparison.Ordinal));
        Assert.False(renderPath.Contains("RecordManualAsioHardwareFlight(", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderPath_DoesNotFormatStrings()
    {
        var coordinatorRenderPath = ReadSlice(
            "src/HapticDrive.Asio.Runtime/Pipeline/HapticPipelineCoordinator.cs",
            "    private AudioOutputRenderCallbackResult RenderOutputBuffer(",
            "    private async ValueTask RenderManualAsioHardwarePulseAsync(");
        var engineRenderPath = ReadSlice(
            "src/HapticDrive.Asio.Audio/Effects/HapticEffectEngine.cs",
            "    internal int RenderInto(",
            "    private HapticEffectGraph BuildGraph(");
        var streamingRenderPath = ReadSlice(
            "src/HapticDrive.Asio.Audio/Devices/AudioOutputDeviceBase.cs",
            "    private void RunStreamingLoop(",
            "    private void RecordRenderCallback(");
        var asioSubmitPath = ReadSlice(
            "src/HapticDrive.Asio.Audio/Devices/AsioAudioOutputDevice.cs",
            "    private AudioOutputDeviceResult SubmitRoutedBuffer(",
            "    public override async ValueTask DisposeAsync()");
        var nullSubmitPath = ReadSlice(
            "src/HapticDrive.Asio.Audio/Devices/NullAudioOutputDevice.cs",
            "    private AudioOutputDeviceResult ConsumeBuffer(",
            "    public NullAudioOutputDeviceSnapshot GetSampleSinkSnapshot()");

        AssertNoStringFormatting(coordinatorRenderPath);
        AssertNoStringFormatting(engineRenderPath);
        AssertNoStringFormatting(streamingRenderPath);
        AssertNoStringFormatting(asioSubmitPath);
        AssertNoStringFormatting(nullSubmitPath);
    }

    private static void AssertNoStringFormatting(string source)
    {
        Assert.False(source.Contains("$\"", StringComparison.Ordinal));
        Assert.False(source.Contains("string.Format(", StringComparison.Ordinal));
    }

    private static string ReadSlice(string relativePath, string startMarker, string endMarker)
    {
        var fullPath = Path.Combine(GetRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(fullPath);
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Could not find '{startMarker}' in {relativePath}.");

        var endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Could not find '{endMarker}' after '{startMarker}' in {relativePath}.");

        return source[startIndex..endIndex];
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HapticDrive.Asio.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located from the test output directory.");
    }
}
