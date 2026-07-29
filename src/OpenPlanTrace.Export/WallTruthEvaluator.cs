using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPlanTrace.Export;

public sealed record WallTruthEvaluationResult(
    string SchemaVersion,
    DateTimeOffset EvaluatedAt,
    string WallTruthSchemaVersion,
    string StructureSchemaVersion,
    string? DatasetName,
    string? DatasetVersion,
    string? DocumentId,
    bool CompleteTruthSet,
    bool Passed,
    WallTruthEvaluationMetrics Metrics,
    IReadOnlyList<WallTruthMatchExport> Matches,
    IReadOnlyList<WallTruthMissExport> Misses,
    IReadOnlyList<WallTruthFalsePositiveExport> FalsePositives,
    IReadOnlyList<WallTruthNotWallViolationExport> NotWallViolations,
    IReadOnlyList<WallTruthGateAssertionExport> Assertions,
    IReadOnlyList<string> Evidence)
{
    public const string CurrentSchemaVersion = "openplantrace.wall-truth-evaluation.v1";
}

public sealed record WallTruthEvaluationMetrics(
    int TruthWallCount,
    int PredictedWallCount,
    int MatchedWallCount,
    int MissedWallCount,
    int FalsePositiveWallCount,
    int NotWallViolationCount,
    double Recall,
    double? Precision,
    double? F1Score,
    double LengthWeightedRecall,
    double? LengthWeightedPrecision,
    double MajorWallRecall,
    double CriticalWallRecall,
    double ExteriorWallRecall,
    double InteriorWallRecall,
    double WallTypeAccuracy,
    double MeanLineDistance,
    double MeanEndpointDistance,
    double MeanAngularDifferenceDegrees,
    double MeanLengthOverlapRatio,
    double TruthWallDrawingLength,
    double MatchedTruthDrawingLength,
    double PredictedWallDrawingLength,
    double FalsePositiveDrawingLength,
    double NotWallViolationDrawingLength,
    double SolverObjectiveScore,
    double SolverEndpointConnectivityRatio,
    double SolverRoomBoundaryClosureRatio);

public sealed record WallTruthMatchExport(
    string TruthId,
    string WallRunId,
    int PageNumber,
    string TruthWallType,
    string DetectedWallType,
    string Importance,
    double Score,
    double LineDistance,
    double EndpointDistance,
    double AngularDifferenceDegrees,
    double LengthOverlapRatio,
    bool WallTypeMatches,
    IReadOnlyList<string> Evidence);

public sealed record WallTruthMissExport(
    string TruthId,
    int PageNumber,
    string WallType,
    string Importance,
    LineExport CenterLine,
    double DrawingLength,
    IReadOnlyList<string> Evidence);

public sealed record WallTruthFalsePositiveExport(
    string WallRunId,
    int PageNumber,
    string WallType,
    LineExport CenterLine,
    double DrawingLength,
    double Confidence,
    IReadOnlyList<string> Evidence);

public sealed record WallTruthNotWallViolationExport(
    string TruthId,
    string WallRunId,
    int PageNumber,
    double OverlapRatio,
    double DrawingLength,
    IReadOnlyList<string> Evidence);

public sealed record WallTruthGateAssertionExport(
    string Metric,
    string Comparator,
    double Expected,
    double Actual,
    bool Passed,
    string Message);

