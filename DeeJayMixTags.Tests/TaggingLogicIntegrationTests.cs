using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
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

                    [Fact]
                    public void ProcessCore_WhenPublisherUnchangedButTxxxMissing_WriteTxxxLabel_IncrementsUpdated()
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
                                DoGenre = false,
                                DoLabel = true,
                                Dedup = true,
                                PrependNew = true,
                                AlwaysAppendToGenre = false,
                                WriteTxxxLabel = false  // Disable TXXX writing for this test
                            };

                            FakeTagFile? fake = null;

                            var result = TaggingLogic.ProcessCore(
                                reportRoot: root,
                                jsonPath: jsonPath,
                                flags: options,
                                files: new[] { filePath },
                                fileFactory: _ => fake = new FakeTagFile("Artist", "Song", "", genres: "House", publisher: "OldPublisher"));

                            Assert.Equal(1, result.Total);
                            Assert.Equal(1, result.Updated);  // Publisher merged: Sony | Oldpublisher
                            Assert.Equal(0, result.Unchanged);
                            Assert.Equal(0, result.Missing);
                            Assert.Equal(0, result.Errors);
                            Assert.NotNull(fake);
                            Assert.Equal(1, fake!.SaveCount);
                            Assert.Equal("Sony | Oldpublisher", fake.Tag.Publisher);  // Prepended merge with title case
                        }
                        finally
                        {
                            TryDeleteDirectory(root);
                        }
                    }

                    [Fact]
                    public void ProcessCore_WhenRemoveWorldPolandFalse_DoesNotRemoveWorldPoland()
                    {
                        var root = CreateTempDirectory();
                        var jsonPath = Path.Combine(root, "db.json");
                        IOFile.WriteAllText(jsonPath, "[{\"artist\":\"A\",\"title\":\"T\",\"version\":\"\",\"genres\":\"Świat | House\",\"labels\":\"\"}]");

                        var filePath = Path.Combine(root, "A - T.mp3");
                        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

                        try
                        {
                            var options = new TaggingOptions
                            {
                                DryRun = false,
                                WriteCsvReport = false,
                                DoGenre = true,
                                DoLabel = false,
                                Dedup = true,
                                PrependNew = true,
                                AlwaysAppendToGenre = false,
                                WriteTxxxLabel = false,
                                RemoveWorldPoland = false,  // KEY: disable removal
                                TitleCase = true,
                                ForcePopUpper = true,
                                NormalizeSeparators = true
                            };

                            FakeTagFile? fake = null;

                            var result = TaggingLogic.ProcessCore(
                                reportRoot: root,
                                jsonPath: jsonPath,
                                flags: options,
                                files: new[] { filePath },
                                fileFactory: _ => fake = new FakeTagFile("A", "T", "", genres: "Old", publisher: ""));

                            Assert.Equal(1, result.Total);
                            Assert.Equal(1, result.Updated);
                            Assert.NotNull(fake);
                            var genres = fake!.Tag.Genres.FirstOrDefault() ?? "";
                            Assert.Contains("Świat", genres);  // Should still contain "Świat"
                            Assert.Contains("House", genres);
                        }
                        finally
                        {
                            TryDeleteDirectory(root);
                        }
                    }

                    [Fact]
                    public void ProcessCore_WhenTitleCaseFalse_PreservesCase()
                    {
                        var root = CreateTempDirectory();
                        var jsonPath = Path.Combine(root, "db.json");
                        IOFile.WriteAllText(jsonPath, "[{\"artist\":\"A\",\"title\":\"T\",\"version\":\"\",\"genres\":\"progressive house\",\"labels\":\"\"}]");

                        var filePath = Path.Combine(root, "A - T.mp3");
                        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

                        try
                        {
                            var options = new TaggingOptions
                            {
                                DryRun = false,
                                WriteCsvReport = false,
                                DoGenre = true,
                                DoLabel = false,
                                Dedup = true,
                                PrependNew = true,
                                AlwaysAppendToGenre = false,
                                WriteTxxxLabel = false,
                                RemoveWorldPoland = true,
                                TitleCase = false,  // KEY: disable title case
                                ForcePopUpper = true,
                                NormalizeSeparators = true
                            };

                            FakeTagFile? fake = null;

                            var result = TaggingLogic.ProcessCore(
                                reportRoot: root,
                                jsonPath: jsonPath,
                                flags: options,
                                files: new[] { filePath },
                                fileFactory: _ => fake = new FakeTagFile("A", "T", "", genres: "Old", publisher: ""));

                            Assert.Equal(1, result.Total);
                            Assert.Equal(1, result.Updated);
                            Assert.NotNull(fake);
                            var genres = fake!.Tag.Genres.FirstOrDefault() ?? "";
                            Assert.Contains("progressive house", genres);  // Should preserve lowercase
                        }
                        finally
                        {
                            TryDeleteDirectory(root);
                        }
                    }

                    [Fact]
                    public void ProcessCore_WhenForcePopUpperFalse_DoesNotForcePopUpper()
                    {
                        var root = CreateTempDirectory();
                        var jsonPath = Path.Combine(root, "db.json");
                        IOFile.WriteAllText(jsonPath, "[{\"artist\":\"A\",\"title\":\"T\",\"version\":\"\",\"genres\":\"pop\",\"labels\":\"\"}]");

                        var filePath = Path.Combine(root, "A - T.mp3");
                        IOFile.WriteAllBytes(filePath, new byte[] { 0x00 });

                        try
                        {
                            var options = new TaggingOptions
                            {
                                DryRun = false,
                                WriteCsvReport = false,
                                DoGenre = true,
                                DoLabel = false,
                                Dedup = true,
                                PrependNew = true,
                                AlwaysAppendToGenre = false,
                                WriteTxxxLabel = false,
                                RemoveWorldPoland = true,
                                TitleCase = true,
                                ForcePopUpper = false,  // KEY: disable force POP upper
                                NormalizeSeparators = true
                            };

                            FakeTagFile? fake = null;

                            var result = TaggingLogic.ProcessCore(
                                reportRoot: root,
                                jsonPath: jsonPath,
                                flags: options,
                                files: new[] { filePath },
                                fileFactory: _ => fake = new FakeTagFile("A", "T", "", genres: "Old", publisher: ""));

                            Assert.Equal(1, result.Total);
                            Assert.Equal(1, result.Updated);
                            Assert.NotNull(fake);
                            var genres = fake!.Tag.Genres.FirstOrDefault() ?? "";
                            // With TitleCase=true but ForcePopUpper=false, 'pop' should become 'Pop' not 'POP'
                            Assert.Contains("Pop", genres);
                            Assert.DoesNotContain("POP", genres);
                        }
                        finally
                        {
                            TryDeleteDirectory(root);
                        }
                    }

                    [Fact]
                    public void ProcessCore_WhenCanceled_EnsuresBackupClosed()
                    {
                        var root = CreateTempDirectory();
                        var jsonPath = Path.Combine(root, "db.json");
                        IOFile.WriteAllText(jsonPath, "[{\"artist\":\"Artist\",\"title\":\"Song1\",\"version\":\"\",\"genres\":\"Trance\",\"labels\":\"Armada\"},{\"artist\":\"Artist\",\"title\":\"Song2\",\"version\":\"\",\"genres\":\"House\",\"labels\":\"Sony\"},{\"artist\":\"Artist\",\"title\":\"Song3\",\"version\":\"\",\"genres\":\"Techno\",\"labels\":\"Stil\"}]");

                        var filePath1 = Path.Combine(root, "Artist - Song1.mp3");
                        var filePath2 = Path.Combine(root, "Artist - Song2.mp3");
                        var filePath3 = Path.Combine(root, "Artist - Song3.mp3");
                        IOFile.WriteAllBytes(filePath1, new byte[] { 0x00 });
                        IOFile.WriteAllBytes(filePath2, new byte[] { 0x00 });
                        IOFile.WriteAllBytes(filePath3, new byte[] { 0x00 });

                        using var cts = new CancellationTokenSource();
                        var processedCount = 0;
                        var fileFactory = new Func<string, TagLib.File>(path =>
                        {
                            processedCount++;
                            if (processedCount == 2)
                            {
                                // After first file processed, cancel for second file
                                cts.Cancel();
                                throw new IOException("Simulated I/O error to trigger cancellation");
                            }
                            return new FakeTagFile("Artist", path.Contains("Song1") ? "Song1" : path.Contains("Song2") ? "Song2" : "Song3", "", "Trance", "Armada");
                        });

                        try
                        {
                            var options = new TaggingOptions
                            {
                                WriteCsvReport = false,
                                DryRun = false,
                                WritePerFileBackup = true,
                                DoGenre = true,
                                DoLabel = true
                            };

                            Assert.Throws<OperationCanceledException>(() =>
                                TaggingLogic.ProcessCore(
                                    reportRoot: root,
                                    jsonPath: jsonPath,
                                    flags: options,
                                    files: new[] { filePath1, filePath2, filePath3 },
                                    fileFactory: fileFactory,
                                    cancellationToken: cts.Token));

                            // Verify backup file was created with exactly one entry (first file before cancellation)
                            var backupFiles = Directory.GetFiles(root, "_tagger_backup_*.json");
                            Assert.Single(backupFiles);

                            var backupPath = backupFiles[0];
                            var backupJson = IOFile.ReadAllText(backupPath);

                            // Verify JSON is properly closed (ends with ])
                            Assert.EndsWith("]", backupJson.Trim());

                            // Parse and verify it's valid JSON
                            var array = JArray.Parse(backupJson);

                            // Should have exactly one entry (first file was processed before cancellation)
                            Assert.Equal(1, array.Count);
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
