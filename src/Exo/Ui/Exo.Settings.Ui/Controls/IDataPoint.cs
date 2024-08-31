using System.Numerics;

namespace Exo.Settings.Ui.Controls;

internal interface IDataPoint<TX, TY>
	where TX : INumber<TX>
	where TY : INumber<TY>
{
	TX X { get; set; }
	TY Y { get; set; }
}
