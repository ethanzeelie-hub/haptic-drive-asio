namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed class SimagicPayloadDiffAnalyzer
{
    public IReadOnlyList<SimagicPayloadDiffObservation> FindClosestPairs(
        IEnumerable<SimagicUsbPayloadObservation> left,
        IEnumerable<SimagicUsbPayloadObservation> right,
        int maxResults = 12)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftUnique = DistinctPayloads(left).ToArray();
        var rightUnique = DistinctPayloads(right).ToArray();
        var results = new List<SimagicPayloadDiffObservation>();

        foreach (var leftPayload in leftUnique)
        {
            foreach (var rightPayload in rightUnique)
            {
                if (leftPayload.PayloadBytes.Length == 0
                    || leftPayload.PayloadBytes.Length != rightPayload.PayloadBytes.Length
                    || leftPayload.PayloadBytes.SequenceEqual(rightPayload.PayloadBytes))
                {
                    continue;
                }

                var differences = BuildDifferences(leftPayload.PayloadBytes, rightPayload.PayloadBytes);
                results.Add(new SimagicPayloadDiffObservation
                {
                    LeftSource = leftPayload.SourceFileName,
                    RightSource = rightPayload.SourceFileName,
                    LeftFingerprint = leftPayload.PayloadFingerprint,
                    RightFingerprint = rightPayload.PayloadFingerprint,
                    PayloadLength = leftPayload.PayloadBytes.Length,
                    ChangedByteCount = differences.Count,
                    LeftPayloadPreviewHex = leftPayload.PayloadPreviewHex,
                    RightPayloadPreviewHex = rightPayload.PayloadPreviewHex,
                    Differences = differences
                });
            }
        }

        return results
            .OrderBy(result => result.ChangedByteCount)
            .ThenBy(result => result.LeftFingerprint, StringComparer.Ordinal)
            .ThenBy(result => result.RightFingerprint, StringComparer.Ordinal)
            .Take(Math.Max(1, maxResults))
            .ToArray();
    }

    private static IReadOnlyList<SimagicUsbPayloadObservation> DistinctPayloads(
        IEnumerable<SimagicUsbPayloadObservation> observations)
    {
        return observations
            .Where(observation => observation.PayloadBytes.Length > 0)
            .GroupBy(observation => observation.PayloadFingerprint, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(observation => observation.PayloadLength)
            .ThenBy(observation => observation.PayloadFingerprint, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SimagicPayloadByteDifference> BuildDifferences(byte[] left, byte[] right)
    {
        var differences = new List<SimagicPayloadByteDifference>();
        for (var index = 0; index < left.Length; index++)
        {
            if (left[index] == right[index])
            {
                continue;
            }

            differences.Add(new SimagicPayloadByteDifference
            {
                Offset = index,
                HexOffset = $"0x{index:X2}",
                LeftValueHex = SimagicPayloadHex.ByteHex(left[index]),
                RightValueHex = SimagicPayloadHex.ByteHex(right[index])
            });
        }

        return differences;
    }
}
