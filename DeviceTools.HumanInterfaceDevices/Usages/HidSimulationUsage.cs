namespace DeviceTools.HumanInterfaceDevices.Usages
{
    public enum HidSimulationUsage : ushort
    {
        Undefined = 0x00,

        FlightSimulationDevice = 0x01,
        AutomobileSimulationDevice = 0x02,
        TankSimulationDevice = 0x03,
        SpaceshipSimulationDevice = 0x04,
        SubmarineSimulationDevice = 0x05,
        SailingSimulationDevice = 0x06,
        MotorcycleSimulationDevice = 0x07,
        SportsSimulationDevice = 0x08,
        AirplaneSimulationDevice = 0x09,
        HelicopterSimulationDevice = 0x0A,
        MagicCarpetSimulationDevice = 0x0B,
        BicylceSimulationDevice = 0x0C,

        FlightControlStick = 0x20,
        FlightStick = 0x21,
        CyclicControl = 0x22,
        CyclicTrim = 0x23,
        FlightYoke = 0x24,
        TrackControl = 0x25,

        Aileron = 0xB0,
        AileronTrim = 0xB1,
        AntiTorqueControl = 0xB2,
        AutopilotEnable = 0xB3,
        ChaffRelease = 0xB4,
        CollectiveControl = 0xB5,
        DiveBrake = 0xB6,
        ElectronicCountermeasures = 0xB7,
        Elevator = 0xB8,
        ElevatorTrim = 0xB9,
        Rudder = 0xBA,
        Throttle = 0xBB,
        FlightCommunications = 0xBC,
        FlareRelease = 0xBD,
        LandingGear = 0xBE,
        ToeBrake = 0xBF,
        Trigger = 0xC0,
        WeaponsArm = 0xC1,
        WeaponsSelect = 0xC2,
        WingFlaps = 0xC3,
        Accelerator = 0xC4,
        Brake = 0xC5,
        Clutch = 0xC6,
        Shifter = 0xC7,
        Steering = 0xC8,
        TurretDirection = 0xC9,
        BarrelElevation = 0xCA,
        DivePlante = 0xCB,
        Ballast = 0xCC,
        BicycleCrank = 0xCD,
        HandleBars = 0xCE,
        FrontBrake = 0xCF,
        RearBrake = 0xD0,
    }
}
