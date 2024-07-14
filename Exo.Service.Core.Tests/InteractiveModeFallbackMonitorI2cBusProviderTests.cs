
using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DeviceTools;
using DeviceTools.DisplayDevices.Mccs;
using Exo.I2C;
using Xunit;

namespace Exo.Service.Core.Tests;

public class InteractiveModeFallbackMonitorI2cBusProviderTests
{
	private const string AdapterDeviceName1 = "DEVICE_NAME_ADAPTER_1";
	private const ushort Adapter1Monitor1VendorId = 0x3333;
	private const ushort Adapter1Monitor1ProductId = 0x1234;
	private const uint Adapter1Monitor1IdSerialNumber = 0x91242;
	private const string Adapter1Monitor1SerialNumber = "MON-A7EFB1C4G";
	private const string Adapter1Monitor1Capabilities = "(prot(monitor)type(LCD)model(Monitor 1)cmds(01 02 03 07 0C E3 F3)vcp(10)mswhql(1)mccs_ver(2.2))";

	private sealed class FakeMonitorControlService : IMonitorControlService
	{
		public Task<IMonitorControlAdapter> ResolveAdapterAsync(string deviceName, CancellationToken cancellationToken)
		{
			if (deviceName == AdapterDeviceName1)
			{
				return Task.FromResult<IMonitorControlAdapter>(new Adapter1());
			}
			return Task.FromException<IMonitorControlAdapter>(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("Adapter not found")));
		}
	}

	private sealed class Adapter1 : IMonitorControlAdapter
	{
		public Task<IMonitorControlMonitor> ResolveMonitorAsync(ushort vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
		{
			if (vendorId == Adapter1Monitor1VendorId &&
				productId == Adapter1Monitor1ProductId &&
				idSerialNumber == Adapter1Monitor1IdSerialNumber &&
				serialNumber == Adapter1Monitor1SerialNumber)
			{
				return Task.FromResult<IMonitorControlMonitor>(new Monitor(Adapter1Monitor1Capabilities));
			}
			return Task.FromException<IMonitorControlMonitor>(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("Monitor not found")));
		}
	}

	private sealed class Monitor : IMonitorControlMonitor
	{
		private readonly ImmutableArray<byte> _capabilities;
		private byte _brightness;

		public Monitor(string capabilities)
		{
			_capabilities = ImmutableCollectionsMarshal.AsImmutableArray(Encoding.UTF8.GetBytes(capabilities));
			_brightness = 70;
		}

		public void Dispose()
		{
		}

		public Task<ImmutableArray<byte>> GetCapabilitiesAsync(CancellationToken cancellationToken) => Task.FromResult(_capabilities);

		public Task<VcpFeatureResponse> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
		{
			switch ((VcpCode)vcpCode)
			{
			case VcpCode.Luminance:
				return Task.FromResult(new VcpFeatureResponse(_brightness, 100, false));
			default:
				throw new NotSupportedException();
			}
		}

		public Task SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken)
		{
			switch ((VcpCode)vcpCode)
			{
			case VcpCode.Luminance:
				_brightness = Math.Clamp((byte)value, (byte)0, (byte)100);
				break;
			}
			return Task.CompletedTask;
		}
	}

	[Fact]
	public async Task ShouldReadCapabilities()
	{
		var service = new FakeMonitorControlService();
		var i2cBusProvider = new InteractiveModeFallbackMonitorI2cBusProvider(service);
		var monitorI2cBusResolver = await i2cBusProvider.GetMonitorBusResolverAsync(AdapterDeviceName1, CancellationToken.None);
		var i2cBus = await monitorI2cBusResolver(PnpVendorId.FromRaw(Adapter1Monitor1VendorId), Adapter1Monitor1ProductId, Adapter1Monitor1IdSerialNumber, Adapter1Monitor1SerialNumber, CancellationToken.None);
		await using var ddc = new DisplayDataChannel(i2cBus, true);
		var buffer = new byte[1024];
		int length = await ddc.GetCapabilitiesAsync(buffer, CancellationToken.None);
		Assert.Equal(Adapter1Monitor1Capabilities, Encoding.UTF8.GetString(buffer.AsSpan(0, length)));
	}

	[Fact]
	public async Task ShouldGetAndSetLuminance()
	{
		var service = new FakeMonitorControlService();
		var i2cBusProvider = new InteractiveModeFallbackMonitorI2cBusProvider(service);
		var monitorI2cBusResolver = await i2cBusProvider.GetMonitorBusResolverAsync(AdapterDeviceName1, CancellationToken.None);
		var i2cBus = await monitorI2cBusResolver(PnpVendorId.FromRaw(Adapter1Monitor1VendorId), Adapter1Monitor1ProductId, Adapter1Monitor1IdSerialNumber, Adapter1Monitor1SerialNumber, CancellationToken.None);
		await using var ddc = new DisplayDataChannel(i2cBus, true);
		var response = await ddc.GetVcpFeatureAsync((byte)VcpCode.Luminance, CancellationToken.None);
		Assert.Equal(70, response.CurrentValue);
		Assert.Equal(100, response.MaximumValue);
		await ddc.SetVcpFeatureAsync((byte)VcpCode.Luminance, 15, CancellationToken.None);
		response = await ddc.GetVcpFeatureAsync((byte)VcpCode.Luminance, CancellationToken.None);
		Assert.Equal(15, response.CurrentValue);
		Assert.Equal(100, response.MaximumValue);
	}
}
