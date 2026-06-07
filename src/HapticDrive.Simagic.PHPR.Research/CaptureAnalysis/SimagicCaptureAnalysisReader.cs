using System.Globalization;
using System.Text.RegularExpressions;

namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

public sealed partial class SimagicCaptureAnalysisReader
{
    private static readonly string[] SupportedExtensions = [".csv", ".txt", ".pcapng", ".pcap"];

    public async ValueTask<SimagicCaptureAnalysisReport> AnalyzePathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var inputPaths = ResolveInputPaths(path);
        var observations = new List<SimagicUsbPayloadObservation>();
        var fileSummaries = new List<SimagicCaptureFileSummary>();
        var pcapSummaries = new List<SimagicPcapCaptureSummary>();
        var parsedTextDiffs = new List<SimagicPayloadDiffObservation>();
        var warnings = new List<SimagicCaptureAnalysisWarning>();

        foreach (var inputPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = DetermineKind(inputPath);
            if (kind == SimagicCaptureAnalysisSourceKind.WiresharkCsv)
            {
                var result = await ReadCsvAsync(inputPath, cancellationToken);
                observations.AddRange(result.Observations);
                fileSummaries.Add(result.Summary);
                warnings.AddRange(result.Warnings);
            }
            else if (kind == SimagicCaptureAnalysisSourceKind.WiresharkTextSummary)
            {
                var result = await ReadTextSummaryAsync(inputPath, cancellationToken);
                observations.AddRange(result.Observations);
                fileSummaries.AddRange(result.FileSummaries);
                parsedTextDiffs.AddRange(result.DiffObservations);
                warnings.AddRange(result.Warnings);
            }
            else if (kind is SimagicCaptureAnalysisSourceKind.PcapNg or SimagicCaptureAnalysisSourceKind.PcapClassic)
            {
                pcapSummaries.Add(await SimagicPcapSummaryReader.ReadAsync(inputPath, warnings, cancellationToken));
            }
        }

