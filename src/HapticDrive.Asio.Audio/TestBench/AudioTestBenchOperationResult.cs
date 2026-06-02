namespace HapticDrive.Asio.Audio.TestBench;

public sealed record AudioTestBenchOperationResult(
    bool Succeeded,
    string Message,
    AudioTestBenchSnapshot Snapshot);
