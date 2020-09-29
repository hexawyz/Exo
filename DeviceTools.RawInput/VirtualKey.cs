using System;
using System.Collections.Generic;
using System.Text;

namespace DeviceTools.RawInput
{
    public enum VirtualKey
    {
        LeftButton = 0x01,
        RightButton = 0x02,
        Cancel = 0x03,
        MiddleButton = 0x04,

        XButton1 = 0x05,
        XButton2 = 0x06,

        // 0x07 : reserved

        Back = 0x08,
        Tab = 0x09,

        // 0x0A - 0x0B : reserved

        Clear = 0x0C,
        Return = 0x0D,

        // 0x0E - 0x0F : unassigned

        Shift = 0x10,
        Control = 0x11,
        Menu = 0x12,
        Pause = 0x13,
        Capital = 0x14,

        Kana = 0x15,
        Hangul = 0x15,

        // 0x16 : unassigned

        Junja = 0x17,
        Final = 0x18,
        Hanja = 0x19,
        Kanji = 0x19,

        // 0x1A : unassigned

        Escape = 0x1B,

        Convert = 0x1C,
        NonConvert = 0x1D,
        Accept = 0x1E,
        ModeChange = 0x1F,

        Space = 0x20,
        PageUp = 0x21, // VK_PRIOR
        PageDown = 0x22, // VK_NEXT
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Select = 0x29,
        Print = 0x2A,
        Execute = 0x2B,
        Snapshot = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,
        Help = 0x2F,

        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,

        // 0x3A - 0x40 : unassigned

        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,

        LeftWindows = 0x5B,
        RightWindows = 0x5C,
        Apps = 0x5D,

        // 0x5E : reserved

        Sleep = 0x5F,

        Numpad0 = 0x60,
        Numpad1 = 0x61,
        Numpad2 = 0x62,
        Numpad3 = 0x63,
        Numpad4 = 0x64,
        Numpad5 = 0x65,
        Numpad6 = 0x66,
        Numpad7 = 0x67,
        Numpad8 = 0x68,
        Numpad9 = 0x69,
        Multiply = 0x6A,
        Add = 0x6B,
        Separator = 0x6C,
        Subtract = 0x6D,
        Decimal = 0x6E,
        Divide = 0x6F,
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
        F13 = 0x7C,
        F14 = 0x7D,
        F15 = 0x7E,
        F16 = 0x7F,
        F17 = 0x80,
        F18 = 0x81,
        F19 = 0x82,
        F20 = 0x83,
        F21 = 0x84,
        F22 = 0x85,
        F23 = 0x86,
        F24 = 0x87,

        // 0x88 - 0x8F : UI navigation

        NavigationView = 0x88, // reserved
        NavigationMenu = 0x89, // reserved
        NavigationUp = 0x8A, // reserved
        NavigationDown = 0x8B, // reserved
        NavigationLeft = 0x8C, // reserved
        NavigationRight = 0x8D, // reserved
        NavigationAccept = 0x8E, // reserved
        NavigationCancel = 0x8F, // reserved

        NumLock = 0x90,
        Scroll = 0x91,

        // NEC PC-9800 kbd definitions
        OemNecEqual = 0x92,   // '=' key on numpad

        // Fujitsu/OASYS kbd definitions
        OemFujitsuJisho = 0x92,   // 'Dictionary' key
        OemFujitsuMasshou = 0x93,   // 'Unregister word' key
        OemFujitsuTouroku = 0x94,   // 'Register word' key
        OemFujitsuLeftOyayubi = 0x95,   // 'Left OYAYUBI' key
        OemFujitsuRightOyayubi = 0x96,   // 'Right OYAYUBI' key

        // 0x97 - 0x9F : unassigned

        LeftShift = 0xA0,
        RightShift = 0xA1,
        LeftControl = 0xA2,
        RightControl = 0xA3,
        LeftMenu = 0xA4,
        RightMenu = 0xA5,

        BrowserBack = 0xA6,
        BrowserForward = 0xA7,
        BrowserRefresh = 0xA8,
        BrowserStop = 0xA9,
        BrowserSearch = 0xAA,
        BrowserFavorites = 0xAB,
        BrowserHome = 0xAC,

