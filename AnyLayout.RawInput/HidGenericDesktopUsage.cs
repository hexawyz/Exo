﻿namespace AnyLayout.RawInput
{
    public enum HidGenericDesktopUsage : ushort
    {
        Undefined = 0x00,

        Pointer = 0x01,
        Mouse = 0x02,
        Joystick = 0x04,
        Gamepad = 0x05,
        Keyboard = 0x06,
        Keypad = 0x07,
        MultiAxisController = 0x08,

        TabletPcSystemControls = 0x09,

        PortableDeviceControl = 0x0D,

        X = 0x30,
        Y = 0x31,
        Z = 0x32,
        Rx = 0x33,
        Ry = 0x34,
        Rz = 0x35,
        Slider = 0x36,
        Dial = 0x37,
        Wheel = 0x38,
        HatSwitch = 0x39,

        CountedBuffer = 0x3A,
        ByteCount = 0x3B,

        MotionWakeup = 0x3C,
        Start = 0x3D,
        Select = 0x3E,

        Vx = 0x40,
        Vy = 0x41,
        Vz = 0x42,
        VbrX = 0x43,
        VbrY = 0x44,
        VbrZ = 0x45,
        Vno = 0x46,

        FeatureNotification = 0x47,

        ResolutionMultiplier = 0x48,

        SystemControl = 0x80,
        SystemPower = 0x81,
        SystemSleep = 0x82,
        SystemWake = 0x83,
        SystemContextMenu = 0x84,
        SystemMainMenu = 0x85,
        SystemAppMenu = 0x86,
        SystemHelpMenu = 0x87,
        SystemMenuExit = 0x88,
        SystemMenuSelect = 0x89,
        SystemMenuRight = 0x8A,
        SystemMenuLeft = 0x8B,
        SystemMenuUp = 0x8C,
        SystemMenuDown = 0x8D,
        SystemColdRestart = 0x8E,
        SystemWarmRestart = 0x8F,

        DirectionPadUp = 0x90,
        DirectionPadDown = 0x91,
        DirectionPadRight = 0x92,
        DirectionPadLeft = 0x93,

        SystemDock = 0xA0,
        SystemUndock = 0xA1,
        SystemSetup = 0xA2,
        SystemBreak = 0xA3,
        SystemDebuggerBreak = 0xA4,
        ApplicationBreak = 0xA5,
        ApplicationDebuggerBreak = 0xA6,
        SystemSpeakerMute = 0xA7,
        SystemHibernate = 0xA8,

        SystemDisplayInvert = 0xB0,
        SystemDisplayInternal = 0xB1,
        SystemDisplayExternal = 0xB2,
        SystemDisplayBoth = 0xB3,
        SystemDisplayDual = 0xB4,
        SystemDisplayToggleInternalExternal = 0xB5,
        SystemDisplaySwapPrimarySecondary = 0xB6,
        SystemDisplayLcdAutoscale = 0xB7,

        SystemDisplayRotationLockButton = 0xC9,
        SystemDisplayRotationLockSliderSwitch = 0xCA,
        ControlEnable = 0xCB,
    }
}
