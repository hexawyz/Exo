namespace Exo.Devices.NVidia;

public enum NvApiError : int
{
	Success = 0,
	Error = -1,
	LibraryNotFound = -2,
	NoImplementation = -3,
	ApiNotInitialized = -4,
	InvalidArgument = -5,
	NvidiaDeviceNotFound = -6,
	EndEnumeration = -7,
	InvalidHandle = -8,
	IncompatibleStructVersion = -9,
	HandleInvalidated = -10,
	OpenGlContextNotCurrent = -11,
	NoGlExpert = -12,
	InstrumentationDisabled = -13,
	InvalidPointer = -14,
	NoGlNsight = -15,
	InvalidUserPrivilege = -137,
	GpuNotPowered = -220,
}
