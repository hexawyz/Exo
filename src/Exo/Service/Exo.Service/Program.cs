using System.Buffers;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Exo.Contracts.Ui;
using Exo.Programming;
using Exo.Utils;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProtoBuf.Meta;
using Serilog;
using SixLabors.ImageSharp.Memory;

namespace Exo.Service;

public class Program
{
	public static readonly string? GitCommitId = GitCommitHelper.GetCommitId(typeof(Program).Assembly);

	private class NativeMemoryAllocator : MemoryAllocator
	{
		// Allows falling back to the default allocator for allocations smaller than a certain size.
		// May get rid of this later, but this does not seem to harm for now.
		private const int FallbackThreshold = 256;

		protected override int GetBufferCapacityInBytes() => 4 << 20;

		public unsafe override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
			=> length <= FallbackThreshold ? Default.Allocate<T>(length, options) : new NativeMemoryHolder<T>(length, options);

		private sealed class NativeMemoryHolder<T> : MemoryManager<T>
			where T : struct
		{
			private nint _pointer;
			private readonly int _length;
			private int _refCount;

			public unsafe NativeMemoryHolder(int length, AllocationOptions options = AllocationOptions.None)
			{
				_length = length;
				_pointer = options == AllocationOptions.Clean ?
					(nint)NativeMemory.AllocZeroed((nuint)length, (nuint)Unsafe.SizeOf<T>()) :
					(nint)NativeMemory.Alloc((nuint)length, (nuint)Unsafe.SizeOf<T>());
				GC.AddMemoryPressure(_length * Unsafe.SizeOf<T>());
			}

#pragma warning disable CA2015 // Do not define finalizers for types derived from MemoryManager<T>
			~NativeMemoryHolder() => Dispose(false);
#pragma warning restore CA2015 // Do not define finalizers for types derived from MemoryManager<T>

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					int refCount;
					if ((refCount = Interlocked.CompareExchange(ref _refCount, int.MinValue >> 1, 0)) is 0)
					{
						TryFree();
						return;
					}
					while (refCount >= 0)
					{
						if (refCount == (refCount = Interlocked.CompareExchange(ref _refCount, refCount | (int.MinValue >> 1), refCount)))
						{
							if (refCount == 0)
							{
								TryFree();
							}
							return;
						}
					}
				}
				else
				{
					TryFree();
				}
			}

			private unsafe void TryFree()
			{
				nint pointer = Interlocked.Exchange(ref _pointer, 0);
				if (pointer != 0)
				{
					NativeMemory.Free((void*)pointer);
					GC.RemoveMemoryPressure(_length * Unsafe.SizeOf<T>());
				}
			}

			public unsafe override Span<T> GetSpan() => new Span<T>((T*)_pointer, _length);

			public unsafe override MemoryHandle Pin(int elementIndex = 0)
			{
				if (Interlocked.Increment(ref _refCount) < 0)
				{
					Unpin();
				}
				return new MemoryHandle((byte*)_pointer + (nuint)elementIndex * (nuint)Unsafe.SizeOf<T>(), default, this);
			}

			public override void Unpin()
			{
				int refCount = Interlocked.Decrement(ref _refCount);
				if (refCount < 0)
				{
					if (refCount <= int.MinValue >> 1)
					{
						TryFree();
						return;
					}
					else if (refCount >= int.MinValue >> 2)
					{
						// Try to mitigate the case where someone did something terribly wrong.
						Interlocked.Increment(ref _refCount);
						throw new InvalidOperationException();
					}
				}
			}
		}
	}

	public static void Main(string[] args)
	{
		// Ensure that logs are written in the right place.
		// To be revisited later when the deployed file structure is better defined.
		Environment.SetEnvironmentVariable("LOGDIR", Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs")));

		foreach (var type in typeof(NamedElement).Assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(NamedElement))))
		{
			var metaType = RuntimeTypeModel.Default[type];

			metaType.Add(1, nameof(NamedElement.Id));
			metaType.Add(2, nameof(NamedElement.Name));
			metaType.Add(3, nameof(NamedElement.Comment));
		}

		RuntimeTypeModel.Default.Add<UInt128>(false).SerializerType = typeof(UInt128Serializer);

		SixLabors.ImageSharp.Configuration.Default.MemoryAllocator = new NativeMemoryAllocator();

		CreateHostBuilder(args).Build().Run();
	}

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.UseWindowsService()
			.ConfigureWebHost
			(
				webBuilder => webBuilder.UseStartup<Startup>()
					.UseKestrel((ctx, o) => o.Configure(ctx.Configuration.GetSection("Kestrel"), reloadOnChange: true))
					.UseNamedPipes
					(
						o =>
						{
							var pipeSecurity = new PipeSecurity();
							// TODO: Fix for having better ACLs.
							pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Translate(typeof(NTAccount)), PipeAccessRights.FullControl, AccessControlType.Allow));
							//pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Translate(typeof(NTAccount)), PipeAccessRights.ReadData | PipeAccessRights.WriteData, AccessControlType.Allow));
							pipeSecurity.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)), PipeAccessRights.FullControl, AccessControlType.Allow));
							o.CurrentUserOnly = false;
							o.PipeSecurity = pipeSecurity;
						}
					)
					.ConfigureKestrel
					(
						o =>
						{
							o.ListenNamedPipe(@"Local\Exo.Service.Configuration", listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
							o.ListenNamedPipe(@"Local\Exo.Service.Overlay", listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
						}
					),
				o => { }
			)
			.UseSerilog((ctx, logger) => logger.ReadFrom.Configuration(ctx.Configuration));
}
