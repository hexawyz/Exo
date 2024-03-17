using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.ColorFormats;
using Exo.Contracts;
using Exo.Lighting.Effects;
using SerializerDataType = Exo.Contracts.DataType;

namespace Exo.Service;

/// <summary>This implements custom serialization for lighting effects.</summary>
/// <remarks>This serialization infrastructure is needed for UI management of lighting effects.</remarks>
public static class EffectSerializer
{
	private static readonly MethodInfo LightingServiceInternalSetEffectMethodInfo =
		typeof(ILightingServiceInternal).GetMethod(nameof(ILightingServiceInternal.SetEffect), BindingFlags.Public | BindingFlags.Instance)!;

	private static readonly ConstructorInfo LightingEffectConstructorInfo = typeof(LightingEffect).GetConstructor(Type.EmptyTypes)!;
	private static readonly PropertyInfo LightingEffectEffectIdPropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.EffectId))!;
	private static readonly PropertyInfo LightingEffectColorPropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.Color))!;
	private static readonly PropertyInfo LightingEffectSpeedPropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.Speed))!;
	private static readonly PropertyInfo LightingEffectExtendedPropertyValuesPropertyInfo = typeof(LightingEffect).GetProperty(nameof(LightingEffect.ExtendedPropertyValues))!;

	private static readonly MethodInfo RgbColorToInt32MethodInfo = typeof(RgbColor).GetMethod(nameof(RgbColor.ToInt32))!;
	private static readonly MethodInfo RgbColorFromInt32MethodInfo = typeof(RgbColor).GetMethod(nameof(RgbColor.FromInt32), BindingFlags.Public | BindingFlags.Static)!;

	private static readonly MethodInfo RgbwColorToInt32MethodInfo = typeof(RgbwColor).GetMethod(nameof(RgbwColor.ToInt32))!;
	private static readonly MethodInfo RgbwColorFromInt32MethodInfo = typeof(RgbwColor).GetMethod(nameof(RgbwColor.FromInt32), BindingFlags.Public | BindingFlags.Static)!;

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
	private static readonly PropertyInfo DataValueBytesValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.BytesValue))!;
	private static readonly PropertyInfo DataValueStringValuePropertyInfo = typeof(DataValue).GetProperty(nameof(DataValue.StringValue))!;

	private static readonly FieldInfo ImmutableArrayEmptyFieldInfo =
		typeof(ImmutableArray<PropertyValue>).GetField(nameof(ImmutableArray<PropertyValue>.Empty), BindingFlags.Public | BindingFlags.Static)!;
	private static readonly MethodInfo ImmutableArrayCreateBuilderMethodInfo =
		typeof(ImmutableArray).GetMethod(nameof(ImmutableArray.CreateBuilder), BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)!;
	private static readonly MethodInfo ImmutableArrayCreateBuilderOfPropertyValueMethodInfo =
		typeof(ImmutableArray)
		.GetMethod(nameof(ImmutableArray.CreateBuilder), BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)!
		.MakeGenericMethod(typeof(PropertyValue));
	private static readonly PropertyInfo ImmutableArrayItemsPropertyInfo = typeof(ImmutableArray<PropertyValue>).GetProperty("Items")!;

	private static readonly MethodInfo ImmutableArrayBuilderAddMethodInfo = typeof(ImmutableArray<PropertyValue>.Builder).GetMethod(nameof(ImmutableArray<PropertyValue>.Builder.Add))!;
	private static readonly MethodInfo ImmutableArrayBuilderDrainToImmutableMethodInfo =
		typeof(ImmutableArray<PropertyValue>.Builder).GetMethod(nameof(ImmutableArray<PropertyValue>.Builder.DrainToImmutable))!;

	private static readonly MethodInfo UnsafeAsMethodInfo =
		typeof(Unsafe)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == nameof(Unsafe.As) && m.GetGenericArguments() is { Length: 2 })
			.Single();

	private static readonly MethodInfo MemoryMarshalCastReadOnlySpanMethodInfo =
		typeof(MemoryMarshal)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == nameof(MemoryMarshal.Cast) && m.GetGenericArguments() is { Length: 2 } && m.GetParameters() is { Length: 1 } p && p[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
			.Single();

	private static readonly MethodInfo MemoryMarshalGetReferenceReadOnlySpanMethodInfo =
		typeof(MemoryMarshal)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == nameof(MemoryMarshal.GetReference) && m.GetGenericArguments() is { Length: 1 } && m.GetParameters() is { Length: 1 } p && p[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
			.Single();

	private static readonly MethodInfo MemoryMarshalCreateReadOnlySpanMethodInfo =
		typeof(MemoryMarshal)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == nameof(MemoryMarshal.CreateReadOnlySpan) && m.GetGenericArguments() is { Length: 1 })
			.Single();

	private static readonly MethodInfo ByteArrayAsSpanMethodInfo =
		typeof(MemoryExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == nameof(MemoryExtensions.AsSpan) && m.GetGenericArguments() is { Length: 1 } a && m.GetParameters() is { Length: 1 } p && p[0].ParameterType.IsArray && p[0].ParameterType.GetElementType() == a[0])
			.Single();

	private static readonly MethodInfo SpanAsReadOnlySpanMethodInfo =
		typeof(Span<byte>)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "op_Implicit" && m.ReturnType == typeof(ReadOnlySpan<byte>) && m.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof(Span<byte>))
			.Single();

	private static readonly MethodInfo UnsafeAsPropertyValueArrayMethodInfo =
		UnsafeAsMethodInfo.MakeGenericMethod(typeof(ImmutableArray<PropertyValue>), typeof(PropertyValue[]));

	private static readonly MethodInfo ReadOnlySpanBytesToArrayMethodInfo =
		typeof(ReadOnlySpan<byte>)
			.GetMethod(nameof(ReadOnlySpan<byte>.ToArray), BindingFlags.Public | BindingFlags.Instance)!;

	private static readonly ConstructorInfo GuidConstructorInfo =
		typeof(Guid)
			.GetConstructor([typeof(uint), typeof(ushort), typeof(ushort), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte), typeof(byte)])!;

	private sealed class LightingEffectSerializationDetails
	{
		public LightingEffectSerializationDetails
		(
			LightingEffectInformation effectInformation,
			MethodInfo serializeMethod,
			MethodInfo deserializeMethod,
			Func<ILightingEffect, LightingEffect> serialize,
			Action<LightingService, Guid, Guid, LightingEffect> deserializeAndSet,
			Action<LightingService, Guid, Guid, LightingEffect> deserializeAndRestore
		)
		{
			EffectInformation = effectInformation;
			SerializeMethod = serializeMethod;
			DeserializeMethod = deserializeMethod;
			Serialize = serialize;
			DeserializeAndSet = deserializeAndSet;
			DeserializeAndRestore = deserializeAndRestore;
		}

		public LightingEffectInformation EffectInformation { get; }


		// Both serialization methods are strongly typed and can't be exposed as-is in a delegate.
		// We'll keep the reference to the instances here, but they will only be used internally in wrapping methods exposed as delegates.

		// Signature: LightingEffect Serialize(in TEffect effect)
		public MethodInfo SerializeMethod { get; }

		// Signature: TEffect Deserialize(LightingEffect effect)
		public MethodInfo DeserializeMethod { get; }

		public Func<ILightingEffect, LightingEffect> Serialize { get; }

		public Action<LightingService, Guid, Guid, LightingEffect> DeserializeAndSet { get; }

		public Action<LightingService, Guid, Guid, LightingEffect> DeserializeAndRestore { get; }
	}

	private static readonly ConditionalWeakTable<Type, LightingEffectSerializationDetails> EffectInformationByTypeCache = new();
	private static readonly ConditionalWeakTable<Type, EnumerationValue[]> EnumerationValuesCache = new();

	private static readonly ConcurrentDictionary<Guid, WeakReference<LightingEffectSerializationDetails>> EffectInformationByTypeNameCache = new();

	private static LightingEffectSerializationDetails GetEffectSerializationDetails(Guid effectTypeId)
		=> EffectInformationByTypeNameCache.TryGetValue(effectTypeId, out var wr) && wr.TryGetTarget(out var d) ?
			d :
			throw new KeyNotFoundException($"Information for the type {effectTypeId} was not found.");

	public static LightingEffectInformation GetEffectInformation(Guid effectTypeId)
		=> GetEffectSerializationDetails(effectTypeId).EffectInformation;

	public static LightingEffectInformation GetEffectInformation(Type effectType)
		=> GetEffectSerializationDetails(effectType).EffectInformation;

	private static LightingEffectSerializationDetails GetEffectSerializationDetails(Type effectType)
		=> EffectInformationByTypeCache.GetValue(effectType, GetNonCachedEffectDetailsAndUpdateNameCache);

	private static LightingEffectSerializationDetails GetNonCachedEffectDetailsAndUpdateNameCache(Type effectType)
	{
		var details = GetNonCachedEffectDetails(effectType);

		EffectInformationByTypeNameCache.GetOrAdd(details.EffectInformation.EffectId, _ => new(null!)).SetTarget(details);

		return details;
	}

	private readonly struct SerializationPropertyDetails
	{
		public required readonly MemberInfo FieldOrProperty { get; init; }
		public required readonly Type RuntimeType { get; init; }
		public required readonly int DataIndex { get; init; }
		public required readonly SerializerDataType DataType { get; init; }
		public required readonly byte FixedArrayLength { get; init; }
		public readonly Label? DeserializationLabel { get; init; }
		public readonly LocalBuilder? DeserializationLocal { get; init; }
		public readonly LocalBuilder? DeserializationConditionalLocal { get; init; }
		public readonly ParameterInfo? ConstructorParameter { get; init; }
		public readonly PropertyInfo? WellKnownProperty { get; init; }
	}

	private static LightingEffectSerializationDetails GetNonCachedEffectDetails(Type effectType)
	{
		Span<byte> typeIdBytes = stackalloc byte[16];

		string effectTypeName = effectType.FullName!;

		var typeId = effectType.GetCustomAttribute<TypeIdAttribute>()?.Value ?? throw new InvalidOperationException($"The effect type {effectType} does not specify a type ID.");
		typeId.TryWriteBytes(typeIdBytes);

		var members = effectType.GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(m => m.MemberType is MemberTypes.Field or MemberTypes.Property).ToArray();

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
		var serializeMethod = new DynamicMethod("Serialize", typeof(LightingEffect), new[] { effectType.MakeByRefType() }, effectType, true);
		var deserializeMethod = new DynamicMethod("Deserialize", effectType, new[] { typeof(LightingEffect) }, effectType, true);

		var serializeIlGenerator = serializeMethod.GetILGenerator();
		var deserializeIlGenerator = deserializeMethod.GetILGenerator();

		// Try to optimize the default capacity of the lists that will hold property details.
		// We want to avoid allocations if we can, so we shouldn't force array creation if there are less properties than the default list capacity,
		// but otherwise, force a single allocation able to fit the number of properties.
		int listInitialCapacity = members.Length > 4 ? members.Length : 0;

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

		for (int i = 0; i < members.Length; i++)
		{
			var member = members[i];
			var property = member as PropertyInfo;
			var field = member as FieldInfo;
			ParameterInfo? parameter = null;
			Type propertyType = field is not null ? field.FieldType : property!.PropertyType;

			var (dataType, fixedArrayLength) = GetDataType(propertyType);

			if (dataType == SerializerDataType.Other) throw new InvalidOperationException($"Could not map {propertyType} for property {member.Name} of effect {effectType}.");

			// Enforce that fixed length array must be fields and not properties.
			// This is both for performance and convenience reasons, as ref properties in structures are not safe,
			// and fixed length arrays returned by value are not convenient to work with in addition to incur a performance cost.
			if (IsFixedLengthArray(dataType) && field is null) throw new InvalidOperationException($"Members of type {propertyType} must be properties.");

			if (field is null && property!.GetMethod is null)
			{
				throw new InvalidOperationException($"The property {member.Name} of effect {effectType} does not have a getter.");
			}
			else if (constructorParametersByName?.TryGetValue(member.Name, out parameter) == true)
			{
				constructorParametersByName.Remove(member.Name);
				if (parameter.ParameterType != (fixedArrayLength > 0 ? propertyType.MakeByRefType() : propertyType))
				{
					throw new InvalidOperationException($"There is a type mismatch between the constructor and the property {member.Name} of effect {effectType}.");
				}
			}
			else if (field is not null ? field.IsInitOnly : property!.SetMethod is null)
			{
				throw new InvalidOperationException
				(
					field is not null ?
					$"The property {member.Name} of effect {effectType} does not have a setter." :
					$"The field {member.Name} of effect {effectType} is read-only."
				);
			}

			int dataIndex;
			Label? deserializationLabel = null;
			LocalBuilder? deserializationLocal = null;
			LocalBuilder? deserializationConditionalLocal = null;

			// Properties will need a local to be deserialized if the constructor is used.
			if (constructorParameters is not null)
			{
				deserializationLocal = deserializeIlGenerator.DeclareLocal(fixedArrayLength > 0 ? propertyType.MakeByRefType() : propertyType);
				if (parameter is not null)
				{
					constructorParameterLocals!.Add(parameter.Name!, deserializationLocal);
				}
			}

			PropertyInfo? wellKnownProperty = null;

			if (member.Name == nameof(LightingEffect.Color) && IsUInt32Compatible(dataType))
			{
				dataIndex = -1;
				wellKnownProperty = LightingEffectColorPropertyInfo;
			}
			else if (member.Name == nameof(LightingEffect.Speed) && IsUInt32Compatible(dataType))
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
				FieldOrProperty = member,
				RuntimeType = propertyType,
				DataIndex = dataIndex,
				DataType = dataType,
				FixedArrayLength = fixedArrayLength,
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

			EmitConversionToTargetType(deserializeIlGenerator, details.DataType, details.RuntimeType, true);

			if (details.DeserializationLocal is null)
			{
				if (details.FieldOrProperty is FieldInfo field)
				{
					deserializeIlGenerator.Emit(OpCodes.Stfld, field);
				}
				else if (details.FieldOrProperty is PropertyInfo property)
				{
					deserializeIlGenerator.Emit(OpCodes.Call, property.SetMethod!);
				}
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

		LocalBuilder? immutableArrayBuilderLocal = null;
		LocalBuilder? propertyValueLocal = null;
		LocalBuilder? rgb24Local = null;
		LocalBuilder? rgbw32Local = null;

		// We only need an ImmutableArray<>.Builder when there are non-well-known properties.
		if (deserializationLabels.Count > 0)
		{
			immutableArrayBuilderLocal = serializeIlGenerator.DeclareLocal(typeof(ImmutableArray<PropertyValue>.Builder));
			propertyValueLocal = serializeIlGenerator.DeclareLocal(typeof(PropertyValue));

			serializeIlGenerator.Emit(OpCodes.Call, ImmutableArrayCreateBuilderOfPropertyValueMethodInfo);
			serializeIlGenerator.Emit(OpCodes.Stloc, immutableArrayBuilderLocal);

			serializeIlGenerator.Emit(OpCodes.Ldloca, propertyValueLocal);
			serializeIlGenerator.Emit(OpCodes.Initobj, typeof(PropertyValue));
		}

		// lightingEffect = new()
		serializeIlGenerator.Emit(OpCodes.Newobj, LightingEffectConstructorInfo);

		// lightingEffect.EffectId = new Guid(a, b, c, d, e, f, g, h, i, j, k); // init-only property assignment
		serializeIlGenerator.Emit(OpCodes.Dup); // From now on, the first element on the stack must always be the effect local. ("lightingEffect")
		serializeIlGenerator.Emit(OpCodes.Ldc_I4, BinaryPrimitives.ReadUInt32LittleEndian(typeIdBytes));
		serializeIlGenerator.Emit(OpCodes.Ldc_I4, BinaryPrimitives.ReadUInt16LittleEndian(typeIdBytes[4..]));
		serializeIlGenerator.Emit(OpCodes.Ldc_I4, BinaryPrimitives.ReadUInt16LittleEndian(typeIdBytes[6..]));
		for (int i = 8; i < typeIdBytes.Length; i++)
		{
			serializeIlGenerator.Emit(OpCodes.Ldc_I4, (ushort)typeIdBytes[i]);
		}
		serializeIlGenerator.Emit(OpCodes.Newobj, GuidConstructorInfo);
		serializeIlGenerator.Emit(OpCodes.Callvirt, LightingEffectEffectIdPropertyInfo.SetMethod!);

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

			var field = details.FieldOrProperty as FieldInfo;
			var property = details.FieldOrProperty as PropertyInfo;

			if (details.DataIndex == -1)
			{
				// lightingEffect.Color = effect.Color;
				serializeIlGenerator.Emit(OpCodes.Dup); // lightingEffect
				serializeIlGenerator.Emit(OpCodes.Ldarg_0);
				if (field is not null)
				{
					serializeIlGenerator.Emit(OpCodes.Ldfld, field);
				}
				else
				{
					serializeIlGenerator.Emit(OpCodes.Call, property!.GetMethod!);
				}
				// Special-case the color types (only one for now)
				if (details.DataType == SerializerDataType.ColorRgb24)
				{
					serializeIlGenerator.Emit(OpCodes.Stloc, rgb24Local ??= serializeIlGenerator.DeclareLocal(typeof(RgbColor)));
					serializeIlGenerator.Emit(OpCodes.Ldloca, rgb24Local);
					serializeIlGenerator.Emit(OpCodes.Call, RgbColorToInt32MethodInfo);
				}
				else if (details.DataType == SerializerDataType.ColorRgbw32)
				{
					serializeIlGenerator.Emit(OpCodes.Stloc, rgbw32Local ??= serializeIlGenerator.DeclareLocal(typeof(RgbwColor)));
					serializeIlGenerator.Emit(OpCodes.Ldloca, rgbw32Local);
					serializeIlGenerator.Emit(OpCodes.Call, RgbwColorToInt32MethodInfo);
				}
				// All compatible integer types are automatically expanded to int32 on the stack, so no additional conversion is needed.
				serializeIlGenerator.Emit(OpCodes.Call, details.WellKnownProperty!.SetMethod!);
			}
			else if (details.DataIndex == -2)
			{
				// lightingEffect.Speed = effect.Speed;
				serializeIlGenerator.Emit(OpCodes.Dup); // lightingEffect
				serializeIlGenerator.Emit(OpCodes.Ldarg_0);
				if (field is not null)
				{
					serializeIlGenerator.Emit(OpCodes.Ldfld, field);
				}
				else
				{
					serializeIlGenerator.Emit(OpCodes.Call, property!.GetMethod!);
				}
				serializeIlGenerator.Emit(OpCodes.Conv_U4);
				serializeIlGenerator.Emit(OpCodes.Call, details.WellKnownProperty!.SetMethod!);
			}
			else
			{
				// Serialization:

				// builder.Add(new PropertyValue { Index = index, Value = new DataValue { Storage = effect.Property } });
				serializeIlGenerator.Emit(OpCodes.Ldloc, immutableArrayBuilderLocal!);
				serializeIlGenerator.Emit(OpCodes.Ldloca, propertyValueLocal!);
				serializeIlGenerator.Emit(OpCodes.Dup); // propertyValueLocal&
				serializeIlGenerator.Emit(OpCodes.Ldc_I4, details.DataIndex);
				serializeIlGenerator.Emit(OpCodes.Call, PropertyValueIndexPropertyInfo.SetMethod!);
				serializeIlGenerator.Emit(OpCodes.Newobj, DataValueConstructorInfo);
				serializeIlGenerator.Emit(OpCodes.Dup); // new DataValue()
				serializeIlGenerator.Emit(OpCodes.Ldarg_0);
				if (field is not null)
				{
					serializeIlGenerator.Emit(details.FixedArrayLength > 0 ? OpCodes.Ldflda : OpCodes.Ldfld, field);
				}
				else
				{
					serializeIlGenerator.Emit(OpCodes.Call, property!.GetMethod!);
				}
				switch (details.DataType)
				{
				case SerializerDataType.UInt8:
				case SerializerDataType.UInt16:
				case SerializerDataType.UInt32:
				case SerializerDataType.Boolean:
					serializeIlGenerator.Emit(OpCodes.Conv_U8);
					goto case SerializerDataType.UInt64;
				case SerializerDataType.UInt64:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueUnsignedValuePropertyInfo.SetMethod!);
					break;
				case SerializerDataType.Int8:
				case SerializerDataType.Int16:
				case SerializerDataType.Int32:
					serializeIlGenerator.Emit(OpCodes.Conv_I8);
					goto case SerializerDataType.Int64;
				case SerializerDataType.Int64:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSignedValuePropertyInfo.SetMethod!);
					break;
				case SerializerDataType.Float16:
					serializeIlGenerator.Emit(OpCodes.Call, HalfToSingleMethodInfo);
					goto case SerializerDataType.Float32;
				case SerializerDataType.Float32:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSingleValuePropertyInfo.SetMethod!);
					break;
				case SerializerDataType.Float64:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueDoubleValuePropertyInfo.SetMethod!);
					break;
				case SerializerDataType.String:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueStringValuePropertyInfo.SetMethod!);
					break;
				case SerializerDataType.Guid:
					serializeIlGenerator.Emit(OpCodes.Callvirt, DataValueGuidValuePropertyInfo.SetMethod!);
					break;
				case SerializerDataType.ColorRgb24:
					serializeIlGenerator.Emit(OpCodes.Stloc, rgb24Local ??= serializeIlGenerator.DeclareLocal(typeof(RgbColor)));
					serializeIlGenerator.Emit(OpCodes.Ldloca, rgb24Local);
					serializeIlGenerator.Emit(OpCodes.Call, RgbColorToInt32MethodInfo);
					goto case SerializerDataType.UInt32;
				case SerializerDataType.ColorRgbw32:
					serializeIlGenerator.Emit(OpCodes.Stloc, rgbw32Local ??= serializeIlGenerator.DeclareLocal(typeof(RgbwColor)));
					serializeIlGenerator.Emit(OpCodes.Ldloca, rgbw32Local);
					serializeIlGenerator.Emit(OpCodes.Call, RgbwColorToInt32MethodInfo);
					goto case SerializerDataType.UInt32;
				case SerializerDataType.ArrayOfColorRgb24:
				case SerializerDataType.ArrayOfColorRgbw32:
					var elementType = details.RuntimeType.GetGenericArguments()[0];
					serializeIlGenerator.Emit(OpCodes.Call, UnsafeAsMethodInfo.MakeGenericMethod(details.RuntimeType, elementType));
					serializeIlGenerator.Emit(OpCodes.Ldc_I4, (int)details.FixedArrayLength);
					serializeIlGenerator.Emit(OpCodes.Call, MemoryMarshalCreateReadOnlySpanMethodInfo.MakeGenericMethod(elementType));
					serializeIlGenerator.Emit(OpCodes.Call, MemoryMarshalCastReadOnlySpanMethodInfo.MakeGenericMethod(elementType, typeof(byte)));
					serializeIlGenerator.Emit(OpCodes.Call, ReadOnlySpanBytesToArrayMethodInfo);
					serializeIlGenerator.Emit(OpCodes.Call, DataValueBytesValuePropertyInfo.SetMethod!);
					break;
				case SerializerDataType.ColorGrayscale8:
				case SerializerDataType.ColorGrayscale16:
				case SerializerDataType.ColorArgb32:
				case SerializerDataType.ArrayOfColorGrayscale8:
				case SerializerDataType.ArrayOfColorGrayscale16:
				case SerializerDataType.ArrayOfColorArgb32:
				case SerializerDataType.DateTime:
				case SerializerDataType.TimeSpan:
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
				case SerializerDataType.UInt8:
				case SerializerDataType.UInt16:
				case SerializerDataType.UInt32:
				case SerializerDataType.Boolean:
				case SerializerDataType.UInt64:
				case SerializerDataType.ColorRgb24:
				case SerializerDataType.ColorRgbw32:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueUnsignedValuePropertyInfo.GetMethod!);
					break;
				case SerializerDataType.Int8:
				case SerializerDataType.Int16:
				case SerializerDataType.Int32:
				case SerializerDataType.Int64:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSignedValuePropertyInfo.GetMethod!);
					break;
				case SerializerDataType.Float16:
				case SerializerDataType.Float32:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueSingleValuePropertyInfo.GetMethod!);
					break;
				case SerializerDataType.Float64:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueDoubleValuePropertyInfo.GetMethod!);
					break;
				case SerializerDataType.String:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueStringValuePropertyInfo.GetMethod!);
					break;
				case SerializerDataType.Guid:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueGuidValuePropertyInfo.GetMethod!);
					break;
				case SerializerDataType.ArrayOfColorRgb24:
				case SerializerDataType.ArrayOfColorRgbw32:
					deserializeIlGenerator.Emit(OpCodes.Callvirt, DataValueBytesValuePropertyInfo.GetMethod!);
					break;
				case SerializerDataType.DateTime:
				case SerializerDataType.TimeSpan:
				default:
					// TODO
					throw new NotImplementedException();
				}

				EmitConversionToTargetType(deserializeIlGenerator, details.DataType, details.RuntimeType, false);

				// property = value;
				if (details.DeserializationLocal is null)
				{
					if (field is not null)
					{
						// Fixed-array must first be dereferenced in order to be copied into the target.
						// NB: We could use ReadOnlySpan<>.CopyTo, but it would be more complex and in the end, produces mostly the same IL as relying on the dumb framework stuff.
						// In the end, all we want to do, and the best we can do, is still copying the data into the target.
						if (IsFixedLengthArray(details.DataType))
						{
							deserializeIlGenerator.Emit(OpCodes.Ldobj, details.RuntimeType);
						}
						deserializeIlGenerator.Emit(OpCodes.Stfld, field);
					}
					else
					{
						deserializeIlGenerator.Emit(OpCodes.Call, property!.SetMethod!);
					}
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

			var displayAttribute = details.FieldOrProperty.GetCustomAttribute<DisplayAttribute>();
			var defaultValueAttribute = details.FieldOrProperty.GetCustomAttribute<DefaultValueAttribute>();
			var rangeAttribute = details.FieldOrProperty.GetCustomAttribute<RangeAttribute>();

			serializableProperties[i] = new()
			{
				Index = details.DataIndex >= 0 ? (uint)details.DataIndex : null,
				Name = details.FieldOrProperty.Name,
				DisplayName = displayAttribute?.Name ?? details.FieldOrProperty.Name,
				Description = displayAttribute?.Description,
				DataType = details.DataType,
				DefaultValue = defaultValueAttribute is not null ? GetValue(details.DataType, defaultValueAttribute.Value) : null,
				MinimumValue = rangeAttribute is not null ? GetValue(details.DataType, rangeAttribute.Minimum) : null,
				MaximumValue = rangeAttribute is not null ? GetValue(details.DataType, rangeAttribute.Maximum) : null,
				EnumerationValues = details.RuntimeType.IsEnum ? GetEnumerationValues(details.RuntimeType) : [],
				ArrayLength = details.FixedArrayLength,
			};
		}

		// Serialization: Complete the method.

		// lightingEffect.ExtendedPropertyValues = builder.DrainToImmutable(); // init-only property assignment
		if (immutableArrayBuilderLocal is not null)
		{
			serializeIlGenerator.Emit(OpCodes.Dup); // lightingEffect
			serializeIlGenerator.Emit(OpCodes.Ldloc, immutableArrayBuilderLocal);
			serializeIlGenerator.Emit(OpCodes.Callvirt, ImmutableArrayBuilderDrainToImmutableMethodInfo);
			serializeIlGenerator.Emit(OpCodes.Callvirt, LightingEffectExtendedPropertyValuesPropertyInfo.SetMethod!);
		}
		// Disable this part of the code for now, as LightingEffect already provides the default value. (Needs the DUP and CALLVIRT to be moved outside of the if to be re-enabled)
		//else
		//{
		//	serializeIlGenerator.Emit(OpCodes.Ldsfld, ImmutableArrayEmptyFieldInfo);
		//}
		// return lightingEffect;
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
				deserializeIlGenerator.Emit(parameter.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc, constructorParameterLocals![parameter.Name!]);
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
					if (details.FieldOrProperty is FieldInfo field)
					{
						deserializeIlGenerator.Emit(OpCodes.Stloc, field);
					}
					else
					{
						deserializeIlGenerator.Emit(OpCodes.Call, Unsafe.As<PropertyInfo>(details.FieldOrProperty).SetMethod!);
					}
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
		unwrapAndSerializeIlGenerator.Emit(OpCodes.Ldarg_0);
		unwrapAndSerializeIlGenerator.Emit(OpCodes.Unbox, effectType);
		unwrapAndSerializeIlGenerator.Emit(OpCodes.Call, serializeMethod);
		unwrapAndSerializeIlGenerator.Emit(OpCodes.Ret);

		// Deserialization: Create the DeserializeAndSet method.

		// This method is necessary to "un-generify" the call to SetEffect<TEffect>.
		var deserializeAndSetMethod = CreateDeserializeAndSetMethod(effectType, deserializeMethod, false);
		var deserializeAndRestoreMethod = CreateDeserializeAndSetMethod(effectType, deserializeMethod, true);

		return new
		(
			new()
			{
				EffectId = typeId,
				EffectTypeName = effectTypeName,
				Properties = serializableProperties.AsImmutable()
			},
			serializeMethod,
			deserializeMethod,
			unwrapAndSerializeMethod.CreateDelegate<Func<ILightingEffect, LightingEffect>>(),
			deserializeAndSetMethod.CreateDelegate<Action<LightingService, Guid, Guid, LightingEffect>>(),
			deserializeAndRestoreMethod.CreateDelegate<Action<LightingService, Guid, Guid, LightingEffect>>()
		);
	}

	private static DynamicMethod CreateDeserializeAndSetMethod(Type effectType, DynamicMethod deserializeMethod, bool isRestore)
	{
		var method = new DynamicMethod
		(
			isRestore ? "DeserializeAndRestore" : "DeserializeAndSet",
			typeof(void), new[] { typeof(LightingService), typeof(Guid), typeof(Guid), typeof(LightingEffect) },
			effectType
		);

		var ilGenerator = method.GetILGenerator();
		var dasEffectLocal = ilGenerator.DeclareLocal(effectType);
		ilGenerator.Emit(OpCodes.Ldarg_0);
		ilGenerator.Emit(OpCodes.Ldarg_1);
		ilGenerator.Emit(OpCodes.Ldarg_2);
		ilGenerator.Emit(OpCodes.Ldarg_3);
		ilGenerator.Emit(OpCodes.Call, deserializeMethod);
		ilGenerator.Emit(OpCodes.Stloc, dasEffectLocal);
		ilGenerator.Emit(OpCodes.Ldloca, dasEffectLocal);
		ilGenerator.Emit(OpCodes.Ldarg_3);
		ilGenerator.Emit(isRestore ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
		//ilGenerator.Emit(OpCodes.Constrained, typeof(LightingService));
		ilGenerator.Emit(OpCodes.Callvirt, LightingServiceInternalSetEffectMethodInfo.MakeGenericMethod(effectType));
		ilGenerator.Emit(OpCodes.Ret);

		return method;
	}

	private static void EmitConversionToTargetType(ILGenerator ilGenerator, SerializerDataType dataType, Type runtimeType, bool isInt32)
	{
		// Convert the read value into the appropriate type.
		switch (dataType)
		{
		case SerializerDataType.Boolean:
			ilGenerator.Emit(OpCodes.Ldc_I4_0);
			ilGenerator.Emit(OpCodes.Cgt_Un);
			break;
		case SerializerDataType.UInt8:
			ilGenerator.Emit(OpCodes.Conv_U1);
			break;
		case SerializerDataType.Int8:
			ilGenerator.Emit(OpCodes.Conv_I1);
			break;
		case SerializerDataType.UInt16:
			ilGenerator.Emit(OpCodes.Conv_U2);
			break;
		case SerializerDataType.Int16:
			ilGenerator.Emit(OpCodes.Conv_I2);
			break;
		case SerializerDataType.UInt32:
			if (!isInt32) ilGenerator.Emit(OpCodes.Conv_U4);
			break;
		case SerializerDataType.Int32:
			if (!isInt32) ilGenerator.Emit(OpCodes.Conv_I4);
			break;
		case SerializerDataType.Float16:
			ilGenerator.Emit(OpCodes.Call, SingleToHalfMethodInfo);
			break;
		case SerializerDataType.UInt64:
		case SerializerDataType.Int64:
		case SerializerDataType.Float32:
		case SerializerDataType.Float64:
		case SerializerDataType.Guid:
		// TODO
		//case SerializerDataType.TimeSpan:
		//case SerializerDataType.DateTime:
		case SerializerDataType.String:
			break;
		case SerializerDataType.ColorRgb24:
			if (!isInt32) ilGenerator.Emit(OpCodes.Conv_I4);
			ilGenerator.Emit(OpCodes.Call, RgbColorFromInt32MethodInfo);
			break;
		case SerializerDataType.ColorRgbw32:
			if (!isInt32) ilGenerator.Emit(OpCodes.Conv_I4);
			ilGenerator.Emit(OpCodes.Call, RgbwColorFromInt32MethodInfo);
			break;
		case SerializerDataType.ArrayOfColorGrayscale8:
		case SerializerDataType.ArrayOfColorGrayscale16:
		case SerializerDataType.ArrayOfColorRgb24:
		case SerializerDataType.ArrayOfColorRgbw32:
		case SerializerDataType.ArrayOfColorArgb32:
			// Fixed length arrays are serialized into raw byte arrays, which we must transform into a reference to the fixed array type.
			// If assignment by value is required, we will later insert a ldobj instruction.
			ilGenerator.Emit(OpCodes.Call, ByteArrayAsSpanMethodInfo);
			ilGenerator.Emit(OpCodes.Call, SpanAsReadOnlySpanMethodInfo);
			ilGenerator.Emit(OpCodes.Call, MemoryMarshalCastReadOnlySpanMethodInfo.MakeGenericMethod(typeof(byte), runtimeType));
			ilGenerator.Emit(OpCodes.Call, MemoryMarshalGetReferenceReadOnlySpanMethodInfo.MakeGenericMethod(runtimeType));
			break;
		default:
			throw new NotImplementedException();
		}
	}

	private static bool IsSigned(SerializerDataType type)
	{
		switch (type)
		{
		case SerializerDataType.Int8:
		case SerializerDataType.Int16:
		case SerializerDataType.Int32:
		case SerializerDataType.Int64:
		case SerializerDataType.Float16:
		case SerializerDataType.Float32:
		case SerializerDataType.Float64:
			return true;
		default:
			return false;
		}
	}

	private static bool IsColor(SerializerDataType type)
	{
		switch (type)
		{
		case SerializerDataType.ColorGrayscale8:
		case SerializerDataType.ColorGrayscale16:
		case SerializerDataType.ColorRgb24:
		case SerializerDataType.ColorRgbw32:
		case SerializerDataType.ColorArgb32:
			return true;
		default:
			return false;
		}
	}

	private static bool IsFixedLengthArray(SerializerDataType type)
	{
		switch (type)
		{
		case SerializerDataType.ArrayOfColorGrayscale8:
		case SerializerDataType.ArrayOfColorGrayscale16:
		case SerializerDataType.ArrayOfColorRgb24:
		case SerializerDataType.ArrayOfColorRgbw32:
		case SerializerDataType.ArrayOfColorArgb32:
			return true;
		default:
			return false;
		}
	}

	private static bool IsUInt32Compatible(SerializerDataType type)
	{
		switch (type)
		{
		case SerializerDataType.UInt8:
		case SerializerDataType.Int8:
		case SerializerDataType.UInt16:
		case SerializerDataType.Int16:
		case SerializerDataType.UInt32:
		case SerializerDataType.Int32:
		case SerializerDataType.UInt64:
		case SerializerDataType.Int64:
		case SerializerDataType.ColorGrayscale8:
		case SerializerDataType.ColorGrayscale16:
		case SerializerDataType.ColorRgb24:
		case SerializerDataType.ColorRgbw32:
		case SerializerDataType.ColorArgb32:
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

	private static DataValue? GetValue(SerializerDataType dataType, object? value)
	{
		if (value is null) return null;

		switch (dataType)
		{
		case SerializerDataType.UInt8:
		case SerializerDataType.UInt16:
		case SerializerDataType.UInt32:
		case SerializerDataType.UInt64:
			return new() { UnsignedValue = Convert.ToUInt64(value) };
		case SerializerDataType.Int8:
		case SerializerDataType.Int16:
		case SerializerDataType.Int32:
		case SerializerDataType.Int64:
			return new() { SignedValue = Convert.ToInt64(value) };
		case SerializerDataType.Float32:
			return new() { SingleValue = Convert.ToSingle(value) };
		case SerializerDataType.Float64:
			return new() { DoubleValue = Convert.ToDouble(value) };
		case SerializerDataType.Boolean:
			return new() { UnsignedValue = Convert.ToBoolean(value) ? 1U : 0U };
		default: return null;
		}
	}

	private static (SerializerDataType, byte fixedArrayLength) GetDataType(Type type)
	{
		SerializerDataType dataType;
		byte arrayLength;

		switch (Type.GetTypeCode(type))
		{
		case TypeCode.Boolean: dataType = SerializerDataType.Boolean; goto SimpleDataTypeIdentified;
		case TypeCode.Byte: dataType = SerializerDataType.UInt8; goto SimpleDataTypeIdentified;
		case TypeCode.SByte: dataType = SerializerDataType.Int8; goto SimpleDataTypeIdentified;
		case TypeCode.UInt16: dataType = SerializerDataType.UInt16; goto SimpleDataTypeIdentified;
		case TypeCode.Int16: dataType = SerializerDataType.Int16; goto SimpleDataTypeIdentified;
		case TypeCode.UInt32: dataType = SerializerDataType.UInt32; goto SimpleDataTypeIdentified;
		case TypeCode.Int32: dataType = SerializerDataType.Int32; goto SimpleDataTypeIdentified;
		case TypeCode.UInt64: dataType = SerializerDataType.UInt64; goto SimpleDataTypeIdentified;
		case TypeCode.Int64: dataType = SerializerDataType.Int64; goto SimpleDataTypeIdentified;
		case TypeCode.Single: dataType = SerializerDataType.Float32; goto SimpleDataTypeIdentified;
		case TypeCode.Double: dataType = SerializerDataType.Float64; goto SimpleDataTypeIdentified;
		case TypeCode.String: dataType = SerializerDataType.String; goto SimpleDataTypeIdentified;
		default:
			if (type.IsGenericType)
			{
				var genericTypeDefinition = type.GetGenericTypeDefinition();
				if (genericTypeDefinition == typeof(FixedArray5<>)) arrayLength = 5;
				else if (genericTypeDefinition == typeof(FixedArray8<>)) arrayLength = 8;
				else if (genericTypeDefinition == typeof(FixedArray10<>)) arrayLength = 10;
				else if (genericTypeDefinition == typeof(FixedArray16<>)) arrayLength = 16;
				else if (genericTypeDefinition == typeof(FixedArray32<>)) arrayLength = 32;
				else
				{
					dataType = SerializerDataType.Other;
					goto SimpleDataTypeIdentified;
				}

				var elementType = type.GetGenericArguments()[0];
				if (elementType == typeof(RgbColor))
				{
					dataType = SerializerDataType.ArrayOfColorRgb24;
				}
				else if (elementType == typeof(RgbwColor))
				{
					dataType = SerializerDataType.ArrayOfColorRgbw32;
				}
				else
				{
					dataType = SerializerDataType.Other;
					goto SimpleDataTypeIdentified;
				}
				goto DataTypeIdentified;
			}
			else if (type == typeof(RgbColor))
			{
				dataType = SerializerDataType.ColorRgb24;
				goto SimpleDataTypeIdentified;
			}
			else if (type == typeof(RgbwColor))
			{
				dataType = SerializerDataType.ColorRgbw32;
				goto SimpleDataTypeIdentified;
			}
			else if (type == typeof(TimeSpan))
			{
				dataType = SerializerDataType.TimeSpan;
				goto SimpleDataTypeIdentified;
			}
			else if (type == typeof(DateTime))
			{
				dataType = SerializerDataType.DateTime;
				goto SimpleDataTypeIdentified;
			}
			else if (type == typeof(Half))
			{
				dataType = SerializerDataType.Float16;
				goto SimpleDataTypeIdentified;
			}
			dataType = SerializerDataType.Other;
			goto SimpleDataTypeIdentified;
		}
	SimpleDataTypeIdentified:;
		arrayLength = 0;
	DataTypeIdentified:;
		return (dataType, arrayLength);
	}

	public static ILightingEffect Deserialize()
	{
		// TODO
		throw new NotImplementedException();
	}

	public static void DeserializeAndSet(LightingService lightingService, Guid deviceId, Guid zoneId, LightingEffect effect)
		=> GetEffectSerializationDetails(effect.EffectId).DeserializeAndSet(lightingService, deviceId, zoneId, effect);

	internal static void DeserializeAndRestore(LightingService lightingService, Guid deviceId, Guid zoneId, LightingEffect effect)
		=> GetEffectSerializationDetails(effect.EffectId).DeserializeAndRestore(lightingService, deviceId, zoneId, effect);

	// TODO: Effects that implement ISingletonLightingEffect should return a cached value.
	public static LightingEffect Serialize(ILightingEffect effect)
		=> GetEffectSerializationDetails(effect.GetType()).Serialize(effect);
}
