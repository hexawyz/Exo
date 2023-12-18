using System.Collections.Immutable;

namespace Exo.Devices.Razer;

public readonly struct RazerMouseDpiProfileStatus
{
	public RazerMouseDpiProfileStatus(byte activeProfileIndex, ImmutableArray<RazerMouseDpiProfile> profiles)
	{
		// NB: We'll do some argument validation here, but we'll tolerate 0 as a valid active profile just in case. (e.g. what if DPI is set manually to a non-profile value ?)
		if (profiles.IsDefault) throw new ArgumentNullException(nameof(profiles));
		if (activeProfileIndex > profiles.Length) throw new ArgumentOutOfRangeException(nameof(activeProfileIndex));

		ActiveProfileIndex = activeProfileIndex;
		Profiles = profiles;
	}

	public byte ActiveProfileIndex { get; }
	public ImmutableArray<RazerMouseDpiProfile> Profiles { get;}
}
