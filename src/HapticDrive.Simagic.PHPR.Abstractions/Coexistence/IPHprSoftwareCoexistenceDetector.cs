namespace HapticDrive.Simagic.PHPR.Abstractions.Coexistence;

public interface IPHprSoftwareCoexistenceDetector
{
    PHprSoftwareCoexistenceSnapshot Scan();
}
