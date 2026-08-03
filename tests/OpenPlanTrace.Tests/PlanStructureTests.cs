using System.Text.Json;
using OpenPlanTrace.Export;

namespace OpenPlanTrace.Tests;

public sealed class PlanStructureTests
{
    [Fact]
    public async Task From_ExportsCanonicalWallRunsWithStableNodeReferencesAndProvenance()
    {
        var result = await CreateScanResultAsync();

        var structure = PlanStructureExport.From(result);
        var nodeIds = structure.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(PlanStructureExport.CurrentSchemaVersion, structure.SchemaVersion);
        Assert.Equal(PlanPlacementExport.CurrentSchemaVersion, structure.SourcePlacementSchemaVersion);
        Assert.NotEmpty(structure.WallRuns);
        Assert.All(structure.WallRuns, run =>
        {
            Assert.True(run.DrawingLength > 0);
            Assert.Contains(run.FromNodeId, nodeIds);
            Assert.Contains(run.ToNodeId, nodeIds);
            Assert.NotEmpty(run.SourceWallIds);
            Assert.NotEmpty(run.SourceWallGraphEdgeIds);
            Assert.NotEmpty(run.SolidIntervals);
            Assert.Equal(
                run.DrawingLength,
                run.SolidDrawingLength + run.OpeningDrawingLength,
                precision: 6);
            Assert.Equal(
                run.SolidDrawingLength,
                run.SolidIntervals.Sum(interval => interval.DrawingLength),
                precision: 6);
        });
        Assert.Equal(structure.WallRuns.Count, structure.Summary.WallRunCount);
        Assert.Equal(structure.Nodes.Count, structure.Summary.NodeCount);
        Assert.InRange(structure.Quality.IntegrityScore, 0, 1);
        Assert.Contains(
            structure.Quality.Evidence,
            evidence => evidence.Contains("not a ground-truth wall accuracy score", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlacementExport_IsCachedPerScanResult()
    {
        var result = await CreateScanResultAsync();

        var first = PlanPlacementExport.From(result);
        var second = PlanPlacementExport.From(result);
        var firstStructure = PlanStructureExport.From(result);
        var secondStructure = PlanStructureExport.From(result);

        Assert.Same(first, second);
        Assert.Same(firstStructure, secondStructure);
    }

    [Fact]
    public async Task Validator_AcceptsGeneratedStructure()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());

        var messages = PlanStructureValidator.Validate(structure);

        Assert.DoesNotContain(
            messages,
            message => string.Equals(message.Severity, "Error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Validator_RejectsBrokenNodeReferenceAndSummary()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var firstRun = Assert.Single(structure.WallRuns.Take(1));
        var brokenRuns = structure.WallRuns
            .Select(run => run == firstRun ? run with { FromNodeId = "missing-node" } : run)
            .ToArray();
        var broken = structure with
        {
            WallRuns = brokenRuns,
            Summary = structure.Summary with { WallRunCount = structure.Summary.WallRunCount + 1 }
        };

        var messages = PlanStructureValidator.Validate(broken);

        Assert.Contains(messages, message => message.Code == "structure.wall_run.node_reference_missing");
        Assert.Contains(messages, message => message.Code == "structure.summary.count_mismatch");
    }

    [Fact]
    public async Task Validator_RejectsBrokenWallSolverSelectionAndRunCount()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var broken = structure with
        {
            WallSolver = structure.WallSolver with
            {
                SelectedHypothesisId = "missing-hypothesis",
                SelectedWallRunCount = structure.WallRuns.Count + 1
            }
        };

        var messages = PlanStructureValidator.Validate(broken);

        Assert.Contains(messages, message => message.Code == "structure.wall_solver.selection_invalid");
        Assert.Contains(messages, message => message.Code == "structure.wall_solver.run_count_mismatch");
    }

    [Fact]
    public async Task Validator_RejectsBrokenSolidOpeningPartition()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());
        var firstRun = Assert.Single(structure.WallRuns.Take(1));
        var firstSolid = Assert.Single(firstRun.SolidIntervals.Take(1));
        var brokenRuns = structure.WallRuns
            .Select(run => run == firstRun
                ? run with
                {
                    SolidIntervals = new[]
                    {
                        firstSolid with
                        {
                            EndParameter = Math.Max(
                                firstSolid.StartParameter,
                                firstSolid.EndParameter - 0.1)
                        }
                    }
                }
                : run)
            .ToArray();
        var broken = structure with { WallRuns = brokenRuns };

        var messages = PlanStructureValidator.Validate(broken);

        Assert.Contains(
            messages,
            message => message.Code is
                "structure.wall_run.interval_geometry_mismatch"
                or "structure.wall_run.interval_coverage_incomplete"
                or "structure.wall_run.interval_length_mismatch");
    }

