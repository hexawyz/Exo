using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;

namespace DeviceTools.DisplayDevices.Configuration
{
	public readonly struct DisplayConfiguration
	{
		private readonly NativeMethods.DisplayConfigPathInfo[] _paths;
		private readonly NativeMethods.DisplayConfigModeInfo[] _modes;
		private readonly Topology _topologyId;

		private DisplayConfiguration(NativeMethods.DisplayConfigPathInfo[] paths, NativeMethods.DisplayConfigModeInfo[] modes, Topology topologyId)
		{
			_paths = paths;
			_modes = modes;
			_topologyId = topologyId;
		}

		public DisplayConfigurationPathCollection Paths => new DisplayConfigurationPathCollection(_paths, _modes);
		public DisplayConfigurationModeCollection Modes => new DisplayConfigurationModeCollection(_modes);

		public Topology Topology => _topologyId;

		private static void ThrowOnError(int result)
		{
			if (result != 0)
			{
				throw new Win32Exception(result);
			}
		}

		public static DisplayConfiguration GetForActivePaths()
			=> GetDisplayConfiguration(NativeMethods.QueryDeviceConfigFlags.OnlyActivePaths | NativeMethods.QueryDeviceConfigFlags.VirtualModeAware);

		private static DisplayConfiguration GetDisplayConfiguration(NativeMethods.QueryDeviceConfigFlags flags)
		{
			NativeMethods.DisplayConfigPathInfo[]? paths = null;
			NativeMethods.DisplayConfigModeInfo[]? modes = null;

		Retry:;
			ThrowOnError(NativeMethods.GetDisplayConfigBufferSizes(flags, out int pathCount, out int modeCount));

			if (paths is null || paths.Length < pathCount) Array.Resize(ref paths, pathCount);
			if (modes is null || modes.Length < modeCount) Array.Resize(ref modes, modeCount);

			Topology topologyId = 0;

			int result = (flags & (NativeMethods.QueryDeviceConfigFlags.AllPaths | NativeMethods.QueryDeviceConfigFlags.OnlyActivePaths)) != 0 ?
				NativeMethods.QueryDisplayConfig
				(
					flags,
					ref pathCount,
					ref paths[0],
					ref modeCount,
					ref modes[0],
					IntPtr.Zero
				) :
				NativeMethods.QueryDisplayConfig
				(
					flags,
					ref pathCount,
					ref paths[0],
					ref modeCount,
					ref modes[0],
					out topologyId
				);

			if (result == NativeMethods.ErrorInsufficientBuffer) goto Retry;
			ThrowOnError(result);

			Array.Resize(ref paths, pathCount);
			Array.Resize(ref modes, modeCount);

			return new DisplayConfiguration(paths, modes, topologyId);
		}

		internal static unsafe string GetSourceDeviceName(NativeMethods.Luid adapterId, uint id)
		{
			var packet = new NativeMethods.DisplayConfigSourceDeviceName
			{
				Header = new NativeMethods.DisplayConfigDeviceInfoHeader
				{
					Type = NativeMethods.DisplayConfigDeviceInfoType.GetSourceName,
					Size = sizeof(NativeMethods.DisplayConfigSourceDeviceName),
					AdapterId = adapterId,
					Id = id
				}
			};

			ThrowOnError(NativeMethods.DisplayConfigGetDeviceInfo(ref packet));

			return packet.ViewGdiDeviceName.ToString();
		}

		internal static unsafe TargetDeviceNameInfo GetTargetDeviceName(NativeMethods.Luid adapterId, uint id)
		{
			var packet = new NativeMethods.DisplayConfigTargetDeviceName
			{
				Header = new NativeMethods.DisplayConfigDeviceInfoHeader
				{
					Type = NativeMethods.DisplayConfigDeviceInfoType.GetTargetName,
					Size = sizeof(NativeMethods.DisplayConfigTargetDeviceName),
					AdapterId = adapterId,
					Id = id
				}
			};

			ThrowOnError(NativeMethods.DisplayConfigGetDeviceInfo(ref packet));

			return new TargetDeviceNameInfo(packet);
		}
	}

