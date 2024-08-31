namespace Exo.Debug;

// This private attribute is to be applied on the only valid existing debug driver factory, in order to distinguish it from anything else.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class DebugFactoryAttribute : Attribute
{
}
