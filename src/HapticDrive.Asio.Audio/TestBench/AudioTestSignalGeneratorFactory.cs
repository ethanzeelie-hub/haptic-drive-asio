namespace HapticDrive.Asio.Audio.TestBench;

public static class AudioTestSignalGeneratorFactory
{
    public static IAudioTestSignalGenerator Create(AudioTestSignalDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Kind switch
        {
            AudioTestSignalKind.Silence => new SilenceTestSignalGenerator(definition),
            AudioTestSignalKind.SineTone => new SineToneTestSignalGenerator(definition),
            AudioTestSignalKind.FrequencySweep => new FrequencySweepTestSignalGenerator(definition),
            AudioTestSignalKind.Pulse => new PulseTestSignalGenerator(definition),
            AudioTestSignalKind.Constant => new ConstantTestSignalGenerator(definition),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, "Unknown test signal kind.")
        };
    }
}