    [Fact]
    public async Task JsonExporter_RoundTripsCurrentStructureContract()
    {
        var structure = PlanStructureExport.From(await CreateScanResultAsync());

        var json = PlanStructureJsonExporter.Serialize(
            structure,
            new PlanStructureJsonExportOptions { WriteIndented = false });
        var roundTripped = JsonSerializer.Deserialize<PlanStructureExport>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        Assert.NotNull(roundTripped);
        Assert.Equal(structure.Summary.WallRunCount, roundTripped.Summary.WallRunCount);
        Assert.Equal(structure.WallRuns[0].CenterLine, roundTripped.WallRuns[0].CenterLine);
        Assert.Equal(
            structure.WallSolver.Topology.OptimizerVersion,
            roundTripped.WallSolver.Topology.OptimizerVersion);
        Assert.Equal(
            structure.Summary.InlineJunctionReferenceCount,
            roundTripped.Summary.InlineJunctionReferenceCount);
    }

    [Fact]
    public async Task From_ExportsReviewOnlyMixedPathTopologyWithoutChangingPlacement()
    {
        var result = await CreateMixedTopologyResultAsync();
        var topology = result.StructuralPathTopology;

        var scan = PlanTraceExport.From(result);
        var structure = PlanStructureExport.From(result);
        var placement = PlanPlacementExport.From(result);

        Assert.Equal(topology.ContractVersion, scan.StructuralPathTopology.ContractVersion);
        Assert.Equal(2, scan.StructuralPathTopology.Paths.Count);
        Assert.Single(scan.StructuralPathTopology.Junctions);
        Assert.Equal(topology.ContractVersion, structure.StructuralPathTopology.ContractVersion);
        var exportedCurve = Assert.Single(structure.StructuralPathTopology.Paths.Where(path => path.Kind == "CircularArc"));
        Assert.NotNull(exportedCurve.CircularArc);
        Assert.Null(exportedCurve.Line);
        Assert.False(exportedCurve.ReadyForCoordinatePlacement);
        Assert.True(exportedCurve.RequiresReview);
        Assert.Equal(result.StructuralPlanSolution.WallRuns.Count, placement.WallSolutions.SelectedWallRuns.Count);

        using var geoJson = JsonDocument.Parse(PlanTraceGeoJsonExporter.Serialize(result));
        var junctionFeature = Assert.Single(geoJson.RootElement.GetProperty("features")
            .EnumerateArray()
            .Where(feature => feature.GetProperty("properties")
                .GetProperty("featureType")
                .GetString() == "structuralPathJunction"));
        Assert.Equal("MultiPoint", junctionFeature.GetProperty("geometry").GetProperty("type").GetString());
        var coordinates = junctionFeature.GetProperty("geometry").GetProperty("coordinates");
        Assert.Equal(10.25, coordinates[0][0].GetDouble(), precision: 6);
        Assert.Equal(0.25, coordinates[0][1].GetDouble(), precision: 6);
        Assert.Equal(10, coordinates[1][0].GetDouble(), precision: 6);
        Assert.Equal(0, coordinates[1][1].GetDouble(), precision: 6);
        var junctionProperties = junctionFeature.GetProperty("properties");
        Assert.True(junctionProperties.GetProperty("advisoryConnectionOnly").GetBoolean());
        Assert.False(junctionProperties.GetProperty("readyForCoordinatePlacement").GetBoolean());
        Assert.True(junctionProperties.GetProperty("requiresReview").GetBoolean());
        Assert.Equal(10.125, junctionProperties.GetProperty("proposedPosition")[0].GetDouble(), precision: 6);
        Assert.Equal(0.125, junctionProperties.GetProperty("proposedPosition")[1].GetDouble(), precision: 6);
    }

