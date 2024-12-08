using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;

namespace DeviceTools.DisplayDevices.Configuration;

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

	public static DisplayConfiguration GetForAllPaths()
		=> GetDisplayConfiguration(NativeMethods.QueryDeviceConfigFlags.VirtualModeAware);

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

	internal static unsafe string GetAdapterName(NativeMethods.Luid adapterId)
	{
		var packet = new NativeMethods.DisplayConfigAdapterName
		{
			Header = new NativeMethods.DisplayConfigDeviceInfoHeader
			{
				Type = NativeMethods.DisplayConfigDeviceInfoType.GetAdapterName,
				Size = sizeof(NativeMethods.DisplayConfigAdapterName),
				AdapterId = adapterId,
			}
		};

		ThrowOnError(NativeMethods.DisplayConfigGetDeviceInfo(ref packet));

		return packet.AdapterDevicePath.ToString();
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

public readonly struct DisplayConfigurationAdapterInfo : IEquatable<DisplayConfigurationAdapterInfo>
{
	private readonly NativeMethods.Luid _adapterId;

	internal DisplayConfigurationAdapterInfo(NativeMethods.Luid adapterId) => _adapterId = adapterId;

	public long Id => _adapterId.ToInt64();

	public string GetDeviceName()
		=> DisplayConfiguration.GetAdapterName(_adapterId);

	public override bool Equals(object? obj) => obj is DisplayConfigurationAdapterInfo info && Equals(info);
	public bool Equals(DisplayConfigurationAdapterInfo other) => _adapterId.Equals(other._adapterId) && Id == other.Id;
	public override int GetHashCode() => HashCode.Combine(_adapterId, Id);

	public static bool operator ==(DisplayConfigurationAdapterInfo left, DisplayConfigurationAdapterInfo right) => left.Equals(right);
	public static bool operator !=(DisplayConfigurationAdapterInfo left, DisplayConfigurationAdapterInfo right) => !(left == right);
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

	public DisplayConfigurationAdapterInfo Adapter => new(_sourceInfo.AdapterId);
	public int Id => (int)_sourceInfo.Id;

	public int? ModeIndex => _supportsVirtualMode ?
		_sourceInfo.ModeInfo.SourceModeInfoIdx != NativeMethods.DisplayConfigPathSourceModeIndexInvalid ?
			_sourceInfo.ModeInfo.SourceModeInfoIdx :
			null :
		_sourceInfo.ModeInfo.ModeInfoIdx != NativeMethods.DisplayConfigPathModeIndexInvalid ?
			(int)_sourceInfo.ModeInfo.ModeInfoIdx :
			null;

	public DisplayConfigurationSourceMode? Mode => ModeIndex is int index ? new DisplayConfigurationSourceMode(_modes[index]) : null;

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

	public DisplayConfigurationAdapterInfo Adapter => new(_targetInfo.AdapterId);
	public int Id => (int)_targetInfo.Id;

	public int? ModeIndex => _supportsVirtualMode ?
		_targetInfo.ModeInfo.TargetModeInfoIdx != NativeMethods.DisplayConfigPathTargetModeIndexInvalid ?
			_targetInfo.ModeInfo.TargetModeInfoIdx :
			null :
		_targetInfo.ModeInfo.ModeInfoIdx != NativeMethods.DisplayConfigPathModeIndexInvalid ?
			(int)_targetInfo.ModeInfo.ModeInfoIdx :
			null;

	public DisplayConfigurationTargetMode? Mode => ModeIndex is int index ? new DisplayConfigurationTargetMode(_modes[index]) : null;

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

	public PnpVendorId EdidVendorId => PnpVendorId.FromRaw(BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(_deviceName.EdidManufactureId) : _deviceName.EdidManufactureId);
	public ushort EdidProductId => _deviceName.EdidProductCodeId;

	public VideoOutputTechnology OutputTechnology => _deviceName.OutputTechnology;
	public int ConnectorInstance => (int)_deviceName.ConnectorInstance;

	public bool IsEdidValid => (_deviceName.Flags & NativeMethods.DisplayConfigTargetDeviceNameFlags.EdidIdsValid) != 0;
	public bool IsFriendlyNameFromEdid => (_deviceName.Flags & NativeMethods.DisplayConfigTargetDeviceNameFlags.FriendlyNameFromEdid) != 0;
	public bool IsFriendlyNameForced => (_deviceName.Flags & NativeMethods.DisplayConfigTargetDeviceNameFlags.FriendlyNameForced) != 0;

	public string GetMonitorFriendlyDeviceName() => _deviceName.MonitorFriendlyDeviceName.ToString();
	public string GetMonitorDeviceName() => _deviceName.MonitorDevicePath.ToString();
}

public readonly struct MonitorId : IEquatable<MonitorId>
{
	public static MonitorId Parse(string? text)
	{
		if (text is not { Length: 7 } ||
			!PnpVendorId.TryParse(text.AsSpan(0, 3), out var vendorId) ||
			!ushort.TryParse(text.AsSpan(3), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort productId))
		{
			throw new ArgumentException("Monitor name should be composed of three ASCII letters and four hexadecimal digits.");
		}

		return new MonitorId(vendorId, productId);
	}

	public static bool TryParse(string? text, out MonitorId monitorName)
	{
		if (text is not { Length: 7 } ||
			!PnpVendorId.TryParse(text.AsSpan(0, 3), out var vendorId) ||
			!ushort.TryParse(text.AsSpan(3), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort productId))
		{
			monitorName = default;
			return false;
		}

		monitorName = new MonitorId(vendorId, productId);
		return true;
	}

	public MonitorId(PnpVendorId vendorId, ushort productId)
	{
		VendorId = vendorId;
		ProductId = productId;
	}

	public PnpVendorId VendorId { get; }
	public ushort ProductId { get; }

	public bool IsValid => VendorId.IsValid;

	public override string? ToString()
		=> VendorId.IsValid ?
			string.Create
			(
				7,
				this,
				(s, n) =>
				{
					n.VendorId.TryFormat(s, out _);
					n.ProductId.TryFormat(s[3..], out _, "X4", CultureInfo.InvariantCulture);
				}
			) :
			null;

	public override bool Equals(object? obj) => obj is MonitorId name && Equals(name);
	public bool Equals(MonitorId other) => VendorId.Equals(other.VendorId) && ProductId == other.ProductId;
	public override int GetHashCode() => HashCode.Combine(VendorId, ProductId);

	public static bool operator ==(MonitorId left, MonitorId right) => left.Equals(right);
	public static bool operator !=(MonitorId left, MonitorId right) => !(left == right);
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
			Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationSourceMode>(ref Unsafe.AsRef(in this)) :
			throw new InvalidOperationException();

	public DisplayConfigurationTargetMode AsTargetMode()
		=> _displayConfigMode.InfoType == ModeInfoType.Target ?
			Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationTargetMode>(ref Unsafe.AsRef(in this)) :
			throw new InvalidOperationException();

	public DisplayConfigurationDesktopImageInfo AsDesktopImageInfo()
		=> _displayConfigMode.InfoType == ModeInfoType.DesktopImage ?
			Unsafe.As<DisplayConfigurationModeInfo, DisplayConfigurationDesktopImageInfo>(ref Unsafe.AsRef(in this)) :
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
