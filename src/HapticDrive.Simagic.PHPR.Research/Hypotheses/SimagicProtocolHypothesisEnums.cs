namespace HapticDrive.Simagic.PHPR.Research.Hypotheses;

public enum SimagicProtocolHypothesisConfidence
{
    Unknown,
    Low,
    Medium,
    High,
    ConfirmedObservation
}

public enum SimagicProtocolHypothesisStatus
{
    EvidenceOnly,
    Hypothesis,
    NeedsMoreCaptures,
    ReadyForMockProtocol,
    BlockedForRealWrite,
    Superseded
}

public enum SimagicProtocolFamily
{
    P700HidInput,
    GtNeoHidInput,
    SimHubF1EcSetReport,
    SimPro801E89SetReport,
    RuntimeIdentity,
    SafetyBoundary
}

public enum SimagicProtocolSource
{
    ArchitectureRule,
    Stage2IAnalysis,
    SanitizedEvidenceBundle,
    SimHub,
    SimProManager,
    P700PedalInputCapture,
    GtNeoPaddleInputCapture
}

public enum SimagicProtocolRisk
{
    NoWriteApproval,
    RealStopUnvalidated,
    DeviceIdentityUnvalidated,
    ReportIdOrInterfaceUnvalidated,
    SimProSimHubCoexistenceUnvalidated,
    InputOutputBoundary,
    ProductionEncoderForbidden,
    PrivateDataLeak
}
