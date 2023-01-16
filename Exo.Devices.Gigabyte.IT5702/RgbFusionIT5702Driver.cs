using System;
using System.IO;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.SystemControl;
using Exo.Core;
using Exo.Core.Features.LightingFeatures;

namespace Exo.Devices.Gigabyte.IT5702
{
	[StructLayout(LayoutKind.Explicit, Size = 64)]
	internal struct FeatureReport
	{
		[FieldOffset(0)]
		public byte ReportId; // 0xCC
		[FieldOffset(1)]
		public FeatureReportHeader Header;

		[FieldOffset(11)]
		public Effect Effect;
		[FieldOffset(12)]
		public ColorEffect ColorEffect;

		[FieldOffset(22)]
		public PulseEffect PulseEffect;
		[FieldOffset(22)]
		public FlashEffect FlashEffect;
		[FieldOffset(22)]
		public ColorCycleEffect ColorCycleEffect;
	}

	internal struct FeatureReportHeader
	{
		public byte CommandId;
		public byte LedMask; // If CommandId between 0x20 and 0x27 included, this is 1 << LedIndex
	}

	[StructLayout(LayoutKind.Explicit, Size = 10)]
	internal struct ColorEffect
	{
		[FieldOffset(0)]
		public byte Brightness;
		[FieldOffset(2)]
		public byte Blue;
		[FieldOffset(3)]
		public byte Green;
		[FieldOffset(4)]
		public byte Red;
	}

	[StructLayout(LayoutKind.Explicit, Size = 10)]
	internal struct PulseEffect
	{
		[FieldOffset(0)]
		public ushort FadeInTicks;
		[FieldOffset(2)]
		public ushort FadeOutTicks;
		[FieldOffset(4)]
		public ushort DurationTicks;
		[FieldOffset(9)]
		public byte One; // Set to 1
	}

	[StructLayout(LayoutKind.Explicit, Size = 11)]
	internal struct FlashEffect
	{
		[FieldOffset(0)]
		public ushort FadeInTicks; // Set to 0x64 … Not sure how to use it otherwise
		[FieldOffset(2)]
		public ushort FadeOutTicks; // Set to 0x64 … Not sure how to use it otherwise
		[FieldOffset(4)]
		public ushort DurationTicks;
		[FieldOffset(9)]
		public byte One; // Set to 1
		[FieldOffset(10)]
		public byte FlashCount; // Acceptable values may depend on the effect Duration ?
	}

	[StructLayout(LayoutKind.Explicit, Size = 11)]
	internal struct ColorCycleEffect
	{
		[FieldOffset(0)]
		public ushort ColorDurationInTicks;
		[FieldOffset(2)]
		public ushort TransitionDurationInTicks;
		[FieldOffset(8)]
		public byte ColorCount; // Number of colors to include in the cycle: 0 to 7
	}

	internal enum Effect : byte
	{
		None = 0,
		Static = 1,
		Pulse = 2,
		Flash = 3,
		ColorCycle = 4,
	}

	[DeviceId(VendorIdSource.Usb, 0x048D, 0x5702, "RGB Fusion IT5702")]
	public sealed class RgbFusionIT5702Driver : LightingDriver
	{
		public string FriendlyName => "RGB Fusion IT5702";

		private readonly HidFullDuplexStream _device;
		private int _changedLeds;
		private readonly FeatureReport[] _zoneSettings;
		private readonly byte[] _rgb = new byte[2 * 3 * 32];
		private readonly byte[] _commonFeatureBuffer = new byte[64];

		public override IDeviceFeatureCollection<ILightingDeviceFeature> Features { get; }

		private RgbFusionIT5702Driver(int ledCount)
		{
			_zoneSettings = new FeatureReport[ledCount];
			for (int i = 0; i < _zoneSettings.Length; i++)
			{
				ref var settings = ref _zoneSettings[i];

				settings.ReportId = 0xCC;
				settings.Header.CommandId = (byte)(0x20 + i);
				settings.Header.LedMask = (byte)(1 << i);
			}
		}

		protected override void ApplyChanges()
		{
			var zoneSettings = _zoneSettings.AsSpan();
			for (int i = 0; i < zoneSettings.Length; i++)
			{
				if ((_changedLeds & 1 << i) != 0)
				{
					_device.SendFeatureReport(MemoryMarshal.AsBytes(zoneSettings.Slice(i, 1)));
				}
			}
			if (_changedLeds != 0)
			{
				Array.Clear(_commonFeatureBuffer, 0, _commonFeatureBuffer.Length);
				_commonFeatureBuffer[0] = 0xCC;
				_commonFeatureBuffer[1] = 0x01;
				_commonFeatureBuffer[2] = 0xFF;
				_device.SendFeatureReport(_commonFeatureBuffer);
			}
		}
	}
}
