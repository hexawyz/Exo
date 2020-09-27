using DeviceTools.DisplayDevices.Mccs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices
{
	public sealed class MonitorCapabilities
	{
		public string Protocol { get; }
		public string Type { get; }
		public string Model { get; }
		public string? MccsVersion { get; }
		public ImmutableArray<DdcCiCommand> SupportedMonitorCommands { get; }
		public ImmutableArray<VcpCommandDefinition> SupportedVcpCommands { get; }

		public MonitorCapabilities(string protocol, string type, string model, string? mccsVersion, ImmutableArray<DdcCiCommand> supportedMonitorCommands, ImmutableArray<VcpCommandDefinition> supportedVcpCommands)
		{
			Protocol = protocol;
			Type = type;
			Model = model;
			MccsVersion = mccsVersion;
			SupportedMonitorCommands = supportedMonitorCommands;
			SupportedVcpCommands = supportedVcpCommands;
		}

		// Example:
		// (prot(monitor)type(LCD)model(VP2785 series)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 0B 0C 10 12 14(01 02 04 05 06 08 0B 0E 0F 10 11 12 13 15 16 17 18) 16 18 1A 1D(F1 15 0F 11 12 17) 21(01 02 03 04 05) 23(01 02 03) 25(01 02 03) 27(01 02) 2B(01 02) 2D(01 02) 2F(01 02) 31(01 02) 33(01 02) 52 59 5A 5B 5C 5D 5E 60(15 0F 11 12 17) 62 66(01 02) 67(00 01 02 03) 68(01 02 03 04) 6C 6E 70 72(00 78 FB 50 64 78 8C A0) 87 8D 96 97 9B 9C 9D 9E 9F A0 AA AC AE B6 C0 C6 C8 C9 CA(01 02 03 04 05) CC(01 02 03 04 05 06 07 09 0A 0B 0C 0D 12 16) D6(01 04 05) DA(00 02) DB(01 02 03 05 06) DC(00 03 04 30 31 32 33 34 35 36 37 38 39 3A 3B 3C 3D 3E) DF E1(00 19 32 4B 64) E2 E3(00 01 02) E4(01 02) E5(01 02) E7(01 02) E8(01 02 03 04 05) E9(01 02) EA EB EC ED(01 02) EF(01 02) F3(00 01 02 03))mswhql(1)asset_eep(40)mccs_ver(2.2))

		// List of known (supported) tag names
		// NB: The specs indicate that TAG is name + '('. For simplicity, we'll omit the '(' here.
		private static ReadOnlySpan<byte> ProtTagName => new byte[] { (byte)'p', (byte)'r', (byte)'o', (byte)'t' };
		private static ReadOnlySpan<byte> TypeTagName => new byte[] { (byte)'t', (byte)'y', (byte)'p', (byte)'e' };
		private static ReadOnlySpan<byte> ModelTagName => new byte[] { (byte)'m', (byte)'o', (byte)'d', (byte)'e', (byte)'l' };
		private static ReadOnlySpan<byte> CmdsTagName => new byte[] { (byte)'c', (byte)'m', (byte)'d', (byte)'s' };
		private static ReadOnlySpan<byte> VcpTagName => new byte[] { (byte)'v', (byte)'c', (byte)'p' };
		private static ReadOnlySpan<byte> VcpNameTagName => new byte[] { (byte)'v', (byte)'c', (byte)'p', (byte)'n', (byte)'a', (byte)'m', (byte)'e' };
		private static ReadOnlySpan<byte> MccsVerTagName => new byte[] { (byte)'m', (byte)'c', (byte)'c', (byte)'s', (byte)'_', (byte)'v', (byte)'e', (byte)'r' };

		private static ReadOnlySpan<byte> WhiteSpace => new byte[] { (byte)'\t', (byte)'\n', (byte)'\r', (byte)' ' };
		private static ReadOnlySpan<byte> WhiteSpaceOrParentheses => new byte[] { (byte)'\t', (byte)'\n', (byte)'\r', (byte)' ', (byte)'(', (byte)')' };
		private static ReadOnlySpan<byte> Parentheses => new byte[] { (byte)'(', (byte)')' };

		private const byte OpeningParenthesis = (byte)'(';
		private const byte ClosingParenthesis = (byte)')';

		// The syntax of the capabilities string is actually quite simple.
		// The global capabilities must be enclosed by parentheses. (Even though VESA gives an example without parentheses; but Windows 7 would not parse it so we should be safe)
		// Inside parentheses is a <CapString>, which is one or more sequences of <String> or <Tag><CapString>")" (optionally separated by spaces; required to delimit two <String>)
		// Tag is <String>"(", and String is any sequence of characters that are not whitespace (and not parentheses, obviously)

		public static bool TryParse(ReadOnlySpan<byte> capabilitiesString, out MonitorCapabilities? capabilities)
		{
			// The spec requires prot, type and model, so (prot()type()model()) would be the minimum we allow.
			if (capabilitiesString.Length < 21 || capabilitiesString[0] != '(' || capabilitiesString[^1] != ')') goto Fail;

			return ReadTopLevel(new Parser(capabilitiesString[1..^1]), out capabilities);

		Fail:;
			return Failure(out capabilities);
		}

		private static bool ReadTopLevel(Parser parser, out MonitorCapabilities? capabilities)
		{
			// NB: The current parser considers all data to always be bytes.
			// Technically, VCP codes could be 2 bytes long (using the "reserved" code pages feature),
			// and some non continuous values should be more than two bytes long. (e.g. Color Preset with its accuracy byte)
			// But at the moment, I don't have any live examples of that, so the parser will be restricted to bytes.

			string? prot = null;
			string? type = null;
			string? model = null;
			string? mccsVersion = null;
			ImmutableArray<DdcCiCommand> commands = default;
			List<(byte VcpCode, ImmutableArray<byte> NonContinuousValues)>? vcpCodes = null;
			Dictionary<byte, (string Name, ImmutableArray<string> ValueNames)>? vcpNames = null;

			parser.SkipWhitespace();
			while (parser.ConsumeString() is var @string and { IsEmpty: false })
			{
				if (parser.TryConsume(OpeningParenthesis))
				{
					// prot(), type() model() and mccs_ver() will eat everything inside as a single string… not very "spec compliant", but also, it shouldn't contain subtags, so… 🤷
					if (AsciiIgnoreCaseEquals(@string, ProtTagName))
					{
						if (prot is not null || !TryConvertToString(parser.ConsumeUntilAny(Parentheses), out prot) || !parser.TryConsume(ClosingParenthesis))
						{
							goto Fail;
						}
					}
					else if (AsciiIgnoreCaseEquals(@string, TypeTagName))
					{
						if (type is not null || !TryConvertToString(parser.ConsumeUntilAny(Parentheses), out type) || !parser.TryConsume(ClosingParenthesis))
						{
							goto Fail;
						}
					}
					else if (AsciiIgnoreCaseEquals(@string, ModelTagName))
					{
						if (model is not null || !TryConvertToString(parser.ConsumeUntilAny(Parentheses), out model) || !parser.TryConsume(ClosingParenthesis))
						{
							goto Fail;
						}
					}
					else if (prot is null || type is null || model is null)
					{
						// The spec requires that prot, type and model be the three first tags specified. (It does not explicitly indicate that they should be in that order, though)
						// The spec also requires that the occur within the first 64 bytes, but that constraint is annoying to add. Should it be added too ?
						goto Fail;
					}
					// cmds should only be (hex) strings
					else if (AsciiIgnoreCaseEquals(@string, CmdsTagName))
					{
						if (!commands.IsDefault || !ParseCmdsTag(ref parser, out commands))
						{
							goto Fail;
						}
					}
					// vcp is the main dish here. It will requires special handling because some of the predefined VCP codes have specific metadata to extract.
					else if (AsciiIgnoreCaseEquals(@string, VcpTagName))
					{
						if (vcpCodes is not null || !ParseVcpTag(ref parser, out vcpCodes))
						{
							goto Fail;
						}
					}
					else if (AsciiIgnoreCaseEquals(@string, VcpNameTagName))
					{
						if (vcpNames is not null || !ParseVcpNameTag(ref parser, out vcpNames))
						{
							goto Fail;
						}
					}
					else if (AsciiIgnoreCaseEquals(@string, MccsVerTagName))
					{
						if (mccsVersion is not null || !TryConvertToString(parser.ConsumeUntilAny(Parentheses), out mccsVersion) || !parser.TryConsume(ClosingParenthesis))
						{
							goto Fail;
						}
					}
					else if (!ConsumeUnknownTag(ref parser))
					{
						goto Fail;
					}
				}

				parser.SkipWhitespace();
			}

			if (parser.IsEndOfLine() && prot is not null && type is not null && model is not null)
			{
				ImmutableArray<VcpCommandDefinition> vcpCommandDefinitions = default;

				if (vcpCodes is { Count: > 0 })
				{
					var vcpCommandDefinitionsBuilder = ImmutableArray.CreateBuilder<VcpCommandDefinition>(vcpCodes.Count);

					foreach (var vcpCode in vcpCodes)
					{
						((VcpCode)vcpCode.VcpCode).TryGetNameAndCategory(out var name, out var category);

						if (vcpNames is not null && vcpNames.TryGetValue(vcpCode.VcpCode, out var names))
						{
							// Maybe we can somehow ignore if there are different number of values and names, but for now, don't handle this.
							if (names.ValueNames.Length != vcpCode.NonContinuousValues.Length) goto Fail;

							if (names.Name is { Length: > 0 })
							{
								name = names.Name;
							}

							if (!vcpCode.NonContinuousValues.IsEmpty)
							{
								var values = ImmutableArray.CreateBuilder<ValueDefinition>(vcpCode.NonContinuousValues.Length);

								for (int i = 0; i < vcpCode.NonContinuousValues.Length; i++)
								{
									values.Add(new ValueDefinition(vcpCode.NonContinuousValues[i], names.ValueNames[i]));
								}

								vcpCommandDefinitionsBuilder.Add
								(
									new VcpCommandDefinition
									(
										vcpCode.VcpCode,
										category,
										name,
										values.MoveToImmutable()
									)
								);
								continue;
							}
						}

						vcpCommandDefinitionsBuilder.Add
						(
							new VcpCommandDefinition
							(
								vcpCode.VcpCode,
								category,
								name,
								ImmutableArray.CreateRange(vcpCode.NonContinuousValues, s => new ValueDefinition(s, null))
							)
						);
					}

					vcpCommandDefinitions = vcpCommandDefinitionsBuilder.MoveToImmutable();
				}

				capabilities = new MonitorCapabilities(prot, type, model, mccsVersion, commands, vcpCommandDefinitions);
				return true;
			}

		Fail:;
			return Failure(out capabilities);
		}

		private static bool ParseCmdsTag(ref Parser parser, out ImmutableArray<DdcCiCommand> commands)
		{
			// The spec mentions that for cmds and vcp, spaces between bytes are optional. Makes the code a little less simple. 😟

			var commandArrayBuilder = ImmutableArray.CreateBuilder<DdcCiCommand>();

			parser.SkipWhitespace();
			while (parser.ConsumeString() is var @string and { IsEmpty: false })
			{
				if ((@string.Length & 1) != 0) goto Fail;

				while (@string.Length > 0)
				{
					if (!TryParseByte(@string, out byte command)) goto Fail;
					@string = @string[2..];
					commandArrayBuilder.Add((DdcCiCommand)command);
				}

				parser.SkipWhitespace();
			}

			if (parser.TryConsume(ClosingParenthesis))
			{
				commands = commandArrayBuilder.Capacity == commandArrayBuilder.Count ?
					commandArrayBuilder.MoveToImmutable() :
					commandArrayBuilder.ToImmutable();
				return true;
			}

		Fail:;
			return Failure(out commands);
		}

		private static bool ParseVcpTag(ref Parser parser, out List<(byte VcpCode, ImmutableArray<byte> NonContinuousValues)>? vcpCodes)
		{
			// The spec mentions that for cmds and vcp, spaces between bytes are optional. Makes the code a little less simple. 😟

			var operations = new List<(byte VcpCode, ImmutableArray<byte> NonContinuousValues)>();
			var values = ImmutableArray.CreateBuilder<byte>(20);

			parser.SkipWhitespace();
			while (parser.ConsumeString() is var vcpCodeString and { IsEmpty: false })
			{
				if ((vcpCodeString.Length & 1) != 0) goto Fail;

				while (true)
				{
					if (!TryParseByte(vcpCodeString, out byte code)) goto Fail;
					vcpCodeString = vcpCodeString[2..];

					if (vcpCodeString.Length == 0)
					{
						if (parser.TryConsume(OpeningParenthesis))
						{
							while (parser.ConsumeString() is var valueString and { IsEmpty: false })
							{
								if ((valueString.Length & 1) != 0) goto Fail;

								while (valueString.Length > 0)
								{
									if (!TryParseByte(valueString, out byte value)) goto Fail;
									valueString = valueString[2..];

									values.Add(value);
								}

								parser.SkipWhitespace();
							}

							if (!parser.TryConsume(ClosingParenthesis)) goto Fail;

							operations.Add((code, values.ToImmutable()));
							values.Clear();
						}
						else
						{
							operations.Add((code, ImmutableArray<byte>.Empty));
						}
						break;
					}

					operations.Add((code, ImmutableArray<byte>.Empty));
				}

				parser.SkipWhitespace();
			}

			if (parser.TryConsume(ClosingParenthesis))
			{
				vcpCodes = operations;
				return true;
			}

		Fail:;
			return Failure(out vcpCodes);
		}

		private static bool ParseVcpNameTag(ref Parser parser, out Dictionary<byte, (string Name, ImmutableArray<string> ValueNames)>? vcpNames)
		{
			var list = new Dictionary<byte, (string, ImmutableArray<string>)>();
			var valueNames = ImmutableArray.CreateBuilder<string>();

			parser.SkipWhitespace();
			while (parser.ConsumeString() is var vcpCodeString and { IsEmpty: false })
			{
				if (vcpCodeString.Length != 2 ||
					!TryParseByte(vcpCodeString, out byte code) ||
					!parser.TryConsume(OpeningParenthesis) ||
					!TryConvertToString(parser.ConsumeUntilAny(Parentheses), out string? name))
				{
					goto Fail;
				}

				if (parser.TryConsume(OpeningParenthesis))
				{
					parser.SkipWhitespace();
					while (parser.ConsumeString() is var valueString and { IsEmpty: false })
					{
						if (!TryConvertToString(valueString, out string? valueName)) goto Fail;

						valueNames.Add(valueName!);

						parser.SkipWhitespace();
					}

					if (!parser.TryConsume(ClosingParenthesis)) goto Fail;

					list.Add(code, (name!, valueNames.ToImmutable()));
					valueNames.Clear();
				}
				else if (name!.Length == 0)
				{
					// Must at least specify a name or a value name, if not both.
					goto Fail;
				}
				else
				{
					list.Add(code, (name!, ImmutableArray<string>.Empty));
				}

				if (!parser.TryConsume(ClosingParenthesis)) goto Fail;

				parser.SkipWhitespace();
			}

			if (parser.TryConsume(ClosingParenthesis))
			{
				vcpNames = list;
				return true;
			}

		Fail:;
			return Failure(out vcpNames);
		}

		// Converts a byte string with possible escape sequences to a System.String with no escape sequences.
		private static bool TryConvertToString(ReadOnlySpan<byte> byteString, out string? text)
		{
			if (byteString.IndexOf((byte)'\\') is int index and >= 0)
			{
				return TryConvertToStringWithEscapes(byteString, index, out text);
			}
			else
			{
				// Capabilities String is expected to be ASCII. Just in case, allow to decode UTF-8.
				// If the string is neither ASCII nor UTF-8, this will not work really well, but hey, what can we do about that ¯\_(ツ)_/¯
				text = Encoding.UTF8.GetString(byteString);
				return true;
			}
		}

		private static bool TryConvertToStringWithEscapes(ReadOnlySpan<byte> byteString, int index, out string? text)
		{
			// All escape sequences are \xHH where H is an hexadecimal digit.
			// Anything starting like an hexadecimal sequence, but which is not such a sequence, is considered to be invalid.

			// Allocate a buffer big enough for the string minus one escape sequence.
			// There may be more, but do we really want to count?
			var buffer = ArrayPool<byte>.Shared.Rent(byteString.Length - 3);
			try
			{
				byteString[0..index].CopyTo(buffer);

				var source = byteString[index..];
				var destination = buffer.AsSpan(index);

				do
				{
					// The \ must always be followed by a valid sequence.
					if (source.Length < 4 ||
						source[1] != (byte)'x' ||
						!TryParseByte(source[2..4], out byte b))
					{
						goto Fail;
					}

					source = source[4..];
					destination[0] = b;
					destination = destination[1..];

					int nextIndex = source.IndexOf((byte)'\\');
					if (nextIndex < 0) nextIndex = source.Length;

					source[0..nextIndex].CopyTo(destination);
					source = source[nextIndex..];
					destination = destination[nextIndex..];
				}
				while (source.Length > 0);

				// Same remark as for the case with no escapes 🙂
				text = Encoding.UTF8.GetString(buffer, 0, buffer.Length - destination.Length);
				return true;

			Fail:;
				return Failure(out text);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		private static bool ConsumeUnknownTag(ref Parser parser)
		{
			parser.SkipWhitespace();
			while (parser.ConsumeString() is var @string and { IsEmpty: false })
			{
				if (parser.TryConsume(OpeningParenthesis) && !ConsumeUnknownTag(ref parser))
				{
					return false;
				}

				parser.SkipWhitespace();
			}

			if (parser.TryConsume(ClosingParenthesis))
			{
				return true;
			}

			return false;
		}

		private static bool Failure<T>(out T? capabilities)
		{
			capabilities = default;
			return false;
		}

		private ref struct Parser
		{
			private ReadOnlySpan<byte> _remaining;

			public Parser(ReadOnlySpan<byte> text)
			{
				_remaining = text;
			}

			public void SkipWhitespace() => _remaining = _remaining.TrimStart(WhiteSpace);

			public ReadOnlySpan<byte> ConsumeString()
				=> ConsumeUntilAny(WhiteSpaceOrParentheses);

			public ReadOnlySpan<byte> ConsumeUntilAny(ReadOnlySpan<byte> chars)
			{
				int index = _remaining.IndexOfAny(chars);

				if (index < 0) index = _remaining.Length;

				var @string = _remaining[0..index];
				_remaining = _remaining[index..];
				return @string;
			}

			public bool TryConsume(byte c)
			{
				if (_remaining.Length > 0 && _remaining[0] == c)
				{
					_remaining = _remaining[1..];
					return true;
				}

				return false;
			}

			public bool IsEndOfLine() => _remaining.Length == 0;
		}

		// Adapted from Microsoft.AspNetCore.Routing.Matching.Ascii
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool AsciiIgnoreCaseEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
		{
			if (a.Length != b.Length) return false;

			int length = a.Length;

			ref var charA = ref MemoryMarshal.GetReference(a);
			ref var charB = ref MemoryMarshal.GetReference(b);

			// Iterates each span for the provided length and compares each character
			// case-insensitively. This looks funky because we're using unsafe operations
			// to elide bounds-checks.
			while (length > 0 && AsciiIgnoreCaseEquals(charA, charB))
			{
				charA = ref Unsafe.Add(ref charA, 1);
				charB = ref Unsafe.Add(ref charB, 1);
				length--;
			}

			return length == 0;
		}

		// Taken from Microsoft.AspNetCore.Routing.Matching.Ascii
		// case-insensitive equality comparison for characters in the ASCII range
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool AsciiIgnoreCaseEquals(byte charA, byte charB)
		{
			const uint AsciiToLower = 0x20;
			return
				// Equal when chars are exactly equal
				charA == charB ||

				// Equal when converted to-lower AND they are letters
				((charA | AsciiToLower) == (charB | AsciiToLower) && (uint)((charA | AsciiToLower) - (byte)'a') <= (uint)('z' - 'a'));
		}

		// Simplified hexadecimal parser assuming span is always > 2 bytes.
		private static bool TryParseByte(ReadOnlySpan<byte> span, out byte value)
		{
			static bool TryParseDigit(byte c, out byte digit)
			{
				if (c >= '0' && c <= 'f')
				{
					if (c <= '9')
					{
						digit = (byte)(c - '0');
					}
					else if (c >= 'A')
					{
						if (c >= 'a')
						{
							digit = (byte)(c - ('a' - 10));
						}
						else if (c <= 'F')
						{
							digit = (byte)(c - ('A' - 10));
						}
						else
						{
							goto Failed;
						}
					}
					else
					{
						goto Failed;
					}

					return true;
				}

			Failed:;
				digit = 0;
				return false;
			}

			byte b;
			int v;
			if (span.Length >= 2)
			{
				if (TryParseDigit(span[0], out b))
				{
					v = b;
					if (TryParseDigit(span[1], out b))
					{
						value = (byte)(v << 4 | b);
						return true;
					}
				}
			}
			value = 0;
			return false;
		}
	}
}
