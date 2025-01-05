using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings.Cooling;

[DataContract]
public sealed class CoolingParameters
{
	private readonly object? _value;

	[IgnoreDataMember]
	public object? RawValue => _value;

	/// <summary>Represents the live power of a cooler when a control curve is applied.</summary>
	/// <remarks>This should not be interpreted as a cooling mode, unlike other parameters.</remarks>
	[DataMember(Order = 1)]
	public LiveCoolingParameters? Live
	{
		get => _value as LiveCoolingParameters;
		init => _value = value;
	}

	[DataMember(Order = 2)]
	public AutomaticCoolingParameters? Automatic
	{
		get => _value as AutomaticCoolingParameters;
		init => _value = value;
	}

	[DataMember(Order = 3)]
	public FixedCoolingParameters? Fixed
	{
		get => _value as FixedCoolingParameters;
		init => _value = value;
	}

	[DataMember(Order = 4)]
	public SoftwareCurveCoolingParameters? SoftwareControlCurve
	{
		get => _value as SoftwareCurveCoolingParameters;
		init => _value = value;
	}

	[DataMember(Order = 5)]
	public HardwareCurveCoolingParameters? HardwareControlCurve
	{
		get => _value as HardwareCurveCoolingParameters;
		init => _value = value;
	}
}