    [Fact]
    public async Task Validator_RejectsNonReciprocalMixedPathConnectionAndMetricsMismatch()
    {
        var structure = PlanStructureExport.From(await CreateMixedTopologyResultAsync());
        var arc = Assert.Single(
            structure.StructuralPathTopology.Paths,
            path => path.Kind == "CircularArc");
        var brokenPaths = structure.StructuralPathTopology.Paths
            .Select(path => path == arc
                ? path with { ConnectedPathIds = Array.Empty<string>() }
                : path)
            .ToArray();
        var broken = structure with
        {
            StructuralPathTopology = structure.StructuralPathTopology with
            {
                Paths = brokenPaths,
                Metrics = structure.StructuralPathTopology.Metrics with
                {
                    JunctionCount = structure.StructuralPathTopology.Metrics.JunctionCount + 1
                }
            }
        };

        var messages = PlanStructureValidator.Validate(broken);

        Assert.Contains(
            messages,
            message => message.Code == "structure.structural_path.connection_not_reciprocal");
        Assert.Contains(
            messages,
            message => message.Code == "structure.structural_paths.metrics_mismatch");
    }

    [Fact]
    public async Task Validator_RejectsPromotedArcAndMutatedJunctionEndpoint()
    {
        var structure = PlanStructureExport.From(await CreateMixedTopologyResultAsync());
        var arc = Assert.Single(
            structure.StructuralPathTopology.Paths,
            path => path.Kind == "CircularArc");
        var junction = Assert.Single(structure.StructuralPathTopology.Junctions);
        var brokenPaths = structure.StructuralPathTopology.Paths
            .Select(path => path == arc
                ? path with
                {
                    ReadyForCoordinatePlacement = true,
                    RequiresReview = false
                }
                : path)
            .ToArray();
        var brokenJunction = junction with
        {
            FirstEndpoint = junction.FirstEndpoint with
            {
                Position = junction.FirstEndpoint.Position with
                {
                    X = junction.FirstEndpoint.Position.X + 1
                }
            }
        };
        var broken = structure with
        {
            StructuralPathTopology = structure.StructuralPathTopology with
            {
                Paths = brokenPaths,
                Junctions = new[] { brokenJunction }
            }
        };

        var messages = PlanStructureValidator.Validate(broken);

        Assert.Contains(
            messages,
            message => message.Code == "structure.structural_path.arc_readiness_invalid");
        Assert.Contains(
            messages,
            message => message.Code == "structure.structural_path_junction.endpoint_invalid");
        Assert.Contains(
            messages,
            message => message.Code == "structure.structural_path_junction.distance_mismatch");
    }

