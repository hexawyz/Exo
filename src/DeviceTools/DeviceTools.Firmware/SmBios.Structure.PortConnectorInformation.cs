namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class PortConnectorInformation : Structure
		{
			public override byte Type => 8;

			public string? InternalReferenceDesignator { get; }
			public PortConnectorType InternalConnectorType { get; }
			public string? ExternalReferenceDesignator { get; }
			public PortConnectorType ExternalConnectorType { get; }
			public PortType PortType { get; }

			internal PortConnectorInformation(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
				// SMBIOS 2.0+
				if (data.Length < 5) throw new InvalidDataException("The data structure for Port Connector Information is not long enough.");

				InternalReferenceDesignator = GetString(strings, data[0]);
				InternalConnectorType = (PortConnectorType)data[1];
				ExternalReferenceDesignator = GetString(strings, data[2]);
				ExternalConnectorType = (PortConnectorType)data[3];
				PortType = (PortType)data[4];
			}
		}
	}
}
