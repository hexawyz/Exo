using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Exo.Features;
using Exo.Features.Monitors;

namespace Exo.Devices.Monitors;

public partial class GenericMonitorDriver
{
	/// <summary>A class that should generally be used to construct a monitor feature set.</summary>
	/// <remarks>
	/// If necessary, this class can be derived from in order to customize the creation of the various features, or to create a custom feature set with some features added or removed.
	/// </remarks>
	protected class MonitorFeatureSetBuilder
	{
		private SupportedFeatures _supportedFeatures;

		private byte _brightnessVcpCode;
		private byte _contrastVcpCode;
		private byte _sharpnessVcpCode;
		private byte _audioVolumeVcpCode;
		private byte _inputSelectVcpCode;
		private byte _redVideoGainVcpCode;
		private byte _greenVideoGainVcpCode;
		private byte _blueVideoGainVcpCode;
		private byte _redSixAxisSaturationControlVcpCode;
		private byte _yellowSixAxisSaturationControlVcpCode;
		private byte _greenSixAxisSaturationControlVcpCode;
		private byte _cyanSixAxisSaturationControlVcpCode;
		private byte _blueSixAxisSaturationControlVcpCode;
		private byte _magentaSixAxisSaturationControlVcpCode;
		private byte _redSixAxisHueControlVcpCode;
		private byte _yellowSixAxisHueControlVcpCode;
		private byte _greenSixAxisHueControlVcpCode;
		private byte _cyanSixAxisHueControlVcpCode;
		private byte _blueSixAxisHueControlVcpCode;
		private byte _magentaSixAxisHueControlVcpCode;
		private byte _osdLanguageVcpCode;
		private byte _responseTimeVcpCode;
		private byte _inputLagVcpCode;
		private byte _blueLightFilterLevelVcpCode;
		private byte _powerIndicatorVcpCode;

		private ushort _powerIndicatorOffValue;
		private ushort _powerIndicatorOnValue;

		private ImmutableArray<NonContinuousValueDescription> _inputSources;
		private ImmutableArray<NonContinuousValueDescription> _inputLagLevels;
		private ImmutableArray<NonContinuousValueDescription> _responseTimeLevels;
		private ImmutableArray<NonContinuousValueDescription> _osdLanguages;

		private void AddFeature(ref byte vcpCodeStorage, SupportedFeatures supportedFeature, byte vcpCode)
		{
			_supportedFeatures |= supportedFeature;
			vcpCodeStorage = vcpCode;
		}

		private void AddFeature(ref byte vcpCodeStorage, ref ImmutableArray<NonContinuousValueDescription> allowedValueStorage, SupportedFeatures supportedFeature, byte vcpCode, ImmutableArray<NonContinuousValueDescription> allowedValues)
		{
			_supportedFeatures |= supportedFeature;
			vcpCodeStorage = vcpCode;
			allowedValueStorage = allowedValues;
		}

		public virtual void AddCapabilitiesFeature() => _supportedFeatures |= SupportedFeatures.Capabilities;

		public virtual void AddBrightnessFeature(byte vcpCode) => AddFeature(ref _brightnessVcpCode, SupportedFeatures.Brightness, vcpCode);
		public virtual void AddContrastFeature(byte vcpCode) => AddFeature(ref _contrastVcpCode, SupportedFeatures.Contrast, vcpCode);
		public virtual void AddSharpnessFeature(byte vcpCode) => AddFeature(ref _sharpnessVcpCode, SupportedFeatures.Sharpness, vcpCode);
		public virtual void AddAudioVolumeFeature(byte vcpCode) => AddFeature(ref _audioVolumeVcpCode, SupportedFeatures.AudioVolume, vcpCode);
		public virtual void AddRedVideoGainFeature(byte vcpCode) => AddFeature(ref _redVideoGainVcpCode, SupportedFeatures.VideoGainRed, vcpCode);
		public virtual void AddGreenVideoGainFeature(byte vcpCode) => AddFeature(ref _greenVideoGainVcpCode, SupportedFeatures.VideoGainGreen, vcpCode);
		public virtual void AddBlueVideoGainFeature(byte vcpCode) => AddFeature(ref _blueVideoGainVcpCode, SupportedFeatures.VideoGainBlue, vcpCode);
		public virtual void AddRedSixAxisSaturationControlFeature(byte vcpCode) => AddFeature(ref _redSixAxisSaturationControlVcpCode, SupportedFeatures.SixAxisSaturationControlRed, vcpCode);
		public virtual void AddYellowSixAxisSaturationControlFeature(byte vcpCode) => AddFeature(ref _yellowSixAxisSaturationControlVcpCode, SupportedFeatures.SixAxisSaturationControlYellow, vcpCode);
		public virtual void AddGreenSixAxisSaturationControlFeature(byte vcpCode) => AddFeature(ref _greenSixAxisSaturationControlVcpCode, SupportedFeatures.SixAxisSaturationControlGreen, vcpCode);
		public virtual void AddCyanSixAxisSaturationControlFeature(byte vcpCode) => AddFeature(ref _cyanSixAxisSaturationControlVcpCode, SupportedFeatures.SixAxisSaturationControlCyan, vcpCode);
		public virtual void AddBlueSixAxisSaturationControlFeature(byte vcpCode) => AddFeature(ref _blueSixAxisSaturationControlVcpCode, SupportedFeatures.SixAxisSaturationControlBlue, vcpCode);
		public virtual void AddMagentaSixAxisSaturationControlFeature(byte vcpCode) => AddFeature(ref _magentaSixAxisSaturationControlVcpCode, SupportedFeatures.SixAxisSaturationControlMagenta, vcpCode);
		public virtual void AddRedSixAxisHueControlFeature(byte vcpCode) => AddFeature(ref _redSixAxisHueControlVcpCode, SupportedFeatures.SixAxisHueControlRed, vcpCode);
		public virtual void AddYellowSixAxisHueControlFeature(byte vcpCode) => AddFeature(ref _yellowSixAxisHueControlVcpCode, SupportedFeatures.SixAxisHueControlYellow, vcpCode);
		public virtual void AddGreenSixAxisHueControlFeature(byte vcpCode) => AddFeature(ref _greenSixAxisHueControlVcpCode, SupportedFeatures.SixAxisHueControlGreen, vcpCode);
		public virtual void AddCyanSixAxisHueControlFeature(byte vcpCode) => AddFeature(ref _cyanSixAxisHueControlVcpCode, SupportedFeatures.SixAxisHueControlCyan, vcpCode);
		public virtual void AddBlueSixAxisHueControlFeature(byte vcpCode) => AddFeature(ref _blueSixAxisHueControlVcpCode, SupportedFeatures.SixAxisHueControlBlue, vcpCode);
		public virtual void AddMagentaSixAxisHueControlFeature(byte vcpCode) => AddFeature(ref _magentaSixAxisHueControlVcpCode, SupportedFeatures.SixAxisHueControlMagenta, vcpCode);
		public virtual void AddBlueLightFilterLevelFeature(byte vcpCode) => AddFeature(ref _blueLightFilterLevelVcpCode, SupportedFeatures.BlueLightFilterLevel, vcpCode);

