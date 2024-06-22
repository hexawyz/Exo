using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo.Monitors;

public static class MonitorDefinitionSerializer
{
	private const byte MonitorDefinitionField_Name = 1;
	private const byte MonitorDefinitionField_Capabilities = 2;
	private const byte MonitorDefinitionField_OverriddenFeatures = 4;
	private const byte MonitorDefinitionField_IgnoredCapabilitiesVcpCodes = 8;
	private const byte MonitorDefinitionField_IgnoreAllCapabilitiesVcpCodes = 16;

	private const byte MonitorFeatureDefinitionField_NameStringId = 1;
	private const byte MonitorFeatureDefinitionField_DiscreteValues = 2;
	private const byte MonitorFeatureDefinitionField_MinimumValue = 4;
	private const byte MonitorFeatureDefinitionField_MaximumValue = 8;
	private const byte MonitorFeatureDefinitionField_AccessMask = 128 | 64;

	private const byte MonitorFeatureDiscreteValueDefinitionField_NameStringId = 1;

	public static byte[] Serialize(MonitorDefinition monitorDefinition)
	{
		byte fields = 0;

		if (monitorDefinition.Name is not null) fields |= MonitorDefinitionField_Name;
		if (monitorDefinition.Capabilities is not null) fields |= MonitorDefinitionField_Capabilities;
		if (!monitorDefinition.OverriddenFeatures.IsDefault) fields |= MonitorDefinitionField_OverriddenFeatures;
		if (!monitorDefinition.IgnoredCapabilitiesVcpCodes.IsDefault) fields |= MonitorDefinitionField_IgnoredCapabilitiesVcpCodes;
		if (monitorDefinition.IgnoreAllCapabilitiesVcpCodes) fields |= MonitorDefinitionField_IgnoreAllCapabilitiesVcpCodes;

		var buffer = new List<byte>();
		var writer = new BufferWriter(buffer);

		writer.WriteByte(fields);

		byte[]? nameBytes = null;
		byte[]? capabilitiesBytes = null;
		if (monitorDefinition.Name is not null) WriteVariableUInt32(writer, (uint)(nameBytes = Encoding.UTF8.GetBytes(monitorDefinition.Name)).Length);
		if (monitorDefinition.Capabilities is not null) WriteVariableUInt32(writer, (uint)(capabilitiesBytes = Encoding.UTF8.GetBytes(monitorDefinition.Capabilities)).Length);
		if (!monitorDefinition.OverriddenFeatures.IsDefault) WriteVariableUInt32(writer, (uint)monitorDefinition.OverriddenFeatures.Length);
		if (!monitorDefinition.IgnoredCapabilitiesVcpCodes.IsDefault) WriteVariableUInt32(writer, (uint)monitorDefinition.IgnoredCapabilitiesVcpCodes.Length);

		if (nameBytes is not null) writer.WriteBytes(nameBytes);
		if (capabilitiesBytes is not null) writer.WriteBytes(capabilitiesBytes);

		if (!monitorDefinition.OverriddenFeatures.IsDefault)
		{
			var array = ImmutableCollectionsMarshal.AsArray(monitorDefinition.OverriddenFeatures)!;
			for (int i = 0; i < array.Length; i++)
			{
				Serialize(writer, in array[i]);
			}
		}

		if (!monitorDefinition.IgnoredCapabilitiesVcpCodes.IsDefault)
		{
			var array = ImmutableCollectionsMarshal.AsArray(monitorDefinition.IgnoredCapabilitiesVcpCodes)!;
			for (int i = 0; i < array.Length; i++)
			{
				writer.WriteByte(array[i]);
			}
		}

		return [.. buffer];
	}

	private static void Serialize(BufferWriter writer, in MonitorFeatureDefinition monitorFeatureDefinition)
	{
		byte fields = 0;

		if (monitorFeatureDefinition.NameStringId is not null) fields |= MonitorFeatureDefinitionField_NameStringId;
		if (!monitorFeatureDefinition.DiscreteValues.IsDefault) fields |= MonitorFeatureDefinitionField_DiscreteValues;
		if (monitorFeatureDefinition.MinimumValue is not null) fields |= MonitorFeatureDefinitionField_MinimumValue;
		if (monitorFeatureDefinition.MaximumValue is not null) fields |= MonitorFeatureDefinitionField_MaximumValue;
		fields |= (byte)(((byte)monitorFeatureDefinition.Access & 0x3) << 6);

		writer.WriteByte(fields);

		if (monitorFeatureDefinition.NameStringId is not null) writer.WriteGuid(monitorFeatureDefinition.NameStringId.GetValueOrDefault());

		writer.WriteByte(monitorFeatureDefinition.VcpCode);
		writer.WriteByte((byte)monitorFeatureDefinition.Feature);

		if (!monitorFeatureDefinition.DiscreteValues.IsDefault) writer.WriteVariableUInt32((uint)monitorFeatureDefinition.DiscreteValues.Length);

		if (monitorFeatureDefinition.MinimumValue is not null) writer.WriteUInt16(monitorFeatureDefinition.MinimumValue.GetValueOrDefault());
		if (monitorFeatureDefinition.MaximumValue is not null) writer.WriteUInt16(monitorFeatureDefinition.MaximumValue.GetValueOrDefault());

		if (!monitorFeatureDefinition.DiscreteValues.IsDefault)
		{
			var array = ImmutableCollectionsMarshal.AsArray(monitorFeatureDefinition.DiscreteValues)!;
			for (int i = 0; i < array.Length; i++)
			{
				Serialize(writer, in array[i]);
			}
		}
	}

