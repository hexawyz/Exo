using System;

namespace Exo;

public static class DriverFactoryValidationErrorCodeExtensions
{
	public static string? FormatError(this DriverFactory.ValidationErrorCode errorCode, Type driverType, string? argument)
		=> errorCode switch
		{
			DriverFactory.ValidationErrorCode.None => null,
			DriverFactory.ValidationErrorCode.CreateAsyncMethodNotFound => $"No method called CreateAsync was found on the type {driverType}.",
			DriverFactory.ValidationErrorCode.InvalidReturnType => $"The method CreateAsync of {driverType} has an invalid return type.",
			DriverFactory.ValidationErrorCode.NoCancellationToken => @$"The method CreateAsync of {driverType} is missing ""cancellationToken"" as its last parameter.",
			DriverFactory.ValidationErrorCode.RequiredParameterMismatch => $"The parameter {argument} of {driverType}.CreateAsync does not match the expected method signature.",
			DriverFactory.ValidationErrorCode.UnallowedByRefParameter => $"The parameter {argument} of {driverType}.CreateAsync cannot be passed by reference.",
			DriverFactory.ValidationErrorCode.OptionalParameterNotFound => $"The parameter {argument} of {driverType}.CreateAsync cannot be bound.",
			_ => "Unknown error"
		};
}
