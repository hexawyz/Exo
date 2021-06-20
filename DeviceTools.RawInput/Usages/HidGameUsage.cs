namespace DeviceTools.RawInput.Usages
{
    public enum HidGameUsage : ushort
    {
        Undefined = 0x00,
        GameController3D = 0x01,
        PinballDevice = 0x02,
        GunDevice = 0x03,

        PointOfView = 0x20,
        TurnRightLeft = 0x21,
        PitchForwardBackward = 0x22,
        RollRightLeft = 0x23,
        MoveRightLeft = 0x24,
        MoveForwardBackward = 0x25,
        MoveUpDown = 0x26,
        LeanRightLeft = 0x27,
        LeanForwardBackward = 0x28,
        HeightOfPov = 0x29,
        Flipper = 0x2A,
        SecondaryFlipper = 0x2B,
        Bump = 0x2C,
        NewGame = 0x2D,
        ShootBall = 0x2E,
        Player = 0x2F,
        GunBolt = 0x30,
        GunClip = 0x31,
        GunSelector = 0x32,
        GunSingleShot = 0x33,
        GunBurst = 0x34,
        GunAutomatic = 0x35,
        GunSafety = 0x36,
        GamepadFireJump = 0x37,
        GamepadTrigger = 0x39,
    }
}
