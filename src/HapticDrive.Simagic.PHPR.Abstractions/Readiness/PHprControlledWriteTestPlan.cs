namespace HapticDrive.Simagic.PHPR.Abstractions.Readiness;

public sealed record PHprControlledWriteTestPlan(
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> FirstPulseLimits,
    IReadOnlyList<string> TestSequence,
    IReadOnlyList<string> PassCriteria,
    IReadOnlyList<string> AbortCriteria,
    IReadOnlyList<string> EvidenceReferences)
{
    public static PHprControlledWriteTestPlan Stage2P { get; } = new(
        Preconditions:
        [
            "User physically present.",
            "SimPro Manager closed unless explicitly testing coexistence.",
            "SimHub closed unless explicitly testing coexistence.",
            "P700 connected and selected.",
            "Brake and throttle P-HPR modules installed and mapped.",
            "Emergency stop visible.",
            "Device, interface, and report selection confirmed.",
            "Real writes disabled by default."
        ],
        FirstPulseLimits:
        [
            "One module only.",
            "Brake first recommended.",
            "Strength <= 10%.",
            "Duration <= 100 ms.",
            "Conservative known frequency.",
            "No loop or repeated pulse.",
            "Immediate stop available."
        ],
        TestSequence:
        [
            "Confirm read-only inventory.",
            "Confirm SimPro/SimHub coexistence status is clear.",
            "Enable direct-control mode.",
            "Select target device/interface/report.",
            "Arm direct control.",
            "Send one low-strength brake pulse.",
            "Verify stop.",
            "Send one low-strength throttle pulse.",
            "Verify stop.",
            "Test emergency stop.",
            "Test telemetry stale, emergency mute, DrivingArmed, and SimPro conflict gates.",
            "Record manual result."
        ],
        PassCriteria:
        [
            "Pulse occurs only on the selected pedal.",
            "No continuous runaway vibration.",
            "Stop works.",
            "Emergency stop works.",
            "App remains responsive.",
            "Direct control cannot start when SimPro/SimHub conflict is active.",
            "No writes occur unless explicitly enabled and armed."
        ],
        AbortCriteria:
        [
            "Wrong pedal vibrates.",
            "Both pedals vibrate unexpectedly.",
            "Vibration continues after stop.",
            "App freezes.",
            "Device disconnects.",
            "SimPro/SimHub conflict appears.",
            "Report/interface identity is unknown.",
            "Unexpected motion, noise, heat, or stronger-than-expected output."
        ],
        EvidenceReferences:
        [
            "SimHub F1 EC start/stop hypothesis.",
            "Brake module byte 01 and throttle module byte 02 for SimHub F1 EC.",
            "Strength and frequency direct-byte mapping for SimHub F1 EC.",
            "Duration handled as software-timed start then stop.",
            "SimPro 80 1E 89 family remains separate and unresolved for the first direct path.",
            "GT Neo paddle mappings are input-only.",
            "P700 brake/throttle mappings are input-only and separate from P-HPR output."
        ]);
}
