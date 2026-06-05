namespace HapticDrive.Simagic.PHPR.Research.Capture;

public static class SimagicCaptureScenarios
{
    private static readonly IReadOnlyDictionary<SimagicCaptureScenarioId, SimagicCaptureScenario> ScenarioById =
        CreateScenarios().ToDictionary(scenario => scenario.Id);

    public static IReadOnlyList<SimagicCaptureScenario> RequiredScenarios { get; } =
        ScenarioById.Values.OrderBy(scenario => (int)scenario.Id).ToArray();

    public static SimagicCaptureScenario Get(SimagicCaptureScenarioId id)
    {
        return ScenarioById[id];
    }

    public static bool TryGet(SimagicCaptureScenarioId id, out SimagicCaptureScenario scenario)
    {
        return ScenarioById.TryGetValue(id, out scenario!);
    }

    private static IReadOnlyList<SimagicCaptureScenario> CreateScenarios()
    {
        return
        [
            new()
            {
                Id = SimagicCaptureScenarioId.SimProOpened,
                Name = "SimPro Manager opened with P700 / P-HPR connected",
                Slug = "simpro-opened",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Unknown,
                Description = "Start capture before opening SimPro Manager, then stop after P700 / P-HPR device detection is stable."
            },
            new()
            {
                Id = SimagicCaptureScenarioId.SimProClosed,
                Name = "SimPro Manager closed with P700 / P-HPR connected",
                Slug = "simpro-closed",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Unknown,
                Description = "Capture the quiet state after SimPro Manager has been closed and the pedals remain connected."
            },
            new()
            {
                Id = SimagicCaptureScenarioId.BrakeTestVibration,
                Name = "Brake P-HPR test vibration",
                Slug = "brake-test",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Brake,
                Description = "Trigger only the brake P-HPR vendor test vibration.",
                RequiresStrength = true,
                RequiresFrequency = true,
                RequiresDuration = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.ThrottleTestVibration,
                Name = "Throttle P-HPR test vibration",
                Slug = "throttle-test",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Throttle,
                Description = "Trigger only the throttle P-HPR vendor test vibration.",
                RequiresStrength = true,
                RequiresFrequency = true,
                RequiresDuration = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.BrakeStrengthChanged,
                Name = "Brake P-HPR strength changed only",
                Slug = "brake-strength",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Brake,
                Description = "Change only brake strength while leaving frequency and duration unchanged.",
                RequiresStrength = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.ThrottleStrengthChanged,
                Name = "Throttle P-HPR strength changed only",
                Slug = "throttle-strength",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Throttle,
                Description = "Change only throttle strength while leaving frequency and duration unchanged.",
                RequiresStrength = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.BrakeFrequencyChanged,
                Name = "Brake P-HPR frequency changed only",
                Slug = "brake-frequency",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Brake,
                Description = "Change only brake frequency while leaving strength and duration unchanged.",
                RequiresFrequency = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.ThrottleFrequencyChanged,
                Name = "Throttle P-HPR frequency changed only",
                Slug = "throttle-frequency",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Throttle,
                Description = "Change only throttle frequency while leaving strength and duration unchanged.",
                RequiresFrequency = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.BrakeDurationChanged,
                Name = "Brake P-HPR pulse duration changed only",
                Slug = "brake-duration",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Brake,
                Description = "Change only brake pulse duration while leaving strength and frequency unchanged.",
                RequiresDuration = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.ThrottleDurationChanged,
                Name = "Throttle P-HPR pulse duration changed only",
                Slug = "throttle-duration",
                SoftwareUnderTest = "simpro",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Throttle,
                Description = "Change only throttle pulse duration while leaving strength and frequency unchanged.",
                RequiresDuration = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.SimHubGearShiftTest,
                Name = "SimHub P-HPR gear-shift test",
                Slug = "simhub-gear-shift",
                SoftwareUnderTest = "simhub",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Both,
                Description = "Trigger only the SimHub P-HPR gear-shift test if it is available.",
                RequiresStrength = true,
                RequiresFrequency = true,
                RequiresDuration = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.SimHubWheelLockTest,
                Name = "SimHub P-HPR wheel-lock test",
                Slug = "simhub-wheel-lock",
                SoftwareUnderTest = "simhub",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Brake,
                Description = "Trigger only the SimHub P-HPR wheel-lock test if it is available.",
                RequiresStrength = true,
                RequiresFrequency = true,
                RequiresDuration = true
            },
            new()
            {
                Id = SimagicCaptureScenarioId.SimHubWheelSlipTest,
                Name = "SimHub P-HPR wheel-slip test",
                Slug = "simhub-wheel-slip",
                SoftwareUnderTest = "simhub",
                DeviceName = "p700",
                RecommendedTarget = SimagicCaptureTargetModule.Throttle,
                Description = "Trigger only the SimHub P-HPR wheel-slip test if it is available.",
                RequiresStrength = true,
                RequiresFrequency = true,
                RequiresDuration = true
            }
        ];
    }
}
