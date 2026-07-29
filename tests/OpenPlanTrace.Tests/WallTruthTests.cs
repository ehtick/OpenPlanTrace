using System.Text.Json;
using OpenPlanTrace.Export;

namespace OpenPlanTrace.Tests;

public sealed class WallTruthTests
{
    [Fact]
    public async Task Dataset_RoundTripsAndBuildsGeometryBenchmark()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var dataset = DatasetFromStructure(structure, completeTruthSet: true);

        var json = WallTruthJsonSerializer.Serialize(dataset, writeIndented: false);
        var parsed = WallTruthDataset.ParseJson(json);
        var manifest = parsed.ToBenchmarkManifest("fixture.pdf", "reviewed-walls");

        Assert.Equal(WallTruthDataset.CurrentSchemaVersion, parsed.SchemaVersion);
        Assert.Equal(dataset.Entries.Count, parsed.Entries.Count);
        var fixture = Assert.Single(manifest.Fixtures);
        Assert.Equal("reviewed-walls", fixture.Id);
        Assert.True(fixture.Expectations.WallMetrics!.CompleteTruthSet);
        Assert.Equal(
            dataset.Entries.Count(entry => entry.LabelKind == WallTruthLabelKind.Wall),
            fixture.Expectations.WallMetrics.Targets.Count);
    }

    [Fact]
    public async Task Evaluator_PassesExactReviewedWallGeometry()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var dataset = DatasetFromStructure(structure, completeTruthSet: true);

        var evaluation = WallTruthEvaluator.Evaluate(dataset, structure);

        Assert.True(evaluation.Passed);
        Assert.Equal(1, evaluation.Metrics.Recall);
        Assert.Equal(1, evaluation.Metrics.Precision);
        Assert.Equal(1, evaluation.Metrics.LengthWeightedRecall);
        Assert.Equal(1, evaluation.Metrics.MajorWallRecall);
        Assert.Empty(evaluation.Misses);
        Assert.Empty(evaluation.FalsePositives);
        Assert.Empty(evaluation.NotWallViolations);
    }

    [Fact]
    public async Task Evaluator_FailsWhenCriticalWallIsMissing()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var dataset = DatasetFromStructure(structure, completeTruthSet: false);
        var first = dataset.Entries.First(entry => entry.LabelKind == WallTruthLabelKind.Wall);
        var missing = first with
        {
            Id = "critical-missing-wall",
            Importance = WallTruthImportance.Critical,
            CenterLine = new PlanLineSegment(new PlanPoint(20, 420), new PlanPoint(560, 420)),
            Bounds = null
        };
        dataset = dataset with
        {
            Entries = dataset.Entries.Append(missing).ToArray(),
            QualityGate = dataset.QualityGate with
            {
                MinRecall = 0,
                MinLengthWeightedRecall = 0,
                MinMajorWallRecall = 1,
                MinExteriorWallRecall = 0,
                MinWallTypeAccuracy = 0,
                MaxMeanLineDistance = 100,
                MaxMeanEndpointDistance = 100
            }
        };

        var evaluation = WallTruthEvaluator.Evaluate(dataset, structure);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Misses, miss => miss.TruthId == missing.Id);
        Assert.Contains(
            evaluation.Assertions,
            assertion => assertion.Metric == "majorWallRecall" && !assertion.Passed);
        Assert.Null(evaluation.Metrics.Precision);
    }

    [Fact]
    public async Task Evaluator_FailsWhenCanonicalRunOverlapsReviewedNotWall()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var firstRun = structure.WallRuns[0];
        var dataset = DatasetFromStructure(structure, completeTruthSet: false) with
        {
            Entries =
            [
                .. DatasetFromStructure(structure, completeTruthSet: false).Entries,
                new WallTruthEntry
                {
                    Id = "door-swing-not-wall",
                    PageNumber = firstRun.PageNumber,
                    LabelKind = WallTruthLabelKind.NotWall,
                    CenterLine = ToPlanLine(firstRun.CenterLine),
                    Importance = WallTruthImportance.Critical,
                    MinLengthOverlapRatio = 0.75,
                    Evidence = new[] { "reviewer marked this geometry as not a wall" }
                }
            ],
            QualityGate = new WallTruthQualityGate
            {
                MinRecall = 0,
                MinLengthWeightedRecall = 0,
                MinMajorWallRecall = 0,
                MinExteriorWallRecall = 0,
                MinWallTypeAccuracy = 0,
                MaxMeanLineDistance = 100,
                MaxMeanEndpointDistance = 100,
                MaxNotWallViolationCount = 0
            }
        };

        var evaluation = WallTruthEvaluator.Evaluate(dataset, structure);

        Assert.False(evaluation.Passed);
        Assert.Contains(
            evaluation.NotWallViolations,
            violation => violation.TruthId == "door-swing-not-wall" && violation.WallRunId == firstRun.Id);
        Assert.Contains(
            evaluation.Assertions,
            assertion => assertion.Metric == "notWallViolationCount" && !assertion.Passed);
    }

    [Fact]
    public async Task Evaluator_RejectsTruthFromAnotherDocumentOrPageFrame()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var dataset = DatasetFromStructure(structure, completeTruthSet: true) with
        {
            DocumentId = "another-document",
            Pages =
            [
                new WallTruthPage(
                    structure.Pages[0].PageNumber,
                    structure.Pages[0].Width + 10,
                    structure.Pages[0].Height)
            ]
        };

        var evaluation = WallTruthEvaluator.Evaluate(dataset, structure);

        Assert.False(evaluation.Passed);
        Assert.Contains(
            evaluation.Assertions,
            assertion => assertion.Metric == "documentIdentityMatch" && !assertion.Passed);
        Assert.Contains(
            evaluation.Assertions,
            assertion => assertion.Metric == "pageFrameMatch" && !assertion.Passed);
    }

    [Fact]
    public async Task EvaluationJson_RoundTripsCurrentContract()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var result = WallTruthEvaluator.Evaluate(
            DatasetFromStructure(structure, completeTruthSet: true),
            structure);

        var json = WallTruthEvaluationJsonSerializer.Serialize(result, writeIndented: false);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            WallTruthEvaluationResult.CurrentSchemaVersion,
            document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.True(document.RootElement.GetProperty("passed").GetBoolean());
        Assert.Equal(
            structure.WallRuns.Count,
            document.RootElement.GetProperty("metrics").GetProperty("matchedWallCount").GetInt32());
    }

    [Fact]
    public void JsonSchemas_AreEmbeddedAndDescribeReviewedGeometry()
    {
        using var truthSchema = JsonDocument.Parse(WallTruthJsonSchema.ReadCurrent());
        using var evaluationSchema = JsonDocument.Parse(WallTruthEvaluationJsonSchema.ReadCurrent());

        Assert.Equal(
            WallTruthDataset.CurrentSchemaVersion,
            truthSchema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.True(truthSchema.RootElement.GetProperty("$defs").TryGetProperty("entry", out var entry));
        Assert.True(entry.GetProperty("properties").TryGetProperty("centerLine", out _));
        Assert.True(entry.GetProperty("properties").TryGetProperty("labelKind", out _));
        Assert.Equal(
            WallTruthEvaluationResult.CurrentSchemaVersion,
            evaluationSchema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.True(evaluationSchema.RootElement.GetProperty("properties").TryGetProperty("metrics", out _));
    }

    [Fact]
    public async Task Cli_ValidatesAndEvaluatesReviewedWallTruth()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "OpenPlanTraceTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var structure = PlanStructureExport.From(await CreateScanResultAsync());
            var truthPath = Path.Combine(directory, "wall-truth.json");
            var structurePath = Path.Combine(directory, "structure.json");
            var evaluationPath = Path.Combine(directory, "wall-truth-evaluation.json");
            var validationPath = Path.Combine(directory, "wall-truth-validation.json");
            var schemaPath = Path.Combine(directory, "wall-truth.schema.json");
            await File.WriteAllTextAsync(
                truthPath,
                WallTruthJsonSerializer.Serialize(
                    DatasetFromStructure(structure, completeTruthSet: true),
                    writeIndented: false));
            await File.WriteAllTextAsync(
                structurePath,
                PlanStructureJsonExporter.Serialize(
                    structure,
                    new PlanStructureJsonExportOptions { WriteIndented = false }));

            var schemaExitCode = await global::OpenPlanTraceCli.RunAsync(new[]
            {
                "schema",
                "wall-truth",
                "--json",
                schemaPath
            });
            var validateExitCode = await global::OpenPlanTraceCli.RunAsync(new[]
            {
                "validate",
                truthPath,
                "--kind",
                "auto",
                "--json",
                validationPath,
                "--compact-json"
            });
            var evaluateExitCode = await global::OpenPlanTraceCli.RunAsync(new[]
            {
                "wall-truth-evaluate",
                truthPath,
                structurePath,
                "--json",
                evaluationPath,
                "--compact-json"
            });

            Assert.Equal(0, schemaExitCode);
            Assert.Equal(0, validateExitCode);
            Assert.Equal(0, evaluateExitCode);
            Assert.True(File.Exists(schemaPath));
            using var validation = JsonDocument.Parse(await File.ReadAllTextAsync(validationPath));
            using var evaluation = JsonDocument.Parse(await File.ReadAllTextAsync(evaluationPath));
            Assert.Equal("wall-truth", validation.RootElement.GetProperty("kind").GetString());
            Assert.True(validation.RootElement.GetProperty("valid").GetBoolean());
            Assert.True(evaluation.RootElement.GetProperty("passed").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static WallTruthDataset DatasetFromStructure(
        PlanStructureExport structure,
        bool completeTruthSet) =>
        new()
        {
            Name = "Reviewed wall truth",
            Version = "1",
            DocumentId = structure.Document.Id,
            CompleteTruthSet = completeTruthSet,
            Pages = structure.Pages
                .Select(page => new WallTruthPage(page.PageNumber, page.Width, page.Height))
                .ToArray(),
            Entries = structure.WallRuns
                .Select((run, index) => new WallTruthEntry
                {
                    Id = $"truth-wall-{index + 1}",
                    PageNumber = run.PageNumber,
                    LabelKind = WallTruthLabelKind.Wall,
                    CenterLine = ToPlanLine(run.CenterLine),
                    Bounds = ToPlanRect(run.Bounds),
                    WallType = Enum.TryParse<WallType>(run.WallType, ignoreCase: true, out var wallType)
                        ? wallType
                        : WallType.Unknown,
                    ThicknessDrawingUnits = Math.Max(0.1, run.ThicknessDrawingUnits),
                    Importance = run.DrawingLength >= 150
                        ? WallTruthImportance.Critical
                        : WallTruthImportance.Major,
                    SourceDetectionIds = run.SourceWallIds,
                    Evidence = new[] { "synthetic reviewed wall truth" }
                })
                .ToArray(),
            QualityGate = new WallTruthQualityGate
            {
                MinRecall = 1,
                MinPrecision = 1,
                MinLengthWeightedRecall = 1,
                MinMajorWallRecall = 1,
                MinExteriorWallRecall = 1,
                MinWallTypeAccuracy = 1,
                MaxMeanLineDistance = 0.001,
                MaxMeanEndpointDistance = 0.001,
                MaxNotWallViolationCount = 0
            }
        };

    private static PlanLineSegment ToPlanLine(LineExport line) =>
        new(
            new PlanPoint(line.Start.X, line.Start.Y),
            new PlanPoint(line.End.X, line.End.Y));

    private static PlanRect ToPlanRect(RectExport bounds) =>
        new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static async Task<PlanScanResult> CreateScanResultAsync()
    {
        var document = new PlanDocument(
            "wall-truth-test",
            new[]
            {
                new PlanPage(
                    1,
                    new PlanSize(600, 450),
                    new PlanPrimitive[]
                    {
                        WallLine("top", new PlanPoint(80, 80), new PlanPoint(500, 80)),
                        WallLine("right", new PlanPoint(500, 80), new PlanPoint(500, 360)),
                        WallLine("bottom", new PlanPoint(500, 360), new PlanPoint(80, 360)),
                        WallLine("left", new PlanPoint(80, 360), new PlanPoint(80, 80)),
                        WallLine("partition", new PlanPoint(300, 80), new PlanPoint(300, 360)),
                        new TextPrimitive("ROOM A", new PlanRect(155, 180, 60, 18)),
                        new TextPrimitive("ROOM B", new PlanRect(385, 180, 60, 18))
                    })
            })
        {
            Metadata = new PlanMetadata
            {
                SourceName = "wall-truth-test.pdf",
                Properties = new Dictionary<string, string>
                {
                    ["format"] = "pdf",
                    ["loader"] = "PDF/PdfPig"
                }
            }
        };

        return await new OpenPlanTraceScanner().ScanAsync(document);
    }

    private static LinePrimitive WallLine(string sourceId, PlanPoint start, PlanPoint end) =>
        new(new PlanLineSegment(start, end))
        {
            SourceId = sourceId,
            Layer = "Wall",
            Source = new PrimitiveSourceMetadata
            {
                SourceFormat = "test",
                SourceId = sourceId,
                EntityType = "LINE",
                Layer = "Wall",
                LineWeight = 1.0,
                DrawingSpace = SourceDrawingSpace.Model
            }
        };
}
