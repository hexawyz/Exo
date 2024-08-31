using DeviceTools.Logitech.HidPlusPlus;
using Exo.Features;
using Exo.Features.Keyboards;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Logitech;

public abstract partial class LogitechUniversalDriver
{
	// Non abstract driver types that can be returned for each device kind.
	// Lot of dirty inheritance stuff here. Maybe some of it can be avoided, I'm not sure yet.
	private static class BaseDrivers
	{
		internal class RegisterAccessDirectGeneric : RegisterAccessDirect
		{
			public RegisterAccessDirectGeneric(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal class RegisterAccessDirectKeyboard : RegisterAccessDirect, IDeviceDriver<IKeyboardDeviceFeature>
		{
			public RegisterAccessDirectKeyboard(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureSet.Empty<IKeyboardDeviceFeature>();
		}

		internal class RegisterAccessDirectMouse : RegisterAccessDirect, IDeviceDriver<IMouseDeviceFeature>
		{
			public RegisterAccessDirectMouse(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureSet.Empty<IMouseDeviceFeature>();
		}

		internal class RegisterAccessThroughReceiverGeneric : RegisterAccessThroughReceiver
		{
			public RegisterAccessThroughReceiverGeneric(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal class RegisterAccessThroughReceiverKeyboard : RegisterAccessThroughReceiver, IDeviceDriver<IKeyboardDeviceFeature>
		{
			public RegisterAccessThroughReceiverKeyboard(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureSet.Empty<IKeyboardDeviceFeature>();
		}

		internal class RegisterAccessThroughReceiverMouse : RegisterAccessThroughReceiver, IDeviceDriver<IMouseDeviceFeature>
		{
			public RegisterAccessThroughReceiverMouse(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureSet.Empty<IMouseDeviceFeature>();
		}

		internal class FeatureAccessDirectGeneric : FeatureAccessDirect
		{
			public FeatureAccessDirectGeneric(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal class FeatureAccessDirectKeyboard : FeatureAccessDirect, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureSet<IKeyboardDeviceFeature> _keyboardFeatures;

			public FeatureAccessDirectKeyboard(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_keyboardFeatures = HasLockKeys ?
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardLockKeysFeature, IKeyboardBacklightFeature>(this) :
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardLockKeysFeature>(this) :
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardBacklightFeature>(this) :
						FeatureSet.Empty<IKeyboardDeviceFeature>();
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => _keyboardFeatures;
		}

		internal class FeatureAccessDirectMouse : FeatureAccessDirect, IDeviceDriver<IMouseDeviceFeature>
		{
			private readonly IDeviceFeatureSet<IMouseDeviceFeature> _mouseFeatures;

			public FeatureAccessDirectMouse(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_mouseFeatures = CreateMouseFeatures();
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => _mouseFeatures;
		}

		internal sealed class FeatureAccessThroughReceiverGeneric : FeatureAccessThroughReceiver
		{
			public FeatureAccessThroughReceiverGeneric(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal sealed class FeatureAccessThroughReceiverKeyboard : FeatureAccessThroughReceiver, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureSet<IKeyboardDeviceFeature> _keyboardFeatures;

			public FeatureAccessThroughReceiverKeyboard(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_keyboardFeatures = HasLockKeys ?
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardLockKeysFeature, IKeyboardBacklightFeature>(this) :
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardLockKeysFeature>(this) :
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardBacklightFeature>(this) :
						FeatureSet.Empty<IKeyboardDeviceFeature>();
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => _keyboardFeatures;
		}

		internal sealed class FeatureAccessThroughReceiverMouse : FeatureAccessThroughReceiver, IDeviceDriver<IMouseDeviceFeature>
		{
			private readonly IDeviceFeatureSet<IMouseDeviceFeature> _mouseFeatures;

			public FeatureAccessThroughReceiverMouse(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_mouseFeatures = CreateMouseFeatures();
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => _mouseFeatures;
		}
	}
}
