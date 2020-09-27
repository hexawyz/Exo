using System;
using System.Collections.Immutable;
using System.Text;
using Xunit;

namespace DeviceTools.DisplayDevices.Tests
{
	public sealed class MonitorCapabilitiesTests
	{
		[Theory]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)mccs_ver(2.2a))", "monitor")]
		[InlineData("(prot(display)type(lcd)model(Monitor Name)mccs_ver(2.2a))", "display")]
		public void ShouldParseProtocol(string capabilitiesString, string protocol)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(protocol, capabilities!.Protocol);
		}

		[Theory]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)mccs_ver(2.2a))", "lcd")]
		[InlineData("(prot(monitor)type(LCD)model(Monitor Name)mccs_ver(2.2a))", "LCD")]
		public void ShouldParseType(string capabilitiesString, string type)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(type, capabilities!.Type);
		}

		[Theory]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)mccs_ver(2.2a))", "Monitor Name")]
		[InlineData("(prot(monitor)type(lcd)model(SuperScreen 1000K)mccs_ver(2.2a))", "SuperScreen 1000K")]
		public void ShouldParseModel(string capabilitiesString, string model)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(model, capabilities!.Model);
		}

		[Theory]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)mccs_ver(2.2))", "2.2")]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)mccs_ver(2.2a))", "2.2a")]
		public void ShouldParseMccsVersion(string capabilitiesString, string version)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(version, capabilities!.MccsVersion);
		}

		[Theory]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(01 02 03 07 0C E3 F3)mccs_ver(2.2))", new DdcCiCommand[] { DdcCiCommand.VcpRequest, DdcCiCommand.VcpReply, DdcCiCommand.VcpSet, DdcCiCommand.TimingRequest, DdcCiCommand.SaveCurrentSettings, DdcCiCommand.CapabilitiesReply, DdcCiCommand.CapabilitiesRequest })]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)mccs_ver(2.2a))", new DdcCiCommand[] { DdcCiCommand.VcpRequest, DdcCiCommand.VcpReply, DdcCiCommand.VcpSet, DdcCiCommand.TimingRequest, DdcCiCommand.SaveCurrentSettings, DdcCiCommand.CapabilitiesReply, DdcCiCommand.CapabilitiesRequest })]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(0102030CE3F3)mccs_ver(2.2a))", new DdcCiCommand[] { DdcCiCommand.VcpRequest, DdcCiCommand.VcpReply, DdcCiCommand.VcpSet, DdcCiCommand.SaveCurrentSettings, DdcCiCommand.CapabilitiesReply, DdcCiCommand.CapabilitiesRequest })]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(01 02 03 06 07 0C E3 F3)mccs_ver(2.2))", new DdcCiCommand[] { DdcCiCommand.VcpRequest, DdcCiCommand.VcpReply, DdcCiCommand.VcpSet, DdcCiCommand.TimingReply, DdcCiCommand.TimingRequest, DdcCiCommand.SaveCurrentSettings, DdcCiCommand.CapabilitiesReply, DdcCiCommand.CapabilitiesRequest })]
		public void ShouldParseCommands(string capabilitiesString, DdcCiCommand[] commands)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal((DdcCiCommand[])commands, capabilities!.SupportedMonitorCommands);
		}

		[Theory]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(000102030405)mccs_ver(2.2a))", new byte[] { 0, 1, 2, 3, 4, 5 })]
		public void ShouldParseVcpCodes(string capabilitiesString, byte[] vcpCodes)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(vcpCodes, ImmutableArray.CreateRange(capabilities!.SupportedVcpCommands, v => v.VcpCode));
		}

		[Theory]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(14(010203))mccs_ver(2.2a))", new byte[] { 1, 2, 3 })]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(14(01 02 03))mccs_ver(2.2a))", new byte[] { 1, 2, 3 })]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(0260(01 02 03))mccs_ver(2.2a))", new byte[] { })]
		[InlineData("(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(60(0F 10))mccs_ver(2.2a))", new byte[] { 15, 16 })]
		public void ShouldParseVcpCodeNonContinuousValues(string capabilitiesString, byte[] values)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(values, ImmutableArray.CreateRange(capabilities!.SupportedVcpCommands[0].NonContinuousValues, v => v.Value));
		}

		[Theory]
		[InlineData(@"(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(FEFF)vcpname(FE(Custom\x201)FF(Custom\x202))mccs_ver(2.2a))", new[] { "Custom 1", "Custom 2" })]
		[InlineData(@"(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(60(0F10)F0(44AFCDEE)F5)vcpname(60((Input\x201 Input\x202))F0(Custom\x201(With Custom Parameter Values))F5(Function\x205))mccs_ver(2.2a))", new[] { "Input Select", "Custom 1", "Function 5" })]
		public void ShouldParseVcpNames(string capabilitiesString, string[] names)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(names, ImmutableArray.CreateRange(capabilities!.SupportedVcpCommands, v => v.Name));
		}

		[Theory]
		[InlineData(@"(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(F3(0102))vcpname(F3(Custom(On Off)))mccs_ver(2.2a))", new[] { "On", "Off" })]
		[InlineData(@"(prot(monitor)type(lcd)model(Monitor Name)cmds(010203070CE3F3)vcp(60(0F10)F0(44AFCDEE)F5)vcpname(60((Input\x201 Input\x202))F0(Custom\x201(With Custom Parameter Values))F5(Function\x205))mccs_ver(2.2a))", new[] { "Input 1", "Input 2" })]
		public void ShouldParseVcpNamesValueNames(string capabilitiesString, string[] valueNames)
		{
			Assert.True(MonitorCapabilities.TryParse(Encoding.UTF8.GetBytes(capabilitiesString), out var capabilities));
			Assert.NotNull(capabilities);
			Assert.Equal(valueNames, ImmutableArray.CreateRange(capabilities!.SupportedVcpCommands[0].NonContinuousValues, v => v.Name));
		}
	}
}
