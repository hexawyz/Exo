namespace Exo.Core
{
	public abstract class Driver
	{
		/// <summary>Gets a friendly name for this driver instance.</summary>
		/// <remarks>
		/// <para>
		/// This property allows providing a name that is more explicit than the generic friendly name contained in the <see cref="DisplayName"/> attribute.
		/// Generally, drivers supporting more than one device (e.g. monitor drivers) would expose the friendly name of the device here.
		/// </para>
		/// </remarks>
		public abstract string FriendlyName { get; }

		private protected Driver() { }
	}
}