	public readonly struct DisplayConfigurationPathCollection : IReadOnlyList<DisplayConfigurationPath>, IList<DisplayConfigurationPath>
	{
		public struct Enumerator : IEnumerator<DisplayConfigurationPath>
		{
			private readonly NativeMethods.DisplayConfigPathInfo[] _paths;
			private readonly NativeMethods.DisplayConfigModeInfo[] _modes;
			private int _index;

			internal Enumerator(NativeMethods.DisplayConfigPathInfo[] paths, NativeMethods.DisplayConfigModeInfo[] modes)
				=> (_paths, _modes, _index) = (paths, modes, -1);

			public void Dispose() { }

			public DisplayConfigurationPath Current => new DisplayConfigurationPath(_paths[_index], _modes);
			object IEnumerator.Current => Current;

			public bool MoveNext() => ++_index < _paths.Length;

			public void Reset() => _index = -1;
		}

		private readonly NativeMethods.DisplayConfigPathInfo[] _paths;
		private readonly NativeMethods.DisplayConfigModeInfo[] _modes;

		internal DisplayConfigurationPathCollection(NativeMethods.DisplayConfigPathInfo[] paths, NativeMethods.DisplayConfigModeInfo[] modes)
			=> (_paths, _modes) = (paths, modes);

		public DisplayConfigurationPath this[int index] => new DisplayConfigurationPath(_paths[index], _modes);

		DisplayConfigurationPath IList<DisplayConfigurationPath>.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException();
		}

		public int Count => _paths.Length;

		bool ICollection<DisplayConfigurationPath>.IsReadOnly => true;

		public bool Contains(DisplayConfigurationPath item) => Array.IndexOf(_paths, item) >= 0;

		public void CopyTo(DisplayConfigurationPath[] array, int arrayIndex)
		{
			if (array is null) throw new ArgumentNullException(nameof(array));
			if ((uint)arrayIndex > (uint)array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

			var items = _paths;

			if ((uint)(arrayIndex + items.Length) > array.Length) throw new ArgumentException();

			for (int i = 0; i < items.Length; i++)
			{
				array[arrayIndex + i] = new DisplayConfigurationPath(items[i], _modes);
			}
		}

		public Enumerator GetEnumerator() => new Enumerator(_paths, _modes);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		IEnumerator<DisplayConfigurationPath> IEnumerable<DisplayConfigurationPath>.GetEnumerator() => GetEnumerator();

		public int IndexOf(DisplayConfigurationPath item) => Array.IndexOf(_paths, item);

		void IList<DisplayConfigurationPath>.Insert(int index, DisplayConfigurationPath item) => throw new NotSupportedException();
		void IList<DisplayConfigurationPath>.RemoveAt(int index) => throw new NotSupportedException();
		void ICollection<DisplayConfigurationPath>.Add(DisplayConfigurationPath item) => throw new NotSupportedException();
		void ICollection<DisplayConfigurationPath>.Clear() => throw new NotSupportedException();
		bool ICollection<DisplayConfigurationPath>.Remove(DisplayConfigurationPath item) => throw new NotSupportedException();
	}

