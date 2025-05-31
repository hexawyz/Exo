using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using WinRT;

namespace Exo.Settings.Ui.Extensions;

internal static class WinRtObjectExtensions
{
	public static bool TryAs<TInterface>(this IWinRTObject? value, [NotNullWhen(true)] out TInterface? result)
		where TInterface : class
	{
		if (value is null) goto NotSuccessful;

		if (value is TInterface i)
		{
			result = i;
			return true;
		}

		nint abi = 0;
		try
		{
			int qir = value.NativeObject.TryAs(GuidGenerator.GetGUID(typeof(TInterface)), out abi);
			if (qir >= 0)
			{
				result = MarshalInspectable<TInterface>.FromAbi(abi);
				return true;
			}
			else if ((uint)qir == 0x80004002U)
			{
				goto NotSuccessful;
			}
			else
			{
				Marshal.ThrowExceptionForHR(qir);
			}
		}
		finally
		{
			if (abi != 0)
			{
				MarshalInspectable<object>.DisposeAbi(abi);
			}
		}

	NotSuccessful:;
		result = null;
		return false;
	}
}
