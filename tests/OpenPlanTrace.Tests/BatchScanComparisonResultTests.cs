using OpenPlanTrace.Export;

namespace OpenPlanTrace.Tests;

public sealed class BatchScanComparisonResultTests
{
    [Fact]
    public void Compare_PreservesReviewArtifactLinksAndFlagsLargeCountDrift()
    {
        var baseline = CreateRun(
            "baseline",
            CreateItem(
                walls: 106,
                rooms: 9,
                openings: 8,
                objects: 12,
                objectAggregates: 3,
                visualDrawableItems: 816,
                durationMilliseconds: 1200,
                scanJsonPath: @"C:\runs\baseline\scan.json",
                visualSnapshotPath: @"C:\runs\baseline\visual-snapshot.json",
                geoJsonPath: @"C:\runs\baseline\scan.geojson",
                placementJsonPath: @"C:\runs\baseline\placement.json",
                overlayDirectory: @"C:\runs\baseline\overlays"));
        var candidate = CreateRun(
            "candidate",
            CreateItem(
                walls: 39,
                rooms: 1,
                openings: 4,
                objects: 9,
                objectAggregates: 2,
                visualDrawableItems: 500,
                durationMilliseconds: 1180,
                scanJsonPath: @"C:\runs\candidate\scan.json",
                visualSnapshotPath: @"C:\runs\candidate\visual-snapshot.json",
                geoJsonPath: @"C:\runs\candidate\scan.geojson",
                placementJsonPath: @"C:\runs\candidate\placement.json",
                overlayDirectory: @"C:\runs\candidate\overlays"));

        var comparison = BatchScanComparisonResult.Compare(baseline, candidate);

        Assert.Equal(BatchScanComparisonResult.CurrentSchemaVersion, comparison.SchemaVersion);
        Assert.True(comparison.Passed);
        Assert.Equal(1, comparison.MatchedItemCount);
        Assert.Equal(0, comparison.RegressionCount);
        Assert.Equal(3, comparison.InfoCount);

        var item = Assert.Single(comparison.Items);
        Assert.Equal(BatchScanComparisonItemStatus.Matched, item.Status);
        Assert.Equal(@"C:\runs\baseline\scan.json", item.BaselineScanJsonPath);
        Assert.Equal(@"C:\runs\candidate\visual-snapshot.json", item.CandidateVisualSnapshotPath);
        Assert.Equal(@"C:\runs\baseline\scan.geojson", item.BaselineGeoJsonPath);
        Assert.Equal(@"C:\runs\candidate\placement.json", item.CandidatePlacementJsonPath);
        Assert.Equal(@"C:\runs\candidate\overlays", item.CandidateOverlayDirectory);
        Assert.Contains(item.Deltas, delta => delta.Name == "walls" && delta.Delta == -67);
        Assert.Contains(item.Deltas, delta => delta.Name == "rooms" && delta.Delta == -8);

        var signalCodes = comparison.Signals.Select(signal => signal.Code).ToArray();
        Assert.Contains("counts.walls_changed", signalCodes);
        Assert.Contains("counts.rooms_changed", signalCodes);
        Assert.Contains("counts.visualDrawableItems_changed", signalCodes);
    }

    [Fact]
    public void Compare_TreatsRemovedItemAsRegression()
    {
        var baseline = CreateRun("baseline", CreateItem());
        var candidate = CreateRun("candidate");

        var comparison = BatchScanComparisonResult.Compare(baseline, candidate);

        Assert.False(comparison.Passed);
        Assert.Equal(0, comparison.MatchedItemCount);
        Assert.Equal(1, comparison.RemovedItemCount);
        Assert.Equal(1, comparison.RegressionCount);

        var item = Assert.Single(comparison.Items);
        Assert.Equal(BatchScanComparisonItemStatus.Removed, item.Status);
        Assert.Equal("light.pdf", item.Key);
        Assert.Null(item.CandidateInputPath);
        Assert.Equal("item.removed", Assert.Single(item.Signals).Code);
    }