	private static void Serialize(BufferWriter writer, in MonitorFeatureDiscreteValueDefinition valueDefinition)
	{
		byte fields = 0;

		if (valueDefinition.NameStringId is not null) fields |= MonitorFeatureDiscreteValueDefinitionField_NameStringId;

		writer.WriteByte(fields);

		writer.WriteUInt16(valueDefinition.Value);

		if (valueDefinition.NameStringId is not null) writer.WriteGuid(valueDefinition.NameStringId.GetValueOrDefault());
	}

	private static void WriteVariableUInt32(BufferWriter writer, uint value)
	{
		uint v = value;

		while (true)
		{
			uint w = v >>> 7;
			v &= 0x7F;
			if (w != 0)
			{
				writer.WriteByte((byte)(0x80 | v));
				v = w;
			}
			else
			{
				writer.WriteByte((byte)v);
				break;
			}
		}
	}

	private static uint ReadVariableUInt32(ref BufferReader reader)
	{
		byte b = reader.ReadByte();
		if ((sbyte)b < 0)
		{
			uint v = (uint)b & 0x8F;
			uint shift = 7;
			while (true)
			{
				b = reader.ReadByte();
				if ((sbyte)b >= 0 || shift >= 28)
				{
					return v | (uint)b << (int)shift;
				}
				else
				{
					v = v | ((uint)b & 0x8F) << (int)shift;
					shift += 7;
				}
			}
		}
		else
		{
			return b;
		}
	}

	private readonly struct BufferWriter
	{
		private readonly List<byte> _buffer;

		public BufferWriter(List<byte> buffer) => _buffer = buffer;

		public void WriteByte(byte value) => _buffer.Add(value);
		public void WriteBytes(ReadOnlySpan<byte> value) => _buffer.AddRange(value);

		public void WriteUInt16(ushort value) => WriteRaw(AdjustEndianness(value));
		public void WriteUInt32(uint value) => WriteRaw(AdjustEndianness(value));
		public void WriteUInt64(ulong value) => WriteRaw(AdjustEndianness(value));

		public void WriteVariableUInt32(uint value) => MonitorDefinitionSerializer.WriteVariableUInt32(this, value);

		public void WriteGuid(Guid value)
		{
			int index = _buffer.Count;
			CollectionsMarshal.SetCount(_buffer, index + 16);
			value.TryWriteBytes(CollectionsMarshal.AsSpan(_buffer)[index..]);
		}

		private void WriteRaw<T>(T value) where T : struct => _buffer.AddRange(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>()));

		private ushort AdjustEndianness(ushort value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
		private uint AdjustEndianness(uint value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
		private ulong AdjustEndianness(ulong value) => BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
	}

	private ref struct BufferReader
	{
		private ref readonly byte _current;
		private readonly ref readonly byte _end;

		public BufferReader(ReadOnlySpan<byte> buffer)
		{
			_current = ref MemoryMarshal.GetReference(buffer);
			_end = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(buffer), buffer.Length);
		}

		public readonly nuint Length => (nuint)Unsafe.ByteOffset(in _current, in _end);

		public byte ReadByte()
		{
			if (!Unsafe.IsAddressLessThan(in _current, in _end)) throw new EndOfStreamException();

			byte value = _current;
			_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 1);
			return value;
		}