		public virtual void AddPowerIndicatorToggleFeature(byte vcpCode, ushort offValue, ushort onValue)
		{
			AddFeature(ref _powerIndicatorVcpCode, SupportedFeatures.PowerIndicator, vcpCode);
			_powerIndicatorOffValue = offValue;
			_powerIndicatorOnValue = onValue;
		}

		public virtual void AddInputSelectFeature(byte vcpCode, ImmutableArray<NonContinuousValueDescription> inputSources)
			=> AddFeature(ref _inputSelectVcpCode, ref _inputSources, SupportedFeatures.InputSelect, vcpCode, inputSources);

		public virtual void AddInputLagFeature(byte vcpCode, ImmutableArray<NonContinuousValueDescription> inputLagLevels)
			=> AddFeature(ref _inputLagVcpCode, ref _inputLagLevels, SupportedFeatures.InputLag, vcpCode, inputLagLevels);

		public virtual void AddResponseTimeFeature(byte vcpCode, ImmutableArray<NonContinuousValueDescription> responseTimeLevels)
			=> AddFeature(ref _responseTimeVcpCode, ref _responseTimeLevels, SupportedFeatures.ResponseTime, vcpCode, responseTimeLevels);

		public virtual void AddOsdLanguageFeature(byte vcpCode, ImmutableArray<NonContinuousValueDescription> osdLanguages)
			=> AddFeature(ref _osdLanguageVcpCode, ref _osdLanguages, SupportedFeatures.OsdLanguage, vcpCode, osdLanguages);

		protected virtual IMonitorCapabilitiesFeature? CreateCapabilitiesFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.Capabilities) != 0 ? driver : null;

