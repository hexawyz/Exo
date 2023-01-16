using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace Exo.Service;

public interface IAssemblyLoader
{
	/// <summary>Triggered after an assembly has been loaded into its own context.</summary>
	/// <remarks>If necessary, services can perform their own initialization task regarding the loaded assembly.</remarks>
	event EventHandler<AssemblyLoadEventArgs>? AfterAssemblyLoad;
	///// <summary>Triggered before an assembly is to be unloaded.</summary>
	///// <remarks>
	///// There is no definite point in time when the assembly will be actually unloaded, but this event allows services to cut their references to this assembly if they still has some.
	///// This way, the assembly has more chances to be succesfully unloaded.
	///// </remarks>
	//event EventHandler<AssemblyLoadEventArgs>? BeforeAssemblyUnload;

	/// <summary>Triggered when the set of available assemblies has changed.</summary>
	/// <remarks>This could occur as a result of the user adding a new plugin search path, or as a result of the user adding or removing an assembly from the file system.</remarks>
	event EventHandler AvailableAssembliesChanged;

	/// <summary>Gets the assemblies that are available for loading.</summary>
	ImmutableArray<AssemblyName> AvailableAssemblies { get; }

	/// <summary>Loads an assembly within its own context.</summary>
	/// <remarks>
	/// <para>
	/// This method may return the same reference when called multiple times with the same assembly name.
	/// Until the assembly load context is unloaded, it can and should be reused without problem.
	/// </para>
	/// </remarks>
	/// <param name="assemblyName"></param>
	/// <returns></returns>
	Assembly LoadAssembly(AssemblyName assemblyName);

	/// <summary>Loads the specified assembly for reflection only.</summary>
	/// <remarks>
	/// <para>
	/// This is the method that should be used when scanning for plugins, as it will avoid running code from the assembly when it might not be needed.
	/// As such, it is always preferable over <see cref="LoadAssembly(AssemblyName)"/> when running code is not required.
	/// </para>
	/// <para>
	/// Services that rely on this method to scan for plugis are advised to cache their result in order to have a quicker startup.
	/// It is then possible to identify added or removed assemblies by looking at the contents of <see cref="AvailableAssemblies"/>.
	/// Although it should be quite rare, the contents of this property could change over time.
	/// In that case, the <see cref="AvailableAssembliesChanged"/> event will be triggered.
	/// </para>
	/// </remarks>
	/// <param name="assemblyName"></param>
	/// <returns></returns>
	PEReader LoadForReflection(AssemblyName assemblyName);
}
