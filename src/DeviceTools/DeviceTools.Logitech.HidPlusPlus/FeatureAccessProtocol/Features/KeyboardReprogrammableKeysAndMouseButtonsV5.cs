using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

#pragma warning disable IDE0044 // Add readonly modifier
public static class KeyboardReprogrammableKeysAndMouseButtonsV5
{
	public const HidPlusPlusFeature FeatureId = HidPlusPlusFeature.KeyboardReprogrammableKeysAndMouseButtonsV5;

	public static class DivertedButtons
	{
		public const byte EventId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _controlId10;
			private byte _controlId11;

			public ushort ControlId1
			{
				get => BigEndian.ReadUInt16(in _controlId10);
				set => BigEndian.Write(ref _controlId10, value);
			}

			private byte _controlId20;
			private byte _controlId21;

			public ushort ControlId2
			{
				get => BigEndian.ReadUInt16(in _controlId20);
				set => BigEndian.Write(ref _controlId20, value);
			}

			private byte _controlId30;
			private byte _controlId31;

			public ushort ControlId3
			{
				get => BigEndian.ReadUInt16(in _controlId30);
				set => BigEndian.Write(ref _controlId30, value);
			}

			private byte _controlId40;
			private byte _controlId41;

			public ushort ControlId4
			{
				get => BigEndian.ReadUInt16(in _controlId40);
				set => BigEndian.Write(ref _controlId40, value);
			}
		}
	}

	public static class DivertedRawMouseXY
	{
		public const byte EventId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _dx0;
			private byte _dx1;

			public ushort DeltaX
			{
				get => BigEndian.ReadUInt16(in _dx0);
				set => BigEndian.Write(ref _dx0, value);
			}

			private byte _dy0;
			private byte _dy1;

			public ushort DeltaY
			{
				get => BigEndian.ReadUInt16(in _dy0);
				set => BigEndian.Write(ref _dy0, value);
			}
		}
	}

	public static class AnalyticsKey
	{
		public const byte EventId = 2;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _controlId10;
			private byte _controlId11;

			public ushort ControlId1
			{
				get => BigEndian.ReadUInt16(in _controlId10);
				set => BigEndian.Write(ref _controlId10, value);
			}

			public byte Event1;

			private byte _controlId20;
			private byte _controlId21;

			public ushort ControlId2
			{
				get => BigEndian.ReadUInt16(in _controlId20);
				set => BigEndian.Write(ref _controlId20, value);
			}

			public byte Event2;

			private byte _controlId30;
			private byte _controlId31;

			public ushort ControlId3
			{
				get => BigEndian.ReadUInt16(in _controlId30);
				set => BigEndian.Write(ref _controlId30, value);
			}

			public byte Event3;

			private byte _controlId40;
			private byte _controlId41;

			public ushort ControlId4
			{
				get => BigEndian.ReadUInt16(in _controlId40);
				set => BigEndian.Write(ref _controlId40, value);
			}

			public byte Event4;

			private byte _controlId50;
			private byte _controlId51;

			public ushort ControlId5
			{
				get => BigEndian.ReadUInt16(in _controlId50);
				set => BigEndian.Write(ref _controlId50, value);
			}

			public byte Event5;
		}
	}

	public static class DivertedRawWheel
	{
		public const byte EventId = 4;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			private byte _periodsAndFlags;

			public ushort PeriodCount
			{
				get => (byte)(_periodsAndFlags & 0xF);
				set
				{
					ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 15);
					_periodsAndFlags = (byte)(_periodsAndFlags & 0xF0 | value);
				}
			}

			public bool IsHighResolution
			{
				get => (_periodsAndFlags & 0x10) != 0;
				set => _periodsAndFlags = value ? (byte)(_periodsAndFlags | 0x10) : (byte)(_periodsAndFlags & 0xEF);
			}

			private byte _dv0;
			private byte _dv1;

			public ushort DeltaV
			{
				get => BigEndian.ReadUInt16(in _dv0);
				set => BigEndian.Write(ref _dv0, value);
			}
		}
	}

	public static class GetCount
	{
		public const byte FunctionId = 0;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			public byte Count;
		}
	}

	public static class GetControlInfo
	{
		public const byte FunctionId = 1;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Request : IMessageRequestParameters, IShortMessageParameters
		{
			public byte Index;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Response : IMessageResponseParameters, ILongMessageParameters
		{
			private byte _controlId0;
			private byte _controlId1;

			public ushort ControlId
			{
				get => BigEndian.ReadUInt16(in _controlId0);
				set => BigEndian.Write(ref _controlId0, value);
			}

			private byte _taskId0;
			private byte _taskId1;

			public ushort TaskId
			{
				get => BigEndian.ReadUInt16(in _taskId0);
				set => BigEndian.Write(ref _taskId0, value);
			}

			private byte _flags;

			public ControlFlags Flags
			{
				get => (ControlFlags)_flags;
				set => _flags = (byte)value;
			}

			public byte Position;

			/// <summary>Indicates which group this control ID belongs to.</summary>
			/// <value><c>0</c> if it does not belong to a group; <c>1</c> to <c>8</c> otherwise.</value>
			/// <remarks>Available starting from V1.</remarks>
			public byte GroupNumber;

			/// <summary>This control can be remapped to any control ID contained in the specified groups.</summary>
			/// <remarks>Available starting from V1.</remarks>
			public byte GroupMask;

			private byte _reportingCapabilities;

			/// <summary>Gets or sets the reporting capabilities associated with the control.</summary>
			/// <remarks>Available starting from V2.</remarks>
			public ControlReportingCapabilities ReportingCapabilities
			{
				get => (ControlReportingCapabilities)_reportingCapabilities;
				set => _reportingCapabilities = (byte)value;
			}
		}
	}

	public static class ControlReporting
	{
		public const byte GetFunctionId = 2;
		public const byte SetFunctionId = 3;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct GetRequest : IMessageRequestParameters, IShortMessageParameters
		{
			private byte _controlId0;
			private byte _controlId1;

			public ushort ControlId
			{
				get => BigEndian.ReadUInt16(in _controlId0);
				set => BigEndian.Write(ref _controlId0, value);
			}
		}

		/// <summary>This is the response for both get and set requests, and also the request for set requests.</summary>
		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
		public struct Parameters : IMessageRequestParameters, IMessageResponseParameters, ILongMessageParameters
		{
			private byte _controlId0;
			private byte _controlId1;

			public ushort ControlId
			{
				get => BigEndian.ReadUInt16(in _controlId0);
				set => BigEndian.Write(ref _controlId0, value);
			}

			private byte _flags0;

			private byte _remappedControlId0;
			private byte _remappedControlId1;

			public ushort RemappedControlId
			{
				get => BigEndian.ReadUInt16(in _remappedControlId0);
				set => BigEndian.Write(ref _remappedControlId0, value);
			}

			private byte _flags1;

			/// <summary>Gets or sets the reporting flags.</summary>
			/// <remarks>
			/// <para>For set requests, the <c>IsValid</c> flags will indicate which of the other associated flags are defined.</para>
			/// <para>For get requests, the <c>IsValid</c> flags should be ignored, as the enabled or disabled status is always reported.</para>
			/// </remarks>
			public ControlReportingFlags Flags
			{
				get => (ControlReportingFlags)(ushort)(_flags1 << 8 | _flags0);
				set
				{
					_flags0 = (byte)value;
					_flags1 = (byte)((ushort)value >>> 8);
				}
			}
		}
	}

	public static class GetCapabilities
	{
		public const byte FunctionId = 4;

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3)]
		public struct Response : IMessageResponseParameters, IShortMessageParameters
		{
			private byte _capabilities;

			public Capabilities Capabilities
			{
				get => (Capabilities)_capabilities;
				set => _capabilities = (byte)value;
			}
		}
	}

	/// <remarks>Available starting from V6.</remarks>
	public static class ResetAllCidReportSettings
	{
		public const byte FunctionId = 5;
	}

	[Flags]
	public enum ControlFlags : byte
	{
		None = 0b00000000,
		/// <summary>This control is a mouse button.</summary>
		Mouse = 0b00000001,
		/// <summary>This control resides on a function key. A function key is a key labeled with a number such as F1 to F16 and is on the function key row.</summary>
		FunctionKey = 0b00000010,
		/// <summary>This control is a nonstandard key which does not reside on a function key. It may be anywhere on the device including the function key row.</summary>
		HotKey = 0b00000100,
		/// <summary>This key is affected by the fnToggle setting. This flag does not indicate whether fn is required to use the key. This flag cannot appear with the mouse flag or hotkey flag.</summary>
		FunctionToggle = 0b00001000,
		/// <summary>The software should let the user reprogram this control. If not set, the software should improve handling of the control ID as described by the task ID without allowing the user to reprogram it.</summary>
		Reprogrammable = 0b00010000,
		/// <summary>This control can be temporarily diverted to software.</summary>
		Divertable = 0b00100000,
		/// <summary>This control can be persistently diverted to software.</summary>
		Persist = 0b01000000,
		/// <summary>
		/// This item is not a physical control but is instead a virtual control representing an optional native
		/// function (broadcasting raw (dx, dy) data can be considered as such) on the device that can be
		/// mapped to a physical control in the same device or another one(e.g.duo-link).
		/// </summary>
		/// <remarks>Available starting from V1.</remarks>
		Virtual = 0b10000000,
	}

	[Flags]
	public enum ControlReportingCapabilities : byte
	{
		None = 0b00000000,
		/// <summary>
		/// This control has the capability of being programmed as a gesture button, which implies the
		/// control can be diverted along with raw mouse xy reports to the SW if this control is currently
		/// diverted and pressed. On the other side, the SW will perform gesture detection and gesture task
		/// injection.
		/// </summary>
		/// <remarks>Available starting from V2.</remarks>
		RawXY = 0b00000001,
		/// <summary>
		/// This control has the capability of being programmed as a gesture button yet the activation of the
		/// raw XY function will be initiated by SW without the need of any user action. However, this
		/// control will still need to be currently diverted.
		/// </summary>
		/// <remarks>Available starting from V3.</remarks>
		ForceRawXY = 0b00000010,
		/// <summary>
		/// This control has the capability of sending project-dependent analytics key events to the SW. On
		/// the other side, the SW will gather these notifications and use them for analytics purposes. This
		/// functionality is entirely independent from the divertable state of the control.
		/// </summary>
		/// <remarks>Available starting from V4.</remarks>
		AnalyticsKeyEvents = 0b00000100,
		/// <summary>
		/// This control has the capability of being programmed to activate a virtual thumbwheel, which
		/// implies the control can be diverted along with raw mouse wheel reports to the SW if this control
		/// is currently diverted and pressed. On the other side, the SW will perform thumbwheel detection
		/// and button task injection.
		/// </summary>
		/// <remarks>Available starting from V5.</remarks>
		RawWheel = 0b00001000
	}

	[Flags]
	public enum ControlReportingFlags : ushort
	{
		None = 0b00000000_00000000,
		/// <summary>Indicates that the control is being temporarily diverted.</summary>
		Diverted = 0b00000000_00000001,
		/// <summary>Indicates that the <see cref="Diverted"/> flag is valid and device should update the temporary divert state of this control ID.</summary>
		DivertedIsValid = 0b00000000_00000010,
		/// <summary>Indicates that the control is being persistently diverted.</summary>
		Persisted = 0b00000000_00000100,
		/// <summary>Indicates that the <see cref="Persisted"/> flag is valid and device should update the persistent divert state of this control ID.</summary>
		PersistedIsValid = 0b00000000_00001000,
		/// <summary>Indicates that the control is being temporarily diverted along with mouse xy reports.</summary>
		/// <remarks>Available starting from V2.</remarks>
		RawXY = 0b00000000_00010000,
		/// <summary>Indicates that the <see cref="RawXY"/> flag is valid and device should update the temporary divert state of this control ID.</summary>
		/// <remarks>Available starting from V2.</remarks>
		RawXYIsValid = 0b00000000_00100000,
		/// <summary>Indicates that the control is being force diverted by SW. i.e. no need of user action to send raw XY.</summary>
		/// <remarks>Available starting from V3.</remarks>
		ForceRawXY = 0b00000000_01000000,
		/// <summary>Indicates that the <see cref="ForceRawXY"/> flag is valid and device should update the persistent forceRawXY state of this control ID.</summary>
		/// <remarks>Available starting from V3.</remarks>
		ForceRawXYIsValid = 0b00000000_10000000,
		/// <summary>Indicates that the control is temporarily reporting project-dependent analytics key events to the SW.</summary>
		/// <remarks>Available starting from V4.</remarks>
		AnalyticsKeyEvents = 0b00000001_00000000,
		/// <summary>Indicates that the <see cref="AnalyticsKeyEvents"/> flag of the CId is valid and device should temporarily update it.</summary>
		/// <remarks>Available starting from V4.</remarks>
		AnalyticsKeyEventsIsValid = 0b00000010_00000000,
		/// <summary>Indicates that the control is being temporarily diverted along with mouse wheel reports.</summary>
		/// <remarks>Available starting from V5.</remarks>
		RawWheel = 0b00000100_00000000,
		/// <summary>Indicates that the divert <see cref="RawWheel"/> flag is valid and device should update the temporary divert state of this control ID.</summary>
		/// <remarks>Available starting from V5.</remarks>
		RawWheelIsValid = 0b00001000_00000000,
	}

	[Flags]
	public enum Capabilities : byte
	{
		None = 0b00000000,
		ResetAllCidReportSettings = 0b00000001,
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