    [Fact]
    public void Compare_FlagsImportReadinessAndWallPlacementImprovements()
    {
        var baseline = CreateRun(
            "baseline",
            CreateItem(
                wallPlacementReady: 45,
                wallPlacementReview: 18,
                wallPlacementOmitted: 88,
                omissionCounts: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["wall_evidence_review_required"] = 8
                },
                importReadiness: ImportReadiness(
                    score: 0.742,
                    coordinateRatio: 0.672,
                    coordinateReady: 78,
                    coordinateTracked: 116,
                    metricRatio: 0.672,
                    metricReady: 78,
                    metricTracked: 116)));
        var candidate = CreateRun(
            "candidate",
            CreateItem(
                wallPlacementReady: 46,
                wallPlacementReview: 17,
                wallPlacementOmitted: 87,
                omissionCounts: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["wall_evidence_review_required"] = 7
                },
                importReadiness: ImportReadiness(
                    score: 0.746,
                    coordinateRatio: 0.681,
                    coordinateReady: 79,
                    coordinateTracked: 116,
                    metricRatio: 0.681,
                    metricReady: 79,
                    metricTracked: 116)));

        var comparison = BatchScanComparisonResult.Compare(baseline, candidate);

        Assert.True(comparison.Passed);
        Assert.Equal(7, comparison.ImprovementCount);

        var signalCodes = comparison.Signals.Select(signal => signal.Code).ToArray();
        Assert.Contains("import_readiness.score_improved", signalCodes);
        Assert.Contains("import_readiness.coordinate_ratio_improved", signalCodes);
        Assert.Contains("import_readiness.metric_ratio_improved", signalCodes);
        Assert.Contains("wall_placement.ready_increased", signalCodes);
        Assert.Contains("wall_placement.review_reduced", signalCodes);
        Assert.Contains("wall_placement.omitted_reduced", signalCodes);
        Assert.Contains("wall_placement.omission.wall_evidence_review_required_reduced", signalCodes);

        var item = Assert.Single(comparison.Items);
        Assert.Contains(item.Deltas, delta => delta.Name == "importReadinessScore" && Math.Round(delta.Delta!.Value, 3) == 0.004);
        Assert.Contains(item.Deltas, delta => delta.Name == "wallPlacementReady" && delta.Delta == 1);

        var markdown = BatchScanComparisonMarkdownReport.Create(comparison);
        Assert.Contains("import_readiness.score_improved", markdown);
        Assert.Contains("wallPlacementReady +1", markdown);
        Assert.Contains("importReadinessScore +0.004", markdown);
    }

    [Fact]
    public void Compare_DoesNotFlagReadyRegressionWhenRepresentedWallsOffsetDrop()
    {
        var baseline = CreateRun(
            "baseline",
            CreateItem(
                wallPlacementReady: 55,
                wallPlacementRepresented: 37,
                wallPlacementOmitted: 92,
                importReadiness: ImportReadiness(
                    score: 0.838,
                    coordinateRatio: 0.837,
                    coordinateReady: 92,
                    coordinateTracked: 110,
                    metricRatio: 0.837,
                    metricReady: 92,
                    metricTracked: 110),
                omissionCounts: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["duplicate_clean_topology_span"] = 30,
                    ["wall_evidence_review_required"] = 8
                }));
        var candidate = CreateRun(
            "candidate",
            CreateItem(
                wallPlacementReady: 54,
                wallPlacementRepresented: 38,
                wallPlacementOmitted: 93,
                importReadiness: ImportReadiness(
                    score: 0.837,
                    coordinateRatio: 0.833,
                    coordinateReady: 92,
                    coordinateTracked: 110,
                    metricRatio: 0.833,
                    metricReady: 92,
                    metricTracked: 110),
                omissionCounts: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["duplicate_clean_topology_span"] = 31,
                    ["wall_evidence_review_required"] = 8
                }));

        var comparison = BatchScanComparisonResult.Compare(baseline, candidate);

        Assert.True(comparison.Passed);
        Assert.Equal(0, comparison.RegressionCount);
        Assert.Contains(comparison.Signals, signal =>
            signal.Code == "wall_placement.ready_represented_offset"
            && signal.Severity == BatchScanComparisonSignalSeverity.Info);
        Assert.Contains(comparison.Signals, signal =>
            signal.Code == "wall_placement.omitted_represented_offset"
            && signal.Severity == BatchScanComparisonSignalSeverity.Info);
        Assert.Contains(comparison.Signals, signal =>
            signal.Code == "wall_placement.omission.duplicate_clean_topology_span_represented_increased"
            && signal.Severity == BatchScanComparisonSignalSeverity.Info);
        Assert.Contains(comparison.Signals, signal =>
            signal.Code == "import_readiness.score_represented_offset"
            && signal.Severity == BatchScanComparisonSignalSeverity.Info);
        Assert.Contains(comparison.Signals, signal =>
            signal.Code == "import_readiness.coordinate_ratio_represented_offset"
            && signal.Severity == BatchScanComparisonSignalSeverity.Info);
        Assert.Contains(comparison.Signals, signal =>
            signal.Code == "import_readiness.metric_ratio_represented_offset"
            && signal.Severity == BatchScanComparisonSignalSeverity.Info);
        Assert.DoesNotContain(comparison.Signals, signal =>
            signal.Code == "wall_placement.ready_decreased"
            && signal.Severity == BatchScanComparisonSignalSeverity.Regression);
        Assert.DoesNotContain(comparison.Signals, signal =>
            signal.Code == "wall_placement.omitted_increased"
            && signal.Severity == BatchScanComparisonSignalSeverity.Regression);
        Assert.DoesNotContain(comparison.Signals, signal =>
            signal.Code == "wall_placement.omission.duplicate_clean_topology_span_increased"
            && signal.Severity == BatchScanComparisonSignalSeverity.Regression);
        Assert.DoesNotContain(comparison.Signals, signal =>
            signal.Code == "import_readiness.score_regressed"
            && signal.Severity == BatchScanComparisonSignalSeverity.Regression);
        Assert.DoesNotContain(comparison.Signals, signal =>
            signal.Code == "import_readiness.coordinate_ratio_regressed"
            && signal.Severity == BatchScanComparisonSignalSeverity.Regression);
        Assert.DoesNotContain(comparison.Signals, signal =>
            signal.Code == "import_readiness.metric_ratio_regressed"
            && signal.Severity == BatchScanComparisonSignalSeverity.Regression);

        var item = Assert.Single(comparison.Items);
        Assert.Contains(item.Deltas, delta => delta.Name == "wallPlacementReady" && delta.Delta == -1);
        Assert.Contains(item.Deltas, delta => delta.Name == "wallPlacementRepresented" && delta.Delta == 1);
        Assert.Contains(item.Deltas, delta => delta.Name == "wallPlacementEffective" && delta.Delta == 0);
        Assert.Contains(item.Deltas, delta => delta.Name == "wallPlacementOmitted" && delta.Delta == 1);
        Assert.Contains(item.Deltas, delta => delta.Name == "importReadinessScore" && Math.Round(delta.Delta!.Value, 3) == -0.001);

        var markdown = BatchScanComparisonMarkdownReport.Create(comparison);
        Assert.Contains("wall_placement.ready_represented_offset", markdown);
        Assert.Contains("wall_placement.omitted_represented_offset", markdown);
        Assert.Contains("import_readiness.score_represented_offset", markdown);
    }

    [Fact]
    public void MarkdownReport_IncludesEvidenceColumnAndArtifactAvailability()
    {
        var baseline = CreateRun(
            "baseline",
            CreateItem(
                scanJsonPath: @"C:\runs\baseline\scan.json",
                visualSnapshotPath: @"C:\runs\baseline\visual-snapshot.json",
                geoJsonPath: @"C:\runs\baseline\scan.geojson",
                placementJsonPath: @"C:\runs\baseline\placement.json",
                overlayDirectory: @"C:\runs\baseline\overlays"));
        var candidate = CreateRun(
            "candidate",
            CreateItem(
                scanJsonPath: @"C:\runs\candidate\scan.json",
                visualSnapshotPath: @"C:\runs\candidate\visual-snapshot.json",
                geoJsonPath: @"C:\runs\candidate\scan.geojson",
                placementJsonPath: @"C:\runs\candidate\placement.json",
                overlayDirectory: @"C:\runs\candidate\overlays"));

        var markdown = BatchScanComparisonMarkdownReport.Create(
            BatchScanComparisonResult.Compare(baseline, candidate));

        Assert.Contains("| Item | Match | Scan Status | Quality | Diagnostics | Visual Issues | Duration | Evidence | Key Deltas |", markdown);
        Assert.Contains("scan+visual+geojson+placement+svg -> scan+visual+geojson+placement+svg", markdown);
    }

    [Fact]
    public void BatchScanMarkdownReport_IncludesCorpusQaTableReviewPrioritiesAndArtifactIndex()
    {
        var run = CreateRun(
            "candidate",
            CreateItem(
                walls: 39,
                rooms: 0,
                openings: 4,
                objects: 12,
                objectAggregates: 3,
                visualDrawableItems: 640,
                durationMilliseconds: 1350,
                scanJsonPath: @"C:\runs\candidate\scan.json",
                visualSnapshotPath: @"C:\runs\candidate\visual-snapshot.json",
                geoJsonPath: @"C:\runs\candidate\scan.geojson",
                placementJsonPath: @"C:\runs\candidate\placement.json",
                overlayDirectory: @"C:\runs\candidate\overlays"));

        var markdown = BatchScanMarkdownReport.Create(run);

        Assert.Contains("# OpenPlanTrace Batch Scan Report", markdown);
        Assert.Contains("Status: BLOCKED", markdown);
        Assert.Contains("| Item | Status | Source | Readiness | Quality | Geometry | Visual QA | Diagnostics | Artifacts |", markdown);
        Assert.Contains("Blocked 0.7 G:N M:N R:N coord 0.559 (66/118) metric 0.559 (66/118)", markdown);
        Assert.Contains("walls 39, nodes 24, rooms 0", markdown);
        Assert.Contains("scan+visual+geojson+placement+svg", markdown);
        Assert.Contains("## Corpus Signals", markdown);
        Assert.Contains("Geometry totals: walls 39, rooms 0, openings 4", markdown);
        Assert.Contains("Import readiness: 1 blocked item(s), 0/1 geometry-ready, 0/1 metric-ready, 0/1 routing-ready, average score 0.7", markdown);
        Assert.Contains("Import blocking codes: placement.import.low_coordinate_ready_ratio:1", markdown);
        Assert.Contains("Wall placement QA: ready 18, review 9, omitted 21, suppressed 2, represented 4", markdown);
        Assert.Contains("Wall placement omissions: wall_evidence_review_required:7, fragmented_short_parallel_pair_review_required:3", markdown);
        Assert.Contains("Visual issue codes: visual.overlay_coverage_high:1", markdown);
        Assert.Contains("## Review Priorities", markdown);
        Assert.Contains("quality review required", markdown);
        Assert.Contains("import readiness Blocked 0.7", markdown);
        Assert.Contains("wall placement omissions wall_evidence_review_required:7", markdown);
        Assert.Contains("walls detected but no rooms solved", markdown);
        Assert.Contains("## Artifact Index", markdown);
        Assert.Contains(@"C:\runs\candidate\placement.json", markdown);
        Assert.Contains("## Next Actions", markdown);
    }

    [Fact]
    public void BatchScanMarkdownReport_UsesPlacementImportReadinessCounts()
    {
        var placementReadiness = new PlacementImportReadinessExport(
            "Blocked",
            0.698,
            ReadyForGeometryImport: false,
            ReadyForMetricImport: false,
            ReadyForRoutingImport: false,
            RequiresReview: true,
            BlockingIssueCodes: ["placement.import.low_coordinate_ready_ratio"],
            ReviewIssueCodes: ["placement.wall_evidence.requires_review"],
            RecommendedActions: ["Review placement walls."],
            Evidence:
            [
                "structural import coordinate readiness ratio 0.585 (69/118 structural import entities)",
                "structural import metric readiness ratio 0.585 (69/118 structural import entities)"
            ]);
        var run = CreateRun(
            "candidate",
            CreateItem(importReadiness: BatchImportReadinessSummary.From(placementReadiness)));

        var markdown = BatchScanMarkdownReport.Create(run);

        Assert.Contains("Blocked 0.698 G:N M:N R:N coord 0.585 (69/118) metric 0.585 (69/118)", markdown);
        Assert.DoesNotContain("112/161", markdown);
    }

    private static BatchScanRunResult CreateRun(
        string outputDirectoryName,
        params BatchScanItemResult[] items) =>
        new(
            BatchScanRunResult.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Path.Combine(@"C:\runs", outputDirectoryName),
            1,
            0,
            items);

    private static BatchScanItemResult CreateItem(
        int walls = 106,
        int rooms = 9,
        int openings = 8,
        int objects = 12,
        int objectAggregates = 3,
        int visualDrawableItems = 816,
        double durationMilliseconds = 1200,
        string? scanJsonPath = null,
        string? visualSnapshotPath = null,
        string? geoJsonPath = null,
        string? placementJsonPath = null,
        string? overlayDirectory = null,
        BatchImportReadinessSummary? importReadiness = null,
        int wallPlacementReady = 18,
        int wallPlacementReview = 9,
        int wallPlacementOmitted = 21,
        int wallPlacementSuppressed = 2,
        int wallPlacementRepresented = 4,
        IReadOnlyDictionary<string, int>? omissionCounts = null) =>
        new(
            ItemNumber: 1,
            InputPath: @"C:\plans\light.pdf",
            FileName: "light.pdf",
            SourceKind: PlanSourceKind.Pdf,
            EffectiveSourceKind: PlanSourceKind.Pdf,
            Status: BatchScanItemStatus.Succeeded,
            AttemptCount: 1,
            DurationMilliseconds: durationMilliseconds,
            Counts: new BatchScanCounts(
                Pages: 1,
                Regions: 2,
                TitleBlocks: 1,
                Dimensions: 6,
                Annotations: 2,
                GridAxes: 0,
                GridBaySpacings: 0,
                SurfacePatterns: 0,
                Walls: walls,
                WallNodes: 24,
                WallEdges: 22,
                Rooms: rooms,
                RoomAdjacencies: 3,
                RoomClusters: 1,
                Openings: openings,
                Objects: objects,
                ObjectGroups: 2,
                ObjectAggregates: objectAggregates,
                RoutingItems: 18,
                Diagnostics: 0,
                DiagnosticWarnings: 0,
                DiagnosticErrors: 0,
                QualityGrade: "Usable",
                QualityConfidence: 0.804,
                RequiresReview: true),
            ScanJsonPath: scanJsonPath,
            GeoJsonPath: geoJsonPath,
            PlacementJsonPath: placementJsonPath,
            OverlayDirectory: overlayDirectory,
            VisualSnapshotPath: visualSnapshotPath,
            VisualSnapshot: new BatchVisualSnapshotSummary(
                PlanOverlaySnapshot.CurrentSchemaVersion,
                PageCount: 1,
                LayerCount: 12,
                DrawableItemCount: visualDrawableItems,
                IssueCount: 1,
                WarningIssueCount: 1,
                ErrorIssueCount: 0,
                MaxDetectionCoverage: 0.83,
                IssueCodes: new[] { "visual.overlay_coverage_high" }),
            WallPlacement: new BatchWallPlacementSummary(
                PlacementReadyWallCount: wallPlacementReady,
                PlacementOmittedWallCount: wallPlacementOmitted,
                RepresentedWallCount: wallPlacementRepresented,
                PlacementSuppressedWallCount: wallPlacementSuppressed,
                PlacementReviewWallCount: wallPlacementReview,
                OmissionCounts: omissionCounts ?? new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["wall_evidence_review_required"] = 7,
                    ["fragmented_short_parallel_pair_review_required"] = 3
                }),
            ImportReadiness: importReadiness ?? new BatchImportReadinessSummary(
                Grade: "Blocked",
                Score: 0.7,
                ReadyForGeometryImport: false,
                ReadyForMetricImport: false,
                ReadyForRoutingImport: false,
                RequiresReview: true,
                CoordinateReadyRatio: 0.559,
                CoordinateReadyEntityCount: 66,
                CoordinateTrackedEntityCount: 118,
                MetricReadyRatio: 0.559,
                MetricReadyEntityCount: 66,
                MetricTrackedEntityCount: 118,
                BlockingIssueCodes: new[] { "placement.import.low_coordinate_ready_ratio" },
                ReviewIssueCodes: new[] { "placement.wall_evidence.requires_review" },
                Evidence: new[] { "structural import coordinate readiness ratio 0.559 (66/118 structural import entities)" }),
            ErrorMessage: null,
            SourceCapability: null);

    private static BatchImportReadinessSummary ImportReadiness(
        double score,
        double coordinateRatio,
        int coordinateReady,
        int coordinateTracked,
        double metricRatio,
        int metricReady,
        int metricTracked) =>
        new(
            Grade: "Blocked",
            Score: score,
            ReadyForGeometryImport: false,
            ReadyForMetricImport: false,
            ReadyForRoutingImport: false,
            RequiresReview: true,
            CoordinateReadyRatio: coordinateRatio,
            CoordinateReadyEntityCount: coordinateReady,
            CoordinateTrackedEntityCount: coordinateTracked,
            MetricReadyRatio: metricRatio,
            MetricReadyEntityCount: metricReady,
            MetricTrackedEntityCount: metricTracked,
            BlockingIssueCodes: new[] { "placement.import.low_coordinate_ready_ratio" },
            ReviewIssueCodes: new[] { "placement.wall_evidence.requires_review" },
            Evidence: new[]
            {
                $"structural import coordinate readiness ratio {coordinateRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} ({coordinateReady.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{coordinateTracked.ToString(System.Globalization.CultureInfo.InvariantCulture)} structural import entities)",
                $"structural import metric readiness ratio {metricRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} ({metricReady.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{metricTracked.ToString(System.Globalization.CultureInfo.InvariantCulture)} structural import entities)"
            });
}
