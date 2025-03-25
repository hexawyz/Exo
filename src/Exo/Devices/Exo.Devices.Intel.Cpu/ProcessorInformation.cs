namespace Exo.Devices.Intel.Cpu;

internal readonly struct ProcessorInformation
{
	private readonly byte _steppingIdAndProcessorType;
	private readonly byte _modelId;
	private readonly ushort _familyId;

	public ProcessorInformation(byte steppingId, byte modelId, ushort familyId, ProcessorType processorType)
	{
		_steppingIdAndProcessorType = (byte)((steppingId & 0xF) | ((byte)processorType & 0x3) << 4);
		_modelId = modelId;
		_familyId = familyId;
	}

	public byte SteppingId => (byte)(_steppingIdAndProcessorType & 0xF);
	public byte ModelId => _modelId;
	public ushort FamilyId => _familyId;
	public ProcessorType ProcessorType => (ProcessorType)(byte)(_steppingIdAndProcessorType >>> 4);

	public uint FamilyAndModel => (uint)((nuint)_familyId << 8 | _modelId);
}