        return BuildReport(inputPaths.Length, observations, fileSummaries, pcapSummaries, parsedTextDiffs, warnings);
    }

    public async ValueTask<SimagicCaptureAnalysisReport> AnalyzeDiffAsync(
        string leftPath,
        string rightPath,
        CancellationToken cancellationToken = default)
    {
        var left = await AnalyzePathAsync(leftPath, cancellationToken);
        var right = await AnalyzePathAsync(rightPath, cancellationToken);
        var leftObservations = await LoadObservationsOnlyAsync(leftPath, cancellationToken);
        var rightObservations = await LoadObservationsOnlyAsync(rightPath, cancellationToken);
        var diffs = new SimagicPayloadDiffAnalyzer().FindClosestPairs(leftObservations, rightObservations);

        var warnings = left.Warnings.Concat(right.Warnings).ToArray();
        return BuildReport(
            left.SourceFileCount + right.SourceFileCount,
            leftObservations.Concat(rightObservations),
            left.FileSummaries.Concat(right.FileSummaries),
            left.PcapSummaries.Concat(right.PcapSummaries),
            diffs,
            warnings);
    }

    private async ValueTask<IReadOnlyList<SimagicUsbPayloadObservation>> LoadObservationsOnlyAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var report = await AnalyzePathAsync(path, cancellationToken);
        var inputPaths = ResolveInputPaths(path);
        var observations = new List<SimagicUsbPayloadObservation>();
        foreach (var inputPath in inputPaths)
        {
            var kind = DetermineKind(inputPath);
            if (kind == SimagicCaptureAnalysisSourceKind.WiresharkCsv)
            {
                observations.AddRange((await ReadCsvAsync(inputPath, cancellationToken)).Observations);
            }
            else if (kind == SimagicCaptureAnalysisSourceKind.WiresharkTextSummary)
            {
                observations.AddRange((await ReadTextSummaryAsync(inputPath, cancellationToken)).Observations);
            }
        }

        if (observations.Count == 0 && report.TopPayloads.Count > 0)
        {
            return [];
        }

        return observations;
    }

    private static SimagicCaptureAnalysisReport BuildReport(
        int sourceFileCount,
        IEnumerable<SimagicUsbPayloadObservation> observations,
        IEnumerable<SimagicCaptureFileSummary> fileSummaries,
        IEnumerable<SimagicPcapCaptureSummary> pcapSummaries,
        IEnumerable<SimagicPayloadDiffObservation> diffObservations,
        IEnumerable<SimagicCaptureAnalysisWarning> warnings)
    {
        var observationArray = observations.ToArray();
        var topPayloads = observationArray
            .GroupBy(observation => observation.PayloadFingerprint, StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group.OrderBy(observation => observation.TimestampSeconds ?? double.MaxValue).ToArray();
                return new SimagicUsbPayloadSummary
                {
                    PayloadFingerprint = group.Key,
                    PayloadLength = ordered[0].PayloadLength,
                    Count = group.Sum(observation => Math.Max(1, observation.Count)),
                    PayloadPreviewHex = ordered[0].PayloadPreviewHex,
                    SourceFileNames = group.Select(observation => observation.SourceFileName)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    SourceColumns = group.Select(observation => observation.SourceColumn)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    FirstTimestampSeconds = ordered.Select(observation => observation.TimestampSeconds).FirstOrDefault(value => value is not null),
                    LastTimestampSeconds = ordered.Select(observation => observation.TimestampSeconds).LastOrDefault(value => value is not null)
                };
            })
            .OrderByDescending(summary => summary.Count)
            .ThenBy(summary => summary.PayloadLength)
            .ThenBy(summary => summary.PayloadFingerprint, StringComparer.Ordinal)
            .Take(32)
            .ToArray();

        return new SimagicCaptureAnalysisReport
        {
            SourceFileCount = sourceFileCount,
            PayloadObservationCount = observationArray.Sum(observation => Math.Max(1, observation.Count)),
            UniquePayloadCount = observationArray.Select(observation => observation.PayloadFingerprint).Distinct(StringComparer.Ordinal).Count(),
            FileSummaries = fileSummaries.OrderBy(summary => summary.SourceFileName, StringComparer.OrdinalIgnoreCase).ToArray(),
            TopPayloads = topPayloads,
            PcapSummaries = pcapSummaries.OrderBy(summary => summary.SourceFileName, StringComparer.OrdinalIgnoreCase).ToArray(),
            DiffObservations = diffObservations.OrderBy(diff => diff.ChangedByteCount).ThenBy(diff => diff.LeftFingerprint, StringComparer.Ordinal).ToArray(),
            Warnings = warnings.OrderBy(warning => warning.SourceFileName, StringComparer.OrdinalIgnoreCase).ThenBy(warning => warning.Message, StringComparer.Ordinal).ToArray()
        };
    }

    private static string[] ResolveInputPaths(string path)
    {
        if (File.Exists(path))
        {
            return [Path.GetFullPath(path)];
        }

        if (!Directory.Exists(path))
        {
            throw new FileNotFoundException("Capture analysis input path does not exist.", path);
        }

        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(file => SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SimagicCaptureAnalysisSourceKind DetermineKind(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csv" => SimagicCaptureAnalysisSourceKind.WiresharkCsv,
            ".txt" => SimagicCaptureAnalysisSourceKind.WiresharkTextSummary,
            ".pcapng" => SimagicCaptureAnalysisSourceKind.PcapNg,
            ".pcap" => SimagicCaptureAnalysisSourceKind.PcapClassic,
            _ => SimagicCaptureAnalysisSourceKind.Unknown
        };
    }

    private static async ValueTask<CsvReadResult> ReadCsvAsync(string path, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        if (lines.Length == 0)
        {
            return new CsvReadResult(
                [],
                CreateFileSummary(fileName, SimagicCaptureAnalysisSourceKind.WiresharkCsv, []),
                [new SimagicCaptureAnalysisWarning { SourceFileName = fileName, Message = "CSV file is empty." }]);
        }

        var header = SimagicCsv.ParseLine(lines[0]).Select(value => value.Trim()).ToArray();
        var columnLookup = header
            .Select((name, index) => new { name, index })
            .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
        var payloadColumn = FindFirstColumn(columnLookup, "payload_spaced", "payload", "usb.data_fragment", "usbhid.data");
        var frameColumn = FindFirstColumn(columnLookup, "frame", "frame.number");
        var timeColumn = FindFirstColumn(columnLookup, "time", "frame.time_relative");
        var kindColumn = FindFirstColumn(columnLookup, "kind", "_ws.col.info");
        var fileColumn = FindFirstColumn(columnLookup, "file");
        var warnings = new List<SimagicCaptureAnalysisWarning>();

        if (payloadColumn is null)
        {
            warnings.Add(new SimagicCaptureAnalysisWarning
            {
                SourceFileName = fileName,
                Message = "CSV file has no recognized payload column."
            });
            return new CsvReadResult(
                [],
                CreateFileSummary(fileName, SimagicCaptureAnalysisSourceKind.WiresharkCsv, []),
                warnings);
        }

        var observations = new List<SimagicUsbPayloadObservation>();
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[lineIndex]))
            {
                continue;
            }

            var values = SimagicCsv.ParseLine(lines[lineIndex]);
            var payloadText = ValueAt(values, payloadColumn.Value);
            if (!SimagicPayloadHex.TryParse(payloadText, out var bytes))
            {
                continue;
            }

            var sourceFileName = ValueAt(values, fileColumn) ?? fileName;
            observations.Add(CreateObservation(
                Path.GetFileName(sourceFileName),
                header[payloadColumn.Value],
                bytes,
                ValueAt(values, kindColumn),
                ParseNullableInt(ValueAt(values, frameColumn)),
                ParseNullableDouble(ValueAt(values, timeColumn))));
        }

        return new CsvReadResult(
            observations,
            CreateFileSummary(fileName, SimagicCaptureAnalysisSourceKind.WiresharkCsv, observations),
            warnings);
    }

    private static async ValueTask<TextReadResult> ReadTextSummaryAsync(string path, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var observations = new List<SimagicUsbPayloadObservation>();
        var summaries = new Dictionary<string, MutableFileSummary>(StringComparer.OrdinalIgnoreCase);
        var diffs = new List<SimagicPayloadDiffObservation>();
        var warnings = new List<SimagicCaptureAnalysisWarning>();
        var currentSource = fileName;
        MutableTextDiff? currentDiff = null;

        foreach (var line in lines)
        {
            var fileMatch = FileHeaderRegex().Match(line);
            if (fileMatch.Success)
            {
                currentSource = Path.GetFileName(fileMatch.Groups["file"].Value.Trim());
                EnsureSummary(summaries, currentSource).Kind = SimagicCaptureAnalysisSourceKind.WiresharkTextSummary;
                continue;
            }

            var declaredPayloadMatch = DeclaredPayloadRecordsRegex().Match(line);
            if (declaredPayloadMatch.Success)
            {
                EnsureSummary(summaries, currentSource).DeclaredPayloadRecordCount = int.Parse(declaredPayloadMatch.Groups["count"].Value, CultureInfo.InvariantCulture);
                continue;
            }

            var setReportMatch = DeclaredSetReportRegex().Match(line);
            if (setReportMatch.Success)
            {
                EnsureSummary(summaries, currentSource).DeclaredSetReportCandidateCount = int.Parse(setReportMatch.Groups["count"].Value, CultureInfo.InvariantCulture);
                continue;
            }

            var payloadMatch = CountedPayloadRegex().Match(line);
            if (payloadMatch.Success && SimagicPayloadHex.TryParse(payloadMatch.Groups["payload"].Value, out var countedBytes))
            {
                var observation = CreateObservation(
                    currentSource,
                    "usb.data_fragment",
                    countedBytes,
                    "set_report_candidate",
                    ParseNullableInt(payloadMatch.Groups["frame"].Value),
                    ParseNullableDouble(payloadMatch.Groups["time"].Value),
                    int.Parse(payloadMatch.Groups["count"].Value, CultureInfo.InvariantCulture));
                observations.Add(observation);
                EnsureSummary(summaries, currentSource).Observations.Add(observation);
                continue;
            }

            var compareMatch = CompareHeaderRegex().Match(line);
            if (compareMatch.Success)
            {
                currentDiff = null;
                continue;
            }

            var changedMatch = ChangedBytesRegex().Match(line);
            if (changedMatch.Success)
            {
                if (currentDiff is not null)
                {
                    diffs.Add(currentDiff.ToObservation());
                }

                currentDiff = new MutableTextDiff
                {
                    ChangedByteCount = int.Parse(changedMatch.Groups["count"].Value, CultureInfo.InvariantCulture)
                };
                continue;
            }

            var comparePayloadMatch = ComparePayloadRegex().Match(line);
            if (comparePayloadMatch.Success && SimagicPayloadHex.TryParse(comparePayloadMatch.Groups["payload"].Value, out var compareBytes))
            {
                var source = Path.GetFileName(comparePayloadMatch.Groups["file"].Value.Trim());
                var observation = CreateObservation(source, "compare_payload", compareBytes, "compare_payload", null, null);
                observations.Add(observation);
                EnsureSummary(summaries, source).Observations.Add(observation);

                if (currentDiff is not null)
                {
                    if (currentDiff.LeftBytes.Length == 0)
                    {
                        currentDiff.LeftSource = source;
                        currentDiff.LeftBytes = compareBytes;
                    }
                    else if (currentDiff.RightBytes.Length == 0)
                    {
                        currentDiff.RightSource = source;
                        currentDiff.RightBytes = compareBytes;
                    }
                }

                continue;
            }

            var byteDiffMatch = ByteDiffRegex().Match(line);
            if (byteDiffMatch.Success && currentDiff is not null)
            {
                currentDiff.Differences.Add(new SimagicPayloadByteDifference
                {
                    Offset = int.Parse(byteDiffMatch.Groups["offset"].Value, CultureInfo.InvariantCulture),
                    HexOffset = byteDiffMatch.Groups["hex"].Value,
                    LeftValueHex = byteDiffMatch.Groups["left"].Value.ToUpperInvariant(),
                    RightValueHex = byteDiffMatch.Groups["right"].Value.ToUpperInvariant()
                });
            }
        }

        if (currentDiff is not null)
        {
            diffs.Add(currentDiff.ToObservation());
        }

        if (summaries.Count == 0)
        {
            summaries[fileName] = new MutableFileSummary
            {
                SourceFileName = fileName,
                Kind = SimagicCaptureAnalysisSourceKind.WiresharkTextSummary,
                Observations = observations
            };
        }

        return new TextReadResult(
            observations,
            summaries.Values.Select(summary => summary.ToSummary()).ToArray(),
            diffs.Where(diff => diff.Differences.Count > 0).ToArray(),
            warnings);
    }

    private static SimagicUsbPayloadObservation CreateObservation(
        string sourceFileName,
        string sourceColumn,
        byte[] bytes,
        string? recordKind,
        int? frameNumber,
        double? timestampSeconds,
        int count = 1)
    {
        return new SimagicUsbPayloadObservation
        {
            SourceFileName = string.IsNullOrWhiteSpace(sourceFileName) ? "unknown" : Path.GetFileName(sourceFileName),
            SourceColumn = sourceColumn,
            RecordKind = string.IsNullOrWhiteSpace(recordKind) ? null : recordKind,
            FrameNumber = frameNumber,
            TimestampSeconds = timestampSeconds,
            PayloadLength = bytes.Length,
            Count = Math.Max(1, count),
            PayloadFingerprint = SimagicPayloadHex.Fingerprint(bytes),
            PayloadPreviewHex = SimagicPayloadHex.Preview(bytes),
            PayloadBytes = bytes
        };
    }

    private static SimagicCaptureFileSummary CreateFileSummary(
        string fileName,
        SimagicCaptureAnalysisSourceKind kind,
        IReadOnlyList<SimagicUsbPayloadObservation> observations)
    {
        return new SimagicCaptureFileSummary
        {
            SourceFileName = fileName,
            SourceKind = kind,
            PayloadRecordCount = observations.Sum(observation => Math.Max(1, observation.Count)),
            PayloadColumnCounts = observations
                .GroupBy(observation => observation.SourceColumn, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Sum(item => Math.Max(1, item.Count)), StringComparer.OrdinalIgnoreCase),
            PayloadLengthCounts = observations
                .GroupBy(observation => observation.PayloadLength)
                .ToDictionary(group => group.Key, group => group.Sum(item => Math.Max(1, item.Count)))
        };
    }

    private static MutableFileSummary EnsureSummary(Dictionary<string, MutableFileSummary> summaries, string sourceFileName)
    {
        if (!summaries.TryGetValue(sourceFileName, out var summary))
        {
            summary = new MutableFileSummary
            {
                SourceFileName = sourceFileName,
                Kind = SimagicCaptureAnalysisSourceKind.WiresharkTextSummary
            };
            summaries[sourceFileName] = summary;
        }

        return summary;
    }

    private static int? FindFirstColumn(IReadOnlyDictionary<string, int> lookup, params string[] names)
    {
        foreach (var name in names)
        {
            if (lookup.TryGetValue(name, out var index))
            {
                return index;
            }
        }

        return null;
    }

    private static string? ValueAt(IReadOnlyList<string> values, int? index)
    {
        if (index is null || index.Value < 0 || index.Value >= values.Count)
        {
            return null;
        }

        var value = values[index.Value].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double? ParseNullableDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private sealed record CsvReadResult(
        IReadOnlyList<SimagicUsbPayloadObservation> Observations,
        SimagicCaptureFileSummary Summary,
        IReadOnlyList<SimagicCaptureAnalysisWarning> Warnings);

    private sealed record TextReadResult(
        IReadOnlyList<SimagicUsbPayloadObservation> Observations,
        IReadOnlyList<SimagicCaptureFileSummary> FileSummaries,
        IReadOnlyList<SimagicPayloadDiffObservation> DiffObservations,
        IReadOnlyList<SimagicCaptureAnalysisWarning> Warnings);

    private sealed class MutableFileSummary
    {
        public string SourceFileName { get; init; } = "";

        public SimagicCaptureAnalysisSourceKind Kind { get; set; }

        public int? DeclaredPayloadRecordCount { get; set; }

        public int? DeclaredSetReportCandidateCount { get; set; }

        public List<SimagicUsbPayloadObservation> Observations { get; set; } = [];

        public SimagicCaptureFileSummary ToSummary()
        {
            var summary = CreateFileSummary(SourceFileName, Kind, Observations);
            return summary with
            {
                DeclaredPayloadRecordCount = DeclaredPayloadRecordCount,
                DeclaredSetReportCandidateCount = DeclaredSetReportCandidateCount
            };
        }
    }

    private sealed class MutableTextDiff
    {
        public string LeftSource { get; set; } = "";

        public string RightSource { get; set; } = "";

        public byte[] LeftBytes { get; set; } = [];

        public byte[] RightBytes { get; set; } = [];

        public int ChangedByteCount { get; set; }

        public List<SimagicPayloadByteDifference> Differences { get; } = [];

        public SimagicPayloadDiffObservation ToObservation()
        {
            return new SimagicPayloadDiffObservation
            {
                LeftSource = LeftSource,
                RightSource = RightSource,
                LeftFingerprint = LeftBytes.Length == 0 ? "" : SimagicPayloadHex.Fingerprint(LeftBytes),
                RightFingerprint = RightBytes.Length == 0 ? "" : SimagicPayloadHex.Fingerprint(RightBytes),
                PayloadLength = LeftBytes.Length == 0 ? RightBytes.Length : LeftBytes.Length,
                ChangedByteCount = ChangedByteCount,
                LeftPayloadPreviewHex = SimagicPayloadHex.Preview(LeftBytes),
                RightPayloadPreviewHex = SimagicPayloadHex.Preview(RightBytes),
                Differences = Differences
            };
        }
    }

    [GeneratedRegex(@"^\s*FILE:\s*(?<file>.+?)\s*$")]
    private static partial Regex FileHeaderRegex();

    [GeneratedRegex(@"^\s*All payload records:\s*(?<count>\d+)\s*$")]
    private static partial Regex DeclaredPayloadRecordsRegex();

    [GeneratedRegex(@"^\s*SET_REPORT candidate records:\s*(?<count>\d+)\s*$")]
    private static partial Regex DeclaredSetReportRegex();

    [GeneratedRegex(@"count=\s*(?<count>\d+)\s+len=\s*(?<len>\d+)\s+time=\s*(?<time>[0-9.]+)\s+frame=\s*(?<frame>\d+)\s+payload=(?<payload>(?:[0-9A-Fa-f]{2}\s*)+)$")]
    private static partial Regex CountedPayloadRegex();

    [GeneratedRegex(@"^\s*COMPARE:\s*(?<left>.+?)\s+VS\s+(?<right>.+?)\s*$")]
    private static partial Regex CompareHeaderRegex();

    [GeneratedRegex(@"^\s*changed bytes:\s*(?<count>\d+)\s*$")]
    private static partial Regex ChangedBytesRegex();

    [GeneratedRegex(@"^\s*(?<file>[^:]+\.csv):\s*(?<payload>(?:[0-9A-Fa-f]{2}\s*)+)$")]
    private static partial Regex ComparePayloadRegex();

    [GeneratedRegex(@"byte\s+(?<offset>\d+)\s+/\s+(?<hex>0x[0-9A-Fa-f]+):\s+.+?=(?<left>[0-9A-Fa-f]{2}),\s+.+?=(?<right>[0-9A-Fa-f]{2})\s*$")]
    private static partial Regex ByteDiffRegex();
}
