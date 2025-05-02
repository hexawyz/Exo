using System.Runtime.InteropServices;
using System.Windows.Input;
using Exo.Contracts;
using Exo.Lighting;
using Windows.UI;

namespace Exo.Settings.Ui.ViewModels;

internal abstract class PropertyViewModel : ChangeableBindableObject
{
	protected static (object?, int) ReadValue(LightingDataType type, ReadOnlySpan<byte> data)
		=> type switch
		{
			LightingDataType.UInt8 => (data[0], 1),
			LightingDataType.SInt8 => ((sbyte)data[0], 1),
			LightingDataType.UInt16 => (MemoryMarshal.Read<ushort>(data), 2),
			LightingDataType.SInt16 => (MemoryMarshal.Read<short>(data), 2),
			LightingDataType.UInt32 => (MemoryMarshal.Read<uint>(data), 4),
			LightingDataType.SInt32 => (MemoryMarshal.Read<int>(data), 4),
			LightingDataType.UInt64 => (MemoryMarshal.Read<ulong>(data), 8),
			LightingDataType.SInt64 => (MemoryMarshal.Read<long>(data), 8),
			LightingDataType.Float16 => (MemoryMarshal.Read<Half>(data), 2),
			LightingDataType.Float32 => (MemoryMarshal.Read<float>(data), 4),
			LightingDataType.Float64 => (MemoryMarshal.Read<double>(data), 4),
			LightingDataType.Boolean => (data[0] != 0, 1),
			LightingDataType.ColorGrayscale8 => (data[0], 1),
			LightingDataType.ColorGrayscale16 => (MemoryMarshal.Read<ushort>(data), 2),
			LightingDataType.ColorRgb24 => (Color.FromArgb(255, data[0], data[1], data[2]), 3),
			LightingDataType.ColorArgb32 => (Color.FromArgb(data[0], data[1], data[2], data[3]), 4),
			LightingDataType.Guid => (new Guid(data[..16]), 16),
			LightingDataType.String => throw new NotImplementedException("TODO"),
			LightingDataType.TimeSpan => throw new NotImplementedException("TODO"),
			LightingDataType.DateTime => throw new NotImplementedException("TODO"),
			_ => throw new NotSupportedException()
		};

	protected static void WriteValue(LightingDataType type, object value, BinaryWriter writer)
	{
		switch (type)
		{
		case LightingDataType.UInt8: writer.Write(Convert.ToByte(value)); break;
		case LightingDataType.SInt8: writer.Write(Convert.ToSByte(value)); break;
		case LightingDataType.UInt16: writer.Write(Convert.ToUInt16(value)); break;
		case LightingDataType.SInt16: writer.Write(Convert.ToInt16(value)); break;
		case LightingDataType.UInt32: writer.Write(Convert.ToUInt32(value)); break;
		case LightingDataType.SInt32: writer.Write(Convert.ToInt32(value)); break;
		case LightingDataType.UInt64: writer.Write(Convert.ToUInt64(value)); break;
		case LightingDataType.SInt64: writer.Write(Convert.ToInt64(value)); break;
		case LightingDataType.Float16: writer.Write(value is Half h ? h : (Half)Convert.ToSingle(value)); break;
		case LightingDataType.Float32: writer.Write(Convert.ToSingle(value)); break;
		case LightingDataType.Float64: writer.Write(Convert.ToDouble(value)); break;
		case LightingDataType.Boolean: writer.Write(Convert.ToBoolean(value) ? (byte)1 : (byte)0); break;
		case LightingDataType.ColorGrayscale8: writer.Write(Convert.ToByte(value)); break;
		case LightingDataType.ColorGrayscale16: writer.Write(Convert.ToUInt16(value)); break;
		case LightingDataType.ColorRgb24: var rgbColor = (Color)value; writer.Write(rgbColor.R); writer.Write(rgbColor.G); writer.Write(rgbColor.B); break;
		case LightingDataType.ColorArgb32: var argbColor = (Color)value; writer.Write(argbColor.A); writer.Write(argbColor.R); writer.Write(argbColor.G); writer.Write(argbColor.B); break;
		case LightingDataType.Guid: writer.Write(((Guid)value).ToByteArray()); break;
		case LightingDataType.String: writer.Write((string)value); break;
		case LightingDataType.TimeSpan: throw new NotImplementedException("TODO");
		case LightingDataType.DateTime:
			throw new NotImplementedException("TODO");
		}
	}

	protected static object? GetDefaultValueForType(LightingDataType type)
		=> type switch
		{
			LightingDataType.UInt8 => (byte?)0,
			LightingDataType.SInt8 => (sbyte?)0,
			LightingDataType.UInt16 => (ushort?)0,
			LightingDataType.SInt16 => (short?)0,
			LightingDataType.UInt32 => (uint?)0,
			LightingDataType.SInt32 => (int?)0,
			LightingDataType.UInt64 => 0UL,
			LightingDataType.SInt64 => 0L,
			LightingDataType.Float16 => 0f,
			LightingDataType.Float32 => 0f,
			LightingDataType.Float64 => 0d,
			LightingDataType.Boolean => false,
			LightingDataType.ColorGrayscale8 => (byte?)0,
			LightingDataType.ColorGrayscale16 => (ushort?)0,
			LightingDataType.ColorRgb24 or LightingDataType.ColorArgb32 => Color.FromArgb(255, 255, 255, 255),
			LightingDataType.Guid => Guid.Empty,
			LightingDataType.String => null,
			LightingDataType.TimeSpan => TimeSpan.Zero,
			LightingDataType.DateTime => DateTime.UtcNow,
			_ => throw new NotSupportedException()
		};

	protected readonly ConfigurablePropertyInformation PropertyInformation;
	private readonly Commands.ResetCommand _resetCommand;
	private readonly int _paddingLength;

	public LightingDataType DataType => PropertyInformation.DataType;

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
