using System.Buffers.Binary;

namespace HapticDrive.Simagic.PHPR.Research.CaptureAnalysis;

internal static class SimagicPcapSummaryReader
{
    private const uint PcapNgSectionHeaderBlock = 0x0A0D0D0A;
    private const uint PcapNgInterfaceDescriptionBlock = 0x00000001;
    private const uint PcapNgEnhancedPacketBlock = 0x00000006;
    private const int LinkTypeUsbPcap = 249;

    public static async ValueTask<SimagicPcapCaptureSummary> ReadAsync(
        string path,
        List<SimagicCaptureAnalysisWarning> warnings,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (bytes.Length < 4)
        {
            warnings.Add(new SimagicCaptureAnalysisWarning
            {
                SourceFileName = fileName,
                Message = "Capture file is too short to identify."
            });
            return new SimagicPcapCaptureSummary { SourceFileName = fileName, SourceKind = SimagicCaptureAnalysisSourceKind.Unknown };
        }

        if (ReadUInt32Little(bytes.AsSpan(0, 4)) == PcapNgSectionHeaderBlock)
        {
            return ReadPcapNg(fileName, bytes, warnings);
        }

        return ReadPcapClassic(fileName, bytes, warnings);
    }

    private static SimagicPcapCaptureSummary ReadPcapNg(
        string fileName,
        byte[] bytes,
        List<SimagicCaptureAnalysisWarning> warnings)
    {
        var offset = 0;
        var littleEndian = true;
        var sections = 0;
        var packetCount = 0;
        long capturedBytes = 0;
        var interfaces = new List<SimagicPcapInterfaceSummary>();

        while (offset + 12 <= bytes.Length)
        {
            var blockType = ReadUInt32(bytes.AsSpan(offset, 4), littleEndian);
            if (blockType == PcapNgSectionHeaderBlock)
            {
                if (offset + 12 > bytes.Length)
                {
                    break;
                }

                var magic = bytes.AsSpan(offset + 8, 4);
                if (ReadUInt32Little(magic) == 0x1A2B3C4D)
                {
                    littleEndian = true;
                }
                else if (ReadUInt32Big(magic) == 0x1A2B3C4D)
                {
                    littleEndian = false;
                }
                else
                {
                    warnings.Add(new SimagicCaptureAnalysisWarning
                    {
                        SourceFileName = fileName,
                        Message = "PCAPNG section header has an unknown byte-order magic."
                    });
                    break;
                }

                sections++;
            }

            var blockLength = ReadUInt32(bytes.AsSpan(offset + 4, 4), littleEndian);
            if (blockLength < 12 || offset + blockLength > bytes.Length)
            {
                warnings.Add(new SimagicCaptureAnalysisWarning
                {
                    SourceFileName = fileName,
                    Message = "PCAPNG block length is invalid or truncated."
                });
                break;
            }

            var body = bytes.AsSpan(offset + 8, (int)blockLength - 12);
            if (blockType == PcapNgInterfaceDescriptionBlock && body.Length >= 2)
            {
                var linkType = ReadUInt16(body[..2], littleEndian);
                interfaces.Add(new SimagicPcapInterfaceSummary
                {
                    InterfaceIndex = interfaces.Count,
                    LinkType = linkType
                });
            }
            else if (blockType == PcapNgEnhancedPacketBlock && body.Length >= 20)
            {
                packetCount++;
                capturedBytes += ReadUInt32(body.Slice(12, 4), littleEndian);
            }

            offset += (int)blockLength;
        }

        WarnForUsbPcap(fileName, interfaces, warnings);
        return new SimagicPcapCaptureSummary
        {
            SourceFileName = fileName,
            SourceKind = SimagicCaptureAnalysisSourceKind.PcapNg,
            Parsed = true,
            SectionCount = sections,
            InterfaceCount = interfaces.Count,
            PacketCount = packetCount,
            TotalCapturedBytes = capturedBytes,
            Interfaces = interfaces
        };
    }

    private static SimagicPcapCaptureSummary ReadPcapClassic(
        string fileName,
        byte[] bytes,
        List<SimagicCaptureAnalysisWarning> warnings)
    {
        if (bytes.Length < 24)
        {
            warnings.Add(new SimagicCaptureAnalysisWarning
            {
                SourceFileName = fileName,
                Message = "Classic pcap file is too short for a global header."
            });
            return new SimagicPcapCaptureSummary { SourceFileName = fileName, SourceKind = SimagicCaptureAnalysisSourceKind.PcapClassic };
        }

        var magicBytes = bytes.AsSpan(0, 4);
        var littleEndian = magicBytes is [0xD4, 0xC3, 0xB2, 0xA1] or [0x4D, 0x3C, 0xB2, 0xA1];
        var bigEndian = magicBytes is [0xA1, 0xB2, 0xC3, 0xD4] or [0xA1, 0xB2, 0x3C, 0x4D];
        if (!littleEndian && !bigEndian)
        {
            warnings.Add(new SimagicCaptureAnalysisWarning
            {
                SourceFileName = fileName,
                Message = "File is not recognized as pcapng or classic pcap."
            });
            return new SimagicPcapCaptureSummary { SourceFileName = fileName, SourceKind = SimagicCaptureAnalysisSourceKind.Unknown };
        }

        var linkType = ReadUInt32(bytes.AsSpan(20, 4), littleEndian);
        var packetCount = 0;
        long capturedBytes = 0;
        var offset = 24;
        while (offset + 16 <= bytes.Length)
        {
            var capturedLength = ReadUInt32(bytes.AsSpan(offset + 8, 4), littleEndian);
            if (offset + 16 + capturedLength > bytes.Length)
            {
                warnings.Add(new SimagicCaptureAnalysisWarning
                {
                    SourceFileName = fileName,
                    Message = "Classic pcap packet record is truncated."
                });
                break;
            }

            packetCount++;
            capturedBytes += capturedLength;
            offset += 16 + (int)capturedLength;
        }

        var interfaces = new[]
        {
            new SimagicPcapInterfaceSummary { InterfaceIndex = 0, LinkType = unchecked((int)linkType) }
        };
        WarnForUsbPcap(fileName, interfaces, warnings);
        return new SimagicPcapCaptureSummary
        {
            SourceFileName = fileName,
            SourceKind = SimagicCaptureAnalysisSourceKind.PcapClassic,
            Parsed = true,
            InterfaceCount = 1,
            PacketCount = packetCount,
            TotalCapturedBytes = capturedBytes,
            Interfaces = interfaces
        };
    }

    private static void WarnForUsbPcap(
        string fileName,
        IReadOnlyList<SimagicPcapInterfaceSummary> interfaces,
        List<SimagicCaptureAnalysisWarning> warnings)
    {
        if (interfaces.Any(item => item.LinkType == LinkTypeUsbPcap))
        {
            warnings.Add(new SimagicCaptureAnalysisWarning
            {
                SourceFileName = fileName,
                Message = "USBPcap link type detected. Stage 2I records container and transfer-summary observations only; Stage 2J documents protocol hypotheses separately."
            });
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian ? ReadUInt32Little(bytes) : ReadUInt32Big(bytes);
    }

    private static uint ReadUInt32Little(ReadOnlySpan<byte> bytes)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static uint ReadUInt32Big(ReadOnlySpan<byte> bytes)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }
}