	public readonly struct DisplayConfigurationModeCollection : IReadOnlyList<DisplayConfigurationModeInfo>, IList<DisplayConfigurationModeInfo>
	{
		public struct Enumerator : IEnumerator<DisplayConfigurationModeInfo>
		{
			private readonly NativeMethods.DisplayConfigModeInfo[] _modes;
			private int _index;

			internal Enumerator(NativeMethods.DisplayConfigModeInfo[] modes)
				=> (_modes, _index) = (modes, -1);

			public void Dispose() { }

			public DisplayConfigurationModeInfo Current => new DisplayConfigurationModeInfo(_modes[_index]);
			object IEnumerator.Current => Current;

			public bool MoveNext() => ++_index < _modes.Length;

			public void Reset() => _index = -1;
		}

		private readonly NativeMethods.DisplayConfigModeInfo[] _modes;

		internal DisplayConfigurationModeCollection(NativeMethods.DisplayConfigModeInfo[] modes)
			=> _modes = modes;

		public DisplayConfigurationModeInfo this[int index] => new DisplayConfigurationModeInfo(_modes[index]);

		DisplayConfigurationModeInfo IList<DisplayConfigurationModeInfo>.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException();
		}

		public int Count => _modes.Length;

		bool ICollection<DisplayConfigurationModeInfo>.IsReadOnly => true;

		public bool Contains(DisplayConfigurationModeInfo item) => Array.IndexOf(_modes, item) >= 0;

		public void CopyTo(DisplayConfigurationModeInfo[] array, int arrayIndex)
		{
			if (array is null) throw new ArgumentNullException(nameof(array));
			if ((uint)arrayIndex > (uint)array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

			var items = _modes;

			if ((uint)(arrayIndex + items.Length) > array.Length) throw new ArgumentException();

			for (int i = 0; i < items.Length; i++)
			{
				array[arrayIndex + i] = new DisplayConfigurationModeInfo(items[i]);
			}
		}

		public Enumerator GetEnumerator() => new Enumerator(_modes);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		IEnumerator<DisplayConfigurationModeInfo> IEnumerable<DisplayConfigurationModeInfo>.GetEnumerator() => GetEnumerator();

		public int IndexOf(DisplayConfigurationModeInfo item) => Array.IndexOf(_modes, item);

		void IList<DisplayConfigurationModeInfo>.Insert(int index, DisplayConfigurationModeInfo item) => throw new NotSupportedException();
		void IList<DisplayConfigurationModeInfo>.RemoveAt(int index) => throw new NotSupportedException();
		void ICollection<DisplayConfigurationModeInfo>.Add(DisplayConfigurationModeInfo item) => throw new NotSupportedException();
		void ICollection<DisplayConfigurationModeInfo>.Clear() => throw new NotSupportedException();
		bool ICollection<DisplayConfigurationModeInfo>.Remove(DisplayConfigurationModeInfo item) => throw new NotSupportedException();
	}

	public readonly struct DisplayConfigurationPath
	{
		private readonly NativeMethods.DisplayConfigPathInfo _pathInfo;
		private readonly NativeMethods.DisplayConfigModeInfo[] _modes;

		internal DisplayConfigurationPath(NativeMethods.DisplayConfigPathInfo pathInfo, NativeMethods.DisplayConfigModeInfo[] modes)
			=> (_pathInfo, _modes) = (pathInfo, modes);

		public DisplayConfigurationPathSourceInfo SourceInfo => new DisplayConfigurationPathSourceInfo(_pathInfo.SourceInfo, _modes, SupportsVirtualMode);
		public DisplayConfigurationPathTargetInfo TargetInfo => new DisplayConfigurationPathTargetInfo(_pathInfo.TargetInfo, _modes, SupportsVirtualMode);

		public bool IsActive => (_pathInfo.Flags | NativeMethods.DisplayConfigPathInfoFlags.PathActive) != 0;
		public bool SupportsVirtualMode => (_pathInfo.Flags | NativeMethods.DisplayConfigPathInfoFlags.SupportVirtualMode) != 0;
	}

