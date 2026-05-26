using Channels;

namespace Common;

public sealed class ReservedChannels
{
    public static readonly List<ChannelDefinition> Channels =
    [
        // Car Temps
        new() { Id = Guid.Parse("3b63f11f-224b-45d9-8b7a-12be256025a6"), Name = "CoolantTemp", IsReserved = true, Abbreviation = "COOLNT", DataType = "Temperature", BaseUnitType = "DegreeFahrenheit", OutputUnitType = "DegreeFahrenheit", Category = "Car Temps" },
        new() { Id = Guid.Parse("c6b14eae-9070-4c2a-91a9-2ce3984d30a7"), Name = "OilTemp", IsReserved = true, Abbreviation = "OILT", DataType = "Temperature", BaseUnitType = "DegreeFahrenheit", OutputUnitType = "DegreeFahrenheit", Category = "Car Temps" },
        new() { Id = Guid.Parse("e43dab22-2971-4de7-a116-b27bb3f85231"), Name = "TransTemp", IsReserved = true, Abbreviation = "TRANST", DataType = "Temperature", BaseUnitType = "DegreeFahrenheit", OutputUnitType = "DegreeFahrenheit", Category = "Car Temps" },
        new() { Id = Guid.Parse("6e704607-45ba-4449-a42e-cf52fcd86b17"), Name = "DiffTemp", IsReserved = true, Abbreviation = "DIFFT", DataType = "Temperature", BaseUnitType = "DegreeFahrenheit", OutputUnitType = "DegreeFahrenheit", Category = "Car Temps" },
        new() { Id = Guid.Parse("6349e70f-171f-4dda-900d-0e4b2a9af7f6"), Name = "AirTemp", IsReserved = true, Abbreviation = "AIRTMP", DataType = "Temperature", BaseUnitType = "DegreeFahrenheit", OutputUnitType = "DegreeFahrenheit", Category = "Car Temps" },

        // Car Pressures
        new() { Id = Guid.Parse("5d7f1ae4-3a6f-45fe-94c2-905e07af4be1"), Name = "OilPress", IsReserved = true, Abbreviation = "OILP", DataType = "Pressure", BaseUnitType = "PoundForcePerSquareInch", OutputUnitType = "PoundForcePerSquareInch", Category = "Car Pressures" },
        new() { Id = Guid.Parse("a7e58908-c5d0-4438-be04-fef0ecc492a0"), Name = "FuelPress", IsReserved = true, Abbreviation = "FUELP", DataType = "Pressure", BaseUnitType = "PoundForcePerSquareInch", OutputUnitType = "PoundForcePerSquareInch", Category = "Car Pressures" },
        new() { Id = Guid.Parse("eb494292-9c88-4e0e-b0d9-6dbd063b292c"), Name = "TireFLPress", IsReserved = true, Abbreviation = "TIREFL", DataType = "Pressure", BaseUnitType = "PoundForcePerSquareInch", OutputUnitType = "PoundForcePerSquareInch", Category = "Car Pressures", GroupTag = "Tire Pressures" },
        new() { Id = Guid.Parse("21b8654d-2160-41e3-9bc5-2b8c5171e003"), Name = "TireFRPress", IsReserved = true, Abbreviation = "TIREFR", DataType = "Pressure", BaseUnitType = "PoundForcePerSquareInch", OutputUnitType = "PoundForcePerSquareInch", Category = "Car Pressures", GroupTag = "Tire Pressures" },
        new() { Id = Guid.Parse("d75a4c25-f764-4033-add1-bc92e9c09ae4"), Name = "TireRLPress", IsReserved = true, Abbreviation = "TIRERL", DataType = "Pressure", BaseUnitType = "PoundForcePerSquareInch", OutputUnitType = "PoundForcePerSquareInch", Category = "Car Pressures", GroupTag = "Tire Pressures" },
        new() { Id = Guid.Parse("ab7a81df-463e-4759-97c1-3aef2988bbbc"), Name = "TireRRPress", IsReserved = true, Abbreviation = "TIRERR", DataType = "Pressure", BaseUnitType = "PoundForcePerSquareInch", OutputUnitType = "PoundForcePerSquareInch", Category = "Car Pressures", GroupTag = "Tire Pressures" },

        // Fuel
        new() { Id = Guid.Parse("a2529acf-a7c6-449f-8a85-c7d76b35dbcb"), Name = "FuelLevel", IsReserved = true, Abbreviation = "FL", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", Category = "Fuel" },
        new() { Id = Guid.Parse("740ce2a6-dc88-4425-85dc-7f99f2a902f1"), Name = "FuelUsed", IsReserved = true, Abbreviation = "FLUSED", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", Category = "Fuel" },
        new() { Id = Guid.Parse("acd3d127-acaf-4f8a-b27a-8623cfda09f3"), Name = "TripFuel", IsReserved = true, Abbreviation = "FLTRIP", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", Category = "Fuel" },
        new() { Id = Guid.Parse("e6b74429-c087-41d5-b78e-406a602d1504"), Name = "TripDistance", IsReserved = true, Abbreviation = "FLDIST", DataType = "Length", BaseUnitType = "Mile", OutputUnitType = "Mile", Category = "Fuel" },
        new() { Id = Guid.Parse("fcb490e7-e5ef-4b91-99d1-685f81d91112"), Name = "FuelConsumption", IsReserved = true, Abbreviation = "FLCONS", DataType = "VolumeFlow", BaseUnitType = "UsGallonPerMinute", OutputUnitType = "UsGallonPerMinute", Category = "Fuel", Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("c3b94831-95f6-4935-bf67-1aacfd611f75"), Name = "FuelFull", IsReserved = true, Abbreviation = "FLFULL", DataType = "Unitless", Category = "Fuel" },
        new() { Id = Guid.Parse("33a050a2-3aed-43cf-866e-0ab8ae7d3766"), Name = "FuelLow", IsReserved = true, Abbreviation = "FLLOW", DataType = "Unitless", Category = "Fuel" },

        // DTCs and warnings
        new() { Id = Guid.Parse("d79e7974-7a4e-4eac-b8a8-d4d45bec2448"), Name = "CheckEngine", IsReserved = true, Abbreviation = "CHKENG", DataType = "Unitless", Category = "DTCs" },
        new() { Id = Guid.Parse("f5465f03-5f5a-4d6d-83c4-5ea23c0964a3"), Name = "DTCLevel", IsReserved = true, Abbreviation = "DTC", DataType = "Unitless", Category = "DTCs" },

        // Brakes
        new() { Id = Guid.Parse("e5755aa0-3802-4729-9210-3c3e9dc8a644"), Name = "BrakePos", IsReserved = true, Abbreviation = "BRKPO", DataType = "Ratio", BaseUnitType = "Percent", OutputUnitType = "Percent", Category = "Brakes" },
        new() { Id = Guid.Parse("9c4ba95c-a57d-4ede-9544-bc01246876c8"), Name = "BrakePress", IsReserved = true, Abbreviation = "BRKPRS", DataType = "Pressure", BaseUnitType = "PoundForcePerSquareInch", OutputUnitType = "PoundForcePerSquareInch", Category = "Brakes" },

        // PDM
        new() { Id = Guid.Parse("0913a56c-9d1c-4c6b-acf8-1963e2cb91dc"), Name = "TotalPowerCurrent", IsReserved = true, Abbreviation = "TOTPOW", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("75712356-63eb-4159-8e2f-fc19739724f0"), Name = "FuelPumpCurrent", IsReserved = true, Abbreviation = "FULPMP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("cb891a5c-169d-4d2a-a58e-b83c71e1bee5"), Name = "LiftPumpCurrent", IsReserved = true, Abbreviation = "LIFTP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("f6f3e31d-f059-449b-93b3-19c6dd2d1dca"), Name = "Abs1Current", IsReserved = true, Abbreviation = "ABS1P", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("8e320e34-13e5-4a49-8b91-3a9ba676ddb0"), Name = "Abs2Current", IsReserved = true, Abbreviation = "ABS2P", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("9c5226b5-c594-4625-abe2-fac71dea2a1b"), Name = "HeadlightsCurrent", IsReserved = true, Abbreviation = "HLDP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("b756681f-985c-44a2-b6bb-bffbc0f64c60"), Name = "TailLightsCurrent", IsReserved = true, Abbreviation = "TLLP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("b5173c8a-0eba-45a7-9032-2e4483272301"), Name = "BrakeLightsCurrent", IsReserved = true, Abbreviation = "BRKLP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("0e54d2ba-cc74-42dc-a1af-0ae0408be2aa"), Name = "DiffPumpCurrent", IsReserved = true, Abbreviation = "DIFFP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("0226c44f-2be8-4c65-984f-af61c71c31b6"), Name = "DiffFanCurrent", IsReserved = true, Abbreviation = "DIFFAN", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("7f09aafa-8d5f-454c-abb8-3ff3c841a61d"), Name = "TransFan1Current", IsReserved = true, Abbreviation = "TRNF1", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("860616c3-170e-401f-9647-26dceb466e4a"), Name = "TransFan2Current", IsReserved = true, Abbreviation = "TRNF2", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("6eb75890-d549-4329-9f5d-551ae0df5b76"), Name = "RadiatorFanCurrent", IsReserved = true, Abbreviation = "RADFAN", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("1b7c2f3b-f99d-42b7-8cd2-ecc2755133e7"), Name = "FuelCoolerCurrent", IsReserved = true, Abbreviation = "FUELC", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("ed0df2ca-77ae-4a7e-9fd6-9260f978301c"), Name = "AccusumpCurrent", IsReserved = true, Abbreviation = "ACCUSP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("5fe5e32f-0093-4242-9c35-cf5778c4b494"), Name = "CoolshirtCurrent", IsReserved = true, Abbreviation = "COOLSH", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("eccdb12a-aaa6-498a-94f5-17be598eb602"), Name = "FuelSolenoidCurrent", IsReserved = true, Abbreviation = "FUELS", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("58d20155-36a4-402d-bf4c-0cb8af5f26ac"), Name = "RainLightCurrent", IsReserved = true, Abbreviation = "RAINLP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },
        new() { Id = Guid.Parse("950c1ad4-afc5-41a7-aead-b8759e45abda"), Name = "RadioCurrent", IsReserved = true, Abbreviation = "RADIOP", DataType = "ElectricCurrent", BaseUnitType = "Ampere", OutputUnitType = "Ampere", Category = "PDM" },

        // Lap
        new() { Id = Guid.Parse("c354b4ae-64d9-47ee-9f02-ba9c44cf6b74"), Name = "LastLapTime", IsReserved = true, Abbreviation = "LLT", DataType = "String", Category = "Lap" },
        new() { Id = Guid.Parse("42c9312d-8e13-4422-af9e-bbc34a12bade"), Name = "BestLapTime", IsReserved = true, Abbreviation = "BLT", DataType = "String", Category = "Lap" },
        new() { Id = Guid.Parse("6559b6e7-d289-4d46-b4ff-70ddeb930318"), Name = "SessionTime", IsReserved = true, Abbreviation = "ST", DataType = "String", Category = "Lap" },
        new() { Id = Guid.Parse("4e70c2d0-d89c-4896-af7c-a286ceda9565"), Name = "Position", IsReserved = true, Abbreviation = "POS", DataType = "Unitless", Category = "Lap" },
        new() { Id = Guid.Parse("7e8153fd-7280-4bcf-a11b-2227b70daddb"), Name = "ClassPosition", IsReserved = true, Abbreviation = "CPOS", DataType = "Unitless", Category = "Lap" },
        new() { Id = Guid.Parse("da12563a-1167-4899-9956-700b0b693005"), Name = "InPit", IsReserved = true, Abbreviation = "INPIT", DataType = "Unitless", Category = "Lap" },
        new() { Id = Guid.Parse("9c5cd301-749e-4bc9-830b-e32baaef2cb0"), Name = "Latitude", IsReserved = true, Abbreviation = "LAT", DataType = "Unitless", Category = "Lap" },
        new() { Id = Guid.Parse("90e054c7-6782-4423-a6eb-7b1ccb9c22a5"), Name = "Longitude", IsReserved = true, Abbreviation = "LON", DataType = "Unitless", Category = "Lap" },

        // Forces
        new() { Id = Guid.Parse("0f6331ae-634a-41d5-bb26-b8ab15b57d50"), Name = "LateralAcc", IsReserved = true, Abbreviation = "LATACC", DataType = "Unitless", Category = "Forces" },
        new() { Id = Guid.Parse("7cee7835-d630-4986-b205-342d73e4055e"), Name = "VerticalAcc", IsReserved = true, Abbreviation = "VERACC", DataType = "Unitless", Category = "Forces" },
        new() { Id = Guid.Parse("38c6c64c-bb8f-4d07-b97c-b197ae8ed7fc"), Name = "InlineAcc", IsReserved = true, Abbreviation = "INLACC", DataType = "Unitless", Category = "Forces" },

        // Speed
        new() { Id = Guid.Parse("cf8698bf-3a17-4cb1-b993-c14937ad2fae"), Name = "GPSSpeed", IsReserved = true, Abbreviation = "GPSSP", DataType = "Speed", BaseUnitType = "MilePerHour", OutputUnitType = "MilePerHour", Category = "Speed" },
        new() { Id = Guid.Parse("07e845bb-493b-4e63-b3d3-0d95480e5335"), Name = "WheelSpeed", IsReserved = true, Abbreviation = "WHLSP", DataType = "Speed", BaseUnitType = "MilePerHour", OutputUnitType = "MilePerHour", Category = "Speed" },

        // Other
        new() { Id = Guid.Parse("74c57a58-d78d-499a-977b-11cee221926a"), Name = "EngineRPM", IsReserved = true, Abbreviation = "RPM", DataType = "Unitless" },
        new() { Id = Guid.Parse("52cba3bc-c5f5-4fb2-8060-c2444eb448c3"), Name = "Battery", IsReserved = true, Abbreviation = "BATT", DataType = "ElectricPotential", BaseUnitType = "Volt", OutputUnitType = "Volt" },
        new() { Id = Guid.Parse("9c45a39f-a107-41db-86d2-1560c1d79cfd"), Name = "Odometer", IsReserved = true, Abbreviation = "ODOM", DataType = "Length", BaseUnitType = "Mile", OutputUnitType = "Mile" },
        new() { Id = Guid.Parse("7d75b9ad-82e5-4449-b7b5-a1226dc5a824"), Name = "SmartyCamRecording", IsReserved = true, Abbreviation = "SCMREC", DataType = "Unitless" },
        new() { Id = Guid.Parse("a013b26a-537d-4b8f-8b39-1cc4f20e2e8a"), Name = "SmartyCamSpaceRemaining", IsReserved = true, Abbreviation = "SMRTSP", DataType = "Unitless" },

        // Engine
        new() { Id = Guid.Parse("c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01"), Name = "ThrottlePosition", IsReserved = true, Abbreviation = "THROT", DataType = "Ratio", BaseUnitType = "Percent", OutputUnitType = "Percent", Category = "Engine", LowRange = 0, HighRange = 100, Distribution = ChannelDistribution.CarToCloud, ManagedByFeature = "throttle-consumption" },

        // Race (team-scoped)
        new() { Id = Guid.Parse("d5b2e9f4-3c1a-4e7d-9b8f-2e4f6c1d3b02"), Name = "RaceFlagState", IsReserved = true, Abbreviation = "FLAG", DataType = "String", Category = "Race", Distribution = ChannelDistribution.CloudToCar, Scope = ChannelScope.PerTeam, ManagedByFeature = "fuel-analysis" },

        // Fuel Analysis — cloud-sourced inputs and reconciler outputs
        new() { Id = Guid.Parse("e6c3f1a5-4d2b-4f8e-1c9a-3f5a7d2e4c03"), Name = "ManualFuelAddedGallons", IsReserved = true, Abbreviation = "MFADD", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", OutputDecimalPlaces = 3, Category = "Fuel", LowRange = 0, HighRange = 50, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("f7d4a2b6-5e3c-4a9f-2d1b-4a6b8e3f5d04"), Name = "FuelRangeMinutes", IsReserved = true, Abbreviation = "FLRMIN", DataType = "Duration", BaseUnitType = "Minute", OutputUnitType = "Minute", OutputDecimalPlaces = 1, Category = "Fuel", LowRange = 0, HighRange = 600, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("18e5b3c7-6f4d-4b1a-3e2c-5b7c9f4a6e05"), Name = "FuelRangeGallons", IsReserved = true, Abbreviation = "FLRGAL", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", OutputDecimalPlaces = 3, Category = "Fuel", LowRange = 0, HighRange = 50, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("29f6c4d8-7a5e-4c2b-4f3d-6c8d1a5b7f06"), Name = "FuelRangeLaps", IsReserved = true, Abbreviation = "FLRLAP", DataType = "Unitless", Category = "Fuel", LowRange = 0, HighRange = 500, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("3a07d5e9-8b6f-4d3c-5a4e-7d9e2b6c8a07"), Name = "FuelRangeMinutesHighConf", IsReserved = true, Abbreviation = "FLRMHC", DataType = "Duration", BaseUnitType = "Minute", OutputUnitType = "Minute", OutputDecimalPlaces = 1, Category = "Fuel", LowRange = 0, HighRange = 600, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("4b18e6fa-9c7a-4e4d-6b5f-8e1f3c7d9b08"), Name = "FuelRangeGallonsHighConf", IsReserved = true, Abbreviation = "FLRGHC", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", OutputDecimalPlaces = 3, Category = "Fuel", LowRange = 0, HighRange = 50, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("5c29f70b-1d8b-4f5e-7c6a-9f2a4d8e1c09"), Name = "FuelRangeLapsHighConf", IsReserved = true, Abbreviation = "FLRLHC", DataType = "Unitless", Category = "Fuel", LowRange = 0, HighRange = 500, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("6d3a181c-2e9c-4a6f-8d7b-1a3b5e9f2d0a"), Name = "FuelRangeConfidence", IsReserved = true, Abbreviation = "FLRCNF", DataType = "Ratio", BaseUnitType = "Percent", OutputUnitType = "Percent", OutputDecimalPlaces = 1, Category = "Fuel", LowRange = 0, HighRange = 100, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("7e4b292d-3fad-4b7a-9e8c-2b4c6f1a3e0b"), Name = "FuelConsumptionGalPerLap", IsReserved = true, Abbreviation = "FLCPL", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", OutputDecimalPlaces = 3, Category = "Fuel", LowRange = 0, HighRange = 10, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },
        new() { Id = Guid.Parse("8f5c3a3e-4abe-4c8b-1f9d-3c5d7a2b4f0c"), Name = "FuelWindowElapsedMinutes", IsReserved = true, Abbreviation = "FLWELM", DataType = "Duration", BaseUnitType = "Minute", OutputUnitType = "Minute", OutputDecimalPlaces = 1, Category = "Fuel", LowRange = 0, HighRange = 600, Distribution = ChannelDistribution.CloudLocal, ManagedByFeature = "fuel-analysis", ProducedByFeature = "fuel-analysis" },

        // Throttle Consumption — in-car throttle proxy module outputs.
        // Distribution is locked: the cloud-side ThrottleProxyIntegralEstimator and ThrottleProxyGridEstimator
        // short-circuit without these values; the whole point of the in-car proxy is to feed cloud reconciliation.
        new() { Id = Guid.Parse("916d4b4f-5bcf-4d9c-2a1e-4d6e8b3c5a0d"), Name = "ThrottleProxyFuelUsed", IsReserved = true, Abbreviation = "TPFUSE", DataType = "Volume", BaseUnitType = "UsGallon", OutputUnitType = "UsGallon", OutputDecimalPlaces = 3, Category = "Fuel", LowRange = 0, HighRange = 100, Distribution = ChannelDistribution.CarToCloud, IsDistributionLocked = true, ManagedByFeature = "throttle-consumption", ProducedByFeature = "throttle-consumption" },
        new() { Id = Guid.Parse("a27e5c50-6cd1-4eac-3b2f-5e7f9c4d6b0e"), Name = "ThrottleProxyRate", IsReserved = true, Abbreviation = "TPRATE", DataType = "VolumeFlow", BaseUnitType = "UsGallonPerMinute", OutputUnitType = "UsGallonPerMinute", OutputDecimalPlaces = 3, Category = "Fuel", LowRange = 0, HighRange = 10, Distribution = ChannelDistribution.CarToCloud, IsDistributionLocked = true, ManagedByFeature = "throttle-consumption", ProducedByFeature = "throttle-consumption" },
        new() { Id = Guid.Parse("b38f6d61-7de2-4fbd-4c3a-6f8a1d5e7c0f"), Name = "ThrottleProxyConfidence", IsReserved = true, Abbreviation = "TPCONF", DataType = "Ratio", BaseUnitType = "Percent", OutputUnitType = "Percent", OutputDecimalPlaces = 1, Category = "Fuel", LowRange = 0, HighRange = 100, Distribution = ChannelDistribution.CarToCloud, IsDistributionLocked = true, ManagedByFeature = "throttle-consumption", ProducedByFeature = "throttle-consumption" },
        new() { Id = Guid.Parse("c49a7e72-8ef3-4ace-5d4b-7a9b2e6f8d10"), Name = "ThrottleProxyGridCoverage", IsReserved = true, Abbreviation = "TPGRID", DataType = "Ratio", BaseUnitType = "Percent", OutputUnitType = "Percent", OutputDecimalPlaces = 1, Category = "Fuel", LowRange = 0, HighRange = 100, Distribution = ChannelDistribution.CarToCloud, IsDistributionLocked = true, ManagedByFeature = "throttle-consumption", ProducedByFeature = "throttle-consumption" },
    ];
}
