using System.Collections.Immutable;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;

namespace DeviceTools.Logitech.HidPlusPlus;

public abstract partial class HidPlusPlusDevice
{
	public abstract partial class FeatureAccess
	{
		private sealed class DpiFeatureHandler : FeatureHandler
		{
			public override HidPlusPlusFeature Feature => HidPlusPlusFeature.AdjustableDpi;

			private byte _sensorCount;
			private ushort _currentDpi;

			public DpiFeatureHandler(FeatureAccess device, byte featureIndex) : base(device, featureIndex)
			{
			}

			public ushort CurrentDpi => _currentDpi;

			public override async Task InitializeAsync(int retryCount, CancellationToken cancellationToken)
			{
				byte sensorCount =
				(
					await Device.SendWithRetryAsync<AdjustableDpi.GetSensorCount.Response>(FeatureIndex, AdjustableDpi.GetSensorCount.FunctionId, retryCount, cancellationToken)
						.ConfigureAwait(false)
				).SensorCount;

				var dpiRange = new List<DpiRange>();

				for (int i = 0; i < sensorCount; i++)
				{
					var dpiInformation = await Device.SendWithRetryAsync<AdjustableDpi.GetSensorDpi.Request, AdjustableDpi.GetSensorDpi.Response>
					(
						FeatureIndex,
						AdjustableDpi.GetSensorDpi.FunctionId,
						new() { SensorIndex = (byte)i },
						retryCount,
						cancellationToken
					).ConfigureAwait(false);

					var dpiRanges = await GetDpiRangesAsync((byte)i, cancellationToken).ConfigureAwait(false);

					if (i == 0)
					{
						_currentDpi = dpiInformation.CurrentDpi;
					}
				}

				_sensorCount = sensorCount;
			}

			public async ValueTask<ImmutableArray<DpiRange>> GetDpiRangesAsync(byte sensorIndex, CancellationToken cancellationToken)
			{
				var response = await Device.SendWithRetryAsync<AdjustableDpi.GetSensorDpiRanges.Request, AdjustableDpi.GetSensorDpiRanges.Response>
				(
					FeatureIndex,
					AdjustableDpi.GetSensorDpiRanges.FunctionId,
					new() { SensorIndex = (byte)sensorIndex },
					HidPlusPlusTransportExtensions.DefaultRetryCount,
					cancellationToken
				).ConfigureAwait(false);

				var ranges = ImmutableArray.CreateBuilder<DpiRange>(1);

				const int StateMinimumValue = 0;
				const int StateStepOrMinimumValue = 1;
				const int StateMaximumValue = 2;

				int state = StateMinimumValue;
				ushort dpi = 0;
				ushort step = 0;

				for (int i = 0; i < response.ItemCount; i++)
				{
					ushort value = response[i];

					switch (state)
					{
					case StateMinimumValue:
						if (value == 0) goto Completed;
						if (value >= 0xE000) goto InvalidDpiValue;
						dpi = value;
						state = StateStepOrMinimumValue;
						break;
					case StateStepOrMinimumValue:
						if (value >= 0xE000)
						{
							step = (ushort)(value - 0xE000);
							state = StateMaximumValue;
						}
						else
						{
							ranges.Add(new(dpi));
							if (value == 0) goto Completed;
							dpi = value;
						}
						break;
					case StateMaximumValue:
						if (value >= 0xE000 || value == 0) goto InvalidDpiValue;
						ranges.Add(new(dpi, value, step));
						state = StateMinimumValue;
						break;
					}
				}

				switch (state)
				{
				case StateMinimumValue: break;
				case StateStepOrMinimumValue:
					ranges.Add(new(dpi));
					break;
				case StateMaximumValue:
					throw new InvalidDataException("List of DPI ranges was truncated.");
				}

			Completed:;
				return ranges.DrainToImmutable();

			InvalidDpiValue:;
				throw new InvalidDataException("Invalid DPI value.");
			}
		}
	}
}