	public readonly struct DisplayConfigurationPathSourceInfo : IEquatable<DisplayConfigurationPathSourceInfo>
	{
		private readonly NativeMethods.DisplayConfigPathSourceInfo _sourceInfo;
		private readonly NativeMethods.DisplayConfigModeInfo[] _modes;
		private readonly bool _supportsVirtualMode;

		internal DisplayConfigurationPathSourceInfo(NativeMethods.DisplayConfigPathSourceInfo sourceInfo, NativeMethods.DisplayConfigModeInfo[] modes, bool supportsVirtualMode)
		{
			_sourceInfo = sourceInfo;
			_supportsVirtualMode = supportsVirtualMode;
			_modes = modes;
		}

		public long AdapterId => _sourceInfo.AdapterId.ToInt64();
		public int Id => (int)_sourceInfo.Id;

		public int? ModeIndex => _supportsVirtualMode ?
			_sourceInfo.ModeInfo.SourceModeInfoIdx != NativeMethods.DisplayConfigPathSourceModeIndexInvalid ?
				_sourceInfo.ModeInfo.SourceModeInfoIdx :
				null :
			_sourceInfo.ModeInfo.ModeInfoIdx != NativeMethods.DisplayConfigPathModeIndexInvalid ?
				(int)_sourceInfo.ModeInfo.ModeInfoIdx :
				null;

		public DisplayConfigurationModeInfo? Mode => ModeIndex is int index ? new DisplayConfigurationModeInfo(_modes[index]) : null;

		public int? CloneGroupId => _supportsVirtualMode && _sourceInfo.ModeInfo.CloneGroupId != NativeMethods.DisplayConfigPathCloneGroupInvalid ?
			_sourceInfo.ModeInfo.CloneGroupId :
			null;

		public bool IsInUse => (_sourceInfo.StatusFlags & NativeMethods.DisplayConfigPathSourceInfoStatus.InUse) != 0;

		public string GetDeviceName()
			=> DisplayConfiguration.GetSourceDeviceName(_sourceInfo.AdapterId, _sourceInfo.Id);

		public override bool Equals(object? obj) => obj is DisplayConfigurationPathSourceInfo info && Equals(info);

		public bool Equals(DisplayConfigurationPathSourceInfo other)
			=> _sourceInfo.AdapterId == other._sourceInfo.AdapterId
			&& _sourceInfo.Id == other._sourceInfo.Id
			&& _sourceInfo.ModeInfo.ModeInfoIdx == other._sourceInfo.ModeInfo.ModeInfoIdx
			&& ReferenceEquals(_modes, other._modes)
			&& _supportsVirtualMode == other._supportsVirtualMode;
		
		public override int GetHashCode() => HashCode.Combine(_sourceInfo.AdapterId, _sourceInfo.Id, _sourceInfo.ModeInfo.ModeInfoIdx, _modes, _supportsVirtualMode);

		public static bool operator ==(DisplayConfigurationPathSourceInfo left, DisplayConfigurationPathSourceInfo right) => left.Equals(right);
		public static bool operator !=(DisplayConfigurationPathSourceInfo left, DisplayConfigurationPathSourceInfo right) => !(left == right);
	}

