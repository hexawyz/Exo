using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools
{
	public static class Device
	{
		/// <summary>Opens a device file.</summary>
		/// <remarks>
		/// <para>
		/// Use of this function is required to open device because <see cref="FileStream"/> won't agree to randomly opening device files.
		/// This is somewhat understandable, though, as <see cref="FileStream"/> was conceived as a stream and may lack sufficient features to operate on devices.
		/// </para>
		/// <para>
		/// Driver device file names will usually be of the form <c>\\.\DeviceName</c> or <c>\\?\DosDeviceName</c>. This is a name (symlink) defined by the driver.
		/// </para>
		/// </remarks>
		/// <param name="deviceName">The name of the devide file.</param>
		/// <param name="access">The required access</param>
		/// <returns>A safe file handle, that can be used to issue IO control, or to create a <see cref="FileStream"/> instance if required.</returns>
		public static SafeFileHandle OpenHandle(string deviceName, DeviceAccess access)
		{
			var handle = NativeMethods.CreateFile
			(
				deviceName,
				access switch
				{
					DeviceAccess.None => 0,
					DeviceAccess.Read => NativeMethods.FileAccessMask.GenericRead,
					DeviceAccess.Write => NativeMethods.FileAccessMask.GenericWrite,
					DeviceAccess.ReadWrite => NativeMethods.FileAccessMask.GenericRead | NativeMethods.FileAccessMask.GenericWrite,
					_ => throw new ArgumentOutOfRangeException(nameof(access))
				},
				FileShare.ReadWrite,
				IntPtr.Zero,
				FileMode.Open,
				0,
				IntPtr.Zero
			);

			if (handle.IsInvalid)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			return handle;
		}

		// Implementations from SetupAPI might be unnecessary: Although finding info on the recommended API to use is difficult:
		// - Both Setup API and Configuration Manager API seem to produce the same results in the current use case. (Minus casing)
		// - Some features of CM_ are implemented by setupapi.dll, and setupapi.dll depends on cfgmgr32.dll…
		// - Only Configuration Manager API seems to be officially supported for UWP: https://docs.microsoft.com/en-us/windows-hardware/drivers/install/porting-from-setupapi-to-cfgmgr32
		//   That may not be an indication that this is the more future proof API, but it is at least the more widely supported one.
		//   Anyway, I doubt either is going away in the short term, as they offer slightly different features.

		// Let's leave this one for now, but I'll probably remove it.
		public static IEnumerable<string> EnumerateInterfacesFromSetupApi(Guid deviceInterfaceClassGuid, bool onlyPresent = true)
		{
			using var handle = NativeMethods.SetupDiGetClassDevs
			(
				deviceInterfaceClassGuid,
				IntPtr.Zero,
				IntPtr.Zero,
				onlyPresent ?
					NativeMethods.GetClassDeviceFlags.DeviceInterface | NativeMethods.GetClassDeviceFlags.Present :
					NativeMethods.GetClassDeviceFlags.DeviceInterface
			);

			var interfaceData = new NativeMethods.DeviceInterfaceData
			{
				Size = (uint)Marshal.SizeOf<NativeMethods.DeviceInterfaceData>()
			};

			uint index = 0;
			while (true)
			{
				if (NativeMethods.SetupDiEnumDeviceInterfaces(handle, IntPtr.Zero, deviceInterfaceClassGuid, index++, ref interfaceData) == 0)
				{
					int lastError = Marshal.GetLastWin32Error();

					if (lastError == NativeMethods.ErrorNoMoreItems)
					{
						break;
					}

					throw new Win32Exception(lastError);
				}

				yield return NativeMethods.SetupDiGetDeviceInterfaceDetail(handle, ref interfaceData);
			}
		}

		public static IEnumerable<string> EnumerateAllDevices()
			=> EnumerateAllDevices(false);

		public static IEnumerable<string> EnumerateAllDevices(bool enumerateAll)
			=> EnumerateAllDevices
			(
				default,
				enumerateAll ? NativeMethods.GetDeviceIdListFlags.FilterNone : NativeMethods.GetDeviceIdListFlags.FilterPresent
			);

		public static IEnumerable<string> EnumerateAllDevices(Guid deviceSetupClassGuid)
			=> EnumerateAllDevices(deviceSetupClassGuid, false);

		public static IEnumerable<string> EnumerateAllDevices(Guid deviceSetupClassGuid, bool enumerateAll)
		{
#if !NETSTANDARD2_0
			var bufferOwner = MemoryPool<char>.Shared.Rent(38 + 1);
			var buffer = bufferOwner.Memory[..39];
			deviceSetupClassGuid.TryFormat(buffer.Span, out int _, "B");
			buffer.Span[^1] = '\0';
#endif
			return EnumerateAllDevices
			(
#if NETSTANDARD2_0
				deviceSetupClassGuid.ToString().AsMemory(),
#else
				buffer,
#endif
				enumerateAll ?
					NativeMethods.GetDeviceIdListFlags.FilterClass :
					NativeMethods.GetDeviceIdListFlags.FilterPresent | NativeMethods.GetDeviceIdListFlags.FilterClass,
#if NETSTANDARD2_0
				null
#else
				bufferOwner
#endif
			);
		}

		private static IEnumerable<string> EnumerateAllDevices(ReadOnlyMemory<char> filter, NativeMethods.GetDeviceIdListFlags flags, IDisposable? disposable = null)
		{
			try
			{
				uint charCount;

				{
					var result = NativeMethods.ConfigurationManagerGetDeviceIdListSize(out charCount, filter.Span.GetPinnableReference(), flags);
					if (result != 0)
					{
						throw new ConfigurationManagerException(result);
					}
				}

				var @lock = new object();
				var buffer = ArrayPool<byte>.Shared.Rent(checked((int)(charCount * 2)));
				try
				{
					var result = NativeMethods.ConfigurationManagerGetDeviceIdList(filter.Span.GetPinnableReference(), ref MemoryMarshal.Cast<byte, char>(buffer)[0], charCount, flags);
					if (result != 0)
					{
						throw new ConfigurationManagerException(result);
					}

					int position = 0;
					while (position < charCount)
					{
						var chars = MemoryMarshal.Cast<byte, char>(buffer).Slice(position);
						int endIndex = chars.IndexOf('\0');

						// Last string will be empty (It seems that the buffer is terminated by double null chars)
						if (endIndex <= 0)
						{
							yield break;
						}

						yield return chars.Slice(0, endIndex).ToString();

						position += endIndex + 1;
					}
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
			finally
			{
				disposable?.Dispose();
			}
		}

		public static IEnumerable<string> EnumerateAllInterfaces(Guid deviceInterfaceClassGuid)
			=> EnumerateAllInterfaces(deviceInterfaceClassGuid, null, false);

		public static IEnumerable<string> EnumerateAllInterfaces(Guid deviceInterfaceClassGuid, bool enumerateAll)
			=> EnumerateAllInterfaces(deviceInterfaceClassGuid, null, enumerateAll);

		public static IEnumerable<string> EnumerateAllInterfaces(Guid deviceInterfaceClassGuid, string? deviceInstanceId)
			=> EnumerateAllInterfaces(deviceInterfaceClassGuid, deviceInstanceId, false);

		public static IEnumerable<string> EnumerateAllInterfaces(Guid deviceInterfaceClassGuid, string? deviceInstanceId, bool enumerateAll)
		{
			var flag = enumerateAll ? NativeMethods.GetDeviceInterfaceListFlags.All : NativeMethods.GetDeviceInterfaceListFlags.Present;

			uint charCount;

			{
				var result = NativeMethods.ConfigurationManagerGetDeviceInterfaceListSize(out charCount, deviceInterfaceClassGuid, deviceInstanceId, flag);
				if (result != 0)
				{
					throw new ConfigurationManagerException(result);
				}
			}

			var buffer = ArrayPool<byte>.Shared.Rent(checked((int)(charCount * 2)));
			try
			{
				var result = NativeMethods.ConfigurationManagerGetDeviceInterfaceList(deviceInterfaceClassGuid, null, ref MemoryMarshal.Cast<byte, char>(buffer)[0], charCount, flag);
				if (result != 0)
				{
					throw new ConfigurationManagerException(result);
				}

				int position = 0;
				while (position < charCount)
				{
					var chars = MemoryMarshal.Cast<byte, char>(buffer).Slice(position);
					int endIndex = chars.IndexOf('\0');

					// Last string will be empty (It seems that the buffer is terminated by double null chars)
					if (endIndex <= 0)
					{
						yield break;
					}

					yield return chars.Slice(0, endIndex).ToString();

					position += endIndex + 1;
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public static IEnumerable<string> GetDeviceHardwareIds(uint deviceInstanceHandle)
		{
			uint dataLength = 0;

			{
				var result = NativeMethods.ConfigurationManagerGetDeviceNodeProperty(deviceInstanceHandle, NativeMethods.DevicePropertyKeys.DeviceHardwareIds, out _, ref Unsafe.NullRef<byte>(), ref dataLength, 0);
				if (result != NativeMethods.ConfigurationManagerResult.BufferSmall)
				{
					throw new ConfigurationManagerException(result);
				}
			}


			var buffer = ArrayPool<byte>.Shared.Rent(checked((int)dataLength));
			try
			{
				var result = NativeMethods.ConfigurationManagerGetDeviceNodeProperty(deviceInstanceHandle, NativeMethods.DevicePropertyKeys.DeviceHardwareIds, out _, ref buffer[0], ref dataLength, 0);
				if (result != 0)
				{
					throw new ConfigurationManagerException(result);
				}

				int position = 0;
				int charCount = (int)(dataLength / 2);
				while (position < charCount)
				{
					var chars = MemoryMarshal.Cast<byte, char>(buffer).Slice(position);
					int endIndex = chars.IndexOf('\0');

					// Last string will be empty (It seems that the buffer is terminated by double null chars)
					if (endIndex <= 0)
					{
						yield break;
					}

					yield return chars.Slice(0, endIndex).ToString();

					position += endIndex + 1;
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		public static string GetDeviceInstanceId(string deviceInterface) => GetDeviceInterfaceStringProperty(deviceInterface, NativeMethods.DevicePropertyKeys.DeviceInstanceId);

		public static string GetDisplayName(uint deviceInstanceHandle) => GetStringProperty(deviceInstanceHandle, NativeMethods.DevicePropertyKeys.ItemNameDisplay);

		public static string GetFriendlyName(uint deviceInstanceHandle) => GetStringProperty(deviceInstanceHandle, NativeMethods.DevicePropertyKeys.DeviceFriendlyName);

		public static Guid GetContainerId(uint deviceInstanceHandle) => GetGuidProperty(deviceInstanceHandle, NativeMethods.DevicePropertyKeys.ContainerId);

		private static string GetDeviceInterfaceStringProperty(string deviceInterface, in NativeMethods.PropertyKey propertyKey)
		{
			uint dataLength = 0;

			{
				var result = NativeMethods.ConfigurationManagerGetDeviceInterfaceProperty(deviceInterface, propertyKey, out _, ref Unsafe.NullRef<byte>(), ref dataLength, 0);
				if (result != NativeMethods.ConfigurationManagerResult.BufferSmall)
				{
					throw new ConfigurationManagerException(result);
				}
			}

#if NETSTANDARD2_0
			var buffer = ArrayPool<byte>.Shared.Rent(checked((int)dataLength));
			try
			{
				var result = NativeMethods.ConfigurationManagerGetDeviceInterfaceProperty(deviceInterface, propertyKey, out _, ref buffer[0], ref dataLength, 0);
				if (result != 0)
				{
					throw new ConfigurationManagerException(result);
				}

				var span = MemoryMarshal.Cast<byte, char>(buffer.AsSpan(0, (int)dataLength));
				return span.Slice(0, span.Length - 1).ToString();
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
#else
			return string.Create
			(
				checked((int)dataLength) / 2 - 1,
				(DeviceInterface: deviceInterface, PropertyKey: propertyKey),
				static (span, state) =>
				{
					uint dataLength = ((uint)span.Length + 1) * 2;
					// NB: This will technically overwrite the internal null terminator of the string. This implementation detail of strings is unlikely to change, but at least you know.
					var result = NativeMethods.ConfigurationManagerGetDeviceInterfaceProperty(state.DeviceInterface, state.PropertyKey, out _, ref MemoryMarshal.AsBytes(span)[0], ref dataLength, 0);
					if (result != 0)
					{
						throw new ConfigurationManagerException(result);
					}
				}
			);
#endif
		}

		private static string GetStringProperty(uint deviceInstanceHandle, in NativeMethods.PropertyKey propertyKey)
		{
			uint dataLength = 0;

			{
				var result = NativeMethods.ConfigurationManagerGetDeviceNodeProperty(deviceInstanceHandle, propertyKey, out _, ref Unsafe.NullRef<byte>(), ref dataLength, 0);
				if (result != NativeMethods.ConfigurationManagerResult.BufferSmall)
				{
					throw new ConfigurationManagerException(result);
				}
			}

#if NETSTANDARD2_0
			var buffer = ArrayPool<byte>.Shared.Rent(checked((int)dataLength));
			try
			{
				var result = NativeMethods.ConfigurationManagerGetDeviceNodeProperty(deviceInstanceHandle, propertyKey, out _, ref buffer[0], ref dataLength, 0);
				if (result != 0)
				{
					throw new ConfigurationManagerException(result);
				}

				var span = MemoryMarshal.Cast<byte, char>(buffer.AsSpan(0, (int)dataLength));
				return span.Slice(0, span.Length - 1).ToString();
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
#else
			return string.Create
			(
				checked((int)dataLength) / 2 - 1,
				(DeviceInstanceHandle: deviceInstanceHandle, PropertyKey: propertyKey),
				static (span, state) =>
				{
					uint dataLength = ((uint)span.Length + 1) * 2;
					// NB: This will technically overwrite the internal null terminator of the string. This implementation detail of strings is unlikely to change, but at least you know.
					var result = NativeMethods.ConfigurationManagerGetDeviceNodeProperty(state.DeviceInstanceHandle, state.PropertyKey, out _, ref MemoryMarshal.AsBytes(span)[0], ref dataLength, 0);
					if (result != 0)
					{
						throw new ConfigurationManagerException(result);
					}
				}
			);
#endif
		}

		private static Guid GetGuidProperty(uint deviceInstanceHandle, in NativeMethods.PropertyKey propertyKey)
		{
			Guid guid = default;
			uint dataLength = 16;

			var result = NativeMethods.ConfigurationManagerGetDeviceNodeProperty(deviceInstanceHandle, propertyKey, out _, ref Unsafe.As<Guid, byte>(ref guid), ref dataLength, 0);
			if (result != 0)
			{
				throw new ConfigurationManagerException(result);
			}

			return guid;
		}

		public static uint LocateDeviceNode(string deviceInstanceId)
		{
			uint deviceInstanceHandle = 0;
			var result = NativeMethods.ConfigurationManagerLocateDeviceNode(out deviceInstanceHandle, deviceInstanceId, NativeMethods.LocateDeviceNodeFlags.Normal);
			if (result != 0)
			{
				throw new ConfigurationManagerException(result);
			}
			return deviceInstanceHandle;
		}

		public static unsafe string GetDeviceContainerDisplayName(Guid deviceContainerId) => GetDeviceContainerStringProperty(deviceContainerId, NativeMethods.DevicePropertyKeys.ItemNameDisplay);
		public static unsafe string GetDeviceContainerPrimaryCategory(Guid deviceContainerId) => GetDeviceContainerStringProperty(deviceContainerId, NativeMethods.DevicePropertyKeys.DeviceContainerPrimaryCategory);

		private static unsafe string GetDeviceContainerStringProperty(Guid deviceContainerId, in NativeMethods.PropertyKey property)
		{
#if NETSTANDARD2_0
			var objectId = deviceContainerId.ToString().AsSpan();
#else
			Span<char> objectId = stackalloc char[37];
			deviceContainerId.TryFormat(objectId, out _);
			objectId[^1] = '\0';
#endif

			var displayNameProperty = new NativeMethods.DevicePropertyCompoundKey
			{
				Key = property,
				Store = NativeMethods.DevicePropertyStore.Sytem
			};

			ref var firstProperty = ref NativeMethods.DeviceGetObjectProperties
			(
				NativeMethods.DeviceObjectType.DeviceContainer,
				ref MemoryMarshal.GetReference(objectId),
				NativeMethods.DeviceQueryFlags.None,
				1,
				ref displayNameProperty,
				out int propertyCount
			);

			//#if NETSTANDARD2_0
			//			var properties = new Span<NativeMethods.DeviceProperty>(Unsafe.AsPointer(ref firstProperty), propertyCount);
			//#else
			//			var properties = MemoryMarshal.CreateReadOnlySpan(ref firstProperty, propertyCount);
			//#endif
			try
			{
				if (firstProperty.Type == NativeMethods.DevicePropertyType.String && firstProperty.BufferLength > 2)
				{
					var text = new ReadOnlySpan<char>((void*)firstProperty.Buffer, (int)(firstProperty.BufferLength / 2) - 1);
					return text.ToString();
				}
				return string.Empty;
			}
			finally
			{
				NativeMethods.DeviceFreeObjectProperties(propertyCount, ref firstProperty);
			}
		}
	}
}
