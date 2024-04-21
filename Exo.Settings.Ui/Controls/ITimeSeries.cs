namespace Exo.Settings.Ui.Controls;

internal interface ITimeSeries
{
	DateTime StartTime { get; }
	TimeSpan Interval { get; }
	int Length { get; }
	double this[int index] { get; }

	double? MaximumReachedValue { get; }
	double? MinimumReachedValue { get; }

	event EventHandler Changed;
}
