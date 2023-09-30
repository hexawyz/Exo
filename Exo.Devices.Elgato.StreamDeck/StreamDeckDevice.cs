using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Elgato.StreamDeck;

internal class StreamDeckDevice : IAsyncDisposable
{
	private readonly struct DeviceInfo
	{
		public DeviceInfo(byte gridWidth, byte gridHeight)
		{
			GridWidth = gridWidth;
			GridHeight = gridHeight;
		}

		public byte GridWidth { get; }
		public byte GridHeight { get; }
	}

	private static readonly Dictionary<ushort, DeviceInfo> DeviceInformations = new()
	{
		{ 0x0060, new(5, 3) },
		{ 0x0063, new(3, 2) },
		{ 0x006C, new(8, 4) },
		{ 0x006D, new(3, 2) },
	};

	private readonly HidFullDuplexStream _stream;
	private readonly DeviceInfo _deviceInfo;

	public StreamDeckDevice(HidFullDuplexStream stream, ushort productId)
	{
		_stream = stream;
		_deviceInfo = DeviceInformations[productId];
	}

	public async ValueTask DisposeAsync()
	{
		await _stream.DisposeAsync().ConfigureAwait(false);
	}

	[SkipLocalsInit]
	public string GetSerialNumber()
	{
		Span<byte> buffer = stackalloc byte[32];
		buffer[0] = 6;
		_stream.ReceiveFeatureReport(buffer);
		return Encoding.ASCII.GetString(buffer.Slice(2, buffer[1]));
	}

	[SkipLocalsInit]
	public string? GetVersion()
	{
		// TODO: There are unknown bytes here.
		// Values of all unknown bytes changed after a fw update (did not manage to see any of the stuff that happened during the update though…)
		// 05 ? ? ? ? ? 1 . 0 1 . 0 0 0
		// 05 ? ? ? ? ? 1 . 0 0 . 0 1 2
		Span<byte> buffer = stackalloc byte[32];
		buffer[0] = 5;
		_stream.ReceiveFeatureReport(buffer);
		return Encoding.ASCII.GetString(buffer.Slice(6, 8));
	}

	//public async Task SetButtonRawImage(byte x, byte y, ReadOnlySpan<byte> data)
	//{
	//}
}
