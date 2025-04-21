using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Exo.Features;
using Exo.Programming;
using Exo.Programming.Annotations;
using Exo.Service.Events;

namespace Exo.Service;

[Module("Global")]
[TypeId(0x303F219C, 0x7A88, 0x44B8, 0x90, 0x98, 0x59, 0x1A, 0xA9, 0x4A, 0xD2, 0xD6)]
public sealed class ProgrammingService : IAsyncDisposable
{
	[DebuggerDisplay("{Definition.Name,nq} ({Definition.Id})")]
	private sealed class ModuleDetails
	{
		public required object Instance { get; init; }
		public required ModuleDefinition Definition { get; init; }
		public required ImmutableArray<TypeDetails> Types { get; init; }
	}

	[DebuggerDisplay("{Definition.Name,nq} ({Definition.Id})")]
	private sealed class TypeDetails
	{
		public required Type Type { get; init; }
		public required TypeDefinition Definition { get; init; }
	}

	private static class ModuleDetails<T>
	{
		public static readonly (ModuleDefinition Definition, ImmutableArray<TypeDetails> Types) StaticDetails = Get();

		private static (ModuleDefinition, ImmutableArray<TypeDetails>) Get()
		{
			var type = typeof(T);

			if (type.GetCustomAttribute<ModuleAttribute>()?.Name is not string moduleName) throw new InvalidOperationException("Missing module name.");

			var moduleId = TypeId.Get<T>();

			var events = ImmutableArray.CreateBuilder<EventDefinition>();

			foreach (var @event in type.GetCustomAttributes<EventAttribute>())
			{
				var eventType = @event.GetType();
				events.Add
				(
					new()
					{
						Id = @event.Id,
						Name = @event.Name,
						Comment = @event.Comment,
						Options = EventOptions.IsModuleEvent,
						ParametersTypeId = eventType.IsGenericType && eventType.GetGenericTypeDefinition() == typeof(Event<>) ?
							TypeId.Get(eventType.GetGenericArguments()[0]) :
							default,
					}
				);
			}

			var types = ImmutableArray.CreateBuilder<TypeDetails>();

			// (Always) register the intrinsic types.
			if (typeof(T) == typeof(ProgrammingService))
			{
				types.Add(new() { Type = typeof(sbyte), Definition = TypeDefinition.Int8 });
				types.Add(new() { Type = typeof(byte), Definition = TypeDefinition.UInt8 });
				types.Add(new() { Type = typeof(short), Definition = TypeDefinition.Int16 });
				types.Add(new() { Type = typeof(ushort), Definition = TypeDefinition.UInt16 });
				types.Add(new() { Type = typeof(int), Definition = TypeDefinition.Int32 });
				types.Add(new() { Type = typeof(uint), Definition = TypeDefinition.UInt32 });
				types.Add(new() { Type = typeof(long), Definition = TypeDefinition.Int64 });
				types.Add(new() { Type = typeof(ulong), Definition = TypeDefinition.UInt64 });

				types.Add(new() { Type = typeof(Half), Definition = TypeDefinition.Float16 });
				types.Add(new() { Type = typeof(float), Definition = TypeDefinition.Float32 });
				types.Add(new() { Type = typeof(double), Definition = TypeDefinition.Float64 });

				types.Add(new() { Type = typeof(ReadOnlyMemory<byte>), Definition = TypeDefinition.Utf8 });
				types.Add(new() { Type = typeof(string), Definition = TypeDefinition.Utf16 });

				types.Add(new() { Type = typeof(Guid), Definition = TypeDefinition.Guid });

				types.Add(new() { Type = typeof(DateOnly), Definition = TypeDefinition.Date });
				types.Add(new() { Type = typeof(TimeOnly), Definition = TypeDefinition.Time });
				types.Add(new() { Type = typeof(DateTime), Definition = TypeDefinition.DateTime });
			}

			var typeDetails = types.DrainToImmutable();

			var moduleDefinition = new ModuleDefinition
			{
				Id = moduleId,
				Name = moduleName,
				Comment = "",
				Types = ImmutableArray.CreateRange(typeDetails, t => t.Definition),
				Events = events.DrainToImmutable(),
			};

			return new(moduleDefinition, typeDetails);
		}
	}