	public readonly struct DisplayConfigurationPathTargetInfo
	{
		private readonly NativeMethods.DisplayConfigPathTargetInfo _targetInfo;
		private readonly NativeMethods.DisplayConfigModeInfo[] _modes;
		private readonly bool _supportsVirtualMode;

		internal DisplayConfigurationPathTargetInfo(NativeMethods.DisplayConfigPathTargetInfo targetInfo, NativeMethods.DisplayConfigModeInfo[] modes, bool supportsVirtualMode)
		{
			_targetInfo = targetInfo;
			_supportsVirtualMode = supportsVirtualMode;
			_modes = modes;
		}

		public long AdapterId => _targetInfo.AdapterId.ToInt64();
		public int Id => (int)_targetInfo.Id;

		public int? ModeIndex => _supportsVirtualMode ?
			_targetInfo.ModeInfo.TargetModeInfoIdx != NativeMethods.DisplayConfigPathTargetModeIndexInvalid ?
				_targetInfo.ModeInfo.TargetModeInfoIdx :
				null :
			_targetInfo.ModeInfo.ModeInfoIdx != NativeMethods.DisplayConfigPathModeIndexInvalid ?
				(int)_targetInfo.ModeInfo.ModeInfoIdx :
				null;

		public DisplayConfigurationModeInfo? Mode => ModeIndex is int index ? new DisplayConfigurationModeInfo(_modes[index]) : null;

		public int? DesktopModeIndex => _supportsVirtualMode && _targetInfo.ModeInfo.DesktopModeInfoIdx != NativeMethods.DisplayConfigPathDesktopImageIndexInvalid ?
			_targetInfo.ModeInfo.DesktopModeInfoIdx :
			null;

		public DisplayConfigurationDesktopImageInfo? DesktopMode => DesktopModeIndex is int index ? new DisplayConfigurationDesktopImageInfo(_modes[index]) : null;

		public VideoOutputTechnology OutputTechnology => _targetInfo.OutputTechnology;
		public Rotation Rotation => _targetInfo.Rotation;
		public Scaling Scaling => _targetInfo.Scaling;
		public Rational RefreshRate => _targetInfo.RefreshRate;
		public ScanlineOrdering ScanLineOrdering => _targetInfo.ScanLineOrdering;
		public bool IsAvailable => _targetInfo.TargetAvailable;
		public bool IsInUse => (_targetInfo.StatusFlags & NativeMethods.DisplayConfigPathTargetInfoStatus.InUse) != 0;
		public bool IsForcible => (_targetInfo.StatusFlags & NativeMethods.DisplayConfigPathTargetInfoStatus.Forcible) != 0;
		public bool IsBootPersistentForced => (_targetInfo.StatusFlags & NativeMethods.DisplayConfigPathTargetInfoStatus.ForcedAvailabilityBoot) != 0;
		public bool IsPathPersistentForced => (_targetInfo.StatusFlags & NativeMethods.DisplayConfigPathTargetInfoStatus.ForcedAvailabilityPath) != 0;
		public bool IsNonPersistentForced => (_targetInfo.StatusFlags & NativeMethods.DisplayConfigPathTargetInfoStatus.ForcedAvailabilitySystem) != 0;
		public bool IsHeadMountedDisplay => (_targetInfo.StatusFlags & NativeMethods.DisplayConfigPathTargetInfoStatus.IsHmd) != 0;

		public TargetDeviceNameInfo GetDeviceNameInformation()
			=> DisplayConfiguration.GetTargetDeviceName(_targetInfo.AdapterId, _targetInfo.Id);
	}

	public readonly struct TargetDeviceNameInfo
	{
		private readonly NativeMethods.DisplayConfigTargetDeviceName _deviceName;

		internal TargetDeviceNameInfo(NativeMethods.DisplayConfigTargetDeviceName deviceName) => _deviceName = deviceName;

		public EdidManufacturerNameId EdidManufacturerNameId => new EdidManufacturerNameId(_deviceName.EdidManufactureId);
		public ushort EdidProductCodeId => _deviceName.EdidProductCodeId;

		public int ConnectorInstance => (int)_deviceName.ConnectorInstance;

		public bool IsEdidValid => (_deviceName.Flags & NativeMethods.DisplayConfigTargetDeviceNameFlags.EdidIdsValid) != 0;
		public bool IsFriendlyNameFromEdid => (_deviceName.Flags & NativeMethods.DisplayConfigTargetDeviceNameFlags.FriendlyNameFromEdid) != 0;
		public bool IsFriendlyNameForced => (_deviceName.Flags & NativeMethods.DisplayConfigTargetDeviceNameFlags.FriendlyNameForced) != 0;

		public string GetMonitorFriendlyDeviceName() => _deviceName.MonitorFriendlyDeviceName.ToString();
		public string GetMonitorDeviceName() => _deviceName.MonitorDevicePath.ToString();
	}

