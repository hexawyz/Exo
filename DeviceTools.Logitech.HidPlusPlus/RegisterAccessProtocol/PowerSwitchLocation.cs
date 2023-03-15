namespace DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;

public enum PowerSwitchLocation : byte
{
	Base = 0x1,
	TopCase = 0x2,
	EdgeOfTopRightCorner = 0x3,
	Other = 0x4,
	TopLeftCorner = 0x5,
	BottomLeftCorner = 0x6,
	TopRightCorner = 0x7,
	BottomRightCorner = 0x8,
	TopEdge = 0x9,
	RightEdge = 0xA,
	LeftEdge = 0xB,
	BottomEdge = 0xC,
}
