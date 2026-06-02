using System.Text;

namespace HapticDrive.Asio.Telemetry.F1_25;

public static class F125PacketParser
{
    public static F125PacketParseResult Parse(ReadOnlySpan<byte> datagram)
    {
        var headerResult = F125PacketHeaderParser.Parse(datagram);

        if (headerResult.Failed)
        {
            return F125PacketParseResult.Failure(
                headerResult.Header,
                headerResult.Definition,
                headerResult.RawDatagram,
                headerResult.Message);
        }

        if (headerResult.WasIgnored)
        {
            return F125PacketParseResult.Ignored(
                headerResult.Header,
                headerResult.Definition,
                headerResult.RawDatagram,
                headerResult.Message);
        }

        if (headerResult.Header is null || headerResult.Definition is null)
        {
            return F125PacketParseResult.Failure(
                headerResult.Header,
                headerResult.Definition,
                headerResult.RawDatagram,
                "Header parser returned success without a header and packet definition.");
        }

        if (!headerResult.Definition.IsV1RequiredPacket)
        {
            return F125PacketParseResult.Ignored(
                headerResult.Header,
                headerResult.Definition,
                headerResult.RawDatagram,
                $"{headerResult.Definition.Name} packet is known but not parsed in Stage 07.");
        }

        try
        {
            var bodyData = datagram[F125PacketDefinitions.HeaderSize..];
            var body = ParseBody(headerResult.Definition.Kind, bodyData);
            return F125PacketParseResult.Success(
                headerResult.Header,
                headerResult.Definition,
                body,
                headerResult.RawDatagram);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException)
        {
            return F125PacketParseResult.Failure(
                headerResult.Header,
                headerResult.Definition,
                headerResult.RawDatagram,
                $"{headerResult.Definition.Name} body parse failed: {ex.Message}");
        }
    }

    private static F125PacketBody ParseBody(F125PacketKind kind, ReadOnlySpan<byte> bodyData)
    {
        return kind switch
        {
            F125PacketKind.Motion => ParseMotion(bodyData),
            F125PacketKind.Session => ParseSession(bodyData),
            F125PacketKind.LapData => ParseLapData(bodyData),
            F125PacketKind.Event => ParseEvent(bodyData),
            F125PacketKind.Participants => ParseParticipants(bodyData),
            F125PacketKind.CarTelemetry => ParseCarTelemetry(bodyData),
            F125PacketKind.CarStatus => ParseCarStatus(bodyData),
            F125PacketKind.CarDamage => ParseCarDamage(bodyData),
            F125PacketKind.MotionEx => ParseMotionEx(bodyData),
            _ => throw new FormatException($"{kind} is not a Stage 07 packet body.")
        };
    }