    [Fact]
    public void JsonSchema_IsEmbeddedAndDescribesCanonicalWallRuns()
    {
        using var schema = JsonDocument.Parse(PlanStructureJsonSchema.ReadCurrent());
        var root = schema.RootElement;
        var required = root.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Equal(PlanStructureExport.CurrentSchemaVersion, root.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.Contains("wallRuns", required);
        Assert.Contains("wallSolver", required);
        Assert.Contains("quality", required);
        Assert.Contains("structuralPathTopology", required);
        Assert.True(root.GetProperty("$defs").TryGetProperty("wallRun", out _));
        Assert.True(root.GetProperty("$defs").TryGetProperty("wallSolver", out _));
        Assert.True(root.GetProperty("$defs").TryGetProperty("wallInlineJunction", out _));
        Assert.True(root.GetProperty("$defs").TryGetProperty("wallTopologyOptimizationSummary", out _));
        Assert.True(root.GetProperty("$defs").TryGetProperty("structuralPath", out _));
        Assert.True(root.GetProperty("$defs").TryGetProperty("structuralPathJunction", out _));
    }

    [Fact]
    public async Task Cli_SchemaAndAutoValidationSupportCanonicalStructure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenPlanTraceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var schemaPath = Path.Combine(directory, "structure.schema.json");
            var structurePath = Path.Combine(directory, "structure.json");
            var validationPath = Path.Combine(directory, "validation.json");
            var structure = PlanStructureExport.From(await CreateScanResultAsync());
            await File.WriteAllTextAsync(
                structurePath,
                PlanStructureJsonExporter.Serialize(
                    structure,
                    new PlanStructureJsonExportOptions { WriteIndented = false }));

            var schemaExitCode = await global::OpenPlanTraceCli.RunAsync(new[]
            {
                "schema",
                "structure",
                "--json",
                schemaPath
            });
            var validationExitCode = await global::OpenPlanTraceCli.RunAsync(new[]
            {
                "validate",
                structurePath,
                "--kind",
                "auto",
                "--deep",
                "--json",
                validationPath,
                "--compact-json"
            });

            Assert.Equal(0, schemaExitCode);
            Assert.Equal(0, validationExitCode);
            using var validation = JsonDocument.Parse(await File.ReadAllTextAsync(validationPath));
            Assert.Equal("structure", validation.RootElement.GetProperty("kind").GetString());
            Assert.True(validation.RootElement.GetProperty("valid").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<PlanScanResult> CreateScanResultAsync()
    {
        var document = new PlanDocument(
            "structure-test",
            new[]
            {
                new PlanPage(
                    1,
                    new PlanSize(500, 400),
                    new PlanPrimitive[]
                    {
                        WallLine("wall-top", new PlanPoint(100, 100), new PlanPoint(300, 100)),
                        WallLine("wall-right", new PlanPoint(300, 100), new PlanPoint(300, 260)),
                        WallLine("wall-bottom", new PlanPoint(300, 260), new PlanPoint(100, 260)),
                        WallLine("wall-left", new PlanPoint(100, 260), new PlanPoint(100, 100)),
                        new TextPrimitive("ROOM", new PlanRect(145, 145, 48, 16))
                    })
            })
        {
            Metadata = new PlanMetadata
            {
                SourceName = "structure-test.pdf",
                Properties = new Dictionary<string, string>
                {
                    ["format"] = "pdf",
                    ["loader"] = "PDF/PdfPig"
                }
            }
        };

        return await new OpenPlanTraceScanner().ScanAsync(document);
    }

    private static async Task<PlanScanResult> CreateMixedTopologyResultAsync()
    {
        var source = await CreateScanResultAsync();
        var line = new StructuralWallRun(
            "mixed-line",
            1,
            new PlanLineSegment(new PlanPoint(10.25, -10), new PlanPoint(10.25, 0.25)),
            1,
            WallType.Interior,
            Confidence.High,
            new[] { "mixed-candidate" },
            new[] { "mixed-wall" },
            Array.Empty<string>(),
            new[] { "wall-top" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "test mixed path" })
        {
            Reliability = new StructuralWallRunReliability(
                ReadyForCoordinatePlacement: true,
                RequiresReview: false,
                Confidence: 0.9,
                Reasons: Array.Empty<string>())
        };
        var curve = new CurvedWallCandidate(
            "mixed-arc",
            1,
            new PlanPoint(0, 0),
            10,
            0,
            Math.PI / 2.0,
            1,
            new PlanRect(-10, -10, 20, 20),
            null,
            CurvedWallSourceKind.NativeArcPair,
            1,
            0,
            ReadyForCoordinatePlacement: false,
            ExcludedFromLinearTopology: true,
            Confidence.High,
            RequiresReview: true,
            new[] { "wall-right" },
            new[] { "test mixed arc" });
        var topology = MixedStructuralPathTopologyBuilder.Build(
            StructuralPlanSolution.Empty with { WallRuns = new[] { line } },
            new[] { curve },
            Array.Empty<WallSegment>(),
            PlanCalibration.Empty,
            new StructuralSolverOptions());

        return source with
        {
            CurvedWalls = new[] { curve },
            StructuralPathTopology = topology
        };
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
