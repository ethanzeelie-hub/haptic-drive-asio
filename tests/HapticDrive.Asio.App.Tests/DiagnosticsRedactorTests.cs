using HapticDrive.Asio.Core.Diagnostics;

namespace HapticDrive.Asio.App.Tests;

public sealed class DiagnosticsRedactorTests
{
    [Fact]
    public void SyntheticSecretCorpus_SafeBundleContainsNoPrivateValues()
    {
        var redactor = new SupportBundleDiagnosticRedactor(DiagnosticRedactionMode.Safe);
        var corpus =
            """
            approval: I approve Phase 2 controlled P-HPR write testing
            path: C:\Users\ethan\OneDrive\Documents\secret\road-flight.jsonl
            unc: \\rig-pc\captures\raw.bin
            ipv4: 192.168.1.50
            ipv6-loopback: ::1
            ipv6-ula: fd00::1234
            ipv6-linklocal: fe80::1234%12
            ipv4-mapped: ::ffff:192.168.1.50
            hid: \\?\hid#vid_3670&pid_0905#private-serial
            serial: ABC123456789
            pid: 4242
            raw usb payload: 01 02 03 04 05 06
            raw protocol bytes: AA BB CC DD
            """;

        var result = redactor.RedactText(corpus);

        Assert.DoesNotContain("I approve Phase 2 controlled P-HPR write testing", result, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Users\ethan", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\\rig-pc\", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.168.1.50", result, StringComparison.Ordinal);
        Assert.DoesNotContain("::1", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fd00::1234", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fe80::1234%12", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-serial", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ABC123456789", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4242", result, StringComparison.Ordinal);
        Assert.DoesNotContain("01 02 03 04 05 06", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AA BB CC DD", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redactor_RedactsIpv6UncAndIpv4MappedIpv6()
    {
        var redactor = new SupportBundleDiagnosticRedactor(DiagnosticRedactionMode.Safe);

        var result = redactor.RedactText(@"Host path \\rig-pc\captures\raw.bin via ::ffff:192.168.1.12 and fd00::beef.");

        Assert.DoesNotContain(@"\\rig-pc\captures\raw.bin", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("::ffff:192.168.1.12", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fd00::beef", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<private-ip>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtendedMode_StillRemovesUsbSerialsAndApprovalPhrase()
    {
        var redactor = new SupportBundleDiagnosticRedactor(DiagnosticRedactionMode.Extended);

        var result = redactor.RedactText(
            "I approve Phase 2 controlled P-HPR write testing serial: ABC123456789 raw usb payload: 01 02 03 04 05 06 source 192.168.1.50");

        Assert.DoesNotContain("I approve Phase 2 controlled P-HPR write testing", result, StringComparison.Ordinal);
        Assert.DoesNotContain("ABC123456789", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("01 02 03 04 05 06", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("192.168.1.50", result, StringComparison.Ordinal);
    }
}
