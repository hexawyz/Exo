// <auto-generated>
using System.Reflection;

namespace FxResources.System.ServiceProcess.ServiceController
{
    internal static class SR { }
}
namespace System
{
    internal static partial class SR
    {
        private static global::System.Resources.ResourceManager s_resourceManager;
        internal static global::System.Resources.ResourceManager ResourceManager => s_resourceManager ?? (s_resourceManager = new global::System.Resources.ResourceManager(typeof(FxResources.System.ServiceProcess.ServiceController.SR)));

        /// <summary>Arguments within the 'args' array passed to Start cannot be null.</summary>
        internal static string @ArgsCantBeNull => GetResourceString("ArgsCantBeNull", @"Arguments within the 'args' array passed to Start cannot be null.");
        /// <summary>MachineName '{0}' is invalid.</summary>
        internal static string @BadMachineName => GetResourceString("BadMachineName", @"MachineName '{0}' is invalid.");
        /// <summary>Cannot start service '{0}' on computer '{1}'.</summary>
        internal static string @CannotStart => GetResourceString("CannotStart", @"Cannot start service '{0}' on computer '{1}'.");
        /// <summary>The value of argument '{0}' ({1}) is invalid for Enum type '{2}'.</summary>
        internal static string @InvalidEnumArgument => GetResourceString("InvalidEnumArgument", @"The value of argument '{0}' ({1}) is invalid for Enum type '{2}'.");
        /// <summary>Invalid value '{1}' for parameter '{0}'.</summary>
        internal static string @InvalidParameter => GetResourceString("InvalidParameter", @"Invalid value '{1}' for parameter '{0}'.");
        /// <summary>Service '{0}' was not found on computer '{1}'.</summary>
        internal static string @NoService => GetResourceString("NoService", @"Service '{0}' was not found on computer '{1}'.");
        /// <summary>Cannot open Service Control Manager on computer '{0}'. This operation might require other privileges.</summary>
        internal static string @OpenSC => GetResourceString("OpenSC", @"Cannot open Service Control Manager on computer '{0}'. This operation might require other privileges.");
        /// <summary>Cannot open '{0}' service on computer '{1}'.</summary>
        internal static string @OpenService => GetResourceString("OpenService", @"Cannot open '{0}' service on computer '{1}'.");
        /// <summary>Cannot pause '{0}' service on computer '{1}'.</summary>
        internal static string @PauseService => GetResourceString("PauseService", @"Cannot pause '{0}' service on computer '{1}'.");
        /// <summary>Cannot resume '{0}' service on computer '{1}'.</summary>
        internal static string @ResumeService => GetResourceString("ResumeService", @"Cannot resume '{0}' service on computer '{1}'.");
        /// <summary>Cannot stop '{0}' service on computer '{1}'.</summary>
        internal static string @StopService => GetResourceString("StopService", @"Cannot stop '{0}' service on computer '{1}'.");
        /// <summary>The operation requested for service '{0}' has not been completed within the specified time interval.</summary>
        internal static string @Timeout => GetResourceString("Timeout", @"The operation requested for service '{0}' has not been completed within the specified time interval.");
        /// <summary>ServiceController enables manipulating and accessing Windows services and it is not applicable for other operating systems.</summary>
        internal static string @PlatformNotSupported_ServiceController => GetResourceString("PlatformNotSupported_ServiceController", @"ServiceController enables manipulating and accessing Windows services and it is not applicable for other operating systems.");
        /// <summary>Cannot change CanStop, CanPauseAndContinue, CanShutdown, CanHandlePowerEvent, or CanHandleSessionChangeEvent property values after the service has been started.</summary>
        internal static string @CannotChangeProperties => GetResourceString("CannotChangeProperties", @"Cannot change CanStop, CanPauseAndContinue, CanShutdown, CanHandlePowerEvent, or CanHandleSessionChangeEvent property values after the service has been started.");
        /// <summary>Cannot change service name when the service is running.</summary>
        internal static string @CannotChangeName => GetResourceString("CannotChangeName", @"Cannot change service name when the service is running.");
        /// <summary>Service name '{0}' contains invalid characters, is empty, or is too long (max length = {1}).</summary>
        internal static string @ServiceName => GetResourceString("ServiceName", @"Service name '{0}' contains invalid characters, is empty, or is too long (max length = {1}).");
        /// <summary>Service has not been supplied. At least one object derived from ServiceBase is required in order to run.</summary>
        internal static string @NoServices => GetResourceString("NoServices", @"Service has not been supplied. At least one object derived from ServiceBase is required in order to run.");
        /// <summary>Cannot start service from the command line or a debugger.  A Windows Service must first be installed and then started with the ServerExplorer, Windows Services Administrative tool or the NET START command.</summary>
        internal static string @CantStartFromCommandLine => GetResourceString("CantStartFromCommandLine", @"Cannot start service from the command line or a debugger.  A Windows Service must first be installed and then started with the ServerExplorer, Windows Services Administrative tool or the NET START command.");
        /// <summary>UpdatePendingStatus can only be called during the handling of Start, Stop, Pause and Continue commands.</summary>
        internal static string @NotInPendingState => GetResourceString("NotInPendingState", @"UpdatePendingStatus can only be called during the handling of Start, Stop, Pause and Continue commands.");
        /// <summary>Service started successfully.</summary>
        internal static string @StartSuccessful => GetResourceString("StartSuccessful", @"Service started successfully.");
        /// <summary>Service stopped successfully.</summary>
        internal static string @StopSuccessful => GetResourceString("StopSuccessful", @"Service stopped successfully.");
        /// <summary>Service paused successfully.</summary>
        internal static string @PauseSuccessful => GetResourceString("PauseSuccessful", @"Service paused successfully.");
        /// <summary>Service continued successfully.</summary>
        internal static string @ContinueSuccessful => GetResourceString("ContinueSuccessful", @"Service continued successfully.");
        /// <summary>Service command was processed successfully.</summary>
        internal static string @CommandSuccessful => GetResourceString("CommandSuccessful", @"Service command was processed successfully.");
        /// <summary>Service cannot be started. {0}</summary>
        internal static string @StartFailed => GetResourceString("StartFailed", @"Service cannot be started. {0}");
        /// <summary>Failed to stop service. {0}</summary>
        internal static string @StopFailed => GetResourceString("StopFailed", @"Failed to stop service. {0}");
        /// <summary>Failed to pause service. {0}</summary>
        internal static string @PauseFailed => GetResourceString("PauseFailed", @"Failed to pause service. {0}");
        /// <summary>Failed to continue service. {0}</summary>
        internal static string @ContinueFailed => GetResourceString("ContinueFailed", @"Failed to continue service. {0}");
        /// <summary>Failed to process session change. {0}</summary>
        internal static string @SessionChangeFailed => GetResourceString("SessionChangeFailed", @"Failed to process session change. {0}");
        /// <summary>Failed to process user-mode reboot. {0}</summary>
        internal static string @UserModeRebootFailed => GetResourceString("SessionChangeFailed", @"Failed to process user-mode reboot. {0}");
        /// <summary>Failed to process service command. {0}</summary>
        internal static string @CommandFailed => GetResourceString("CommandFailed", @"Failed to process service command. {0}");
        /// <summary>Service has been successfully shut down.</summary>
        internal static string @ShutdownOK => GetResourceString("ShutdownOK", @"Service has been successfully shut down.");
        /// <summary>Failed to shut down service. The error that occurred was: {0}.</summary>
        internal static string @ShutdownFailed => GetResourceString("ShutdownFailed", @"Failed to shut down service. The error that occurred was: {0}.");
        /// <summary>PowerEvent handled successfully by the service.</summary>
        internal static string @PowerEventOK => GetResourceString("PowerEventOK", @"PowerEvent handled successfully by the service.");
        /// <summary>Failed in handling the PowerEvent. The error that occurred was: {0}.</summary>
        internal static string @PowerEventFailed => GetResourceString("PowerEventFailed", @"Failed in handling the PowerEvent. The error that occurred was: {0}.");
        /// <summary>Cannot control '{0}' service on computer '{1}'.</summary>
        internal static string @ControlService => GetResourceString("ControlService", @"Cannot control '{0}' service on computer '{1}'.");
    }
}