	public readonly struct MonitorName : IEquatable<MonitorName>
	{
		public static MonitorName Parse(string text)
		{
			if (text is not { Length: 7 } ||
				!EdidManufacturerNameId.TryParse(text.AsSpan(0, 3), out var manufacturerNameId) ||
				!ushort.TryParse(text.AsSpan(3), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort productCodeId))
			{
				throw new ArgumentException("Monitor name should be composed of three ASCII letters and four hexadecimal digits.");
			}

			return new MonitorName(manufacturerNameId, productCodeId);
		}

		public MonitorName(EdidManufacturerNameId manufacturerNameId, ushort productCodeId)
		{
			ManufacturerNameId = manufacturerNameId;
			ProductCodeId = productCodeId;
		}

		public EdidManufacturerNameId ManufacturerNameId { get; }
		public ushort ProductCodeId { get; }

		public bool IsValid => ManufacturerNameId.IsValid;

		public override string? ToString()
			=> ManufacturerNameId.IsValid ?
				string.Create
				(
					7,
					this,
					(s, n) =>
					{
						n.ManufacturerNameId.TryFormat(s, out _);
						n.ProductCodeId.TryFormat(s[3..], out _, "X4", CultureInfo.InvariantCulture);
					}
				) :
				null;

		public override bool Equals(object? obj) => obj is MonitorName name && Equals(name);
		public bool Equals(MonitorName other) => ManufacturerNameId.Equals(other.ManufacturerNameId) && ProductCodeId == other.ProductCodeId;
		public override int GetHashCode() => HashCode.Combine(ManufacturerNameId, ProductCodeId);

		public static bool operator ==(MonitorName left, MonitorName right) => left.Equals(right);
		public static bool operator !=(MonitorName left, MonitorName right) => !(left == right);
	}

	public readonly struct EdidManufacturerNameId : IEquatable<EdidManufacturerNameId>
	{
		/// <summary>This is the raw value of the manufacturer ID.</summary>
		/// <remarks>
		/// <para>
		/// On little-endian hosts, the endianness of this value has been reversed to compensate for windows returning the bytes in their (original) big endian order.
		/// It is unknown how this value would appear on a big endian host. Windows could either compensate by reversing it in the wrong order too, or simply always return the raw bytes.
		/// </para>
		/// </remarks>
		public readonly ushort Value { get; }

		public static EdidManufacturerNameId Parse(string text)
		{
			if (text is null) throw new ArgumentNullException(text);

			return Parse(text.AsSpan());
		}

		public static bool TryParse(string text, out EdidManufacturerNameId manufacturerNameId)
		{
			if (text is null) throw new ArgumentNullException(text);

			return TryParse(text.AsSpan(), out manufacturerNameId);
		}

		public static EdidManufacturerNameId Parse(ReadOnlySpan<char> text)
		{
			if (!TryParse(text, out var manufacturerNameId))
			{
				throw new ArgumentException("Valid PNP IDs must be composed of three letters.");
			}

			return manufacturerNameId;
		}

		public static bool TryParse(ReadOnlySpan<char> text, out EdidManufacturerNameId manufacturerNameId)
		{
			if (text.Length != 3 || !IsLetter(text[0]) || !IsLetter(text[1]) || !IsLetter(text[2]))
			{
				manufacturerNameId = default;
				return false;
			}

			manufacturerNameId = new EdidManufacturerNameId((ushort)IPAddress.NetworkToHostOrder((short)((text[0] & ~0x20 - 'A' + 1) << 10 | (text[1] & ~0x20 - 'A' + 1) << 5 | (text[2] & ~0x20 - 'A' + 1))));
			return true;
		}

		private static bool IsLetter(char c)
			=> c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';

		private static bool IsValueValid(ushort value)
			=> value <= 0b11010_11010_11010 && (value & 0b11111_11111) <= 0b11010_11010 & (value & 0b11111) <= 11010;

		// FIXME: Endiannes on Big Endian hosts ?
		internal EdidManufacturerNameId(ushort value) => Value = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;

		public bool IsValid => IsValueValid(Value);

		public override bool Equals(object? obj) => obj is EdidManufacturerNameId id && Equals(id);
		public bool Equals(EdidManufacturerNameId other) => Value == other.Value;
		public override int GetHashCode() => HashCode.Combine(Value);

		public override string ToString()
			=> IsValid ?
				string.Create(3, Value, (s, v) => (s[0], s[1], s[2]) = ((char)('A' - 1 + (v >> 10)), (char)('A' - 1 + ((v >> 5) & 0x1f)), (char)('A' - 1 + (v & 0x1f)))) :
				Value.ToString("X4");

		public bool TryFormat(Span<char> destination, out int charsWritten)
		{
			if (IsValid)
			{
				if (destination.Length >= 3)
				{
					(destination[0], destination[1], destination[2]) = ((char)('A' - 1 + (Value >> 10)), (char)('A' - 1 + ((Value >> 5) & 0x1f)), (char)('A' - 1 + (Value & 0x1f)));
					charsWritten = 3;
					return true;
				}
				else
				{
					charsWritten = 0;
					return false;
				}
			}
			else
			{
				return Value.TryFormat(destination, out charsWritten, "X4", CultureInfo.InvariantCulture);
			}
		}

		public static bool operator ==(EdidManufacturerNameId left, EdidManufacturerNameId right) => left.Equals(right);
		public static bool operator !=(EdidManufacturerNameId left, EdidManufacturerNameId right) => !(left == right);
	}

