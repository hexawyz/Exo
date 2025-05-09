// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using ABI.Microsoft.UI.Content;
using Microsoft.Foundation;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Windows.Foundation.Metadata;
using WinRT;
using WinRT.Interop;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130
namespace Microsoft.UI.Content;
#pragma warning restore IDE0130

[WindowsRuntimeType("Microsoft.UI")]
[WindowsRuntimeHelperType(typeof(global::ABI.Microsoft.UI.Content.ContentExternalBackdropLink))]
[ContentExternalBackdropLinkRcwFactory]
[ProjectedRuntimeClass(typeof(IContentExternalBackdropLink))]
[ContractVersion(typeof(WindowsAppSDKContract), 65543u)]
internal sealed class ContentExternalBackdropLink : IDisposable, ICompositionSupportsSystemBackdrop, ICustomQueryInterface, IWinRTObject, IEquatable<ContentExternalBackdropLink>
{
	private static volatile IObjectReference? _objRefMicrosoftUIContentIContentExternalBackdropLinkStatics;

#pragma warning disable CA2213
	private readonly IObjectReference _inner;
#pragma warning restore CA2213

	private volatile IObjectReference? _objRefSystemIDisposable;
	private volatile IObjectReference? _objRefMicrosoftUICompositionICompositionSupportsSystemBackdrop;
	private volatile ConcurrentDictionary<RuntimeTypeHandle, IObjectReference>? _queryInterfaceCache;
	private volatile ConcurrentDictionary<RuntimeTypeHandle, object>? _additionalTypeData;

	internal ContentExternalBackdropLink(IObjectReference objRef)
	{
		_inner = objRef.As(IContentExternalBackdropLinkMethods.IID);
	}

	public DispatcherQueue DispatcherQueue
	{
		get => IContentExternalBackdropLinkMethods.get_DispatcherQueue(ObjRefMicrosoftUIContentIContentExternalBackdropLink);
	}

	public CompositionBorderMode ExternalBackdropBorderMode
	{
		get => IContentExternalBackdropLinkMethods.get_ExternalBackdropBorderMode(ObjRefMicrosoftUIContentIContentExternalBackdropLink);
		set => IContentExternalBackdropLinkMethods.set_ExternalBackdropBorderMode(ObjRefMicrosoftUIContentIContentExternalBackdropLink, value);
	}

	public Visual PlacementVisual
	{
		get => IContentExternalBackdropLinkMethods.get_PlacementVisual(ObjRefMicrosoftUIContentIContentExternalBackdropLink);
	}

	public global::Windows.UI.Composition.CompositionBrush SystemBackdrop
	{
		get => ABI.Microsoft.UI.Composition.ICompositionSupportsSystemBackdropMethods.get_SystemBackdrop(ObjRefMicrosoftUICompositionICompositionSupportsSystemBackdrop);
		set => ABI.Microsoft.UI.Composition.ICompositionSupportsSystemBackdropMethods.set_SystemBackdrop(ObjRefMicrosoftUICompositionICompositionSupportsSystemBackdrop, value);
	}

	bool IWinRTObject.HasUnwrappableNativeObject { get => true; }

	IObjectReference IWinRTObject.NativeObject { get => _inner; }

	ConcurrentDictionary<RuntimeTypeHandle, IObjectReference> IWinRTObject.QueryInterfaceCache { get => _queryInterfaceCache ?? MakeQueryInterfaceCache(); }

	ConcurrentDictionary<RuntimeTypeHandle, object> IWinRTObject.AdditionalTypeData { get => _additionalTypeData ?? MakeAdditionalTypeData(); }

	private static IObjectReference ObjRefMicrosoftUIContentIContentExternalBackdropLinkStatics
	{
		get
		{
			IObjectReference? factory = _objRefMicrosoftUIContentIContentExternalBackdropLinkStatics;
			return factory is not null && factory.IsInCurrentContext
				? factory
				: _objRefMicrosoftUIContentIContentExternalBackdropLinkStatics = ActivationFactory.Get("Microsoft.UI.Content.ContentExternalBackdropLink", IContentExternalBackdropLinkStaticsMethods.IID);
		}
	}

	// ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
	private nint ThisPtr { get => _inner?.ThisPtr ?? ((IWinRTObject)this).NativeObject.ThisPtr; }

	// ReSharper disable once ConvertToAutoPropertyWhenPossible
	private IObjectReference ObjRefMicrosoftUIContentIContentExternalBackdropLink { get => _inner; }

	private IObjectReference ObjRefSystemIDisposable => _objRefSystemIDisposable ?? MakeObjRefSystemIDisposable();

	private IObjectReference ObjRefMicrosoftUICompositionICompositionSupportsSystemBackdrop => _objRefMicrosoftUICompositionICompositionSupportsSystemBackdrop ?? MakeObjRefMicrosoftUICompositionICompositionSupportsSystemBackdrop();

	public static bool operator ==(ContentExternalBackdropLink? x, ContentExternalBackdropLink? y)
	{
		return (x?.ThisPtr ?? 0) == (y?.ThisPtr ?? 0);
	}

	public static bool operator !=(ContentExternalBackdropLink x, ContentExternalBackdropLink y)
	{
		return !(x == y);
	}

	public static TInterface As<TInterface>()
	{
		return ActivationFactory.Get("Microsoft.UI.Content.ContentExternalBackdropLink").AsInterface<TInterface>();
	}

	public static ContentExternalBackdropLink Create(Compositor compositor)
	{
		return IContentExternalBackdropLinkStaticsMethods.Create(ObjRefMicrosoftUIContentIContentExternalBackdropLinkStatics, compositor);
	}

	public static ContentExternalBackdropLink FromAbi(nint thisPtr)
	{
		return thisPtr is 0 ? default! : MarshalInspectable<ContentExternalBackdropLink>.FromAbi(thisPtr);
	}

	public bool Equals(ContentExternalBackdropLink? other)
	{
		return this == other;
	}

	public override bool Equals(object? obj)
	{
		return obj is ContentExternalBackdropLink that && this == that;
	}

	public override int GetHashCode()
	{
		return ThisPtr.GetHashCode();
	}

	public void Dispose() => ABI.System.IDisposableMethods.Dispose(ObjRefSystemIDisposable);

	CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid, out nint ppv)
	{
		ppv = 0;
		if (IsOverridableInterface(iid) || IID.IID_IInspectable == iid)
		{
			return CustomQueryInterfaceResult.NotHandled;
		}

		if (((IWinRTObject)this).NativeObject.TryAs(iid, out ppv) >= 0)
		{
			return CustomQueryInterfaceResult.Handled;
		}

		return CustomQueryInterfaceResult.NotHandled;
	}

	private IObjectReference MakeObjRefSystemIDisposable()
	{
		Interlocked.CompareExchange(ref _objRefSystemIDisposable, ((IWinRTObject)this).NativeObject.As<IUnknownVftbl>(ABI.System.IDisposableMethods.IID), null);
		return _objRefSystemIDisposable;
	}

	private IObjectReference MakeObjRefMicrosoftUICompositionICompositionSupportsSystemBackdrop()
	{
		Interlocked.CompareExchange(ref _objRefMicrosoftUICompositionICompositionSupportsSystemBackdrop, ((IWinRTObject)this).NativeObject.As<IUnknownVftbl>(ABI.Microsoft.UI.Composition.ICompositionSupportsSystemBackdropMethods.IID), null);
		return _objRefMicrosoftUICompositionICompositionSupportsSystemBackdrop;
	}

	private ConcurrentDictionary<RuntimeTypeHandle, IObjectReference> MakeQueryInterfaceCache()
	{
		Interlocked.CompareExchange(ref _queryInterfaceCache, new(), null);
		return _queryInterfaceCache;
	}

	private ConcurrentDictionary<RuntimeTypeHandle, object> MakeAdditionalTypeData()
	{
		Interlocked.CompareExchange(ref _additionalTypeData, new(), null);
		return _additionalTypeData;
	}

	// ReSharper disable once MemberCanBeMadeStatic.Local
	// ReSharper disable once UnusedParameter.Local
	private bool IsOverridableInterface(Guid iid)
	{
		return false;
	}

	// ReSharper disable once UnusedType.Local
	private struct InterfaceTag<TInterface>;
}
