using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.Contracts;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class PropertyViewModel : ChangeableBindableObject
{
	protected static object? GetValue(DataType type, DataValue value)
		=> !value.IsDefault ?
			type switch
			{
				DataType.UInt8 => (byte?)value.UnsignedValue,
				DataType.Int8 => (sbyte?)value.SignedValue,
				DataType.UInt16 => (ushort?)value.UnsignedValue,
				DataType.Int16 => (short?)value.SignedValue,
				DataType.UInt32 => (uint?)value.UnsignedValue,
				DataType.Int32 => (int?)value.SignedValue,
				DataType.UInt64 => value.UnsignedValue,
				DataType.Int64 => value.SignedValue,
				DataType.Float16 => value.SingleValue,
				DataType.Float32 => value.SingleValue,
				DataType.Float64 => value.DoubleValue,
				DataType.Boolean => value.UnsignedValue is ulong uv ? (byte)uv != 0 : null,
				DataType.ColorGrayscale8 => (byte?)value.UnsignedValue,
				DataType.ColorGrayscale16 => (ushort?)value.UnsignedValue,
				DataType.ColorRgb24 => value.UnsignedValue is ulong uv ? Color.FromArgb(255, (byte)(uv >> 16), (byte)(uv >> 8), (byte)uv) : null,
				DataType.ColorArgb32 => value.UnsignedValue is ulong uv ? Color.FromArgb((byte)(uv >> 24), (byte)(uv >> 16), (byte)(uv >> 8), (byte)uv) : null,
				DataType.Guid => value.GuidValue,
				DataType.String => value.StringValue,
				DataType.TimeSpan => throw new NotImplementedException(),
				DataType.DateTime => throw new NotImplementedException(),
				_ => throw new NotSupportedException()
			} :
			null;

	protected static (object?, int) ReadValue(DataType type, ReadOnlySpan<byte> data)
		=> type switch
		{
			DataType.UInt8 => (data[0], 1),
			DataType.Int8 => ((sbyte)data[0], 1),
			DataType.UInt16 => (MemoryMarshal.Read<ushort>(data), 2),
			DataType.Int16 => (MemoryMarshal.Read<short>(data), 2),
			DataType.UInt32 => (MemoryMarshal.Read<uint>(data), 4),
			DataType.Int32 => (MemoryMarshal.Read<int>(data), 4),
			DataType.UInt64 => (MemoryMarshal.Read<ulong>(data), 8),
			DataType.Int64 => (MemoryMarshal.Read<long>(data), 8),
			DataType.Float16 => (MemoryMarshal.Read<Half>(data), 2),
			DataType.Float32 => (MemoryMarshal.Read<float>(data), 4),
			DataType.Float64 => (MemoryMarshal.Read<double>(data), 4),
			DataType.Boolean => (data[0] != 0, 1),
			DataType.ColorGrayscale8 => (data[0], 1),
			DataType.ColorGrayscale16 => (MemoryMarshal.Read<ushort>(data), 2),
			DataType.ColorRgb24 => (Color.FromArgb(255, data[0], data[1], data[2]), 3),
			DataType.ColorArgb32 => (Color.FromArgb(data[0], data[1], data[2], data[3]), 4),
			DataType.Guid => (new Guid(data[..16]), 16),
			DataType.String => throw new NotImplementedException("TODO"),
			DataType.TimeSpan => throw new NotImplementedException("TODO"),
			DataType.DateTime => throw new NotImplementedException("TODO"),
			_ => throw new NotSupportedException()
		};

	protected static void WriteValue(DataType type, object value, BinaryWriter writer)
	{
		switch (type)
		{
		case DataType.UInt8: writer.Write(Convert.ToByte(value)); break;
		case DataType.Int8: writer.Write(Convert.ToSByte(value)); break;
		case DataType.UInt16: writer.Write(Convert.ToUInt16(value)); break;
		case DataType.Int16: writer.Write(Convert.ToInt16(value)); break;
		case DataType.UInt32: writer.Write(Convert.ToUInt32(value)); break;
		case DataType.Int32: writer.Write(Convert.ToInt32(value)); break;
		case DataType.UInt64: writer.Write(Convert.ToUInt64(value)); break;
		case DataType.Int64: writer.Write(Convert.ToInt64(value)); break;
		case DataType.Float16: writer.Write(value is Half h ? h : (Half)Convert.ToSingle(value)); break;
		case DataType.Float32: writer.Write(Convert.ToSingle(value)); break;
		case DataType.Float64: writer.Write(Convert.ToDouble(value)); break;
		case DataType.Boolean: writer.Write(Convert.ToBoolean(value) ? (byte)1 : (byte)0); break;
		case DataType.ColorGrayscale8: writer.Write(Convert.ToByte(value)); break;
		case DataType.ColorGrayscale16: writer.Write(Convert.ToUInt16(value)); break;
		case DataType.ColorRgb24: var rgbColor = (Color)value; writer.Write(rgbColor.R); writer.Write(rgbColor.G); writer.Write(rgbColor.B); break;
		case DataType.ColorArgb32: var argbColor = (Color)value; writer.Write(argbColor.A); writer.Write(argbColor.R); writer.Write(argbColor.G); writer.Write(argbColor.B); break;
		case DataType.Guid: writer.Write(((Guid)value).ToByteArray()); break;
		case DataType.String: writer.Write((string)value); break;
		case DataType.TimeSpan: throw new NotImplementedException("TODO"); break;
		case DataType.DateTime:
			throw new NotImplementedException("TODO");
		}
	}

	private static object? GetValue(DataType type, DataValue value, int index)
		=> !value.IsDefault ?
			type switch
			{
				DataType.ArrayOfColorRgb24 => value.BytesValue is { } bytes && 3 * index is int offset ? Color.FromArgb(255, bytes[offset], bytes[offset + 2], bytes[offset + 2]) : null,
				DataType.ArrayOfColorArgb32 => value.BytesValue is { } bytes && 4 * index is int offset ? Color.FromArgb(bytes[offset], bytes[offset + 2], bytes[offset + 2], bytes[offset + 3]) : null,
				_ => throw new NotSupportedException()
			} :
			null;

	protected static object? GetDefaultValueForType(DataType type)
		=> type switch
		{
			DataType.UInt8 => (byte?)0,
			DataType.Int8 => (sbyte?)0,
			DataType.UInt16 => (ushort?)0,
			DataType.Int16 => (short?)0,
			DataType.UInt32 => (uint?)0,
			DataType.Int32 => (int?)0,
			DataType.UInt64 => 0UL,
			DataType.Int64 => 0L,
			DataType.Float16 => 0f,
			DataType.Float32 => 0f,
			DataType.Float64 => 0d,
			DataType.Boolean => false,
			DataType.ColorGrayscale8 => (byte?)0,
			DataType.ColorGrayscale16 => (ushort?)0,
			DataType.ColorRgb24 or DataType.ColorArgb32 => Color.FromArgb(255, 255, 255, 255),
			DataType.Guid => Guid.Empty,
			DataType.String => null,
			DataType.TimeSpan => TimeSpan.Zero,
			DataType.DateTime => DateTime.UtcNow,
			_ => throw new NotSupportedException()
		};

	protected readonly ConfigurablePropertyInformation PropertyInformation;
	private readonly Commands.ResetCommand _resetCommand;
	private readonly int _paddingLength;

	public DataType DataType => PropertyInformation.DataType;

	public string Name => PropertyInformation.Name;

	public string DisplayName => PropertyInformation.DisplayName;

	public ICommand ResetCommand => _resetCommand;

	// Represents the amount of padding between this property and the following one.
	// This is used for reading and writing the raw data.
	public int PaddingLength => _paddingLength;

	public PropertyViewModel(ConfigurablePropertyInformation propertyInformation, int paddingLength)
	{
		PropertyInformation = propertyInformation;
		_resetCommand = new(this);
		_paddingLength = paddingLength;
	}

	protected abstract void Reset();
	public abstract int ReadInitialValue(ReadOnlySpan<byte> data);
	public abstract void WriteValue(BinaryWriter writer);

	protected sealed override void OnChanged(bool isChanged)
	{
		_resetCommand.OnChanged();
		base.OnChanged(isChanged);
	}

	private static class Commands
	{
		public sealed class ResetCommand : ICommand
		{
			private readonly PropertyViewModel _property;

			public ResetCommand(PropertyViewModel property) => _property = property;

			public bool CanExecute(object? parameter) => _property.IsChanged;
			public void Execute(object? parameter) => _property.Reset();

			public event EventHandler? CanExecuteChanged;

			internal void OnChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
