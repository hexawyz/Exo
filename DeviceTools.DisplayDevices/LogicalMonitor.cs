using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Drawing;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.DisplayDevices;

public sealed class LogicalMonitor
{
	[UnmanagedCallersOnly]
	private static uint EnumDisplayMonitorsCallback(IntPtr monitorHandle, /* in NativeMethods.Rectangle */ IntPtr deviceContextHandle, IntPtr clipRectangle, IntPtr data)
	{
		if (GCHandle.FromIntPtr(data).Target is List<LogicalMonitor> list)
		{
			list.Add(new LogicalMonitor(monitorHandle));
		}

		return 1;
	}

	public static unsafe LogicalMonitor[] GetAll()
	{
		var list = new List<LogicalMonitor>();
		var handle = GCHandle.Alloc(list);
		try
		{
			if
			(
				NativeMethods.EnumDisplayMonitors
				(
					IntPtr.Zero,
					MemoryMarshal.GetReference(default(Span<NativeMethods.Rectangle>)),
					(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, uint>)&EnumDisplayMonitorsCallback,
					GCHandle.ToIntPtr(handle)
				) == 0
			)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}
		finally
		{
			handle.Free();
		}
		return list.ToArray();
	}

	public static LogicalMonitor? TryGetFromPoint(int x, int y)
	{
		var handle = NativeMethods.MonitorFromPoint(new() { X = x, Y = y }, NativeMethods.MonitorFromPointFlags.DefaultToNull);

		return handle != (nint)0 ? new(handle) : null;
	}

	public static LogicalMonitor GetNearestFromPoint(int x, int y)
		=> new(NativeMethods.MonitorFromPoint(new() { X = x, Y = y }, NativeMethods.MonitorFromPointFlags.DefaultToNearest));

	public static LogicalMonitor GetFromPointOrPrimary(int x, int y)
		=> new(NativeMethods.MonitorFromPoint(new() { X = x, Y = y }, NativeMethods.MonitorFromPointFlags.DefaultToPrimary));

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	public IntPtr Handle { get; }

	private LogicalMonitor(IntPtr handle)
	{
		Handle = handle;
	}

	public LogicalMonitorInformation GetMonitorInformation()
	{
		var info = new NativeMethods.LogicalMonitorInfoEx { Size = Unsafe.SizeOf<NativeMethods.LogicalMonitorInfoEx>() };
		// TODO: Avoid throwing exceptions in the constructor, which is (indirectly) called from unmanaged code.
		// This information should not be cached, so that we can better react to display configuration changes, and detect when the monitor handle becomes invalid.
		if (NativeMethods.GetMonitorInfo(Handle, ref info) == 0)
		{
			int error = Marshal.GetLastWin32Error();

			// In case the error is Invalid Parameter, it should mean that the handle is now invalid.
			if (error == NativeMethods.ErrorInvalidParameter) throw new ObjectDisposedException(nameof(LogicalMonitor));
			else throw new Win32Exception(error);
		}
		return new(Convert(info.MonitorArea), Convert(info.WorkArea), info.DeviceName.ToString(), (info.Flags & NativeMethods.MonitorInfoFlags.Primary) != 0);
	}

	private static Rectangle Convert(NativeMethods.Rectangle rectangle)
		=> new(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);

	public DotsPerInch GetDpi()
		=> GetDpi(Handle, NativeMethods.MonitorDpiType.EffectiveDpi);

	public DotsPerInch GetAngularDpi()
		=> GetDpi(Handle, NativeMethods.MonitorDpiType.AngularDpi);

	public DotsPerInch GetRawDpi()
		=> GetDpi(Handle, NativeMethods.MonitorDpiType.RawDpi);

	private static unsafe DotsPerInch GetDpi(IntPtr handle, NativeMethods.MonitorDpiType dpiType)
	{
		uint x, y;
		uint result = NativeMethods.GetDpiForMonitor(handle, dpiType, &x, &y);

		if (result != 0)
		{
			if (result == NativeMethods.ErrorInvalidArgument) throw new ObjectDisposedException(nameof(LogicalMonitor));
			else Marshal.ThrowExceptionForHR((int)result);
		}
		return new DotsPerInch(x, y);
	}

	public ImmutableArray<PhysicalMonitor> GetPhysicalMonitors()
	{
		if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(Handle, out uint physicalMonitorCount) == 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		var physicalMonitors = new NativeMethods.PhysicalMonitor[physicalMonitorCount];

		if (NativeMethods.GetPhysicalMonitorsFromHMONITOR(Handle, physicalMonitorCount, physicalMonitors) == 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		var builder = ImmutableArray.CreateBuilder<PhysicalMonitor>(physicalMonitors.Length);

		foreach (var physicalMonitor in physicalMonitors)
		{
			builder.Add(new PhysicalMonitor(new SafePhysicalMonitorHandle(physicalMonitor.Handle), physicalMonitor.Description.ToString()));
		}

		return builder.MoveToImmutable();
	}
}

public readonly struct LogicalMonitorInformation
{
	public Rectangle MonitorArea { get; }
	public Rectangle WorkingArea { get; }
	public string DeviceName { get; }
	public bool IsPrimary { get; }

	public LogicalMonitorInformation(Rectangle monitorArea, Rectangle workingArea, string deviceName, bool isPrimary)
	{
		MonitorArea = monitorArea;
		WorkingArea = workingArea;
		DeviceName = deviceName;
		IsPrimary = isPrimary;
	}
}
