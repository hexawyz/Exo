namespace DeviceTools.HumanInterfaceDevices.Usages
{
    public enum HidGenericDeviceUsage : ushort
    {
        Undefined = 0x00,

        BatteryStrength = 0x20,
        WirelessChannel = 0x21,
        WirelessId = 0x22,
        DiscoverWirelessControl = 0x23,
        SecurityCodeCharacterEntered = 0x24,
        SecurityCodeCharacterErased = 0x25,
        SecurityCodeCleared = 0x26,
    }
}
