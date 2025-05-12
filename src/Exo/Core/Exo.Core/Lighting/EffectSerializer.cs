using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Lighting.Effects;
using Exo.Primitives;

namespace Exo.Lighting;

public static class EffectSerializer
{
	private delegate bool TrySetEffectDelegate(ILightingZone lightingZone, ReadOnlySpan<byte> data);
	private delegate bool TrySetEffectDelegate<TEffect>(ILightingZone lightingZone, in TEffect effect)
		where TEffect : struct, ILightingEffect<TEffect>;

	private static readonly ConcurrentDictionary<Guid, RegisteredEffectState> EffectStates = new();
	private static readonly Lock EffectAddLock = new();
	private static ChangeBroadcaster<LightingEffectInformation> _effectBroadcaster;

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void RegisterEffect<TEffect>()
		where TEffect : struct, ILightingEffect<TEffect>
	{
		var metadata = TEffect.GetEffectMetadata();

		// Work around the fact that GetOrAdd is not atomic…
		lock (EffectAddLock)
		{
			bool isUpdated;
			if (!EffectStates.TryGetValue(metadata.EffectId, out var state))
			{
				isUpdated = true;
				state = new(metadata);
				EffectStates.TryAdd(metadata.EffectId, state);
			}
			else if (isUpdated = state.Metadata != metadata)
			{
				state.Metadata = metadata;
			}
			state.RegisterDeserializer<TEffect>();
			if (isUpdated)
			{
				_effectBroadcaster.Push(metadata);
			}
		}
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void RegisterEffectConversion<TSourceEffect, TDestinationEffect>()
		where TSourceEffect : struct, ILightingEffect<TSourceEffect>
		where TDestinationEffect : struct, IConvertibleLightingEffect<TSourceEffect, TDestinationEffect>
	{
		if (EffectStates.TryGetValue(TSourceEffect.EffectId, out var state))
		{
			lock (EffectAddLock)
			{
				state.RegisterConverter<TSourceEffect, TDestinationEffect>();
			}
		}
	}

	public static bool TryGetEffectMetadata(Guid effectId, [NotNullWhen(true)] out LightingEffectInformation? metadata)
	{
		if (EffectStates.TryGetValue(effectId, out var state))
		{
			metadata = state.Metadata;
			return true;
		}
		metadata = null;
		return false;
	}

	public static bool TrySetEffect(ILightingZone lightingZone, LightingEffect effect)
		=> TrySetEffect(lightingZone, effect.EffectId, effect.EffectData ?? []);

	public static bool TrySetEffect(ILightingZone lightingZone, Guid effectId, ReadOnlySpan<byte> data)
	{
		if (EffectStates.TryGetValue(effectId, out var state))
		{
			return state.SetEffect(lightingZone, data);
		}
		return false;
	}

	public static bool TrySetEffect(ILightingZone lightingZone, ReadOnlySpan<byte> data)
	{
		var effectId = new Guid(data[..16]);
		if (EffectStates.TryGetValue(effectId, out var state))
		{
			return state.SetEffect(lightingZone, data[16..]);
		}
		return false;
	}

	// NB: For now, LightingEffect is a class, which kinda negates separating the Guid from the rest of the data, as we still have to allocate an object.
	// Once lighting stuff is migrated out of protobuf, we should be able to change this to be a struct.
	// Having LightingEffect be a struct would make all singleton lighting effects totally free.
	public static LightingEffect GetEffect(ILightingZone lightingZone)
	{
		var effect = lightingZone.GetCurrentEffect();
		if (effect.TryGetSize(out uint size) || size > 0)
		{
			var effectId = effect.GetEffectId();
			byte[] buffer;
			if (size == 0)
			{
				buffer = [];
			}
			else
			{
				buffer = new byte[size];
				var writer = new BufferWriter(buffer);
				effect.Serialize(ref writer);
				var finalSize = buffer.Length;
				if (finalSize != size) buffer = finalSize > 0 ? buffer[..(int)(uint)finalSize] : [];
			}
			return new LightingEffect(effectId, buffer);
		}
		else
		{
			return GetEffectFromPoolBuffer(effect);
		}
	}

	private static LightingEffect GetEffectFromPoolBuffer(ILightingEffect effect)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(256);
		try
		{
			var effectId = effect.GetEffectId();
			var writer = new BufferWriter(buffer);
			effect.Serialize(ref writer);
			var finalSize = buffer.Length;
			return new LightingEffect(effectId, finalSize > 0 ? buffer[..(int)(uint)finalSize] : []);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, false);
		}
	}

	public static byte[] GetRawEffect(ILightingZone lightingZone)
	{
		var effect = lightingZone.GetCurrentEffect();
		if (effect.TryGetSize(out uint size) || size > 0)
		{
			var buffer = new byte[size + 16];
			effect.GetEffectId().TryWriteBytes(buffer);
			var writer = new BufferWriter(buffer.AsSpan(16));
			effect.Serialize(ref writer);
			if (writer.Length == size)
			{
				return buffer;
			}
			else
			{
				return buffer[..(int)(uint)(writer.Length + 16)];
			}
		}
		else
		{
			return GetRawEffectFromPoolBuffer(effect);
		}
	}

	private static byte[] GetRawEffectFromPoolBuffer(ILightingEffect effect)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(256);
		try
		{
			effect.GetEffectId().TryWriteBytes(buffer);
			var writer = new BufferWriter(buffer.AsSpan(16));
			effect.Serialize(ref writer);
			return buffer[..(int)(uint)(writer.Length + 16)];
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, false);
		}
	}

	public static LightingEffect Serialize<TEffect>(in TEffect effect)
		where TEffect : struct, ILightingEffect<TEffect>
	{
		if (effect.TryGetSize(out uint size) || size > 0)
		{
			var effectId = effect.GetEffectId();
			byte[] buffer;
			if (size == 0)
			{
				buffer = [];
			}
			else
			{
				buffer = new byte[size];
				var writer = new BufferWriter(buffer);
				effect.Serialize(ref writer);
				var finalSize = buffer.Length;
				if (finalSize != size) buffer = finalSize > 0 ? buffer[..(int)(uint)finalSize] : [];
			}
			return new LightingEffect(effectId, buffer);
		}
		else
		{
			return SerializeFromPoolBuffer(effect);
		}
	}

	private static LightingEffect SerializeFromPoolBuffer<TEffect>(in TEffect effect)
		where TEffect : struct, ILightingEffect<TEffect>
	{
		var buffer = ArrayPool<byte>.Shared.Rent(256);
		try
		{
			var effectId = effect.GetEffectId();
			var writer = new BufferWriter(buffer);
			effect.Serialize(ref writer);
			var finalSize = buffer.Length;
			return new LightingEffect(effectId, finalSize > 0 ? buffer[..(int)(uint)finalSize] : []);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, false);
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	[SkipLocalsInit]
	public static TEffect UnsafeDeserialize<TEffect>(ReadOnlySpan<byte> data)
		where TEffect : struct, ILightingEffect<TEffect>
	{
		var reader = new BufferReader(data);
		TEffect.Deserialize(ref reader, out var effect);
		return effect;
	}

	public static async IAsyncEnumerable<LightingEffectInformation> WatchEffectsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var channel = Channel.CreateUnbounded<LightingEffectInformation>(new() { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = true });

		List<LightingEffectInformation>? initialEffects = new();
		lock (EffectAddLock)
		{
			foreach (var effectState in EffectStates.Values)
			{
				initialEffects.Add(effectState.Metadata);
			}
			_effectBroadcaster.Register(channel);
		}
		try
		{
			foreach (var initialEffect in initialEffects)
			{
				yield return initialEffect;
			}
			initialEffects = null;

			while (true)
			{
				await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
				while (channel.Reader.TryRead(out var effect))
				{
					yield return effect;
				}
			}
		}
		finally
		{
			_effectBroadcaster.Unregister(channel);
		}
	}

	private sealed class RegisteredEffectState : IDisposable
	{
		// TODO: ConditionalWeakTable< Type, SetConvertedEffect >
		public LightingEffectInformation Metadata;
		private DependentHandle _dependentHandle;
		private ConditionalWeakTable<Type, Delegate>? _converters;

		public RegisteredEffectState(LightingEffectInformation information)
		{
			Metadata = information;
		}

		~RegisteredEffectState() => Dispose(false);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			_dependentHandle.Dispose();
			_converters?.Clear();
		}

		public void RegisterDeserializer<TEffect>()
			where TEffect : struct, ILightingEffect<TEffect>
		{
			if (_dependentHandle.IsAllocated)
			{
				_dependentHandle.Dependent = new TrySetEffectDelegate(TrySetEffect<TEffect>);
			}
			else
			{
				_dependentHandle = new(typeof(TEffect), new TrySetEffectDelegate(TrySetEffect<TEffect>));
			}
		}

		public void RegisterConverter<TSourceEffect, TDestinationEffect>()
			where TSourceEffect : struct, ILightingEffect<TSourceEffect>
			where TDestinationEffect : struct, IConvertibleLightingEffect<TSourceEffect, TDestinationEffect>
		{
			_ = (_converters ??= new()).GetValue(typeof(TDestinationEffect), _ => new TrySetEffectDelegate<TSourceEffect>(TrySetConvertedEffect<TSourceEffect, TDestinationEffect>));
		}

		public bool SetEffect(ILightingZone lightingZone, ReadOnlySpan<byte> data)
		{
			if (_dependentHandle.Dependent is TrySetEffectDelegate d)
			{
				if (!d.Invoke(lightingZone, data))
				{
					throw _dependentHandle.Target is Type effectType ?
						new InvalidOperationException($"The specified zone does not support effects of type {effectType}.") :
						new InvalidOperationException($"The specified zone does not support effects of the specified type.");
				}
				return true;
			}
			return false;
		}

		public bool TrySetEffect(ILightingZone lightingZone, ReadOnlySpan<byte> data)
			=> (_dependentHandle.Dependent as TrySetEffectDelegate)?.Invoke(lightingZone, data) ?? false;

		private bool TrySetEffect<TEffect>(ILightingZone lightingZone, ReadOnlySpan<byte> data)
			where TEffect : struct, ILightingEffect<TEffect>
		{
			var reader = new BufferReader(data);
			TEffect.Deserialize(ref reader, out var effect);
			if (lightingZone is ILightingZoneEffect<TEffect> typedLightingZone)
			{
				typedLightingZone.ApplyEffect(in effect);
				return true;
			}

			// TODO: Enumerating ConditionalWeakTable is probably far from efficient but we can rework that part later.
			// We do want to allow unloading assemblies so we can't keep strong references, but we also don't want to double register anything.
			// As we know the list of converters is unlikely to change frequently, it would probably work it to do the job of ConditionalWeakTable manually in a lighter way.
			if (_converters is { } converters)
			{
				foreach (var kvp in _converters)
				{
					if (Unsafe.As<TrySetEffectDelegate<TEffect>>(kvp.Value)(lightingZone, in effect)) return true;
				}
			}

			return false;
		}

		private bool TrySetConvertedEffect<TSourceEffect, TDestinationEffect>(ILightingZone lightingZone, in TSourceEffect effect)
			where TSourceEffect : struct, ILightingEffect<TSourceEffect>
			where TDestinationEffect : struct, IConvertibleLightingEffect<TSourceEffect, TDestinationEffect>
		{
			if (lightingZone is not ILightingZoneEffect<TDestinationEffect> typedLightingZone) return false;
			typedLightingZone.ApplyEffect(effect);
			return true;
		}
	}
}
