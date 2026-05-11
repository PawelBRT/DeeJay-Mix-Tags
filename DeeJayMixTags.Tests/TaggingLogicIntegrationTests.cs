using System;
using System.IO;
using System.Linq;
using System.Threading;
using IOFile = System.IO.File;

namespace Mp3TaggerGUI.Tests;

public class TaggingLogicIntegrationTests
{
    [Fact]
    public void Process_WithNoMp3Files_ReturnsZeroSummaryAndCreatesCsv()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "[]");

        try
        {
            var options = new TaggingOptions
            {
                WriteCsvReport = true,
                DryRun = true
            };

            var result = TaggingLogic.Process(root, jsonPath, options);

            Assert.Equal(0, result.Total);
            Assert.Equal(0, result.Updated);
            Assert.Equal(0, result.Unchanged);
            Assert.Equal(0, result.Missing);
            Assert.Equal(0, result.Errors);

            Assert.False(string.IsNullOrWhiteSpace(result.CsvPath));
            Assert.True(IOFile.Exists(result.CsvPath!));

            var csv = IOFile.ReadAllText(result.CsvPath!);
            Assert.StartsWith("File;Status;GenresBefore;GenresAfter;LabelBefore;LabelAfter", csv);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void Process_WithNoMp3Files_WhenCsvDisabled_DoesNotSetCsvPath()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "[]");

        try
        {
            var options = new TaggingOptions
            {
                WriteCsvReport = false,
                DryRun = true
            };

            var result = TaggingLogic.Process(root, jsonPath, options);

            Assert.Equal(0, result.Total);
            Assert.Null(result.CsvPath);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void Process_WithInvalidJson_Throws()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "{ this is not json }");

        try
        {
            var options = new TaggingOptions
            {
                WriteCsvReport = false,
                DryRun = true
            };

            Assert.ThrowsAny<Exception>(() => TaggingLogic.Process(root, jsonPath, options));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ProcessCore_WhenEntryMissing_IncrementsMissing()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "[]");

        var filePath = Path.Combine(root, "Artist - Song.mp3");
        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

        try
        {
            var options = new TaggingOptions
            {
                FilenameFallback = false,
                DryRun = true,
                WriteCsvReport = false
            };

            var result = TaggingLogic.ProcessCore(
                reportRoot: root,
                jsonPath: jsonPath,
                flags: options,
                files: new[] { filePath },
                fileFactory: _ => new FakeTagFile("Artist", "Song", "", genres: "Old", publisher: "OldLabel"));

            Assert.Equal(1, result.Total);
            Assert.Equal(1, result.Missing);
            Assert.Equal(0, result.Updated);
            Assert.Equal(0, result.Unchanged);
            Assert.Equal(0, result.Errors);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ProcessCore_WhenEntryExists_IncrementsUpdated()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "[{\"artist\":\"Artist\",\"title\":\"Song\",\"version\":\"\",\"genres\":\"Trance\",\"labels\":\"Armada\"}]");

        var filePath = Path.Combine(root, "Artist - Song.mp3");
        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

        try
        {
            var options = new TaggingOptions
            {
                DryRun = false,
                WriteCsvReport = false,
                DoGenre = true,
                DoLabel = true,
                Dedup = true,
                PrependNew = true,
                AlwaysAppendToGenre = false,
                WriteTxxxLabel = false
            };

            FakeTagFile? fake = null;

            var result = TaggingLogic.ProcessCore(
                reportRoot: root,
                jsonPath: jsonPath,
                flags: options,
                files: new[] { filePath },
                fileFactory: _ => fake = new FakeTagFile("Artist", "Song", "", genres: "House", publisher: "Spinnin"));

            Assert.Equal(1, result.Total);
            Assert.Equal(1, result.Updated);
            Assert.Equal(0, result.Unchanged);
            Assert.Equal(0, result.Missing);
            Assert.Equal(0, result.Errors);
            Assert.NotNull(fake);
            Assert.Equal(1, fake!.SaveCount);
            Assert.Equal("Trance | House", fake.Tag.Genres.FirstOrDefault());
            Assert.Equal("Armada | Spinnin", fake.Tag.Publisher);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ProcessCore_WhenNoChanges_IncrementsUnchanged()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "[{\"artist\":\"Artist\",\"title\":\"Song\",\"version\":\"\",\"genres\":\"House\",\"labels\":\"Sony\"}]");

        var filePath = Path.Combine(root, "Artist - Song.mp3");
        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

        try
        {
            var options = new TaggingOptions
            {
                DryRun = false,
                WriteCsvReport = false,
                DoGenre = true,
                DoLabel = true,
                Dedup = true,
                PrependNew = true,
                AlwaysAppendToGenre = false,
                WriteTxxxLabel = false
            };

            FakeTagFile? fake = null;

            var result = TaggingLogic.ProcessCore(
                reportRoot: root,
                jsonPath: jsonPath,
                flags: options,
                files: new[] { filePath },
                fileFactory: _ => fake = new FakeTagFile("Artist", "Song", "", genres: "House", publisher: "Sony"));

            Assert.Equal(1, result.Total);
            Assert.Equal(0, result.Updated);
            Assert.Equal(1, result.Unchanged);
            Assert.Equal(0, result.Missing);
            Assert.Equal(0, result.Errors);
            Assert.NotNull(fake);
            Assert.Equal(0, fake!.SaveCount);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ProcessCore_WhenFactoryThrows_IncrementsErrors()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "[]");

        var filePath = Path.Combine(root, "broken.mp3");
        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

        try
        {
            var options = new TaggingOptions
            {
                WriteCsvReport = false,
                DryRun = true
            };

            var result = TaggingLogic.ProcessCore(
                reportRoot: root,
                jsonPath: jsonPath,
                flags: options,
                files: new[] { filePath },
                fileFactory: _ => throw new InvalidOperationException("boom"));

            Assert.Equal(1, result.Total);
            Assert.Equal(0, result.Updated);
            Assert.Equal(0, result.Unchanged);
            Assert.Equal(0, result.Missing);
            Assert.Equal(1, result.Errors);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ProcessCore_WhenCanceled_ThrowsOperationCanceledException()
    {
        var root = CreateTempDirectory();
        var jsonPath = Path.Combine(root, "db.json");
        IOFile.WriteAllText(jsonPath, "[]");

        var filePath = Path.Combine(root, "a.mp3");
        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            var options = new TaggingOptions
            {
                WriteCsvReport = false,
                DryRun = true
            };

            Assert.Throws<OperationCanceledException>(() =>
                TaggingLogic.ProcessCore(
                    reportRoot: root,
                    jsonPath: jsonPath,
                    flags: options,
                    files: new[] { filePath },
                    fileFactory: _ => new FakeTagFile("A", "T", "", "G", "L"),
                    cancellationToken: cts.Token));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Mp3TaggerGUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