	private readonly ChannelReader<Event> _eventReader;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _runTask;

	// TODO:
	// - This implements hardcoded logic for now.
	// - All services should be plugged in as modules using RegisterModule<T>(), and the overlay notifications should be done via user-programmed code.
	// - A default program containing overlay programming should then be provided as a startup point for users to customize the logic.
	private readonly OverlayNotificationService _overlayNotificationService;
	private readonly Dictionary<Guid, Action<object?>> _hardcodedEventHandlers;
	private readonly ConcurrentDictionary<Guid, TypeDetails> _types;
	private readonly ConcurrentDictionary<Guid, ModuleDetails> _modules;
	private ModuleDefinition[] _moduleDefinitions;

	// These hardcoded event handlers should be translated and included in the default program once the user-programing code logic is ready.
	private Dictionary<Guid, Action<object?>> CreateHardcodedEventHandlers()
		=> new()
		{
			// Caps Lock
			{
				KeyboardService.CapsLockOffEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.CapsLockOff, e.DeviceId);
				}
			},
			{
				KeyboardService.CapsLockOnEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.CapsLockOn, e.DeviceId);
				}
			},

			// Num Lock
			{
				KeyboardService.NumLockOffEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.NumLockOff, e.DeviceId);
				}
			},
			{
				KeyboardService.NumLockOnEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.NumLockOn, e.DeviceId);
				}
			},

			// Scroll Lock
			{
				KeyboardService.ScrollLockOffEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.ScrollLockOff, e.DeviceId);
				}
			},
			{
				KeyboardService.ScrollLockOnEventGuid,
				p =>
				{
					var e = (DeviceEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.ScrollLockOn, e.DeviceId);
				}
			},

			// Backlight
			{
				KeyboardService.BacklightDownEventGuid,
				p =>
				{
					var e = (BacklightLevelEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.KeyboardBacklightDown, e.DeviceId, e.Level, e.MaximumLevel);
				}
			},
			{
				KeyboardService.BacklightUpEventGuid,
				p =>
				{
					var e = (BacklightLevelEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.KeyboardBacklightUp, e.DeviceId, e.Level, e.MaximumLevel);
				}
			},

			// Battery
			{
				PowerService.BatteryDeviceConnectedEventGuid,
				p =>
				{
					var e = (BatteryEventParameters)p!;
					if ((e.ExternalPowerStatus & ExternalPowerStatus.IsConnected) != 0 && e.CurrentLevel.GetValueOrDefault() is <= 0.1f)
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryLow, e.DeviceId, GetBatteryLevel(e.CurrentLevel.GetValueOrDefault()), 10);
					}
				}
			},
			{
				PowerService.BatteryExternalPowerConnectedEventGuid,
				p =>
				{
					var e = (BatteryEventParameters)p!;
					if (e.CurrentLevel is not null)
					{
						_overlayNotificationService.PostRequest
						(
							OverlayNotificationKind.BatteryExternalPowerConnected,
							e.DeviceId,
							GetBatteryLevel(e.CurrentLevel.GetValueOrDefault()),
							10
						);
					}
					else
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryExternalPowerConnected, e.DeviceId, 0, 0);
					}
				}
			},
			{
				PowerService.BatteryExternalPowerDisconnectedEventGuid,
				p =>
				{
					var e = (BatteryEventParameters)p!;
					if (e.CurrentLevel is not null)
					{
						_overlayNotificationService.PostRequest
						(
							OverlayNotificationKind.BatteryExternalPowerDisconnected,
							e.DeviceId,
							GetBatteryLevel(e.CurrentLevel.GetValueOrDefault()),
							10
						);
					}
					else
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryExternalPowerDisconnected, e.DeviceId, 0, 0);
					}
				}
			},
			{
				PowerService.BatteryChargingCompleteEventGuid,
				p =>
				{
					var e = (BatteryEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryExternalPowerConnected, e.DeviceId, 10, 10);
				}
			},
			{
				PowerService.BatteryErrorEventGuid,
				p =>
				{
					var e = (BatteryEventParameters)p!;
					_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryError, e.DeviceId, 0, 0);
				}
			},
			{
				PowerService.BatteryDischargingEventGuid,
				p =>
				{
					var e = (BatteryEventParameters)p!;
					if (e.CurrentLevel <= 0.1f && e.PreviousLevel is null or > 0.1f)
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.BatteryLow, e.DeviceId, GetBatteryLevel(e.CurrentLevel.GetValueOrDefault()), 0);
					}
				}
			},

			// Mouse
			{
				MouseService.DpiDownEventGuid,
				p =>
				{
					var e = (MouseDpiEventParameters)p!;
					if (e.LevelCount > 0 && e.CurrentLevel is not null)
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.MouseDpiDown, e.DeviceId, (uint)e.CurrentLevel.GetValueOrDefault() + 1, e.LevelCount, e.Horizontal);
					}
					else
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.MouseDpiDown, e.DeviceId, 0, 0, e.Horizontal);
					}
				}
			},
			{
				MouseService.DpiUpEventGuid,
				p =>
				{
					var e = (MouseDpiEventParameters)p!;
					if (e.LevelCount > 0 && e.CurrentLevel is not null)
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.MouseDpiUp, e.DeviceId, (uint)e.CurrentLevel.GetValueOrDefault() + 1, e.LevelCount, e.Horizontal);
					}
					else
					{
						_overlayNotificationService.PostRequest(OverlayNotificationKind.MouseDpiUp, e.DeviceId, 0, 0, e.Horizontal);
					}
				}
			},
		};

	private static uint GetBatteryLevel(float level)
	{
		if (level < 0) return 0;
		if (level > 1) return 1;

		return (uint)((level + 0.05f) * 10);
	}

	public ProgrammingService(ChannelReader<Event> eventReader, OverlayNotificationService overlayNotificationService)
	{
		_eventReader = eventReader;
		_overlayNotificationService = overlayNotificationService;
		_types = new();
		_modules = new();
		_moduleDefinitions = [];
		RegisterModule(this);
		_hardcodedEventHandlers = CreateHardcodedEventHandlers();
		_cancellationTokenSource = new();
		_runTask = RunAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		if (_runTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		_cancellationTokenSource.Dispose();
		await _runTask.ConfigureAwait(false);
	}

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var @event in _eventReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_hardcodedEventHandlers.TryGetValue(@event.EventId, out var handler))
				{
					handler(@event.Parameters);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	public void RegisterModule<T>(T instance)
		where T : notnull
	{
		var (definition, types) = ModuleDetails<T>.StaticDetails;

		if (!_modules.TryAdd(definition.Id, new() { Instance = instance, Definition = definition, Types = types }))
		{
			throw new InvalidOperationException("A module with the same ID was already added.");
		}

		for (int i = 0; i < types.Length; i++)
		{
			var type = types[i];
			if (!_types.TryAdd(type.Definition.Id, type))
			{
				for (int j = 0; j < i; j++)
				{
					_types.TryRemove(types[i].Definition.Id, out _);
				}
				throw new InvalidOperationException("A type with the same ID was already added.");
			}
		}

		var oldDefinitions = Volatile.Read(ref _moduleDefinitions);
		while (true)
		{
			var newDefinitions = oldDefinitions;
			Array.Resize(ref newDefinitions, oldDefinitions.Length + 1);
			newDefinitions[^1] = definition;
			if (oldDefinitions == (oldDefinitions = Interlocked.CompareExchange(ref _moduleDefinitions, newDefinitions, oldDefinitions))) break;
		}
	}

	public ImmutableArray<ModuleDefinition> GetModules() => ImmutableCollectionsMarshal.AsImmutableArray( _moduleDefinitions);

	//public async IAsyncEnumerable<TypeDefinition> GetCustomTypesAsync()
	//{
	//}
}
