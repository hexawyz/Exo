using Microsoft.Extensions.Logging;

namespace Exo.Devices.Intel.Cpu;

internal static partial class LoggerExtensions
{
	[LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Cpu #{ProcessorNumber} is {ProcessorType} {FamilyId:X}:{ModelId:X}:{SteppingId}.")]
	public static partial void IntelProcessorInformation(this ILogger logger, ushort processorNumber, ProcessorType processorType, ushort familyId, byte modelId, byte steppingId);
}
