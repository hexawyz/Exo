using System;

namespace Exo.Core
{
	/// <summary>An interface exposed by driver implementations whose feature set can change dynamically.</summary>
	/// <remarks></remarks>
	public interface INotifyFeaturesChanged
	{
		event EventHandler FeaturesChanged;
	}
}
