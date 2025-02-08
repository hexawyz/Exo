using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.WinUsb;

namespace Exo.Devices.Nzxt.Kraken;

internal sealed class KrakenWinUsbImageTransport : IAsyncDisposable
{
	public static async Task<KrakenWinUsbImageTransport> CreateAsync(DeviceStream deviceStream, CancellationToken cancellationToken)
	{
		var configuration = await deviceStream.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
		if (configuration.Interfaces.Count != 1) goto UnexpectedConfiguration;
		var @interface = configuration.Interfaces[0];
		if (@interface.Endpoints.Count != 1) goto UnexpectedConfiguration;
		return new KrakenWinUsbImageTransport(deviceStream, @interface.Endpoints[0].Address);
	UnexpectedConfiguration:;
		throw new InvalidOperationException("The device has an unexpected configuration.");
	}

	private static ReadOnlySpan<byte> TransmissionHeader => [0x12, 0xfa, 0x01, 0xe8, 0xab, 0xcd, 0xef, 0x98, 0x76, 0x54, 0x32, 0x10, 0x00, 0x00, 0x00, 0x00];

	private readonly DeviceStream _deviceStream;
	private readonly AsyncLock _lock;
	private readonly byte[] _uploadSetupMessage;
	private readonly byte _pipeAddress;

	private KrakenWinUsbImageTransport(DeviceStream deviceStream, byte pipeAddress)
	{
		_deviceStream = deviceStream;
		_lock = new();
		_uploadSetupMessage = GC.AllocateUninitializedArray<byte>(20, true);
		TransmissionHeader.CopyTo(_uploadSetupMessage);
		_pipeAddress = pipeAddress;
	}

	public ValueTask DisposeAsync() => _deviceStream.DisposeAsync();

	public async Task UploadImageAsync(KrakenImageFormat imageFormat, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken)
	{
		if (memory.Length == 0) throw new ArgumentException();

		const int PacketSize = 2097152;

		using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			_uploadSetupMessage[12] = (byte)imageFormat;
			LittleEndian.Write(ref _uploadSetupMessage[16], (uint)memory.Length);
			await _deviceStream.WritePipeAsync(0, _pipeAddress, MemoryMarshal.CreateFromPinnedArray(_uploadSetupMessage, 0, _uploadSetupMessage.Length), cancellationToken).ConfigureAwait(false);
			while (true)
			{
				if (memory.Length <= PacketSize)
				{
					await _deviceStream.WritePipeAsync(0, _pipeAddress, memory, cancellationToken).ConfigureAwait(false);
					return;
				}
				else
				{
					await _deviceStream.WritePipeAsync(0, _pipeAddress, memory[..PacketSize], cancellationToken).ConfigureAwait(false);
					memory = memory[PacketSize..];
				}
			}
		}
	}
}
