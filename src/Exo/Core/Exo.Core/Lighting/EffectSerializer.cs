using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Contracts;
using Exo.Lighting.Effects;
using Exo.Primitives;

namespace Exo.Lighting;

// To replace the current implementation
public static class EffectSerializer
{
	private delegate void SetEffectDelegate(ILightingZone lightingZone, ReadOnlySpan<byte> data);

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
			_effectBroadcaster.Push(metadata);
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
	{
		if (EffectStates.TryGetValue(effect.EffectId, out var state))
		{
			return state.SetEffect(lightingZone, effect.EffectData ?? []);
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
				await channel.Reader.WaitToReadAsync().ConfigureAwait(false);
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
		public LightingEffectInformation Metadata;
		private DependentHandle _dependentHandle;

		public RegisteredEffectState(LightingEffectInformation information)
		{
			Metadata = information;
		}

		public void Dispose() => _dependentHandle.Dispose();

		public void RegisterDeserializer<TEffect>()
			where TEffect : struct, ILightingEffect<TEffect>
		{
			if (_dependentHandle.IsAllocated)
			{
				_dependentHandle.Dependent = new SetEffectDelegate(SetEffect<TEffect>);
			}
			else
			{
				_dependentHandle = new(typeof(TEffect), new SetEffectDelegate(SetEffect<TEffect>));
			}
		}

		public bool SetEffect(ILightingZone lightingZone, ReadOnlySpan<byte> data)
		{
			if (_dependentHandle.Dependent is SetEffectDelegate d)
			{
				d.Invoke(lightingZone, data);
				return true;
			}
			return false;
		}

		private void SetEffect<TEffect>(ILightingZone lightingZone, ReadOnlySpan<byte> data)
			where TEffect : struct, ILightingEffect<TEffect>
		{
			TEffect effect;
			var reader = new BufferReader(data);
			TEffect.Deserialize(ref reader, out effect);
			if (lightingZone is not ILightingZoneEffect<TEffect> typedLightingZone)
			{
				throw new InvalidOperationException($"The specified zone does not support effects of type {typeof(TEffect)}.");
			}
			typedLightingZone.ApplyEffect(in effect);
		}
	}
}
