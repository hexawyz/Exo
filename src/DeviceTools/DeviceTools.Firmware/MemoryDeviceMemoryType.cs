﻿namespace DeviceTools.Firmware;

public enum MemoryDeviceMemoryType : byte
{
	Other = 0x01,
	Unknown = 0x02,
	DynamicRam = 0x03,
	EmbeddedDynamicRam = 0x04,
	VideoRam = 0x05,
	StaticRam = 0x06,
	Ram = 0x07,
	Rom = 0x08,
	Flash = 0x09,
	EepRom = 0x0A,
	FepRom = 0x0B,
	Eprom = 0x0C,
	CdRam = 0x0D,
	ThreeDRam = 0x0E,
	SdRam = 0x0F,
	SgRam = 0x10,
	RdRam = 0x11,
	DoubleDataRate = 0x12,
	DoubleDataRate2 = 0x13,
	DoubleDataRate2FullyBufferedDimm = 0x14,
	DoubleDataRate3 = 0x18,
	FullyBufferedDimm2 = 0x19,
	DoubleDataRate4 = 0x1A,
	LowPowerDoubleDataRate = 0x1B,
	LowPowerDoubleDataRate2 = 0x1C,
	LowPowerDoubleDataRate3 = 0x1D,
	LowPowerDoubleDataRate4 = 0x1E,
	LogicalNonVolatileDevice = 0x1F,
	HighBandwidthMemory = 0x20,
	HighBandwidthMemory2 = 0x21,
	DoubleDataRate5 = 0x22,
	LowPowerDoubleDataRate5 = 0x23,
	HighBandwidthMemory3 = 0x24,
}