		public ushort ReadUInt16()
		{
			if (Length < 2) throw new EndOfStreamException();

			ushort value = LittleEndian.ReadUInt16(in _current);
			_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 2);
			return value;
		}

		public uint ReadUInt32()
		{
			if (Length < 4) throw new EndOfStreamException();

			uint value = LittleEndian.ReadUInt32(in _current);
			_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 4);
			return value;
		}

		public uint ReadVariableUInt32() => MonitorDefinitionSerializer.ReadVariableUInt32(ref this);

		public string ReadString(uint length)
		{
			if (length > Length) throw new EndOfStreamException();

			string value = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpan(in _current, (int)length));
			_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), (nint)(nuint)length);
			return value;
		}

		public Guid ReadGuid()
		{
			if (Length < 16) throw new EndOfStreamException();

			var value = new Guid(MemoryMarshal.CreateReadOnlySpan(in _current, 16));
			_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 16);
			return value;
		}
	}

	public static MonitorDefinition Deserialize(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);
		return Deserialize(ref reader);
	}

	private static T[] CreateArray<T>(uint length) => length == 0 ? [] : new T[length];

	private static MonitorDefinition Deserialize(ref BufferReader reader)
	{
		byte fields = reader.ReadByte();

		uint nameLength = 0;
		uint capabilitiesLength = 0;
		MonitorFeatureDefinition[]? overriddenFeatures = null;
		byte[]? ignoredCapabilitiesVcpCodes = null;

		if ((fields & MonitorDefinitionField_Name) != 0) nameLength = reader.ReadVariableUInt32();
		if ((fields & MonitorDefinitionField_Capabilities) != 0) capabilitiesLength = reader.ReadVariableUInt32();
		if ((fields & MonitorDefinitionField_OverriddenFeatures) != 0) overriddenFeatures = CreateArray<MonitorFeatureDefinition>(reader.ReadVariableUInt32());
		if ((fields & MonitorDefinitionField_IgnoredCapabilitiesVcpCodes) != 0) ignoredCapabilitiesVcpCodes = CreateArray<byte>(reader.ReadVariableUInt32());

		string? name = (fields & MonitorDefinitionField_Name) != 0 ? reader.ReadString(nameLength) : null;
		string? capabilities = (fields & MonitorDefinitionField_Capabilities) != 0 ? reader.ReadString(capabilitiesLength) : null;

		if (overriddenFeatures is not null)
		{
			for (int i = 0; i < overriddenFeatures.Length; i++)
			{
				overriddenFeatures[i] = DeserializeMonitorFeatureDefinition(ref reader);
			}
		}

		if (ignoredCapabilitiesVcpCodes is not null)
		{
			for (int i = 0; i < ignoredCapabilitiesVcpCodes.Length; i++)
			{
				ignoredCapabilitiesVcpCodes[i] = reader.ReadByte();
			}
		}

		return new()
		{
			Name = name,
			Capabilities = capabilities,
			OverriddenFeatures = ImmutableCollectionsMarshal.AsImmutableArray(overriddenFeatures),
			IgnoredCapabilitiesVcpCodes = ImmutableCollectionsMarshal.AsImmutableArray(ignoredCapabilitiesVcpCodes),
			IgnoreAllCapabilitiesVcpCodes = (fields & MonitorDefinitionField_IgnoreAllCapabilitiesVcpCodes) != 0,
		};
	}

	private static MonitorFeatureDefinition DeserializeMonitorFeatureDefinition(ref BufferReader reader)
	{
		byte fields = reader.ReadByte();

		Guid? nameStringId = (fields & MonitorFeatureDefinitionField_NameStringId) != 0 ? reader.ReadGuid() : null;
		byte vcpCode = reader.ReadByte();
		var access = (MonitorFeatureAccess)((fields & MonitorFeatureDefinitionField_AccessMask) >>> 6);
		var feature = (MonitorFeature)reader.ReadByte();

		MonitorFeatureDiscreteValueDefinition[]? discreteValues = null;
		if ((fields & MonitorFeatureDefinitionField_DiscreteValues) != 0) discreteValues = CreateArray<MonitorFeatureDiscreteValueDefinition>(reader.ReadVariableUInt32());

		ushort? minimumValue = (fields & MonitorFeatureDefinitionField_MinimumValue) != 0 ? reader.ReadUInt16() : null;
		ushort? maximumValue = (fields & MonitorFeatureDefinitionField_MaximumValue) != 0 ? reader.ReadUInt16() : null;

		if (discreteValues is not null)
		{
			for (int i = 0; i < discreteValues.Length; i++)
			{
				discreteValues[i] = DeserializeMonitorFeatureDiscreteValueDefinition(ref reader);
			}
		}

		return new()
		{
			NameStringId = nameStringId,
			VcpCode = vcpCode,
			Access = access,
			Feature = feature,
			DiscreteValues = ImmutableCollectionsMarshal.AsImmutableArray(discreteValues),
			MinimumValue = minimumValue,
			MaximumValue = maximumValue,
		};
	}

	private static MonitorFeatureDiscreteValueDefinition DeserializeMonitorFeatureDiscreteValueDefinition(ref BufferReader reader)
	{
		byte fields = reader.ReadByte();

		ushort value = reader.ReadUInt16();

		Guid? nameStringId = (fields & MonitorFeatureDiscreteValueDefinitionField_NameStringId) != 0 ? reader.ReadGuid() : null;

		return new()
		{
			Value = value,
			NameStringId = nameStringId,
		};
	}
}
