// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using ABI.Microsoft.UI.Composition;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WinRT;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130
namespace ABI.Microsoft.UI.Content;
#pragma warning restore IDE0130

internal static class IContentExternalBackdropLinkStaticsMethods
{
	public static Guid IID => new(0x46cac6fb, 0xbb51, 0x510a, 0x95, 0x8D, 0xE0, 0xEB, 0x41, 0x60, 0xF6, 0x78);

	internal static unsafe global::Microsoft.UI.Content.ContentExternalBackdropLink Create(IObjectReference obj, global::Microsoft.UI.Composition.Compositor compositor)
	{
		nint thisPtr = obj.ThisPtr;

		ObjectReferenceValue compositorAbi = default;
		nint retval = default;
		try
		{
			compositorAbi = Compositor.CreateMarshaler2(compositor);
			ExceptionHelpers.ThrowExceptionForHR((*(delegate* unmanaged[Stdcall]<nint, nint, nint*, int>**)thisPtr)[6](thisPtr, MarshalInspectable<object>.GetAbi(compositorAbi), &retval));
			GC.KeepAlive(obj);
			return ContentExternalBackdropLink.FromAbi(retval);
		}
		finally
		{
			MarshalInspectable<object>.DisposeMarshaler(compositorAbi);
			ContentExternalBackdropLink.DisposeAbi(retval);
		}
	}
}
