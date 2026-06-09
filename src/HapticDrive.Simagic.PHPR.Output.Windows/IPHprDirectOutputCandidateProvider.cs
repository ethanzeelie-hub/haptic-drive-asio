namespace HapticDrive.Simagic.PHPR.Output.Windows;

public interface IPHprDirectOutputCandidateProvider
{
    IReadOnlyList<PHprDirectOutputCandidate> DiscoverCandidates(DateTimeOffset? discoveredAtUtc = null);
}
