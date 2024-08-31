using System.Diagnostics;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
[DebuggerDisplay("{Horizontal,d}x{Vertical,d}")]
public readonly struct DotsPerInch
{
	[DataMember(Order = 1)]
	public required ushort Horizontal { get; init; }

	[DataMember(Order = 2)]
	public required ushort Vertical { get; init; }
}