        VolumeMute = 0xAD,
        VolumeDown = 0xAE,
        VolumeUp = 0xAF,
        MediaNextTrack = 0xB0,
        MediaPreviousTrack = 0xB1,
        MediaStop = 0xB2,
        MediaPlayPause = 0xB3,
        LaunchMail = 0xB4,
        LaunchMediaSelect = 0xB5,
        LaunchApp1 = 0xB6,
        LaunchApp2 = 0xB7,

        // 0xB8 - 0xB9 : reserved

        Oem1 = 0xBA,   // ';:' for US
        OemPlus = 0xBB,   // '+' any country
        OemComma = 0xBC,   // ',' any country
        OemMinus = 0xBD,   // '-' any country
        OemPeriod = 0xBE,   // '.' any country
        Oem2 = 0xBF,   // '/?' for US
        Oem3 = 0xC0,   // '`~' for US

        // 0xC1 - 0xC2 : reserved

        GamepadA = 0xC3, // reserved
        GamepadB = 0xC4, // reserved
        GamepadX = 0xC5, // reserved
        GamepadY = 0xC6, // reserved
        GamepadRightShoulder = 0xC7, // reserved
        GamepadLeftShoulder = 0xC8, // reserved
        GamepadLeftTrigger = 0xC9, // reserved
        GamepadRightTrigger = 0xCA, // reserved
        GamepadDirectionPadUp = 0xCB, // reserved
        GamepadDirectionPadDown = 0xCC, // reserved
        GamepadDirectionPadLeft = 0xCD, // reserved
        GamepadDirectionPadRight = 0xCE, // reserved
        GamepadMenu = 0xCF, // reserved
        GamepadView = 0xD0, // reserved
        GamepadLeftThumbstickButton = 0xD1, // reserved
        GamepadRightThumbstickButton = 0xD2, // reserved
        GamepadLeftThumbstickUp = 0xD3, // reserved
        GamepadLeftThumbstickDown = 0xD4, // reserved
        GamepadLeftThumbstickRight = 0xD5, // reserved
        GamepadLeftThumbstickLeft = 0xD6, // reserved
        GamepadRightThumbstickUp = 0xD7, // reserved
        GamepadRightThumbstickDown = 0xD8, // reserved
        GamepadRightThumbstickRight = 0xD9, // reserved
        GamepadRightThumbstickLeft = 0xDA, // reserved

        Oem4 = 0xDB,  //  '[{' for US
        Oem5 = 0xDC,  //  '\|' for US
        Oem6 = 0xDD,  //  ']}' for US
        Oem7 = 0xDE,  //  ''"' for US
        Oem8 = 0xDF,

        // 0xE0 : reserved

        // Various extended or enhanced keyboards
        OemAx = 0xE1,  //  'AX' key on Japanese AX kbd
        Oem102 = 0xE2,  //  "<>" or "\|" on RT 102-key kbd.
        IcoHelp = 0xE3,  //  Help key on ICO
        Ico00 = 0xE4,  //  00 key on ICO

        ProcessKey = 0xE5,

        IcoClear = 0xE6,

        Packet = 0xE7,

        // 0xE8 : unassigned

        // Nokia/Ericsson definitions
        OemReset = 0xE9,
        OemJump = 0xEA,
        OemProgramAttention1 = 0xEB,
        OemProgramAttention2 = 0xEC,
        OemProgramAttention3 = 0xED,
        OemWsControl = 0xEE,
        OemCursorSelect = 0xEF, // VK_OEM_CUSEL
        OemAttn = 0xF0,
        OemFinish = 0xF1,
        OemCopy = 0xF2,
        OemAuto = 0xF3,
        OemEnlw = 0xF4,
        OemBackTab = 0xF5,

        Attn = 0xF6,
        CursorSelect = 0xF7,
        ExtendSelection = 0xF8,
        EraseEof = 0xF9,
        Play = 0xFA,
        Zoom = 0xFB,
        NoName = 0xFC,
        ProgramAttention1 = 0xFD,
        OemClear = 0xFE,

        // 0xFF : reserved
    }
}