public static class WallTruthEvaluator
{
    public static WallTruthEvaluationResult Evaluate(
        WallTruthDataset dataset,
        PlanStructureExport structure)
    {
        dataset = WallTruthDataset.Validate(dataset);
        ArgumentNullException.ThrowIfNull(structure);

        var truthWalls = dataset.Entries
            .Where(entry => entry.LabelKind == WallTruthLabelKind.Wall && entry.CenterLine is not null)
            .OrderByDescending(entry => entry.Importance)
            .ThenByDescending(entry => entry.CenterLine!.Value.Length)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToArray();
        var notWalls = dataset.Entries
            .Where(entry => entry.LabelKind == WallTruthLabelKind.NotWall)
            .ToArray();
        var predicted = structure.WallRuns
            .Where(run => run.DrawingLength > 0)
            .ToArray();
        var unmatchedPredictionIndexes = Enumerable.Range(0, predicted.Length).ToHashSet();
        var matches = new List<WallTruthMatchExport>();
        var misses = new List<WallTruthMissExport>();

        foreach (var truth in truthWalls)
        {
            var best = unmatchedPredictionIndexes
                .Select(index => Match(truth, predicted[index], index))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.LineDistance)
                .ThenBy(candidate => candidate.EndpointDistance)
                .ThenBy(candidate => predicted[candidate.PredictionIndex].Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (best is null)
            {
                misses.Add(new WallTruthMissExport(
                    truth.Id,
                    truth.PageNumber,
                    truth.WallType?.ToString() ?? "Unknown",
                    truth.Importance.ToString(),
                    LineExport.From(truth.CenterLine!.Value),
                    truth.CenterLine.Value.Length,
                    truth.Evidence
                        .Append("no canonical wall run matched the reviewed centerline tolerances")
                        .ToArray()));
                continue;
            }

            unmatchedPredictionIndexes.Remove(best.PredictionIndex);
            var run = predicted[best.PredictionIndex];
            matches.Add(new WallTruthMatchExport(
                truth.Id,
                run.Id,
                truth.PageNumber,
                truth.WallType?.ToString() ?? "Unknown",
                run.WallType,
                truth.Importance.ToString(),
                Round(best.Score),
                Round(best.LineDistance),
                Round(best.EndpointDistance),
                Round(best.AngularDifferenceDegrees),
                Round(best.LengthOverlapRatio),
                best.WallTypeMatches,
                new[]
                {
                    $"centerline distance {best.LineDistance:0.###} drawing units",
                    $"endpoint distance {best.EndpointDistance:0.###} drawing units",
                    $"angular difference {best.AngularDifferenceDegrees:0.###} degrees",
                    $"length overlap ratio {best.LengthOverlapRatio:0.###}",
                    best.WallTypeMatches ? "wall type matches" : "wall type differs"
                }));
        }

        var falsePositives = dataset.CompleteTruthSet
            ? unmatchedPredictionIndexes
                .Select(index => predicted[index])
                .Select(run => new WallTruthFalsePositiveExport(
                    run.Id,
                    run.PageNumber,
                    run.WallType,
                    run.CenterLine,
                    run.DrawingLength,
                    run.Confidence,
                    run.Evidence
                        .Take(10)
                        .Append("canonical wall run did not match the complete reviewed truth set")
                        .ToArray()))
                .OrderByDescending(item => item.DrawingLength)
                .ThenBy(item => item.WallRunId, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<WallTruthFalsePositiveExport>();
        var violations = FindNotWallViolations(notWalls, predicted);
        var metrics = BuildMetrics(dataset, structure, truthWalls, predicted, matches, misses, falsePositives, violations);
        var documentIdentityMatches = string.IsNullOrWhiteSpace(dataset.DocumentId)
            || string.Equals(dataset.DocumentId, structure.Document.Id, StringComparison.Ordinal);
        var pageFramesMatch = DatasetPageFramesMatch(dataset.Pages, structure.Pages);
        var assertions = BuildAssertions(
            dataset.QualityGate,
            metrics,
            dataset.CompleteTruthSet,
            documentIdentityMatches,
            pageFramesMatch);
        var passed = assertions.All(assertion => assertion.Passed);

        return new WallTruthEvaluationResult(
            WallTruthEvaluationResult.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            dataset.SchemaVersion,
            structure.SchemaVersion,
            dataset.Name,
            dataset.Version,
            dataset.DocumentId,
            dataset.CompleteTruthSet,
            passed,
            metrics,
            matches,
            misses,
            falsePositives,
            violations,
            assertions,
            new[]
            {
                $"evaluated {truthWalls.Length} reviewed wall(s) against {predicted.Length} canonical wall run(s)",
                $"matched {matches.Count} reviewed wall(s); missed {misses.Count}",
                dataset.CompleteTruthSet
                    ? $"complete truth precision evaluated with {falsePositives.Length} unmatched canonical run(s)"
                    : "truth set is incomplete; precision is intentionally not claimed",
                $"not-wall geometry produced {violations.Count} canonical violation(s)",
                documentIdentityMatches
                    ? "wall truth document identity matches canonical structure"
                    : "wall truth document identity does not match canonical structure",
                pageFramesMatch
                    ? "wall truth page frames match canonical structure"
                    : "wall truth page frames do not match canonical structure",
                $"quality gate {(passed ? "passed" : "failed")}"
            });
    }

    private static WallTruthEvaluationMetrics BuildMetrics(
        WallTruthDataset dataset,
        PlanStructureExport structure,
        IReadOnlyList<WallTruthEntry> truthWalls,
        IReadOnlyList<PlanStructureWallRunExport> predicted,
        IReadOnlyList<WallTruthMatchExport> matches,
        IReadOnlyList<WallTruthMissExport> misses,
        IReadOnlyList<WallTruthFalsePositiveExport> falsePositives,
        IReadOnlyList<WallTruthNotWallViolationExport> violations)
    {
        var truthLengthById = truthWalls.ToDictionary(
            entry => entry.Id,
            entry => entry.CenterLine!.Value.Length,
            StringComparer.Ordinal);
        var truthLength = truthLengthById.Values.Sum();
        var matchedTruthLength = matches.Sum(match => truthLengthById[match.TruthId]);
        var predictedLength = predicted.Sum(run => run.DrawingLength);
        var falsePositiveLength = falsePositives.Sum(item => item.DrawingLength);
        var recall = Ratio(matches.Count, truthWalls.Count);
        double? precision = dataset.CompleteTruthSet
            ? Ratio(matches.Count, predicted.Count)
            : null;
        double? f1 = precision is null || precision.Value + recall <= 0
            ? null
            : 2 * precision.Value * recall / (precision.Value + recall);
        var lengthRecall = Ratio(matchedTruthLength, truthLength);
        double? lengthPrecision = dataset.CompleteTruthSet
            ? Ratio(Math.Max(0, predictedLength - falsePositiveLength), predictedLength)
            : null;
        var matchByTruthId = matches.ToDictionary(match => match.TruthId, StringComparer.Ordinal);
        var major = truthWalls.Where(entry => entry.Importance >= WallTruthImportance.Major).ToArray();
        var critical = truthWalls.Where(entry => entry.Importance == WallTruthImportance.Critical).ToArray();
        var exterior = truthWalls.Where(entry => entry.WallType == WallType.Exterior).ToArray();
        var interior = truthWalls.Where(entry => entry.WallType == WallType.Interior).ToArray();

        return new WallTruthEvaluationMetrics(
            truthWalls.Count,
            predicted.Count,
            matches.Count,
            misses.Count,
            falsePositives.Count,
            violations.Count,
            Round(recall),
            precision is null ? null : Round(precision.Value),
            f1 is null ? null : Round(f1.Value),
            Round(lengthRecall),
            lengthPrecision is null ? null : Round(lengthPrecision.Value),
            Round(MatchedRatio(major, matchByTruthId)),
            Round(MatchedRatio(critical, matchByTruthId)),
            Round(MatchedRatio(exterior, matchByTruthId)),
            Round(MatchedRatio(interior, matchByTruthId)),
            Round(matches.Count == 0 ? 0 : matches.Count(match => match.WallTypeMatches) / (double)matches.Count),
            Round(matches.Count == 0 ? 0 : matches.Average(match => match.LineDistance)),
            Round(matches.Count == 0 ? 0 : matches.Average(match => match.EndpointDistance)),
            Round(matches.Count == 0 ? 0 : matches.Average(match => match.AngularDifferenceDegrees)),
            Round(matches.Count == 0 ? 0 : matches.Average(match => match.LengthOverlapRatio)),
            Round(truthLength),
            Round(matchedTruthLength),
            Round(predictedLength),
            Round(falsePositiveLength),
            Round(violations.Sum(item => item.DrawingLength)),
            structure.WallSolver.SelectedScore,
            structure.WallSolver.Metrics.EndpointConnectivityRatio,
            structure.WallSolver.Metrics.RoomBoundaryClosureRatio);
    }

    private static IReadOnlyList<WallTruthGateAssertionExport> BuildAssertions(
        WallTruthQualityGate gate,
        WallTruthEvaluationMetrics metrics,
        bool completeTruthSet,
        bool documentIdentityMatches,
        bool pageFramesMatch)
    {
        var assertions = new List<WallTruthGateAssertionExport>
        {
            Exact("documentIdentityMatch", 1, documentIdentityMatches ? 1 : 0),
            Exact("pageFrameMatch", 1, pageFramesMatch ? 1 : 0),
            Minimum("recall", gate.MinRecall, metrics.Recall),
            Minimum("lengthWeightedRecall", gate.MinLengthWeightedRecall, metrics.LengthWeightedRecall),
            Minimum("majorWallRecall", gate.MinMajorWallRecall, metrics.MajorWallRecall),
            Minimum("exteriorWallRecall", gate.MinExteriorWallRecall, metrics.ExteriorWallRecall),
            Minimum("wallTypeAccuracy", gate.MinWallTypeAccuracy, metrics.WallTypeAccuracy),
            Maximum("meanLineDistance", gate.MaxMeanLineDistance, metrics.MeanLineDistance),
            Maximum("meanEndpointDistance", gate.MaxMeanEndpointDistance, metrics.MeanEndpointDistance),
            Maximum("notWallViolationCount", gate.MaxNotWallViolationCount, metrics.NotWallViolationCount)
        };
        if (completeTruthSet)
        {
            assertions.Add(Minimum("precision", gate.MinPrecision, metrics.Precision ?? 0));
        }

        return assertions;
    }

    private static WallTruthGateAssertionExport Exact(string metric, double expected, double actual) =>
        new(
            metric,
            "==",
            expected,
            actual,
            Math.Abs(actual - expected) <= 0.000001,
            $"{metric} {actual:0.###} must equal {expected:0.###}");

    private static WallTruthGateAssertionExport Minimum(string metric, double expected, double actual) =>
        new(
            metric,
            ">=",
            expected,
            actual,
            actual >= expected,
            $"{metric} {actual:0.###} must be at least {expected:0.###}");

    private static WallTruthGateAssertionExport Maximum(string metric, double expected, double actual) =>
        new(
            metric,
            "<=",
            expected,
            actual,
            actual <= expected,
            $"{metric} {actual:0.###} must not exceed {expected:0.###}");

    private static double MatchedRatio(
        IReadOnlyList<WallTruthEntry> entries,
        IReadOnlyDictionary<string, WallTruthMatchExport> matchByTruthId) =>
        entries.Count == 0
            ? 1
            : entries.Count(entry => matchByTruthId.ContainsKey(entry.Id)) / (double)entries.Count;

    private static bool DatasetPageFramesMatch(
        IReadOnlyList<WallTruthPage> truthPages,
        IReadOnlyList<PlacementPageExport> structurePages)
    {
        if (truthPages.Count == 0)
        {
            return true;
        }

        var structureByNumber = structurePages.ToDictionary(page => page.PageNumber);
        return truthPages.All(truth =>
            structureByNumber.TryGetValue(truth.PageNumber, out var structure)
            && Math.Abs(truth.Width - structure.Width) <= Math.Max(0.01, structure.Width * 0.000001)
            && Math.Abs(truth.Height - structure.Height) <= Math.Max(0.01, structure.Height * 0.000001));
    }

    private static WallMatchCandidate? Match(
        WallTruthEntry truth,
        PlanStructureWallRunExport run,
        int predictionIndex)
    {
        if (truth.PageNumber != run.PageNumber || truth.CenterLine is null)
        {
            return null;
        }

        var expected = truth.CenterLine.Value;
        var actual = ToPlanLine(run.CenterLine);
        var angle = AngularDifferenceDegrees(expected, actual);
        var lineDistance = SymmetricLineDistance(expected, actual);
        var endpointDistance = EndpointDistance(expected, actual);
        var overlap = LengthOverlapRatio(expected, actual);
        if (angle > truth.MaxAngularDifferenceDegrees
            || lineDistance > truth.MaxLineDistance
            || endpointDistance > truth.MaxEndpointDistance
            || overlap < truth.MinLengthOverlapRatio)
        {
            return null;
        }

        var wallTypeMatches = truth.WallType is null
            || string.Equals(truth.WallType.Value.ToString(), run.WallType, StringComparison.OrdinalIgnoreCase);
        var score =
            (1 - Math.Clamp(lineDistance / truth.MaxLineDistance, 0, 1)) * 0.30
            + (1 - Math.Clamp(endpointDistance / truth.MaxEndpointDistance, 0, 1)) * 0.25
            + (1 - Math.Clamp(angle / truth.MaxAngularDifferenceDegrees, 0, 1)) * 0.15
            + overlap * 0.25
            + (wallTypeMatches ? 0.05 : 0);
        return new WallMatchCandidate(
            predictionIndex,
            score,
            lineDistance,
            endpointDistance,
            angle,
            overlap,
            wallTypeMatches);
    }

    private static IReadOnlyList<WallTruthNotWallViolationExport> FindNotWallViolations(
        IReadOnlyList<WallTruthEntry> notWalls,
        IReadOnlyList<PlanStructureWallRunExport> predicted)
    {
        var violations = new List<WallTruthNotWallViolationExport>();
        foreach (var truth in notWalls)
        {
            foreach (var run in predicted.Where(run => run.PageNumber == truth.PageNumber))
            {
                var overlap = NotWallOverlapRatio(truth, run);
                if (overlap < truth.MinLengthOverlapRatio)
                {
                    continue;
                }

                violations.Add(new WallTruthNotWallViolationExport(
                    truth.Id,
                    run.Id,
                    truth.PageNumber,
                    Round(overlap),
                    Round(run.DrawingLength * overlap),
                    truth.Evidence
                        .Append($"canonical wall run overlaps reviewed not-wall geometry at ratio {overlap:0.###}")
                        .ToArray()));
            }
        }

        return violations
            .OrderByDescending(item => item.DrawingLength)
            .ThenBy(item => item.TruthId, StringComparer.Ordinal)
            .ThenBy(item => item.WallRunId, StringComparer.Ordinal)
            .ToArray();
    }

    private static double NotWallOverlapRatio(
        WallTruthEntry truth,
        PlanStructureWallRunExport run)
    {
        var actual = ToPlanLine(run.CenterLine);
        if (truth.CenterLine is { } line)
        {
            if (AngularDifferenceDegrees(line, actual) > truth.MaxAngularDifferenceDegrees
                || SymmetricLineDistance(line, actual) > truth.MaxLineDistance)
            {
                return 0;
            }

            return LengthOverlapRatio(line, actual);
        }

        if (truth.Bounds is not { } bounds)
        {
            return 0;
        }

        const int sampleCount = 24;
        var inside = 0;
        for (var index = 0; index <= sampleCount; index++)
        {
            if (bounds.Contains(actual.PointAt(index / (double)sampleCount)))
            {
                inside++;
            }
        }

        return inside / (double)(sampleCount + 1);
    }

    private static double SymmetricLineDistance(PlanLineSegment first, PlanLineSegment second) =>
        (second.DistanceToPoint(first.Midpoint) + first.DistanceToPoint(second.Midpoint)) / 2.0;

    private static double EndpointDistance(PlanLineSegment expected, PlanLineSegment actual)
    {
        var direct = expected.Start.DistanceTo(actual.Start) + expected.End.DistanceTo(actual.End);
        var reversed = expected.Start.DistanceTo(actual.End) + expected.End.DistanceTo(actual.Start);
        return Math.Min(direct, reversed) / 2.0;
    }

    private static double AngularDifferenceDegrees(PlanLineSegment first, PlanLineSegment second)
    {
        var difference = Math.Abs(first.AngleRadians - second.AngleRadians) * 180.0 / Math.PI;
        difference %= 180;
        return Math.Min(difference, 180 - difference);
    }

    private static double LengthOverlapRatio(PlanLineSegment expected, PlanLineSegment actual)
    {
        if (expected.Length <= 0 || actual.Length <= 0)
        {
            return 0;
        }

        var direction = expected.Vector.Normalize();
        var actualStart = Dot(actual.Start - expected.Start, direction);
        var actualEnd = Dot(actual.End - expected.Start, direction);
        var overlapStart = Math.Max(0, Math.Min(actualStart, actualEnd));
        var overlapEnd = Math.Min(expected.Length, Math.Max(actualStart, actualEnd));
        return Math.Clamp(Math.Max(0, overlapEnd - overlapStart) / expected.Length, 0, 1);
    }

    private static double Dot(PlanVector first, PlanVector second) =>
        first.X * second.X + first.Y * second.Y;

    private static PlanLineSegment ToPlanLine(LineExport line) =>
        new(new PlanPoint(line.Start.X, line.Start.Y), new PlanPoint(line.End.X, line.End.Y));

    private static double Ratio(double numerator, double denominator) =>
        denominator <= 0 ? 1 : Math.Clamp(numerator / denominator, 0, 1);

    private static double Round(double value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private sealed record WallMatchCandidate(
        int PredictionIndex,
        double Score,
        double LineDistance,
        double EndpointDistance,
        double AngularDifferenceDegrees,
        double LengthOverlapRatio,
        bool WallTypeMatches);
}

public static class WallTruthEvaluationJsonSerializer
{
    public static string Serialize(WallTruthEvaluationResult result, bool writeIndented = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, CreateOptions(writeIndented));
    }

    public static async ValueTask WriteAsync(
        WallTruthEvaluationResult result,
        Stream output,
        bool writeIndented = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);
        await JsonSerializer.SerializeAsync(
                output,
                result,
                CreateOptions(writeIndented),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