		protected virtual IMonitorRawCapabilitiesFeature? CreateRawCapabilitiesFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.Capabilities) != 0 ? driver : null;

		protected virtual IMonitorRawVcpFeature? CreateRawVcpFeature(GenericMonitorDriver driver)
			=> driver;

		protected virtual IMonitorBrightnessFeature? CreateBrightnessFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.Brightness) != 0 ? new BrightnessFeature(driver, _brightnessVcpCode) : null;

		protected virtual IMonitorContrastFeature? CreateContrastFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.Contrast) != 0 ? new ContrastFeature(driver, _contrastVcpCode) : null;

		protected virtual IMonitorSharpnessFeature? CreateSharpnessFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.Sharpness) != 0 ? new SharpnessFeature(driver, _sharpnessVcpCode) : null;

		protected virtual IMonitorSpeakerAudioVolumeFeature? CreateAudioVolumeFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.AudioVolume) != 0 ? new SpeakerAudioVolumeFeature(driver, _audioVolumeVcpCode) : null;

		protected virtual IMonitorRedVideoGainFeature? CreateRedVideoGainFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.VideoGainRed) != 0 ? new RedVideoGainFeature(driver, _redVideoGainVcpCode) : null;

		protected virtual IMonitorGreenVideoGainFeature? CreateGreenVideoGainFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.VideoGainGreen) != 0 ? new GreenVideoGainFeature(driver, _greenVideoGainVcpCode) : null;

		protected virtual IMonitorBlueVideoGainFeature? CreateBlueVideoGainFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.VideoGainBlue) != 0 ? new BlueVideoGainFeature(driver, _blueVideoGainVcpCode) : null;

		protected virtual IMonitorRedSixAxisSaturationControlFeature? CreateRedSixAxisSaturationControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisSaturationControlRed) != 0 ? new RedSixAxisSaturationControlFeature(driver, _redSixAxisSaturationControlVcpCode) : null;

		protected virtual IMonitorYellowSixAxisSaturationControlFeature? CreateYellowSixAxisSaturationControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisSaturationControlYellow) != 0 ? new YellowSixAxisSaturationControlFeature(driver, _yellowSixAxisSaturationControlVcpCode) : null;

		protected virtual IMonitorGreenSixAxisSaturationControlFeature? CreateGreenSixAxisSaturationControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisSaturationControlGreen) != 0 ? new GreenSixAxisSaturationControlFeature(driver, _greenSixAxisSaturationControlVcpCode) : null;

		protected virtual IMonitorCyanSixAxisSaturationControlFeature? CreateCyanSixAxisSaturationControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisSaturationControlCyan) != 0 ? new CyanSixAxisSaturationControlFeature(driver, _cyanSixAxisSaturationControlVcpCode) : null;

		protected virtual IMonitorBlueSixAxisSaturationControlFeature? CreateBlueSixAxisSaturationControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisSaturationControlBlue) != 0 ? new BlueSixAxisSaturationControlFeature(driver, _blueSixAxisSaturationControlVcpCode) : null;

		protected virtual IMonitorMagentaSixAxisSaturationControlFeature? CreateMagentaSixAxisSaturationControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisSaturationControlMagenta) != 0 ? new MagentaSixAxisSaturationControlFeature(driver, _magentaSixAxisSaturationControlVcpCode) : null;

		protected virtual IMonitorRedSixAxisHueControlFeature? CreateRedSixAxisHueControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisHueControlRed) != 0 ? new RedSixAxisHueControlFeature(driver, _redSixAxisHueControlVcpCode) : null;

		protected virtual IMonitorYellowSixAxisHueControlFeature? CreateYellowSixAxisHueControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisHueControlYellow) != 0 ? new YellowSixAxisHueControlFeature(driver, _yellowSixAxisHueControlVcpCode) : null;

		protected virtual IMonitorGreenSixAxisHueControlFeature? CreateGreenSixAxisHueControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisHueControlGreen) != 0 ? new GreenSixAxisHueControlFeature(driver, _greenSixAxisHueControlVcpCode) : null;

		protected virtual IMonitorCyanSixAxisHueControlFeature? CreateCyanSixAxisHueControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisHueControlCyan) != 0 ? new CyanSixAxisHueControlFeature(driver, _cyanSixAxisHueControlVcpCode) : null;

		protected virtual IMonitorBlueSixAxisHueControlFeature? CreateBlueSixAxisHueControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisHueControlBlue) != 0 ? new BlueSixAxisHueControlFeature(driver, _blueSixAxisHueControlVcpCode) : null;

		protected virtual IMonitorMagentaSixAxisHueControlFeature? CreateMagentaSixAxisHueControlFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.SixAxisHueControlMagenta) != 0 ? new MagentaSixAxisHueControlFeature(driver, _magentaSixAxisHueControlVcpCode) : null;

		protected virtual IMonitorBlueLightFilterLevelFeature? CreateBlueLightFilterLevelFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.BlueLightFilterLevel) != 0 ? new BlueLightFilterLevelFeature(driver, _blueLightFilterLevelVcpCode) : null;

		protected virtual IMonitorInputLagFeature? CreateInputLagFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.InputLag) != 0 ? new InputLagFeature(driver, _inputLagVcpCode, _inputLagLevels) : null;

		protected virtual IMonitorInputSelectFeature? CreateInputSelectFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.InputSelect) != 0 ? new InputSelectFeature(driver, _inputSelectVcpCode, _inputSources) : null;

		protected virtual IMonitorResponseTimeFeature? CreateResponseTimeFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.ResponseTime) != 0 ? new ResponseTimeFeature(driver, _responseTimeVcpCode, _responseTimeLevels) : null;

		protected virtual IMonitorOsdLanguageFeature? CreateOsdLanguageFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.OsdLanguage) != 0 ? new OsdLanguageFeature(driver, _osdLanguageVcpCode, _osdLanguages) : null;

		protected virtual IMonitorPowerIndicatorToggleFeature? CreatePowerIndicatorToggleFeature(GenericMonitorDriver driver)
			=> (_supportedFeatures & SupportedFeatures.PowerIndicator) != 0 ? new PowerIndicatorToggleFeature(driver, _powerIndicatorVcpCode, _powerIndicatorOffValue, _powerIndicatorOnValue) : null;

		public virtual IDeviceFeatureSet<IMonitorDeviceFeature> CreateFeatureSet(GenericMonitorDriver driver)
			=> new MonitorFeatureSet
			(
				CreateCapabilitiesFeature(driver),
				CreateRawCapabilitiesFeature(driver),
				CreateRawVcpFeature(driver),
				CreateBrightnessFeature(driver),
				CreateContrastFeature(driver),
				CreateSharpnessFeature(driver),
				CreateBlueLightFilterLevelFeature(driver),
				CreateAudioVolumeFeature(driver),
				CreateInputSelectFeature(driver),
				CreateRedVideoGainFeature(driver),
				CreateGreenVideoGainFeature(driver),
				CreateBlueVideoGainFeature(driver),
				CreateRedSixAxisSaturationControlFeature(driver),
				CreateYellowSixAxisSaturationControlFeature(driver),
				CreateGreenSixAxisSaturationControlFeature(driver),
				CreateCyanSixAxisSaturationControlFeature(driver),
				CreateBlueSixAxisSaturationControlFeature(driver),
				CreateMagentaSixAxisSaturationControlFeature(driver),
				CreateRedSixAxisHueControlFeature(driver),
				CreateYellowSixAxisHueControlFeature(driver),
				CreateGreenSixAxisHueControlFeature(driver),
				CreateCyanSixAxisHueControlFeature(driver),
				CreateBlueSixAxisHueControlFeature(driver),
				CreateMagentaSixAxisHueControlFeature(driver),
				CreateInputLagFeature(driver),
				CreateResponseTimeFeature(driver),
				CreateOsdLanguageFeature(driver),
				CreatePowerIndicatorToggleFeature(driver)
			);
	}

	/// <summary>The basic monitor feature set, exposing all currently supported monitor features.</summary>
	protected sealed class MonitorFeatureSet : IDeviceFeatureSet<IMonitorDeviceFeature>
	{
		private readonly IMonitorCapabilitiesFeature? _capabilitiesFeature;
		private readonly IMonitorRawCapabilitiesFeature? _rawCapabilitiesFeature;
		private readonly IMonitorRawVcpFeature? _rawVcpFeature;
		private readonly IMonitorBrightnessFeature? _brightnessFeature;
		private readonly IMonitorContrastFeature? _contrastFeature;
		private readonly IMonitorSharpnessFeature? _sharpnessFeature;
		private readonly IMonitorBlueLightFilterLevelFeature? _blueLightFilterLevelFeature;
		private readonly IMonitorSpeakerAudioVolumeFeature? _speakerAudioVolumeFeature;
		private readonly IMonitorInputSelectFeature? _inputSelectFeature;
		private readonly IMonitorRedVideoGainFeature? _redVideoGainFeature;
		private readonly IMonitorGreenVideoGainFeature? _greenVideoGainFeature;
		private readonly IMonitorBlueVideoGainFeature? _blueVideoGainFeature;
		private readonly IMonitorRedSixAxisSaturationControlFeature? _redSixAxisSaturationControlFeature;
		private readonly IMonitorYellowSixAxisSaturationControlFeature? _yellowSixAxisSaturationControlFeature;
		private readonly IMonitorGreenSixAxisSaturationControlFeature? _greenSixAxisSaturationControlFeature;
		private readonly IMonitorCyanSixAxisSaturationControlFeature? _cyanSixAxisSaturationControlFeature;
		private readonly IMonitorBlueSixAxisSaturationControlFeature? _blueSixAxisSaturationControlFeature;
		private readonly IMonitorMagentaSixAxisSaturationControlFeature? _magentaSixAxisSaturationControlFeature;
		private readonly IMonitorRedSixAxisHueControlFeature? _redSixAxisHueControlFeature;
		private readonly IMonitorYellowSixAxisHueControlFeature? _yellowSixAxisHueControlFeature;
		private readonly IMonitorGreenSixAxisHueControlFeature? _greenSixAxisHueControlFeature;
		private readonly IMonitorCyanSixAxisHueControlFeature? _cyanSixAxisHueControlFeature;
		private readonly IMonitorBlueSixAxisHueControlFeature? _blueSixAxisHueControlFeature;
		private readonly IMonitorMagentaSixAxisHueControlFeature? _magentaSixAxisHueControlFeature;
		private readonly IMonitorInputLagFeature? _inputLagFeature;
		private readonly IMonitorResponseTimeFeature? _responseTimeFeature;
		private readonly IMonitorOsdLanguageFeature? _osdLanguageFeature;
		private readonly IMonitorPowerIndicatorToggleFeature? _powerIndicatorToggleFeature;

		private Dictionary<Type, IMonitorDeviceFeature>? _cachedFeatureDictionary;

		public MonitorFeatureSet
		(
			IMonitorCapabilitiesFeature? capabilitiesFeature,
			IMonitorRawCapabilitiesFeature? rawCapabilitiesFeature,
			IMonitorRawVcpFeature? rawVcpFeature,
			IMonitorBrightnessFeature? brightnessFeature,
			IMonitorContrastFeature? contrastFeature,
			IMonitorSharpnessFeature? sharpnessFeature,
			IMonitorBlueLightFilterLevelFeature? blueLightFilterLevelFeature,
			IMonitorSpeakerAudioVolumeFeature? speakerAudioVolumeFeature,
			IMonitorInputSelectFeature? inputSelectFeature,
			IMonitorRedVideoGainFeature? redVideoGainFeature,
			IMonitorGreenVideoGainFeature? greenVideoGainFeature,
			IMonitorBlueVideoGainFeature? blueVideoGainFeature,
			IMonitorRedSixAxisSaturationControlFeature? redSixAxisSaturationControlFeature,
			IMonitorYellowSixAxisSaturationControlFeature? yellowSixAxisSaturationControlFeature,
			IMonitorGreenSixAxisSaturationControlFeature? greenSixAxisSaturationControlFeature,
			IMonitorCyanSixAxisSaturationControlFeature? cyanSixAxisSaturationControlFeature,
			IMonitorBlueSixAxisSaturationControlFeature? blueSixAxisSaturationControlFeature,
			IMonitorMagentaSixAxisSaturationControlFeature? magentaSixAxisSaturationControlFeature,
			IMonitorRedSixAxisHueControlFeature? redSixAxisHueControlFeature,
			IMonitorYellowSixAxisHueControlFeature? yellowSixAxisHueControlFeature,
			IMonitorGreenSixAxisHueControlFeature? greenSixAxisHueControlFeature,
			IMonitorCyanSixAxisHueControlFeature? cyanSixAxisHueControlFeature,
			IMonitorBlueSixAxisHueControlFeature? blueSixAxisHueControlFeature,
			IMonitorMagentaSixAxisHueControlFeature? magentaSixAxisHueControlFeature,
			IMonitorInputLagFeature? inputLagFeature,
			IMonitorResponseTimeFeature? responseTimeFeature,
			IMonitorOsdLanguageFeature? osdLanguageFeature,
			IMonitorPowerIndicatorToggleFeature? powerIndicatorToggleFeature
		)
		{
			_capabilitiesFeature = capabilitiesFeature;
			_rawCapabilitiesFeature = rawCapabilitiesFeature;
			_rawVcpFeature = rawVcpFeature;
			_brightnessFeature = brightnessFeature;
			_contrastFeature = contrastFeature;
			_sharpnessFeature = sharpnessFeature;
			_blueLightFilterLevelFeature = blueLightFilterLevelFeature;
			_speakerAudioVolumeFeature = speakerAudioVolumeFeature;
			_inputSelectFeature = inputSelectFeature;
			_redVideoGainFeature = redVideoGainFeature;
			_greenVideoGainFeature = greenVideoGainFeature;
			_blueVideoGainFeature = blueVideoGainFeature;
			_redSixAxisSaturationControlFeature = redSixAxisSaturationControlFeature;
			_yellowSixAxisSaturationControlFeature = yellowSixAxisSaturationControlFeature;
			_greenSixAxisSaturationControlFeature = greenSixAxisSaturationControlFeature;
			_cyanSixAxisSaturationControlFeature = cyanSixAxisSaturationControlFeature;
			_blueSixAxisSaturationControlFeature = blueSixAxisSaturationControlFeature;
			_magentaSixAxisSaturationControlFeature = magentaSixAxisSaturationControlFeature;
			_redSixAxisHueControlFeature = redSixAxisHueControlFeature;
			_yellowSixAxisHueControlFeature = yellowSixAxisHueControlFeature;
			_greenSixAxisHueControlFeature = greenSixAxisHueControlFeature;
			_cyanSixAxisHueControlFeature = cyanSixAxisHueControlFeature;
			_blueSixAxisHueControlFeature = blueSixAxisHueControlFeature;
			_magentaSixAxisHueControlFeature = magentaSixAxisHueControlFeature;
			_inputLagFeature = inputLagFeature;
			_responseTimeFeature = responseTimeFeature;
			_osdLanguageFeature = osdLanguageFeature;
			_powerIndicatorToggleFeature = powerIndicatorToggleFeature;
		}

		public IMonitorDeviceFeature? this[Type type] => (_cachedFeatureDictionary ??= new(this))[type];

		public bool IsEmpty => Count == 0;

		public int Count
		{
			get
			{
				int count = 0;

				if (_capabilitiesFeature is not null) count++;
				if (_rawCapabilitiesFeature is not null) count++;
				if (_rawVcpFeature is not null) count++;

				if (_brightnessFeature is not null) count++;
				if (_contrastFeature is not null) count++;
				if (_sharpnessFeature is not null) count++;
				if (_blueLightFilterLevelFeature is not null) count++;

				if (_speakerAudioVolumeFeature is not null) count++;
				if (_inputSelectFeature is not null) count++;

				if (_redVideoGainFeature is not null) count++;
				if (_greenVideoGainFeature is not null) count++;
				if (_blueVideoGainFeature is not null) count++;

				if (_redSixAxisSaturationControlFeature is not null) count++;
				if (_yellowSixAxisSaturationControlFeature is not null) count++;
				if (_greenSixAxisSaturationControlFeature is not null) count++;
				if (_cyanSixAxisSaturationControlFeature is not null) count++;
				if (_blueSixAxisSaturationControlFeature is not null) count++;
				if (_magentaSixAxisSaturationControlFeature is not null) count++;

				if (_redSixAxisHueControlFeature is not null) count++;
				if (_yellowSixAxisHueControlFeature is not null) count++;
				if (_greenSixAxisHueControlFeature is not null) count++;
				if (_cyanSixAxisHueControlFeature is not null) count++;
				if (_blueSixAxisHueControlFeature is not null) count++;
				if (_magentaSixAxisHueControlFeature is not null) count++;

				if (_inputLagFeature is not null) count++;
				if (_responseTimeFeature is not null) count++;

				if (_osdLanguageFeature is not null) count++;
				if (_powerIndicatorToggleFeature is not null) count++;

				return count;
			}
		}

		T? IDeviceFeatureSet<IMonitorDeviceFeature>.GetFeature<T>() where T : class => Unsafe.As<T>(GetFeature<T>());

		private IMonitorDeviceFeature? GetFeature<T>() where T : class, IMonitorDeviceFeature
		{
			if (typeof(T) == typeof(IMonitorCapabilitiesFeature) && _capabilitiesFeature is not null) return _capabilitiesFeature;
			if (typeof(T) == typeof(IMonitorRawCapabilitiesFeature) && _rawCapabilitiesFeature is not null) return _rawCapabilitiesFeature;
			if (typeof(T) == typeof(IMonitorRawVcpFeature) && _rawVcpFeature is not null) return _rawVcpFeature;

			if (typeof(T) == typeof(IMonitorBrightnessFeature) && _brightnessFeature is not null) return _brightnessFeature;
			if (typeof(T) == typeof(IMonitorContrastFeature) && _contrastFeature is not null) return _contrastFeature;
			if (typeof(T) == typeof(IMonitorSharpnessFeature) && _sharpnessFeature is not null) return _sharpnessFeature;
			if (typeof(T) == typeof(IMonitorBlueLightFilterLevelFeature) && _blueLightFilterLevelFeature is not null) return _blueLightFilterLevelFeature;

			if (typeof(T) == typeof(IMonitorSpeakerAudioVolumeFeature) && _speakerAudioVolumeFeature is not null) return _speakerAudioVolumeFeature;
			if (typeof(T) == typeof(IMonitorInputSelectFeature) && _inputSelectFeature is not null) return _inputSelectFeature;

			if (typeof(T) == typeof(IMonitorRedVideoGainFeature) && _redVideoGainFeature is not null) return _redVideoGainFeature;
			if (typeof(T) == typeof(IMonitorGreenVideoGainFeature) && _greenVideoGainFeature is not null) return _greenVideoGainFeature;
			if (typeof(T) == typeof(IMonitorBlueVideoGainFeature) && _blueVideoGainFeature is not null) return _blueVideoGainFeature;

			if (typeof(T) == typeof(IMonitorRedSixAxisSaturationControlFeature) && _redSixAxisSaturationControlFeature is not null) return _redSixAxisSaturationControlFeature;
			if (typeof(T) == typeof(IMonitorYellowSixAxisSaturationControlFeature) && _yellowSixAxisSaturationControlFeature is not null) return _yellowSixAxisSaturationControlFeature;
			if (typeof(T) == typeof(IMonitorGreenSixAxisSaturationControlFeature) && _greenSixAxisSaturationControlFeature is not null) return _greenSixAxisSaturationControlFeature;
			if (typeof(T) == typeof(IMonitorCyanSixAxisSaturationControlFeature) && _cyanSixAxisSaturationControlFeature is not null) return _cyanSixAxisSaturationControlFeature;
			if (typeof(T) == typeof(IMonitorBlueSixAxisSaturationControlFeature) && _blueSixAxisSaturationControlFeature is not null) return _blueSixAxisSaturationControlFeature;
			if (typeof(T) == typeof(IMonitorMagentaSixAxisSaturationControlFeature) && _magentaSixAxisSaturationControlFeature is not null) return _magentaSixAxisSaturationControlFeature;

			if (typeof(T) == typeof(IMonitorRedSixAxisHueControlFeature) && _redSixAxisHueControlFeature is not null) return _redSixAxisHueControlFeature;
			if (typeof(T) == typeof(IMonitorYellowSixAxisHueControlFeature) && _yellowSixAxisHueControlFeature is not null) return _yellowSixAxisHueControlFeature;
			if (typeof(T) == typeof(IMonitorGreenSixAxisHueControlFeature) && _greenSixAxisHueControlFeature is not null) return _greenSixAxisHueControlFeature;
			if (typeof(T) == typeof(IMonitorCyanSixAxisHueControlFeature) && _cyanSixAxisHueControlFeature is not null) return _cyanSixAxisHueControlFeature;
			if (typeof(T) == typeof(IMonitorBlueSixAxisHueControlFeature) && _blueSixAxisHueControlFeature is not null) return _blueSixAxisHueControlFeature;
			if (typeof(T) == typeof(IMonitorMagentaSixAxisHueControlFeature) && _magentaSixAxisHueControlFeature is not null) return _magentaSixAxisHueControlFeature;

			if (typeof(T) == typeof(IMonitorInputLagFeature) && _inputLagFeature is not null) return _inputLagFeature;
			if (typeof(T) == typeof(IMonitorResponseTimeFeature) && _responseTimeFeature is not null) return _responseTimeFeature;

			if (typeof(T) == typeof(IMonitorOsdLanguageFeature) && _osdLanguageFeature is not null) return _osdLanguageFeature;
			if (typeof(T) == typeof(IMonitorPowerIndicatorToggleFeature) && _powerIndicatorToggleFeature is not null) return _powerIndicatorToggleFeature;

			return null;
		}

		IEnumerator<KeyValuePair<Type, IMonitorDeviceFeature>> IEnumerable<KeyValuePair<Type, IMonitorDeviceFeature>>.GetEnumerator()
		{
			if (_capabilitiesFeature is not null) yield return new(typeof(IMonitorCapabilitiesFeature), _capabilitiesFeature);
			if (_rawCapabilitiesFeature is not null) yield return new(typeof(IMonitorRawCapabilitiesFeature), _rawCapabilitiesFeature);
			if (_rawVcpFeature is not null) yield return new(typeof(IMonitorRawVcpFeature), _rawVcpFeature);

			if (_brightnessFeature is not null) yield return new(typeof(IMonitorBrightnessFeature), _brightnessFeature);
			if (_contrastFeature is not null) yield return new(typeof(IMonitorContrastFeature), _contrastFeature);
			if (_sharpnessFeature is not null) yield return new(typeof(IMonitorSharpnessFeature), _sharpnessFeature);
			if (_blueLightFilterLevelFeature is not null) yield return new(typeof(IMonitorBlueLightFilterLevelFeature), _blueLightFilterLevelFeature);

			if (_speakerAudioVolumeFeature is not null) yield return new(typeof(IMonitorSpeakerAudioVolumeFeature), _speakerAudioVolumeFeature);
			if (_inputSelectFeature is not null) yield return new(typeof(IMonitorInputSelectFeature), _inputSelectFeature);

			if (_redVideoGainFeature is not null) yield return new(typeof(IMonitorRedVideoGainFeature), _redVideoGainFeature);
			if (_greenVideoGainFeature is not null) yield return new(typeof(IMonitorGreenVideoGainFeature), _greenVideoGainFeature);
			if (_blueVideoGainFeature is not null) yield return new(typeof(IMonitorBlueVideoGainFeature), _blueVideoGainFeature);

			if (_redSixAxisSaturationControlFeature is not null) yield return new(typeof(IMonitorRedSixAxisSaturationControlFeature), _redSixAxisSaturationControlFeature);
			if (_yellowSixAxisSaturationControlFeature is not null) yield return new(typeof(IMonitorYellowSixAxisSaturationControlFeature), _yellowSixAxisSaturationControlFeature);
			if (_greenSixAxisSaturationControlFeature is not null) yield return new(typeof(IMonitorGreenSixAxisSaturationControlFeature), _greenSixAxisSaturationControlFeature);
			if (_cyanSixAxisSaturationControlFeature is not null) yield return new(typeof(IMonitorCyanSixAxisSaturationControlFeature), _cyanSixAxisSaturationControlFeature);
			if (_blueSixAxisSaturationControlFeature is not null) yield return new(typeof(IMonitorBlueSixAxisSaturationControlFeature), _blueSixAxisSaturationControlFeature);
			if (_magentaSixAxisSaturationControlFeature is not null) yield return new(typeof(IMonitorMagentaSixAxisSaturationControlFeature), _magentaSixAxisSaturationControlFeature);

			if (_redSixAxisHueControlFeature is not null) yield return new(typeof(IMonitorRedSixAxisHueControlFeature), _redSixAxisHueControlFeature);
			if (_yellowSixAxisHueControlFeature is not null) yield return new(typeof(IMonitorYellowSixAxisHueControlFeature), _yellowSixAxisHueControlFeature);
			if (_greenSixAxisHueControlFeature is not null) yield return new(typeof(IMonitorGreenSixAxisHueControlFeature), _greenSixAxisHueControlFeature);
			if (_cyanSixAxisHueControlFeature is not null) yield return new(typeof(IMonitorCyanSixAxisHueControlFeature), _cyanSixAxisHueControlFeature);
			if (_blueSixAxisHueControlFeature is not null) yield return new(typeof(IMonitorBlueSixAxisHueControlFeature), _blueSixAxisHueControlFeature);
			if (_magentaSixAxisHueControlFeature is not null) yield return new(typeof(IMonitorMagentaSixAxisHueControlFeature), _magentaSixAxisHueControlFeature);

			if (_inputLagFeature is not null) yield return new(typeof(IMonitorInputLagFeature), _inputLagFeature);
			if (_responseTimeFeature is not null) yield return new(typeof(IMonitorResponseTimeFeature), _responseTimeFeature);

			if (_osdLanguageFeature is not null) yield return new(typeof(IMonitorOsdLanguageFeature), _osdLanguageFeature);
			if (_powerIndicatorToggleFeature is not null) yield return new(typeof(IMonitorPowerIndicatorToggleFeature), _powerIndicatorToggleFeature);
		}

		IEnumerator IEnumerable.GetEnumerator() => Unsafe.As<IEnumerable<KeyValuePair<Type, IMonitorDeviceFeature>>>(this).GetEnumerator();
	}

	protected abstract class ContinuousVcpFeature : IMonitorDeviceFeature, IContinuousVcpFeature
	{
		private readonly GenericMonitorDriver _driver;
		private readonly byte _vcpCode;

		private protected ContinuousVcpFeature(GenericMonitorDriver driver, byte vcpCode)
		{
			_driver = driver;
			_vcpCode = vcpCode;
		}

		public async ValueTask<ContinuousValue> GetValueAsync(CancellationToken cancellationToken)
			=> await _driver.GetVcpAsync(_vcpCode, cancellationToken).ConfigureAwait(false);

		public ValueTask SetValueAsync(ushort value, CancellationToken cancellationToken)
			=> _driver.SetVcpAsync(_vcpCode, value, cancellationToken);
	}

	protected abstract class BooleanVcpFeature : IMonitorDeviceFeature, IBooleanVcpFeature
	{
		private readonly GenericMonitorDriver _driver;
		private readonly ushort _offValue;
		private readonly ushort _onValue;
		private readonly byte _vcpCode;

		protected BooleanVcpFeature(GenericMonitorDriver driver, byte vcpCode, ushort offValue, ushort onValue)
		{
			_driver = driver;
			_vcpCode = vcpCode;
			_offValue = offValue;
			_onValue = onValue;
		}

		public async ValueTask<bool> GetValueAsync(CancellationToken cancellationToken)
			=> await _driver.GetBooleanVcpAsync(_vcpCode, _onValue, cancellationToken).ConfigureAwait(false);

		public ValueTask SetValueAsync(bool value, CancellationToken cancellationToken)
			=> _driver.SetVcpAsync(_vcpCode, value ? _onValue : _offValue, cancellationToken);
	}

	protected abstract class NonContinuousVcpFeature : IMonitorDeviceFeature, INonContinuousVcpFeature
	{
		private readonly GenericMonitorDriver _driver;
		private readonly byte _vcpCode;

		public ImmutableArray<NonContinuousValueDescription> AllowedValues { get; }
		private readonly HashSet<ushort>? _allowedValueSet;

		private protected NonContinuousVcpFeature(GenericMonitorDriver driver, byte vcpCode, ImmutableArray<NonContinuousValueDescription> allowedValues)
		{
			_driver = driver;
			_vcpCode = vcpCode;
			AllowedValues = allowedValues;

			HashSet<ushort>? allowedValueSet = null;
			if (!allowedValues.IsDefaultOrEmpty)
			{
				allowedValueSet = [];
				foreach (var description in allowedValues)
				{
					if (!allowedValueSet.Add(description.Value))
					{
						throw new InvalidOperationException("Duplicate value detected.");
					}
				}
			}
			_allowedValueSet = allowedValueSet;
		}

		public async ValueTask<ushort> GetValueAsync(CancellationToken cancellationToken)
			=> await _driver.GetNonContinuousVcpAsync(_vcpCode, cancellationToken).ConfigureAwait(false);

		public ValueTask SetValueAsync(ushort value, CancellationToken cancellationToken)
			=> _driver.SetVcpAsync(_allowedValueSet, _vcpCode, value, cancellationToken);
	}

	protected sealed class BrightnessFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorBrightnessFeature { }
	protected sealed class ContrastFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorContrastFeature { }
	protected sealed class SharpnessFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorSharpnessFeature { }
	protected sealed class BlueLightFilterLevelFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorBlueLightFilterLevelFeature { }

	protected sealed class SpeakerAudioVolumeFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorSpeakerAudioVolumeFeature { }

	protected sealed class InputSelectFeature(GenericMonitorDriver driver, byte vcpCode, ImmutableArray<NonContinuousValueDescription> allowedValues)
		: NonContinuousVcpFeature(driver, vcpCode, allowedValues), IMonitorInputSelectFeature
	{ }

	protected sealed class RedVideoGainFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorRedVideoGainFeature { }
	protected sealed class GreenVideoGainFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorGreenVideoGainFeature { }
	protected sealed class BlueVideoGainFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorBlueVideoGainFeature { }

	protected sealed class RedSixAxisSaturationControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorRedSixAxisSaturationControlFeature { }
	protected sealed class YellowSixAxisSaturationControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorYellowSixAxisSaturationControlFeature { }
	protected sealed class GreenSixAxisSaturationControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorGreenSixAxisSaturationControlFeature { }
	protected sealed class CyanSixAxisSaturationControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorCyanSixAxisSaturationControlFeature { }
	protected sealed class BlueSixAxisSaturationControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorBlueSixAxisSaturationControlFeature { }
	protected sealed class MagentaSixAxisSaturationControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorMagentaSixAxisSaturationControlFeature { }

	protected sealed class RedSixAxisHueControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorRedSixAxisHueControlFeature { }
	protected sealed class YellowSixAxisHueControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorYellowSixAxisHueControlFeature { }
	protected sealed class GreenSixAxisHueControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorGreenSixAxisHueControlFeature { }
	protected sealed class CyanSixAxisHueControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorCyanSixAxisHueControlFeature { }
	protected sealed class BlueSixAxisHueControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorBlueSixAxisHueControlFeature { }
	protected sealed class MagentaSixAxisHueControlFeature(GenericMonitorDriver driver, byte vcpCode) : ContinuousVcpFeature(driver, vcpCode), IMonitorMagentaSixAxisHueControlFeature { }

	protected sealed class InputLagFeature(GenericMonitorDriver driver, byte vcpCode, ImmutableArray<NonContinuousValueDescription> allowedValues)
		: NonContinuousVcpFeature(driver, vcpCode, allowedValues), IMonitorInputLagFeature
	{ }

	protected sealed class ResponseTimeFeature(GenericMonitorDriver driver, byte vcpCode, ImmutableArray<NonContinuousValueDescription> allowedValues)
		: NonContinuousVcpFeature(driver, vcpCode, allowedValues), IMonitorResponseTimeFeature
	{ }

	protected sealed class OsdLanguageFeature(GenericMonitorDriver driver, byte vcpCode, ImmutableArray<NonContinuousValueDescription> allowedValues)
		: NonContinuousVcpFeature(driver, vcpCode, allowedValues), IMonitorOsdLanguageFeature
	{ }

	protected sealed class PowerIndicatorToggleFeature(GenericMonitorDriver driver, byte vcpCode, ushort offValue, ushort onValue)
		: BooleanVcpFeature(driver, vcpCode, offValue, onValue), IMonitorPowerIndicatorToggleFeature
	{ }
}
