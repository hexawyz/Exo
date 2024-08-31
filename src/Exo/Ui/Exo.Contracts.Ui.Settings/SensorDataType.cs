using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public enum SensorDataType : byte
{
	[EnumMember]
	UInt8,
	[EnumMember]
	UInt16,
	[EnumMember]
	UInt32,
	[EnumMember]
	UInt64,
	[EnumMember]
	UInt128,
	[EnumMember]
	SInt8,
	[EnumMember]
	SInt16,
	[EnumMember]
	SInt32,
	[EnumMember]
	SInt64,
	[EnumMember]
	SInt128,
	[EnumMember]
	Float16,
	[EnumMember]
	Float32,
	[EnumMember]
	Float64,
}
