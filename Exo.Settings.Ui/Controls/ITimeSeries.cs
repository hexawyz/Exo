namespace Exo.Settings.Ui.Controls;

internal interface ITimeSeries
{
	DateTime StartTime { get; }
	TimeSpan Interval { get; }
	int Length { get; }
	double this[int index] { get; }

	event EventHandler Changed;
}
