namespace Exo.Settings.Ui.Controls;

internal sealed class LinearScale
{
	private readonly double _inputMinimum;
	private readonly double _inputAmplitude;

	private readonly double _outputMinimum;
	private readonly double _outputAmplitude;

	private readonly double _inputMaximum;
	private readonly double _outputMaximum;

	public double InputMinimum => _inputMinimum;
	public double InputMaximum => _inputMaximum;

	public double OutputMinimum => _outputMinimum;
	public double OutputMaximum => _outputMaximum;

	public double InputAmplitude => _inputAmplitude;
	public double OutputAmplitude => _outputAmplitude;

	public LinearScale(double inputMinimum, double inputMaximum, double outputMinimum, double outputMaximum)
	{
		_inputMinimum = inputMinimum;
		_inputAmplitude = inputMaximum - inputMinimum;
		_outputMinimum = outputMinimum;
		_outputAmplitude = outputMaximum - outputMinimum;
		_inputMaximum = inputMaximum;
		_outputMaximum = outputMaximum;
	}

	public double this[double value] => _outputMinimum + (value - _inputMinimum) * _outputAmplitude / _inputAmplitude;

	public double Inverse(double value) => _inputMinimum + (value - _outputMinimum) * _inputAmplitude / _outputAmplitude;
}