	public readonly struct DisplayConfigurationModeInfo
	{
		private readonly NativeMethods.DisplayConfigModeInfo _displayConfigMode;

		internal DisplayConfigurationModeInfo(NativeMethods.DisplayConfigModeInfo displayConfigMode)
		{
			_displayConfigMode = displayConfigMode;
		}

		public int Id => (int)_displayConfigMode.Id;
		public long AdapterId => _displayConfigMode.AdapterId.ToInt64();

		public ModeInfoType InfoType => _displayConfigMode.InfoType;

		public DisplayConfigurationSourceMode AsSourceMode()
			=> _displayConfigMode.InfoType == ModeInfoType.Source ?
				Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationSourceMode>(ref Unsafe.AsRef(this)) :
				throw new InvalidOperationException();

		public DisplayConfigurationSourceMode AsTargetMode()
			=> _displayConfigMode.InfoType == ModeInfoType.Source ?
				Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationSourceMode>(ref Unsafe.AsRef(this)) :
				throw new InvalidOperationException();

		public DisplayConfigurationDesktopImageInfo AsDesktopImageInfo()
			=> _displayConfigMode.InfoType == ModeInfoType.Source ?
				Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationDesktopImageInfo>(ref Unsafe.AsRef(this)) :
				throw new InvalidOperationException();

		public static explicit operator DisplayConfigurationSourceMode(DisplayConfigurationModeInfo info)
			=> info._displayConfigMode.InfoType == ModeInfoType.Source ?
				Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationSourceMode>(ref info) :
				throw new InvalidCastException();

		public static explicit operator DisplayConfigurationTargetMode(DisplayConfigurationModeInfo info)
			=> info._displayConfigMode.InfoType == ModeInfoType.Target ?
				Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationTargetMode>(ref info) :
				throw new InvalidCastException();

		public static explicit operator DisplayConfigurationDesktopImageInfo(DisplayConfigurationModeInfo info)
			=> info._displayConfigMode.InfoType == ModeInfoType.DesktopImage ?
				Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationDesktopImageInfo>(ref info) :
				throw new InvalidCastException();
	}

