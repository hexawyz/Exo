using System.Reflection;

namespace Exo;

public static class ComponentFactoryValidationErrorCodeExtensions
{
	public static string? FormatError(this ComponentFactory.ValidationErrorCode errorCode, MethodInfo? method, string? argument)
		=> errorCode switch
		{
			ComponentFactory.ValidationErrorCode.None => null,
			ComponentFactory.ValidationErrorCode.InvalidReturnType => $"The method {method} has an invalid return type.",
			ComponentFactory.ValidationErrorCode.InvalidCancellationToken => @$"The method {method} has an invalid ""cancellationToken"" as its last parameter.",
			ComponentFactory.ValidationErrorCode.RequiredParameterMismatch => $"The parameter {argument} of {method} does not match the expected method signature.",
			ComponentFactory.ValidationErrorCode.UnallowedByRefParameter => $"The parameter {argument} of {method} cannot be passed by reference.",
			ComponentFactory.ValidationErrorCode.OptionalParameterNotFound => $"The parameter {argument} of {method} cannot be bound.",
			_ => "Unknown error"
		};
}
