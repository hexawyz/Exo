using System.ComponentModel;
using System.Windows.Media;
using Exo.Ui;

namespace StreamDeckPlayground;

public sealed class StreamDeckButtonViewModel : BindableObject
{
	private static readonly PropertyChangedEventArgs RedProperty = new(nameof(Red));
	private static readonly PropertyChangedEventArgs GreenProperty = new(nameof(Green));
	private static readonly PropertyChangedEventArgs BlueProperty = new(nameof(Blue));
	private static readonly PropertyChangedEventArgs ColorProperty = new(nameof(Color));
	private static readonly PropertyChangedEventArgs HtmlColorCodeProperty = new(nameof(HtmlColorCode));

	private readonly StreamDeckViewModel _streamDeckDevice;
	private readonly byte _index;
	private byte _red;
	private byte _green;
	private byte _blue;

	public StreamDeckButtonViewModel(StreamDeckViewModel streamDeckDevice, byte index)
	{
		_streamDeckDevice = streamDeckDevice;
		_index = index;
	}

	public int Index => _index;

	public int Width => _streamDeckDevice.ButtonImageWidth;
	public int Height => _streamDeckDevice.ButtonImageHeight;

	private void OnColorUpdated()
	{
		NotifyPropertyChanged(ColorProperty);
		NotifyPropertyChanged(HtmlColorCodeProperty);
		_streamDeckDevice.RequestColorUpdate(_index, _red, _green, _blue);
	}

	public byte Red
	{
		get => _red;
		set
		{
			if (SetValue(ref _red, value, RedProperty))
			{
				OnColorUpdated();
			}
		}
	}

	public byte Green
	{
		get => _green;
		set
		{
			if (SetValue(ref _green, value, GreenProperty))
			{
				OnColorUpdated();
			}
		}
	}

	public byte Blue
	{
		get => _blue;
		set
		{
			if (SetValue(ref _blue, value, BlueProperty))
			{
				OnColorUpdated();
			}
		}
	}

	public Color Color => Color.FromArgb(255, _red, _green, _blue);

	public string HtmlColorCode => $"#{_red:X2}{_green:X2}{_blue:X2}";
}