    private static F125MotionPacketBody ParseMotion(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var cars = ReadCarMotionDataArray(bodyData, ref offset, F125PacketDefinitions.CarCount);
        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.Motion);
        return new F125MotionPacketBody(cars);
    }

    private static F125CarMotionData ReadCarMotionData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadInt16(data, ref offset),
            F125BinaryReader.ReadInt16(data, ref offset),
            F125BinaryReader.ReadInt16(data, ref offset),
            F125BinaryReader.ReadInt16(data, ref offset),
            F125BinaryReader.ReadInt16(data, ref offset),
            F125BinaryReader.ReadInt16(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset));
    }

    private static F125SessionPacketBody ParseSession(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var weather = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var trackTemperature = F125BinaryReader.ReadInt8(bodyData, ref offset);
        var airTemperature = F125BinaryReader.ReadInt8(bodyData, ref offset);
        var totalLaps = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var trackLength = F125BinaryReader.ReadUInt16(bodyData, ref offset);
        var sessionType = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var trackId = F125BinaryReader.ReadInt8(bodyData, ref offset);
        var formula = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var sessionTimeLeft = F125BinaryReader.ReadUInt16(bodyData, ref offset);
        var sessionDuration = F125BinaryReader.ReadUInt16(bodyData, ref offset);
        var pitSpeedLimit = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var gamePaused = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var isSpectating = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var spectatorCarIndex = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var sliProNativeSupport = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var numMarshalZones = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var marshalZones = ReadMarshalZoneArray(bodyData, ref offset, F125PacketDefinitions.MarshalZoneCount);
        var safetyCarStatus = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var networkGame = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var numWeatherForecastSamples = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var weatherForecastSamples = ReadWeatherForecastSampleArray(bodyData, ref offset, F125PacketDefinitions.WeatherForecastSampleCount);
        var forecastAccuracy = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var aiDifficulty = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var seasonLinkIdentifier = F125BinaryReader.ReadUInt32(bodyData, ref offset);
        var weekendLinkIdentifier = F125BinaryReader.ReadUInt32(bodyData, ref offset);
        var sessionLinkIdentifier = F125BinaryReader.ReadUInt32(bodyData, ref offset);
        var pitStopWindowIdealLap = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var pitStopWindowLatestLap = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var pitStopRejoinPosition = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var steeringAssist = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var brakingAssist = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var gearboxAssist = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var pitAssist = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var pitReleaseAssist = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var ersAssist = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var drsAssist = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var dynamicRacingLine = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var dynamicRacingLineType = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var gameMode = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var ruleSet = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var timeOfDay = F125BinaryReader.ReadUInt32(bodyData, ref offset);
        var sessionLength = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var speedUnitsLeadPlayer = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var temperatureUnitsLeadPlayer = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var speedUnitsSecondaryPlayer = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var temperatureUnitsSecondaryPlayer = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var numSafetyCarPeriods = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var numVirtualSafetyCarPeriods = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var numRedFlagPeriods = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var equalCarPerformance = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var recoveryMode = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var flashbackLimit = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var surfaceType = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var lowFuelMode = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var raceStarts = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var tyreTemperature = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var pitLaneTyreSim = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var carDamage = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var carDamageRate = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var collisions = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var collisionsOffForFirstLapOnly = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var mpUnsafePitRelease = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var mpOffForGriefing = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var cornerCuttingStringency = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var parcFermeRules = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var pitStopExperience = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var safetyCar = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var safetyCarExperience = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var formationLap = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var formationLapExperience = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var redFlags = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var affectsLicenceLevelSolo = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var affectsLicenceLevelMp = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var numSessionsInWeekend = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var weekendStructure = F125BinaryReader.ReadBytes(bodyData, ref offset, F125PacketDefinitions.WeekendStructureCount);
        var sector2LapDistanceStart = F125BinaryReader.ReadSingle(bodyData, ref offset);
        var sector3LapDistanceStart = F125BinaryReader.ReadSingle(bodyData, ref offset);

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.Session);

        return new F125SessionPacketBody(
            weather,
            trackTemperature,
            airTemperature,
            totalLaps,
            trackLength,
            sessionType,
            trackId,
            formula,
            sessionTimeLeft,
            sessionDuration,
            pitSpeedLimit,
            gamePaused,
            isSpectating,
            spectatorCarIndex,
            sliProNativeSupport,
            numMarshalZones,
            marshalZones,
            safetyCarStatus,
            networkGame,
            numWeatherForecastSamples,
            weatherForecastSamples,
            forecastAccuracy,
            aiDifficulty,
            seasonLinkIdentifier,
            weekendLinkIdentifier,
            sessionLinkIdentifier,
            pitStopWindowIdealLap,
            pitStopWindowLatestLap,
            pitStopRejoinPosition,
            steeringAssist,
            brakingAssist,
            gearboxAssist,
            pitAssist,
            pitReleaseAssist,
            ersAssist,
            drsAssist,
            dynamicRacingLine,
            dynamicRacingLineType,
            gameMode,
            ruleSet,
            timeOfDay,
            sessionLength,
            speedUnitsLeadPlayer,
            temperatureUnitsLeadPlayer,
            speedUnitsSecondaryPlayer,
            temperatureUnitsSecondaryPlayer,
            numSafetyCarPeriods,
            numVirtualSafetyCarPeriods,
            numRedFlagPeriods,
            equalCarPerformance,
            recoveryMode,
            flashbackLimit,
            surfaceType,
            lowFuelMode,
            raceStarts,
            tyreTemperature,
            pitLaneTyreSim,
            carDamage,
            carDamageRate,
            collisions,
            collisionsOffForFirstLapOnly,
            mpUnsafePitRelease,
            mpOffForGriefing,
            cornerCuttingStringency,
            parcFermeRules,
            pitStopExperience,
            safetyCar,
            safetyCarExperience,
            formationLap,
            formationLapExperience,
            redFlags,
            affectsLicenceLevelSolo,
            affectsLicenceLevelMp,
            numSessionsInWeekend,
            weekendStructure,
            sector2LapDistanceStart,
            sector3LapDistanceStart);
    }

    private static F125MarshalZone ReadMarshalZone(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadInt8(data, ref offset));
    }

    private static F125WeatherForecastSample ReadWeatherForecastSample(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadInt8(data, ref offset),
            F125BinaryReader.ReadInt8(data, ref offset),
            F125BinaryReader.ReadInt8(data, ref offset),
            F125BinaryReader.ReadInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset));
    }

    private static F125LapDataPacketBody ParseLapData(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var lapData = ReadLapDataArray(bodyData, ref offset, F125PacketDefinitions.CarCount);
        var timeTrialPbCarIndex = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var timeTrialRivalCarIndex = F125BinaryReader.ReadUInt8(bodyData, ref offset);

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.LapData);

        return new F125LapDataPacketBody(lapData, timeTrialPbCarIndex, timeTrialRivalCarIndex);
    }

    private static F125LapData ReadLapData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadUInt32(data, ref offset),
            F125BinaryReader.ReadUInt32(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset));
    }

    private static F125EventPacketBody ParseEvent(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var eventCodeBytes = F125BinaryReader.ReadBytes(bodyData, ref offset, F125PacketDefinitions.EventCodeLength);
        var eventDetailsRaw = F125BinaryReader.ReadBytes(bodyData, ref offset, F125PacketDefinitions.EventDetailsLength);
        var eventCode = Encoding.ASCII.GetString(eventCodeBytes);
        var eventDetails = ParseEventDetails(eventCode, eventDetailsRaw);

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.Event);

        return new F125EventPacketBody(eventCode, eventCodeBytes, eventDetails, eventDetailsRaw);
    }

    private static F125EventDetails ParseEventDetails(string eventCode, ReadOnlySpan<byte> details)
    {
        var offset = 0;

        return eventCode switch
        {
            "SSTA" or "SEND" or "DRSE" or "CHQF" or "LGOT" or "RDFL" => new F125EmptyEventDetails(eventCode),
            "FTLP" => new F125FastestLapEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadSingle(details, ref offset)),
            "RTMT" => new F125RetirementEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset)),
            "DRSD" => new F125DrsDisabledEventDetails(F125BinaryReader.ReadUInt8(details, ref offset)),
            "TMPT" => new F125TeamMateInPitsEventDetails(F125BinaryReader.ReadUInt8(details, ref offset)),
            "RCWN" => new F125RaceWinnerEventDetails(F125BinaryReader.ReadUInt8(details, ref offset)),
            "PENA" => new F125PenaltyEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset)),
            "SPTP" => new F125SpeedTrapEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadSingle(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadSingle(details, ref offset)),
            "STLG" => new F125StartLightsEventDetails(F125BinaryReader.ReadUInt8(details, ref offset)),
            "DTSV" => new F125DriveThroughPenaltyServedEventDetails(F125BinaryReader.ReadUInt8(details, ref offset)),
            "SGSV" => new F125StopGoPenaltyServedEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadSingle(details, ref offset)),
            "FLBK" => new F125FlashbackEventDetails(
                F125BinaryReader.ReadUInt32(details, ref offset),
                F125BinaryReader.ReadSingle(details, ref offset)),
            "BUTN" => new F125ButtonsEventDetails(F125BinaryReader.ReadUInt32(details, ref offset)),
            "OVTK" => new F125OvertakeEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset)),
            "SCAR" => new F125SafetyCarEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset)),
            "COLL" => new F125CollisionEventDetails(
                F125BinaryReader.ReadUInt8(details, ref offset),
                F125BinaryReader.ReadUInt8(details, ref offset)),
            _ => new F125UnknownEventDetails(eventCode)
        };
    }

    private static F125ParticipantsPacketBody ParseParticipants(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var numActiveCars = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var participants = ReadParticipantDataArray(bodyData, ref offset, F125PacketDefinitions.CarCount);

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.Participants);

        return new F125ParticipantsPacketBody(numActiveCars, participants);
    }

    private static F125ParticipantData ReadParticipantData(ReadOnlySpan<byte> data, ref int offset)
    {
        var aiControlled = F125BinaryReader.ReadUInt8(data, ref offset);
        var driverId = F125BinaryReader.ReadUInt8(data, ref offset);
        var networkId = F125BinaryReader.ReadUInt8(data, ref offset);
        var teamId = F125BinaryReader.ReadUInt8(data, ref offset);
        var myTeam = F125BinaryReader.ReadUInt8(data, ref offset);
        var raceNumber = F125BinaryReader.ReadUInt8(data, ref offset);
        var nationality = F125BinaryReader.ReadUInt8(data, ref offset);
        var nameBytes = F125BinaryReader.ReadBytes(data, ref offset, F125PacketDefinitions.ParticipantNameLength);
        var name = F125BinaryReader.ReadNullTerminatedUtf8(nameBytes);
        var yourTelemetry = F125BinaryReader.ReadUInt8(data, ref offset);
        var showOnlineNames = F125BinaryReader.ReadUInt8(data, ref offset);
        var techLevel = F125BinaryReader.ReadUInt16(data, ref offset);
        var platform = F125BinaryReader.ReadUInt8(data, ref offset);
        var numColours = F125BinaryReader.ReadUInt8(data, ref offset);
        var liveryColours = ReadLiveryColourArray(data, ref offset, F125PacketDefinitions.LiveryColourCount);

        return new F125ParticipantData(
            aiControlled,
            driverId,
            networkId,
            teamId,
            myTeam,
            raceNumber,
            nationality,
            nameBytes,
            name,
            yourTelemetry,
            showOnlineNames,
            techLevel,
            platform,
            numColours,
            liveryColours);
    }

    private static F125LiveryColour ReadLiveryColour(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset));
    }

    private static F125CarTelemetryPacketBody ParseCarTelemetry(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var carTelemetryData = ReadCarTelemetryDataArray(bodyData, ref offset, F125PacketDefinitions.CarCount);
        var mfdPanelIndex = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var mfdPanelIndexSecondaryPlayer = F125BinaryReader.ReadUInt8(bodyData, ref offset);
        var suggestedGear = F125BinaryReader.ReadInt8(bodyData, ref offset);

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.CarTelemetry);

        return new F125CarTelemetryPacketBody(
            carTelemetryData,
            mfdPanelIndex,
            mfdPanelIndexSecondaryPlayer,
            suggestedGear);
    }

    private static F125CarTelemetryData ReadCarTelemetryData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadInt8(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt16WheelData(data, ref offset),
            F125BinaryReader.ReadByteWheelData(data, ref offset),
            F125BinaryReader.ReadByteWheelData(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadSingleWheelData(data, ref offset),
            F125BinaryReader.ReadByteWheelData(data, ref offset));
    }

    private static F125CarStatusPacketBody ParseCarStatus(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var carStatusData = ReadCarStatusDataArray(bodyData, ref offset, F125PacketDefinitions.CarCount);

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.CarStatus);

        return new F125CarStatusPacketBody(carStatusData);
    }

    private static F125CarStatusData ReadCarStatusData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt16(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadInt8(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadSingle(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset));
    }

    private static F125CarDamagePacketBody ParseCarDamage(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var carDamageData = ReadCarDamageDataArray(bodyData, ref offset, F125PacketDefinitions.CarCount);

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.CarDamage);

        return new F125CarDamagePacketBody(carDamageData);
    }

    private static F125CarDamageData ReadCarDamageData(ReadOnlySpan<byte> data, ref int offset)
    {
        return new(
            F125BinaryReader.ReadSingleWheelData(data, ref offset),
            F125BinaryReader.ReadByteWheelData(data, ref offset),
            F125BinaryReader.ReadByteWheelData(data, ref offset),
            F125BinaryReader.ReadByteWheelData(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset),
            F125BinaryReader.ReadUInt8(data, ref offset));
    }

    private static F125MotionExPacketBody ParseMotionEx(ReadOnlySpan<byte> bodyData)
    {
        var offset = 0;
        var body = new F125MotionExPacketBody(
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingle(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset),
            F125BinaryReader.ReadSingleWheelData(bodyData, ref offset));

        F125BinaryReader.EnsureConsumed(bodyData, offset, F125PacketKind.MotionEx);

        return body;
    }

    private static F125CarMotionData[] ReadCarMotionDataArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125CarMotionData[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadCarMotionData(data, ref offset);
        }

        return values;
    }

    private static F125MarshalZone[] ReadMarshalZoneArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125MarshalZone[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadMarshalZone(data, ref offset);
        }

        return values;
    }

    private static F125WeatherForecastSample[] ReadWeatherForecastSampleArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125WeatherForecastSample[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadWeatherForecastSample(data, ref offset);
        }

        return values;
    }

    private static F125LapData[] ReadLapDataArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125LapData[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadLapData(data, ref offset);
        }

        return values;
    }

    private static F125ParticipantData[] ReadParticipantDataArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125ParticipantData[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadParticipantData(data, ref offset);
        }

        return values;
    }

    private static F125LiveryColour[] ReadLiveryColourArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125LiveryColour[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadLiveryColour(data, ref offset);
        }

        return values;
    }

    private static F125CarTelemetryData[] ReadCarTelemetryDataArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125CarTelemetryData[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadCarTelemetryData(data, ref offset);
        }

        return values;
    }

    private static F125CarStatusData[] ReadCarStatusDataArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125CarStatusData[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadCarStatusData(data, ref offset);
        }

        return values;
    }

    private static F125CarDamageData[] ReadCarDamageDataArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var values = new F125CarDamageData[count];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadCarDamageData(data, ref offset);
        }

        return values;
    }
}
