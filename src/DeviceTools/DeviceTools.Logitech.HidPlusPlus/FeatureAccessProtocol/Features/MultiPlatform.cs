using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class MultiPlatform
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.MultiPlatform;

	public static class GetFeatureInfos
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _capabilities0;
			private byte _capabilities1;

			public Capabilities Capabilities
			{
				get => (Capabilities)LittleEndian.ReadUInt16(in _capabilities0);
				set => LittleEndian.Write(ref _capabilities0, (ushort)value);
			}

			public byte PlatformCount;
			public byte PlatformDescriptorCount;
			public byte HostCount;
			public byte CurrentHostIndex;
			public byte CurrentPlatformIndex;
		}
	}

	public static class GetPlatformDescriptor
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte PlatformDescriptorIndex;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte PlatformIndex;
			public byte PlatformDescriptorIndex;

			private byte _operatingSystem0;
			private byte _operatingSystem1;

			public OperatingSystems OperatingSystems
			{
				get => (OperatingSystems)LittleEndian.ReadUInt16(in _operatingSystem0);
				set => LittleEndian.Write(ref _operatingSystem0, (ushort)_operatingSystem0);
			}

			public byte FromVersion;
			public byte FromRevision;
			public byte ToVersion;
			public byte ToRevision;
		}
	}

	public static class GetHostPlatform
	{
		public const byte FunctionId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			/// <summary>The host index or <c>0xFF</c> for the current host.</summary>
			public byte HostIndex;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			public byte HostIndex;

			private byte _status;

			public bool IsPaired
			{
				get => (_status & 0x1) != 0;
				set => _status = value ? (byte)(_status | 1) : (byte)(_status & ~0xFF);
			}

			/// <summary>Platform index currently used (auto or manually set).</summary>
			/// <remarks><c>0xFF</c> if undefined, when the slot is empty.</remarks>
			public byte PlatformIndex;

			/// <summary>Origin of current platform index configuration.</summary>
			public PlatformSource PlatformSource;

			/// <summary>Platform index automatically defined at pairing.</summary>
			public byte AutomaticPlatformIndex;

			/// <summary>Platform descriptor index automatically defined at pairing.</summary>
			public byte AutomaticPlatformDescriptorIndex;

		}
	}

	public static class SetHostPlatform
	{
		public const byte FunctionId = 3;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			/// <summary>The host index or <c>0xFF</c> for the current host.</summary>
			public byte HostIndex;
			public byte PlatformIndex;
		}
	}

	public static class PlatformChange
	{
		public const byte EventId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte HostIndex;
			public byte PlatformIndex;
			public PlatformSource PlatformSource;
		}
	}

	[Flags]
	public enum Capabilities : ushort
	{
		OsDetection = 0b00000000_00000001,
		SetHostPlatform = 0b00000000_00000010,
	}

	[Flags]
	public enum OperatingSystems : ushort
	{
		Windows = 0b00000000_00000001,
		WindowsEmbedded = 0b00000000_00000010,
		Linux = 0b00000000_00000100,
		Chrome = 0b00000000_00001000,
		Android = 0b00000000_00010000,
		MacOs = 0b00000000_00100000,
		iOS = 0b00000000_01000000,
		WebOs = 0b00000000_10000000,
		Tizen = 0b00000001_00000000,
	}

	public enum PlatformSource : byte
	{
		Default = 0,
		Auto = 1,
		Manual = 2,
		Software = 3,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
