using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Exo.Ui.Contracts;
using GrpcDataType = Exo.Ui.Contracts.DataType;

namespace Exo.Service.Services;

/// <summary>This implements custom serialization for lighting effects.</summary>
/// <remarks>This serialization infrastructure is needed for UI management of lighting effects.</remarks>
internal static class GrpcEffectSerializer
{
	private static readonly MethodInfo LightingServiceSetEffectMethodInfo = typeof(LightingService).GetMethod(nameof(LightingService.SetEffect), BindingFlags.Public | BindingFlags.Instance)!;

	private static readonly ConstructorInfo LightingEffectConstructorInfo = typeof(LightingEffect).GetConstructor(Type.EmptyTypes)!;
	private static readonly PropertyInfo LightingEffectTypeNamePropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.TypeName))!;
	private static readonly PropertyInfo LightingEffectColorPropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.Color))!;
	private static readonly PropertyInfo LightingEffectSpeedPropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.Speed))!;
	private static readonly PropertyInfo LightingEffectExtendedPropertyValuesPropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.ExtendedPropertyValues))!;

	private static readonly MethodInfo RgbColorToInt32MethodInfo = typeof(RgbColor).GetMethod(nameof(RgbColor.ToInt32))!;
	private static readonly MethodInfo RgbColorFromInt32MethodInfo = typeof(RgbColor).GetMethod(nameof(RgbColor.FromInt32), BindingFlags.Public | BindingFlags.Static)!;

	private static readonly MethodInfo HalfToSingleMethodInfo =
		typeof(Half)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "op_Explicit" && m.ReturnType == typeof(float) && m.GetParameters() is { Length: 1 } parameters && parameters[0].ParameterType == typeof(Half))
			.Single();
	private static readonly MethodInfo SingleToHalfMethodInfo =
		typeof(Half)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "op_Explicit" && m.ReturnType == typeof(Half) && m.GetParameters() is { Length: 1 } parameters && parameters[0].ParameterType == typeof(float))
			.Single();

	private static readonly PropertyInfo PropertyValueIndexPropertyInfo = typeof(PropertyValue).GetProperty(nameof(PropertyValue.Index))!;
	private static readonly PropertyInfo PropertyValueValuePropertyInfo = typeof(PropertyValue).GetProperty(nameof(PropertyValue.Value))!;

	private static readonly ConstructorInfo DataValueConstructorInfo = typeof(DataValue).GetConstructor(Type.EmptyTypes)!;
	private static readonly PropertyInfo DataValueUnsignedValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.UnsignedValue))!;
	private static readonly PropertyInfo DataValueSignedValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.SignedValue))!;
	private static readonly PropertyInfo DataValueSingleValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.SingleValue))!;
	private static readonly PropertyInfo DataValueDoubleValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.DoubleValue))!;
	private static readonly PropertyInfo DataValueGuidValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.GuidValue))!;
	private static readonly PropertyInfo DataValueStringValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.StringValue))!;

	private static readonly FieldInfo ImmutableArrayEmptyFieldInfo =
		typeof(ImmutableArray<PropertyValue>).GetField(nameof(ImmutableArray<PropertyValue>.Empty), BindingFlags.Public | BindingFlags.Static)!;
	private static readonly MethodInfo ImmutableArrayCreateBuilderMethodInfo =
		typeof(ImmutableArray).GetMethod(nameof(ImmutableArray.CreateBuilder), BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)!;
	private static readonly PropertyInfo ImmutableArrayItemsPropertyInfo = typeof(ImmutableArray<PropertyValue>).GetProperty("Items")!;

	private static readonly MethodInfo ImmutableArrayBuilderAddMethodInfo = typeof(ImmutableArray<PropertyValue>.Builder).GetMethod(nameof(ImmutableArray<PropertyValue>.Builder.Add))!;
	private static readonly MethodInfo ImmutableArrayBuilderDrainToImmutableMethodInfo =
		typeof(ImmutableArray<PropertyValue>.Builder).GetMethod(nameof(ImmutableArray<PropertyValue>.Builder.DrainToImmutable))!;

	private static readonly MethodInfo UnsafeAsPropertyValueArrayMethodInfo =
		typeof(Unsafe)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == nameof(Unsafe.As) && m.GetGenericArguments() is { Length: 2 })
			.Single().MakeGenericMethod(typeof(ImmutableArray<PropertyValue>), typeof(PropertyValue[]));

	private sealed class LightingEffectSerializationDetails
	{
		public LightingEffectSerializationDetails
		(
			LightingEffectInformation effectInformation,
			MethodInfo serializeMethod,
			MethodInfo deserializeMethod,
			Func<ILightingEffect, LightingEffect> serialize,
			Action<LightingService, Guid, Guid, LightingEffect> deserializeAndSet
		)
		{
			EffectInformation = effectInformation;
			SerializeMethod = serializeMethod;
			DeserializeMethod = deserializeMethod;
			Serialize = serialize;
			DeserializeAndSet = deserializeAndSet;
		}

		public LightingEffectInformation EffectInformation { get; }


		// Both serialization methods are strongly typed and can't be exposed as-is in a delegate.
		// We'll keep the reference to the instances here, but they will only be used internally in wrapping methods exposed as delegates.

		// Signature: LightingEffect Serialize(TEffect effect)
		public MethodInfo SerializeMethod { get; }

		// Signature: TEffect Deserialize(LightingEffect effect)
		public MethodInfo DeserializeMethod { get; }

		public Func<ILightingEffect, LightingEffect> Serialize { get; }
		public Action<LightingService, Guid, Guid, LightingEffect> DeserializeAndSet { get; }
	}

	private static readonly ConditionalWeakTable<Type, LightingEffectSerializationDetails> EffectInformationByTypeCache = new();
	private static readonly ConditionalWeakTable<Type, EnumerationValue[]> EnumerationValuesCache = new();

	private static readonly ConcurrentDictionary<string, WeakReference<LightingEffectSerializationDetails>> EffectInformationByTypeNameCache = new();

	private static LightingEffectSerializationDetails GetEffectSerializationDetails(string effectTypeName)
		=> EffectInformationByTypeNameCache.TryGetValue(effectTypeName, out var wr) && wr.TryGetTarget(out var d) ?
			d :
			throw new KeyNotFoundException($"Information for the type {effectTypeName} was not found.");

	public static LightingEffectInformation GetEffectInformation(string effectTypeName)
		=> GetEffectSerializationDetails(effectTypeName).EffectInformation;

	public static LightingEffectInformation GetEffectInformation(Type effectType)
		=> GetEffectSerializationDetails(effectType).EffectInformation;

	private static LightingEffectSerializationDetails GetEffectSerializationDetails(Type effectType)
		=> EffectInformationByTypeCache.GetValue(effectType, GetNonCachedEffectDetailsAndUpdateNameCache);

	private static LightingEffectSerializationDetails GetNonCachedEffectDetailsAndUpdateNameCache(Type effectType)
	{
		var details = GetNonCachedEffectDetails(effectType);

		EffectInformationByTypeNameCache.GetOrAdd(effectType.ToString(), _ => new(null!)).SetTarget(details);

		return details;
	}

	private readonly struct SerializationPropertyDetails
	{
		public required readonly PropertyInfo Property { get; init; }
		public required readonly int DataIndex { get; init; }
		public required readonly GrpcDataType DataType { get; init; }
		public readonly Label? DeserializationLabel { get; init; }
		public readonly LocalBuilder? DeserializationLocal { get; init; }
		public readonly LocalBuilder? DeserializationConditionalLocal { get; init; }
		public readonly ParameterInfo? ConstructorParameter { get; init; }
		public readonly PropertyInfo? WellKnownProperty { get; init; }
	}

	private static LightingEffectSerializationDetails GetNonCachedEffectDetails(Type effectType)
	{
		string effectTypeName = effectType.FullName!;

		var properties = effectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		var constructors = effectType.GetConstructors();
		ConstructorInfo? parameterlessConstructor = null;
		ConstructorInfo? parameterizedConstructor = null;
		Dictionary<string, ParameterInfo>? constructorParametersByName = null;
		ParameterInfo[]? constructorParameters = null;

		// It could be possible to do better that this, but constructors are going to be much less useful with init-only properties.
		if (constructors.Length == 2) throw new InvalidOperationException($"Too many constructors specified on the effect type {effectType}.");

		foreach (var constructor in constructors)
		{
			constructorParameters = constructor.GetParameters();

			if (constructorParameters.Length == 0)
			{
				parameterlessConstructor = constructor;
			}
			else
			{
				// If a non-default constructor is found, we expect the parameters to match properties. (Same name minus casing, and same type)
				// Checking will be done when looping on properties.
				parameterizedConstructor = constructor;
				constructorParametersByName = new(constructorParameters.Length);
				foreach (var parameter in constructorParameters)
				{
					// We tolerate PascalCase => PascalCase for C# records, but we generally expect camelCase => PascalCase.
					constructorParametersByName.Add(Naming.StartsWithLowerCase(parameter.Name!) ? Naming.MakePascalCase(parameter.Name!) : parameter.Name!, parameter);
				}
			}
		}

		// The serialization and deserialization methods will be built in parallel, which may make the code a bit confusing,
		// but it makes sense, since all code here makes use of the properties
		var serializeMethod = new DynamicMethod("Serialize", typeof(LightingEffect), new[] { effectType }, effectType, true);
		var deserializeMethod = new DynamicMethod("Deserialize", effectType, new[] { typeof(LightingEffect) }, effectType, true);

		var serializeIlGenerator = serializeMethod.GetILGenerator();
		var deserializeIlGenerator = deserializeMethod.GetILGenerator();

		// Try to optimize the default capacity of the lists that will hold property details.
		// We want to avoid allocations if we can, so we shouldn't force array creation if there are less properties than the default list capacity,
		// but otherwise, force a single allocation able to fit the number of properties.
		int listInitialCapacity = properties.Length > 4 ? properties.Length : 0;

		// We need to do up to four passes for deserialization, but having at least two passes improves serialization when all properties are well-known.
		// The DataIndex will be set negative for well-known properties, and used to write the serialization logic in the second pass.
		var serializablePropertyDetails = new List<SerializationPropertyDetails>(listInitialCapacity);
		var defaultPropertyDetails = new List<SerializationPropertyDetails>();

		// Each property to deserialize (except well-known) will require a label for the switch (dataIndex).
		var deserializationLabels = new List<Label>(listInitialCapacity);

		Dictionary<string, LocalBuilder>? constructorParameterLocals = null;

		// Deserialization: Setup the IL Generator now. Deserialization of well-known properties will be done in the second pass

		var deserializationEffectLocal = deserializeIlGenerator.DeclareLocal(effectType);

		// If there is no specific constructor, we can straight up initialize the effect type and set all properties using the setters.
		if (constructorParameters is null)
		{
			if (parameterlessConstructor is null)
			{
				deserializeIlGenerator.Emit(OpCodes.Ldloca, deserializationEffectLocal);
				deserializeIlGenerator.Emit(OpCodes.Initobj, effectType);
			}
			else
			{
				deserializeIlGenerator.Emit(OpCodes.Newobj, parameterlessConstructor);
				deserializeIlGenerator.Emit(OpCodes.Stloc, deserializationEffectLocal);
			}
		}
		else
		{
			constructorParameterLocals = new(constructorParameters.Length);
		}

		// First pass: Validate all properties and prepare the deserialization.

		int currentPropertyDataIndex = 0;

		for (int i = 0; i < properties.Length; i++)
		{
			var property = properties[i];
			ParameterInfo? parameter = null;

			if (property.GetMethod is null)
			{
				throw new InvalidOperationException($"The property {property.Name} of effect {effectType} does not have a getter.");
			}
			else if (constructorParametersByName?.TryGetValue(property.Name, out parameter) == true)
			{
				constructorParametersByName.Remove(property.Name);
				if (parameter.ParameterType != property.PropertyType)
				{
					throw new InvalidOperationException($"There is a type mismatch between the constructor and the property {property.Name} of effect {effectType}.");
				}
			}
			else if (property.SetMethod is null)
			{
				throw new InvalidOperationException($"The property {property.Name} of effect {effectType} does not have a setter.");
			}

			var dataType = GetDataType(property.PropertyType);

			if (dataType == GrpcDataType.Other) throw new InvalidOperationException($"Could not map {property.PropertyType} for property {property.Name} of effect {effectType}.");

			int dataIndex;
			Label? deserializationLabel = null;
			LocalBuilder? deserializationLocal = null;
			LocalBuilder? deserializationConditionalLocal = null;

			// Properties will need a local to be deserialized if the constructor is used.
			if (constructorParameters is not null)
			{
				deserializationLocal = deserializeIlGenerator.DeclareLocal(property.PropertyType);
				if (parameter is not null)
				{
					constructorParameterLocals!.Add(parameter.Name!, deserializationLocal);
				}
			}

			PropertyInfo? wellKnownProperty = null;

			if (property.Name == nameof(LightingEffect.Color) && IsUInt32Compatible(dataType))
			{
				dataIndex = -1;
				wellKnownProperty = LightingEffectColorPropertyInfo;
			}
			else if (property.Name == nameof(LightingEffect.Speed) && IsUInt32Compatible(dataType))
			{
				dataIndex = -2;
				wellKnownProperty = LightingEffectSpeedPropertyInfo;
			}
			else
			{
				dataIndex = currentPropertyDataIndex++;

				// All non-well-known properties will need a label in the switch statement for deserialization.
				var label = deserializeIlGenerator.DefineLabel();
				deserializationLabels.Add(label);
				deserializationLabel = label;

				if (deserializationLocal is not null && parameter is null)
				{
					deserializationConditionalLocal = deserializeIlGenerator.DeclareLocal(typeof(bool));
				}
			}

			var details = new SerializationPropertyDetails()
			{
				Property = property,
				DataIndex = dataIndex,
				DataType = dataType,
				DeserializationLabel = deserializationLabel,
				DeserializationLocal = deserializationLocal,
				DeserializationConditionalLocal = deserializationConditionalLocal,
				ConstructorParameter = parameter,
				WellKnownProperty = wellKnownProperty,
			};

			serializablePropertyDetails.Add(details);

			if (wellKnownProperty is not null)
			{
				defaultPropertyDetails.Add(details);
			}
		}

		// Deserialization: Initialize the Loop if necessary

		LocalBuilder? deserializationPropertyValuesLocal = null; // We'll unsafe-convert this from ImmutableArray for convenience.
		LocalBuilder? counterLocal = null;
		LocalBuilder? deserializationPropertyValueLocal = null;

		Label deserializationLoopStartLabel = default;
		Label deserializationLoopEndLabel = default;
		Label deserializationDefaultCaseLabel = default;

		if (deserializationLabels.Count > 0)
		{
			var deserializationPropertyValuesImmutableArrayLocal = deserializeIlGenerator.DeclareLocal(typeof(ImmutableArray<PropertyValue>));
			deserializationPropertyValuesLocal = deserializeIlGenerator.DeclareLocal(typeof(PropertyValue[])); // We'll unsafe-convert this from ImmutableArray for convenience.
			counterLocal = deserializeIlGenerator.DeclareLocal(typeof(int));
			deserializationPropertyValueLocal = deserializeIlGenerator.DeclareLocal(typeof(PropertyValue).MakeByRefType());

			deserializationLoopStartLabel = deserializeIlGenerator.DefineLabel();
			deserializationLoopEndLabel = deserializeIlGenerator.DefineLabel();
			deserializationDefaultCaseLabel = deserializeIlGenerator.DefineLabel();

			// propertyValues = Unsafe.As<ImmutableArray<PropertyValue>, PropertyValue[]>(effect.ExtendedPropertyValues);
			deserializeIlGenerator.Emit(OpCodes.Ldarg_0);
			deserializeIlGenerator.Emit(OpCodes.Call, LightingEffectExtendedPropertyValuesPropertyInfo.GetMethod!);
			deserializeIlGenerator.Emit(OpCodes.Stloc, deserializationPropertyValuesImmutableArrayLocal);
			deserializeIlGenerator.Emit(OpCodes.Ldloca, deserializationPropertyValuesImmutableArrayLocal);
			deserializeIlGenerator.Emit(OpCodes.Call, UnsafeAsPropertyValueArrayMethodInfo); // Could probably be omitted.
			deserializeIlGenerator.Emit(OpCodes.Ldind_Ref);
			deserializeIlGenerator.Emit(OpCodes.Stloc, deserializationPropertyValuesLocal);
		}

		// Second pass: Deserialization: Process well-known properties.

		foreach (var details in defaultPropertyDetails)
		{
			if (details.DeserializationLocal is null)
			{
				deserializeIlGenerator.Emit(OpCodes.Ldloca, deserializationEffectLocal);
			}

			deserializeIlGenerator.Emit(OpCodes.Ldarg_0);
			deserializeIlGenerator.Emit(OpCodes.Call, details.WellKnownProperty!.GetMethod!);

			EmitConversionToTargetType(deserializeIlGenerator, details.DataType, true);

			if (details.DeserializationLocal is null)
			{
				deserializeIlGenerator.Emit(OpCodes.Call, details.Property.SetMethod!);
			}
			else
			{
				deserializeIlGenerator.Emit(OpCodes.Stloc, details.DeserializationLocal);
			}
		}

		if (constructorParametersByName?.Count > 0)
		{
			throw new InvalidOperationException($"Some constructor parameters for {effectType} do not have a matching property.");
		}

		// Serialization: Setup the IL generator with the informations we collected in the first pass.

		var serializationEffectLocal = serializeIlGenerator.DeclareLocal(typeof(LightingEffect));
		LocalBuilder? immutableArrayBuilderLocal = null;
		LocalBuilder? propertyValueLocal = null;

		// We only need an ImmutableArray<>.Builder when there are non-well-known properties.
		if (deserializationLabels.Count > 0)
		{
			immutableArrayBuilderLocal = serializeIlGenerator.DeclareLocal(typeof(ImmutableArray<PropertyValue>.Builder));
			propertyValueLocal = serializeIlGenerator.DeclareLocal(typeof(PropertyValue));
		}

		// lightingEffect = new()
		serializeIlGenerator.Emit(OpCodes.Newobj, LightingEffectConstructorInfo);
		serializeIlGenerator.Emit(OpCodes.Stloc, serializationEffectLocal);

		// lightingEffect.TypeName = "TEffect"; // init-only property assignment
		serializeIlGenerator.Emit(OpCodes.Ldloc, serializationEffectLocal);
		serializeIlGenerator.Emit(OpCodes.Ldstr, effectTypeName);
		serializeIlGenerator.Emit(OpCodes.Call, LightingEffectTypeNamePropertyInfo.SetMethod!);

		// Deserialization: Prepare the loop

		if (deserializationLabels.Count > 0)
		{
			// i = 0;
			deserializeIlGenerator.Emit(OpCodes.Ldc_I4_0);
			deserializeIlGenerator.Emit(OpCodes.Stloc, counterLocal!);

			deserializeIlGenerator.Emit(OpCodes.Ldloc, deserializationPropertyValuesLocal!);
			deserializeIlGenerator.Emit(OpCodes.Ldnull);
			deserializeIlGenerator.Emit(OpCodes.Ceq);
			deserializeIlGenerator.Emit(OpCodes.Brtrue, deserializationLoopEndLabel);

			deserializeIlGenerator.MarkLabel(deserializationLoopStartLabel);

			// if (i >= propertyValues.Length) goto LoopEnd;
			deserializeIlGenerator.Emit(OpCodes.Ldloc, counterLocal!);
			deserializeIlGenerator.Emit(OpCodes.Ldloc, deserializationPropertyValuesLocal!);
			deserializeIlGenerator.Emit(OpCodes.Ldlen);
			deserializeIlGenerator.Emit(OpCodes.Clt);
			deserializeIlGenerator.Emit(OpCodes.Brfalse, deserializationLoopEndLabel);

			// ref readonly var propertyValue = ref propertyValues[i];
			deserializeIlGenerator.Emit(OpCodes.Ldloc, deserializationPropertyValuesLocal!);
			deserializeIlGenerator.Emit(OpCodes.Ldloc, counterLocal!);
			deserializeIlGenerator.Emit(OpCodes.Ldelema, typeof(PropertyValue));
			deserializeIlGenerator.Emit(OpCodes.Stloc, deserializationPropertyValueLocal!);

			// switch (propertyValue.Index)
			deserializeIlGenerator.Emit(OpCodes.Ldloc, deserializationPropertyValueLocal!);
			deserializeIlGenerator.Emit(OpCodes.Call, PropertyValueIndexPropertyInfo.GetMethod!);
			deserializeIlGenerator.Emit(OpCodes.Switch, deserializationLabels.ToArray());

			deserializeIlGenerator.Emit(OpCodes.Br, deserializationDefaultCaseLabel);
		}

		// Third pass:

		var serializableProperties = new ConfigurablePropertyInformation[serializablePropertyDetails.Count];
		for (int i = 0; i < serializablePropertyDetails.Count; i++)
		{
			var details = serializablePropertyDetails[i];

			if (details.DeserializationLabel is { } label)
			{
				deserializeIlGenerator.MarkLabel(label);
			}

			if (details.DataIndex == -1)
			{
				// lightingEffect.Color = effect.Color;
				serializeIlGenerator.Emit(OpCodes.Ldloc, serializationEffectLocal);
				serializeIlGenerator.Emit(OpCodes.Ldarg_0);
				serializeIlGenerator.Emit(OpCodes.Call, details.Property.GetMethod!);
				// Special-case the color types (only one for now)
				if (details.DataType == GrpcDataType.ColorRgb24)
				{
					serializeIlGenerator.Emit(OpCodes.Call, RgbColorToInt32MethodInfo);

				}
				// All compatible integer types are automatically expanded to int32 on the stack, so no additional conversion is needed.
				serializeIlGenerator.Emit(OpCodes.Call, details.WellKnownProperty!.SetMethod!);
			}
			else if (details.DataIndex == -2)
			{
				// lightingEffect.Speed = effect.Speed;
				serializeIlGenerator.Emit(OpCodes.Ldloc, serializationEffectLocal);
				serializeIlGenerator.Emit(OpCodes.Ldarg_0);
				serializeIlGenerator.Emit(OpCodes.Call, details.Property.GetMethod!);
				serializeIlGenerator.Emit(OpCodes.Conv_U4);
				serializeIlGenerator.Emit(OpCodes.Call, details.WellKnownProperty!.SetMethod!);
			}
			else
			{
				// Serialization:

				// builder.Add(new PropertyValue { Index = index, Value = new DataValue { Storage = effect.Property } });
				serializeIlGenerator.Emit(OpCodes.Ldloc, immutableArrayBuilderLocal!);
				serializeIlGenerator.Emit(OpCodes.Ldloca, propertyValueLocal!);
				serializeIlGenerator.Emit(OpCodes.Initobj, typeof(PropertyValue));
				serializeIlGenerator.Emit(OpCodes.Ldloca, propertyValueLocal!);
				serializeIlGenerator.Emit(OpCodes.Ldc_I4, details.DataIndex);
				serializeIlGenerator.Emit(OpCodes.Call, PropertyValueIndexPropertyInfo.SetMethod!);
				serializeIlGenerator.Emit(OpCodes.Ldloca, propertyValueLocal!);
				serializeIlGenerator.Emit(OpCodes.Newobj, DataValueConstructorInfo);
				serializeIlGenerator.Emit(OpCodes.Dup);
				serializeIlGenerator.Emit(OpCodes.Ldarg_0);
				serializeIlGenerator.Emit(OpCodes.Call, details.Property.GetMethod!);
				switch (details.DataType)
				{
				case GrpcDataType.UInt8:
				case GrpcDataType.UInt16:
				case GrpcDataType.UInt32:
				case GrpcDataType.Boolean:
					serializeIlGenerator.Emit(OpCodes.Conv_U8);
					goto case GrpcDataType.UInt64;
				case GrpcDataType.UInt64:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueUnsignedValuePropertyInfo.SetMethod!);
					break;
				case GrpcDataType.Int8:
				case GrpcDataType.Int16:
				case GrpcDataType.Int32:
					serializeIlGenerator.Emit(OpCodes.Conv_I8);
					goto case GrpcDataType.Int64;
				case GrpcDataType.Int64:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSignedValuePropertyInfo.SetMethod!);
					break;
				case GrpcDataType.Float16:
					serializeIlGenerator.Emit(OpCodes.Call, HalfToSingleMethodInfo);
					goto case GrpcDataType.Float32;
				case GrpcDataType.Float32:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSingleValuePropertyInfo.SetMethod!);
					break;
				case GrpcDataType.Float64:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueDoubleValuePropertyInfo.SetMethod!);
					break;
				case GrpcDataType.String:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueStringValuePropertyInfo.SetMethod!);
					break;
				case GrpcDataType.Guid:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueGuidValuePropertyInfo.SetMethod!);
					break;
				case GrpcDataType.DateTime:
				case GrpcDataType.TimeSpan:
				default:
					// TODO
					throw new NotImplementedException();
				}
				serializeIlGenerator.Emit(OpCodes.Call, PropertyValueValuePropertyInfo.SetMethod!);
				serializeIlGenerator.Emit(OpCodes.Ldloc, propertyValueLocal!);
				serializeIlGenerator.Emit(OpCodes.Call, ImmutableArrayBuilderAddMethodInfo);

				// Deserialization:

				if (details.DeserializationLocal is null)
				{
					// ref effect // reference to the effect struct for setting the property later on.
					deserializeIlGenerator.Emit(OpCodes.Ldloca, deserializationEffectLocal);
				}

				// property.Value // reference the property value for assigning the property afterwards
				deserializeIlGenerator.Emit(OpCodes.Ldloc, deserializationPropertyValueLocal!);
				deserializeIlGenerator.Emit(OpCodes.Call, PropertyValueValuePropertyInfo.GetMethod!);

				// Read the field matching the DataType.
				switch (details.DataType)
				{
				case GrpcDataType.UInt8:
				case GrpcDataType.UInt16:
				case GrpcDataType.UInt32:
				case GrpcDataType.Boolean:
				case GrpcDataType.UInt64:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueUnsignedValuePropertyInfo.GetMethod!);
					break;
				case GrpcDataType.Int8:
				case GrpcDataType.Int16:
				case GrpcDataType.Int32:
				case GrpcDataType.Int64:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSignedValuePropertyInfo.GetMethod!);
					break;
				case GrpcDataType.Float16:
				case GrpcDataType.Float32:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSingleValuePropertyInfo.GetMethod!);
					break;
				case GrpcDataType.Float64:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueDoubleValuePropertyInfo.GetMethod!);
					break;
				case GrpcDataType.String:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueStringValuePropertyInfo.GetMethod!);
					break;
				case GrpcDataType.Guid:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueGuidValuePropertyInfo.GetMethod!);
					break;
				case GrpcDataType.DateTime:
				case GrpcDataType.TimeSpan:
				default:
					// TODO
					throw new NotImplementedException();
				}

				EmitConversionToTargetType(deserializeIlGenerator, details.DataType, false);

				// property = value;
				if (details.DeserializationLocal is null)
				{
					deserializeIlGenerator.Emit(OpCodes.Call, details.Property.SetMethod!);
				}
				else
				{
					deserializeIlGenerator.Emit(OpCodes.Stloc, details.DeserializationLocal);
				}

				// propertyWasSet = true;
				if (details.DeserializationConditionalLocal is not null)
				{
					deserializeIlGenerator.Emit(OpCodes.Ldc_I4_1);
					deserializeIlGenerator.Emit(OpCodes.Stloc, details.DeserializationConditionalLocal);
				}
			}

			var displayAttribute = details.Property.GetCustomAttribute<DisplayAttribute>();
			var defaultValueAttribute = details.Property.GetCustomAttribute<DefaultValueAttribute>();
			var rangeAttribute = details.Property.GetCustomAttribute<RangeAttribute>();

			serializableProperties[i] = new()
			{
				Index = details.DataIndex >= 0 ? (uint)details.DataIndex : null,
				Name = details.Property.Name,
				DisplayName = displayAttribute?.Name ?? details.Property.Name,
				Description = displayAttribute?.Description,
				DataType = details.DataType,
				DefaultValue = defaultValueAttribute is not null ? GetValue(details.DataType, defaultValueAttribute.Value) : null,
				MinimumValue = rangeAttribute is not null ? GetValue(details.DataType, rangeAttribute.Minimum) : null,
				MaximumValue = rangeAttribute is not null ? GetValue(details.DataType, rangeAttribute.Maximum) : null,
				EnumerationValues = details.Property.PropertyType.IsEnum ? GetEnumerationValues(details.Property.PropertyType) : ImmutableArray<EnumerationValue>.Empty,
			};
		}

		// Serialization: Complete the method.

		// lightingEffect.ExtendedPropertyValues = builder.DrainToImmutable(); // init-only property assignment
		serializeIlGenerator.Emit(OpCodes.Ldloc, serializationEffectLocal);
		if (immutableArrayBuilderLocal is not null)
		{
			serializeIlGenerator.Emit(OpCodes.Ldloc, immutableArrayBuilderLocal);
			serializeIlGenerator.Emit(OpCodes.Callvirt, ImmutableArrayBuilderDrainToImmutableMethodInfo);
		}
		else
		{
			serializeIlGenerator.Emit(OpCodes.Ldsfld, ImmutableArrayEmptyFieldInfo);
		}
		serializeIlGenerator.Emit(OpCodes.Callvirt, LightingEffectExtendedPropertyValuesPropertyInfo.SetMethod!);
		// return lightingEffect;
		serializeIlGenerator.Emit(OpCodes.Ldloc, serializationEffectLocal);
		serializeIlGenerator.Emit(OpCodes.Ret);

		// Deserialization: Complete the loop.

		if (deserializationLabels.Count > 0)
		{
			// default: break;
			deserializeIlGenerator.MarkLabel(deserializationDefaultCaseLabel);

			// i++;
			deserializeIlGenerator.Emit(OpCodes.Ldloc, counterLocal!);
			deserializeIlGenerator.Emit(OpCodes.Ldc_I4_1);
			deserializeIlGenerator.Emit(OpCodes.Add);
			deserializeIlGenerator.Emit(OpCodes.Stloc, counterLocal!);

			// goto LoopStart;
			deserializeIlGenerator.Emit(OpCodes.Br, deserializationLoopStartLabel);
			deserializeIlGenerator.MarkLabel(deserializationLoopEndLabel);
		}

		// Fourth pass for the constructor case: We need to finally build the object using all locals.
		// This is subdivided in two passes: First, calling the constructor, and second, initializing all remaining properties.
		if (constructorParameters is not null)
		{
			// First sub-pass: Create the instance.
			foreach (var parameter in constructorParameters)
			{
				deserializeIlGenerator.Emit(OpCodes.Ldloc, constructorParameterLocals![parameter.Name!]);
			}
			deserializeIlGenerator.Emit(OpCodes.Newobj, parameterizedConstructor!);
			deserializeIlGenerator.Emit(OpCodes.Stloc, deserializationEffectLocal);

			// Second sub-pass: set every property that was read previously.
			foreach (var details in serializablePropertyDetails)
			{
				if (details.ConstructorParameter is null)
				{
					var skipLabel = deserializeIlGenerator.DefineLabel();
					deserializeIlGenerator.Emit(OpCodes.Ldloc, details.DeserializationConditionalLocal!);
					deserializeIlGenerator.Emit(OpCodes.Brfalse, skipLabel);
					deserializeIlGenerator.Emit(OpCodes.Ldloca, deserializationEffectLocal);
					deserializeIlGenerator.Emit(OpCodes.Ldloc, details.DeserializationLocal!);
					deserializeIlGenerator.Emit(OpCodes.Call, details.Property.SetMethod!);
					deserializeIlGenerator.MarkLabel(skipLabel);
				}
			}
		}

		// Deserialization: Complete the method.

		deserializeIlGenerator.Emit(OpCodes.Ldloc, deserializationEffectLocal);
		deserializeIlGenerator.Emit(OpCodes.Ret);

		// Deserialization: Create the wrapper Serialize method.

		// This method is necessary to convert the effect type to its proper representation before calling the specialized serialize method.
		var unwrapAndSerializeMethod = new DynamicMethod("Serialize", typeof(LightingEffect), new[] { typeof(ILightingEffect) }, effectType);
		var unwrapAndSerializeIlGenerator = unwrapAndSerializeMethod.GetILGenerator();
		unwrapAndSerializeIlGenerator.Emit(OpCodes.Unbox, effectType);
		unwrapAndSerializeIlGenerator.Emit(OpCodes.Call, serializeMethod);
		unwrapAndSerializeIlGenerator.Emit(OpCodes.Ret);

		// Deserialization: Create the DeserializeAndSet method.

		// This method is necessary to "un-generify" the call to SetEffect<TEffect>.
		var deserializeAndSetMethod = new DynamicMethod("DeserializeAndSet", typeof(void), new[] { typeof(LightingService), typeof(Guid), typeof(Guid), typeof(LightingEffect) }, effectType);
		var deserializeAndSetIlGenerator = deserializeAndSetMethod.GetILGenerator();
		var dasEffectLocal = deserializeAndSetIlGenerator.DeclareLocal(effectType);
		deserializeAndSetIlGenerator.Emit(OpCodes.Ldarg_0);
		deserializeAndSetIlGenerator.Emit(OpCodes.Ldarg_1);
		deserializeAndSetIlGenerator.Emit(OpCodes.Ldarg_2);
		deserializeAndSetIlGenerator.Emit(OpCodes.Ldarg_3);
		deserializeAndSetIlGenerator.Emit(OpCodes.Call, deserializeMethod);
		deserializeAndSetIlGenerator.Emit(OpCodes.Stloc, dasEffectLocal);
		deserializeAndSetIlGenerator.Emit(OpCodes.Ldloca, dasEffectLocal);
		deserializeAndSetIlGenerator.Emit(OpCodes.Callvirt, LightingServiceSetEffectMethodInfo.MakeGenericMethod(effectType));
		deserializeAndSetIlGenerator.Emit(OpCodes.Ret);

		string displayName = effectType.GetCustomAttribute<EffectNameAttribute>()?.Name ?? effectType.GetCustomAttribute<DisplayAttribute>()?.Name ?? effectType.Name;

		return new
		(
			new()
			{
				EffectTypeName = effectTypeName,
				EffectDisplayName = displayName,
				Properties = serializableProperties.AsImmutable()
			},
			serializeMethod,
			deserializeMethod,
			unwrapAndSerializeMethod.CreateDelegate<Func<ILightingEffect, LightingEffect>>(),
			deserializeAndSetMethod.CreateDelegate<Action<LightingService, Guid, Guid, LightingEffect>>()
		);
	}

	private static void EmitConversionToTargetType(ILGenerator ilGenerator, GrpcDataType dataType, bool isInt32)
	{
		// Convert the read value into the appropriate type.
		switch (dataType)
		{
		case GrpcDataType.Boolean:
			ilGenerator.Emit(OpCodes.Ldc_I4_0);
			ilGenerator.Emit(OpCodes.Cgt_Un);
			break;
		case GrpcDataType.UInt8:
			ilGenerator.Emit(OpCodes.Conv_U1);
			break;
		case GrpcDataType.Int8:
			ilGenerator.Emit(OpCodes.Conv_I1);
			break;
		case GrpcDataType.UInt16:
			ilGenerator.Emit(OpCodes.Conv_U2);
			break;
		case GrpcDataType.Int16:
			ilGenerator.Emit(OpCodes.Conv_I2);
			break;
		case GrpcDataType.UInt32:
			if (!isInt32) ilGenerator.Emit(OpCodes.Conv_U4);
			break;
		case GrpcDataType.Int32:
			if (!isInt32) ilGenerator.Emit(OpCodes.Conv_I4);
			break;
		case GrpcDataType.Float16:
			ilGenerator.Emit(OpCodes.Call, SingleToHalfMethodInfo);
			break;
		case GrpcDataType.UInt64:
		case GrpcDataType.Int64:
		case GrpcDataType.Float32:
		case GrpcDataType.Float64:
		case GrpcDataType.Guid:
		// TODO
		//case GrpcDataType.TimeSpan:
		//case GrpcDataType.DateTime:
		case GrpcDataType.String:
			break;
		case GrpcDataType.ColorRgb24:
			if (!isInt32) ilGenerator.Emit(OpCodes.Conv_I4);
			ilGenerator.Emit(OpCodes.Call, RgbColorFromInt32MethodInfo);
			break;
		default:
			throw new NotImplementedException();
		}
	}

	private static bool IsSigned(GrpcDataType type)
	{
		switch (type)
		{
		case GrpcDataType.Int8:
		case GrpcDataType.Int16:
		case GrpcDataType.Int32:
		case GrpcDataType.Int64:
		case GrpcDataType.Float16:
		case GrpcDataType.Float32:
		case GrpcDataType.Float64:
			return true;
		default:
			return false;
		}
	}

	private static bool IsColor(GrpcDataType type)
	{
		switch (type)
		{
		case GrpcDataType.ColorGrayscale8:
		case GrpcDataType.ColorGrayscale16:
		case GrpcDataType.ColorRgb24:
		case GrpcDataType.ColorArgb32:
			return true;
		default:
			return false;
		}
	}

	private static bool IsUInt32Compatible(GrpcDataType type)
	{
		switch (type)
		{
		case GrpcDataType.UInt8:
		case GrpcDataType.Int8:
		case GrpcDataType.UInt16:
		case GrpcDataType.Int16:
		case GrpcDataType.UInt32:
		case GrpcDataType.Int32:
		case GrpcDataType.UInt64:
		case GrpcDataType.Int64:
		case GrpcDataType.ColorGrayscale8:
		case GrpcDataType.ColorGrayscale16:
		case GrpcDataType.ColorRgb24:
		case GrpcDataType.ColorArgb32:
			return true;
		default:
			return false;
		}
	}
	private static ImmutableArray<EnumerationValue> GetEnumerationValues(Type type)
		=> EnumerationValuesCache.GetValue(type, GetNonCachedEnumerationValues).AsImmutable();

	private static EnumerationValue[] GetNonCachedEnumerationValues(Type type)
	{
		bool isSigned = Type.GetTypeCode(type) switch
		{
			TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 => true,
			_ => false
		};

		var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public);
		var values = new EnumerationValue[fields.Length];
		for (int i = 0; i < fields.Length; i++)
		{
			var field = fields[i];
			var value = field.GetValue(null);
			string displayName = field.Name;
			string? description = null;
			if (field.GetCustomAttribute<DisplayAttribute>() is { } displayAttribute)
			{
				displayName = displayAttribute.Name ?? displayName;
				description = displayAttribute.Description;
			}
			values[i] = new()
			{
				Value = isSigned ? (ulong)Convert.ToInt64(value) : Convert.ToUInt64(value),
				DisplayName = displayName,
				Description = description,
			};
		}

		return values;
	}

	private static DataValue? GetValue(GrpcDataType dataType, object? value)
	{
		if (value is null) return null;

		switch (dataType)
		{
		case GrpcDataType.UInt8:
		case GrpcDataType.UInt16:
		case GrpcDataType.UInt32:
		case GrpcDataType.UInt64:
			return new() { UnsignedValue = Convert.ToUInt64(value) };
		case GrpcDataType.Int8:
		case GrpcDataType.Int16:
		case GrpcDataType.Int32:
		case GrpcDataType.Int64:
			return new() { SignedValue = Convert.ToInt64(value) };
		case GrpcDataType.Float32:
			return new() { SingleValue = Convert.ToSingle(value) };
		case GrpcDataType.Float64:
			return new() { DoubleValue = Convert.ToDouble(value) };
		case GrpcDataType.Boolean:
			return new() { UnsignedValue = Convert.ToBoolean(value) ? 1U : 0U };
		default: return null;
		}
	}

	private static GrpcDataType GetDataType(Type type)
	{
		switch (Type.GetTypeCode(type))
		{
		case TypeCode.Boolean: return GrpcDataType.Boolean;
		case TypeCode.Byte: return GrpcDataType.UInt8;
		case TypeCode.SByte: return GrpcDataType.Int8;
		case TypeCode.UInt16: return GrpcDataType.UInt16;
		case TypeCode.Int16: return GrpcDataType.Int16;
		case TypeCode.UInt32: return GrpcDataType.UInt32;
		case TypeCode.Int32: return GrpcDataType.Int32;
		case TypeCode.UInt64: return GrpcDataType.UInt64;
		case TypeCode.Int64: return GrpcDataType.Int64;
		case TypeCode.Single: return GrpcDataType.Float32;
		case TypeCode.Double: return GrpcDataType.Float64;
		case TypeCode.String: return GrpcDataType.String;
		default:
			if (type == typeof(RgbColor))
			{
				return GrpcDataType.ColorRgb24;
			}
			else if (type == typeof(TimeSpan))
			{
				return GrpcDataType.TimeSpan;
			}
			else if (type == typeof(DateTime))
			{
				return GrpcDataType.DateTime;
			}
			else if (type == typeof(Half))
			{
				return GrpcDataType.Float16;
			}
			return GrpcDataType.Other;
		}
	}

	//public static ILightingEffect Deserialize()
	//{
	//	return null;
	//}

	public static void DeserializeAndSet(LightingService lightingService, Guid deviceId, Guid zoneId, LightingEffect effect)
		=> GetEffectSerializationDetails(effect.TypeName).DeserializeAndSet(lightingService, deviceId, zoneId, effect);

	public static LightingEffect Serialize(ILightingEffect effect)
		=> GetEffectSerializationDetails(effect.GetType()).Serialize(effect);
}
