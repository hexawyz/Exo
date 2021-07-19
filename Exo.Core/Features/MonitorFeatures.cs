using System;
using System.Collections.Immutable;
using DeviceTools.DisplayDevices.Mccs;

namespace Exo.Core.Features.MonitorFeatures
{
	public interface IMonitorDeviceFeature : IDeviceFeature
	{
	}

	public interface IMonitorCapabilitiesFeature : IMonitorDeviceFeature
	{
	}

	public interface IRawVcpFeature : IMonitorDeviceFeature
	{
		void SetVcpFeature(byte vcpCode, uint value);
		VcpFeatureReply GetVcpFeature(byte vcpCode);
	}

	public readonly struct ContinuousValue : IEquatable<ContinuousValue>
	{
		public ContinuousValue(uint minimum, uint current, uint maximum)
		{
			Minimum = minimum;
			Current = current;
			Maximum = maximum;
		}

		public uint Minimum { get; }
		public uint Current { get; }
		public uint Maximum { get; }

		public override bool Equals(object? obj) => obj is ContinuousValue value && Equals(value);
		public bool Equals(ContinuousValue other) => Minimum == other.Minimum && Current == other.Current && Maximum == other.Maximum;
		public override int GetHashCode() => HashCode.Combine(Minimum, Current, Maximum);

		public static bool operator ==(ContinuousValue left, ContinuousValue right) => left.Equals(right);
		public static bool operator !=(ContinuousValue left, ContinuousValue right) => !(left == right);
	}

	public interface IBrightnessFeature : IMonitorDeviceFeature
	{
		ContinuousValue GetBrightness();
		void SetBrightness(uint value);
	}

	public interface IContrastFeature : IMonitorDeviceFeature
	{
		ContinuousValue GetContrast();
		void SetContrast(uint value);
	}

	public readonly struct InputSourceDescription
	{
		public byte Value { get; }
		public string Name { get; }
	}

	public interface IInputSelectFeature : IMonitorDeviceFeature
	{
		ImmutableArray<InputSourceDescription> InputSources { get; }
		byte GetCurrentSourceId();
		void SurCurrentSourceId(byte sourceId);
	}
}
