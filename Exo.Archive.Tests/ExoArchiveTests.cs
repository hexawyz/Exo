using System.Security.Cryptography;
using Exo.Metadata;
using Xunit;

namespace Exo.Archive.Tests;

public sealed class ExoArchiveTests
{
	[Fact]
	public async Task ShouldCreateAndReadEmptyArchive()
	{
		var builder = new InMemoryExoArchiveBuilder();

		try
		{
			await builder.SaveAsync("tmp0.xoa", default);

			using var archive = new ExoArchive("tmp0.xoa");

		}
		finally
		{
			File.Delete("tmp0.xoa");
		}
	}

	[Fact]
	public async Task ShouldCreateAndReadSingleFileArchive()
	{
		var builder = new InMemoryExoArchiveBuilder();

		builder.AddFile("theonefile"u8, "Hello World"u8);

		try
		{

			await builder.SaveAsync("tmp1.xoa", default);

			using var archive = new ExoArchive("tmp1.xoa");

			Assert.True(archive.TryGetFileEntry("theonefile"u8, out var file));
			var data = file.DangerousGetSpan().ToArray();
			Assert.True(file.DangerousGetSpan().SequenceEqual("Hello World"u8));
		}
		finally
		{
			File.Delete("tmp1.xoa");
		}
	}

	[Fact]
	public async Task ShouldCreateAndReadTwoFileArchive()
	{
		var builder = new InMemoryExoArchiveBuilder();

		builder.AddFile("xxx/file1"u8, "CONTENTS_OF_FILE_1"u8);
		builder.AddFile("xxx/file2"u8, "CONTENTS_OF_FILE_2"u8);

		try
		{

			await builder.SaveAsync("tmp2.xoa", default);

			using var archive = new ExoArchive("tmp2.xoa");

			Assert.True(archive.TryGetFileEntry("xxx/file1"u8, out var file));
			Assert.True(file.DangerousGetSpan().SequenceEqual("CONTENTS_OF_FILE_1"u8));

			Assert.True(archive.TryGetFileEntry("xxx/file2"u8, out file));
			Assert.True(file.DangerousGetSpan().SequenceEqual("CONTENTS_OF_FILE_2"u8));
		}
		finally
		{
			File.Delete("tmp2.xoa");
		}
	}

	[Fact]
	public async Task ShouldCreateAndReadArchiveWithMultipleFiles()
	{
		var files = new List<(byte[] Key, byte[] Data)>();

		for (int i = 0; i < 100; i++)
		{
			files.Add((RandomNumberGenerator.GetBytes(25), RandomNumberGenerator.GetBytes(123 + i)));
		}

		var builder = new InMemoryExoArchiveBuilder();
		foreach (var file in files)
		{
			builder.AddFile(file.Key, file.Data);
		}

		try
		{
			await builder.SaveAsync("tmpN.xoa", default);

			using var archive = new ExoArchive("tmpN.xoa");

			foreach (var f in files)
			{
				Assert.True(archive.TryGetFileEntry(f.Key, out var file));
				Assert.True(file.DangerousGetSpan().SequenceEqual(f.Data));
			}
		}
		finally
		{
			File.Delete("tmpN.xoa");
		}
	}
}
