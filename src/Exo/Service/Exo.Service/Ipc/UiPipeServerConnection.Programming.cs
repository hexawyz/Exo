using System.Collections.Immutable;

namespace Exo.Service.Ipc;

partial class UiPipeServerConnection
{
	private async Task InitializeProgrammingMetadataAsync(CancellationToken cancellationToken)
	{
		try
		{
			var modules = _programmingService.GetModules();
			if (modules is { Length: > 0 })
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int length = WriteNotification(buffer.Span, modules);
					await WriteAsync(buffer[..length], cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}

		static int WriteNotification(Span<byte> buffer, ImmutableArray<Programming.ModuleDefinition> modules)
		{
			var writer = new BufferWriter(buffer);
			writer.Write((byte)ExoUiProtocolServerMessage.ProgrammingMetadata);
			Write(ref writer, modules);
			return (int)writer.Length;
		}
	}

	static void Write(ref BufferWriter writer, ImmutableArray<Programming.ModuleDefinition> modules)
	{
		if (modules.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)modules.Length);
			foreach (var module in modules)
			{
				Write(ref writer, module);
			}
		}
	}

	static void Write(ref BufferWriter writer, Programming.ModuleDefinition module)
	{
		writer.Write(module.Id);
		writer.WriteVariableString(module.Name);
		writer.WriteVariableString(module.Comment);
	}
}