	public readonly struct DisplayConfigurationSourceMode
	{
		private readonly NativeMethods.DisplayConfigModeInfo _displayConfigMode;

		internal DisplayConfigurationSourceMode(NativeMethods.DisplayConfigModeInfo displayConfigMode)
		{
			if (displayConfigMode.InfoType != ModeInfoType.Source) throw new InvalidOperationException();

			_displayConfigMode = displayConfigMode;
		}

		public int Id => (int)_displayConfigMode.Id;
		public long AdapterId => _displayConfigMode.AdapterId.ToInt64();

		public ModeInfoType InfoType => _displayConfigMode.InfoType;

		public int Width => (int)_displayConfigMode.SourceMode.Width;
		public int Height => (int)_displayConfigMode.SourceMode.Height;
		public PixelFormat PixelFormat => _displayConfigMode.SourceMode.PixelFormat;
		public Point Position => new Point(_displayConfigMode.SourceMode.Position.X, _displayConfigMode.SourceMode.Position.Y);

		public static implicit operator DisplayConfigurationModeInfo(DisplayConfigurationSourceMode info)
			=> Unsafe.As<DisplayConfigurationSourceMode, DisplayConfigurationModeInfo>(ref info);
	}

	public readonly struct DisplayConfigurationTargetMode
	{
		private readonly NativeMethods.DisplayConfigModeInfo _displayConfigMode;

		internal DisplayConfigurationTargetMode(NativeMethods.DisplayConfigModeInfo displayConfigMode)
		{
			if (displayConfigMode.InfoType != ModeInfoType.Target) throw new InvalidOperationException();

			_displayConfigMode = displayConfigMode;
		}

		public int Id => (int)_displayConfigMode.Id;
		public long AdapterId => _displayConfigMode.AdapterId.ToInt64();

		public ModeInfoType InfoType => _displayConfigMode.InfoType;

		public VideoSignalInfo VideoSignalInfo => new VideoSignalInfo(_displayConfigMode.TargetMode.TargetVideoSignalInfo);

		public static implicit operator DisplayConfigurationModeInfo(DisplayConfigurationTargetMode info)
			=> Unsafe.As<DisplayConfigurationTargetMode, DisplayConfigurationModeInfo>(ref info);
	}

	public readonly struct DisplayConfigurationDesktopImageInfo
	{
		private readonly NativeMethods.DisplayConfigModeInfo _displayConfigMode;

		internal DisplayConfigurationDesktopImageInfo(NativeMethods.DisplayConfigModeInfo displayConfigMode)
		{
			if (displayConfigMode.InfoType != ModeInfoType.DesktopImage) throw new InvalidOperationException();

			_displayConfigMode = displayConfigMode;
		}

		public int Id => (int)_displayConfigMode.Id;
		public long AdapterId => _displayConfigMode.AdapterId.ToInt64();

		public ModeInfoType InfoType => _displayConfigMode.InfoType;

		public Point PathSourceSize => new Point(_displayConfigMode.DesktopImageInfo.PathSourceSize.X, _displayConfigMode.DesktopImageInfo.PathSourceSize.Y);

		public Rectangle DesktopImageRegion => Rectangle.FromLTRB
		(
			_displayConfigMode.DesktopImageInfo.DesktopImageRegion.Left,
			_displayConfigMode.DesktopImageInfo.DesktopImageRegion.Top,
			_displayConfigMode.DesktopImageInfo.DesktopImageRegion.Right,
			_displayConfigMode.DesktopImageInfo.DesktopImageRegion.Bottom
		);

		public Rectangle DesktopImageClip => Rectangle.FromLTRB
		(
			_displayConfigMode.DesktopImageInfo.DesktopImageClip.Left,
			_displayConfigMode.DesktopImageInfo.DesktopImageClip.Top,
			_displayConfigMode.DesktopImageInfo.DesktopImageClip.Right,
			_displayConfigMode.DesktopImageInfo.DesktopImageClip.Bottom
		);

		public static implicit operator DisplayConfigurationModeInfo(DisplayConfigurationDesktopImageInfo info)
			=> Unsafe.As<DisplayConfigurationDesktopImageInfo, DisplayConfigurationModeInfo>(ref info);
	}
}
