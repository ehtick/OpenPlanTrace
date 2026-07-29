using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPlanTrace;

public enum WallTruthLabelKind
{
    Wall = 0,
    NotWall
}

public enum WallTruthImportance
{
    Minor = 0,
    Standard,
    Major,
    Critical
}

public sealed record WallTruthDataset
{
    public const string CurrentSchemaVersion = "openplantrace.wall-truth.v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string? Name { get; init; }

    public string? Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string CoordinateSpace { get; init; } = "OpenPlanTracePageCoordinates";

    public string CoordinateUnit { get; init; } = "drawing-unit";

    public string CoordinateOrigin { get; init; } = "TopLeft";

    public string CoordinateYAxisDirection { get; init; } = "Down";

    public string? DocumentId { get; init; }

    public string? SourceName { get; init; }

    public string? SourceFingerprint { get; init; }

    public bool CompleteTruthSet { get; init; }

    public IReadOnlyList<WallTruthPage> Pages { get; init; } = Array.Empty<WallTruthPage>();

    public IReadOnlyList<WallTruthEntry> Entries { get; init; } = Array.Empty<WallTruthEntry>();

    public WallTruthQualityGate QualityGate { get; init; } = new();

    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>();

    public static WallTruthDataset ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Wall truth JSON is empty.", nameof(json));
        }

        WallTruthDataset? dataset;
        try
        {
            dataset = JsonSerializer.Deserialize<WallTruthDataset>(
                json,
                WallTruthJsonSerializer.CreateOptions(writeIndented: false));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"Wall truth JSON is invalid: {exception.Message}", exception);
        }

        if (dataset is null)
        {
            throw new ArgumentException("Wall truth JSON did not contain an object.", nameof(json));
        }

        return Validate(dataset);
    }

    public static WallTruthDataset Validate(WallTruthDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        if (!string.Equals(dataset.SchemaVersion, CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported wall truth schemaVersion '{dataset.SchemaVersion}'. Expected '{CurrentSchemaVersion}'.");
        }

        RequireConstant(dataset.CoordinateSpace, "OpenPlanTracePageCoordinates", nameof(CoordinateSpace));
        RequireConstant(dataset.CoordinateUnit, "drawing-unit", nameof(CoordinateUnit));
        RequireConstant(dataset.CoordinateOrigin, "TopLeft", nameof(CoordinateOrigin));
        RequireConstant(dataset.CoordinateYAxisDirection, "Down", nameof(CoordinateYAxisDirection));

        var pages = dataset.Pages ?? Array.Empty<WallTruthPage>();
        if (pages.Any(page => page.PageNumber <= 0 || page.Width <= 0 || page.Height <= 0))
        {
            throw new ArgumentException("Wall truth pages require positive pageNumber, width, and height.");
        }

        if (pages.GroupBy(page => page.PageNumber).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Wall truth page numbers must be unique.");
        }

        var entries = dataset.Entries ?? Array.Empty<WallTruthEntry>();
        if (entries.GroupBy(entry => entry.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Wall truth entry ids must be unique.");
        }

        var pageNumbers = pages.Select(page => page.PageNumber).ToHashSet();
        foreach (var entry in entries)
        {
            ValidateEntry(entry, pageNumbers);
        }

        ValidateQualityGate(dataset.QualityGate ?? new WallTruthQualityGate());
        return dataset with
        {
            SchemaVersion = CurrentSchemaVersion,
            Pages = pages.OrderBy(page => page.PageNumber).ToArray(),
            Entries = entries
                .OrderBy(entry => entry.PageNumber)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .ToArray(),
            QualityGate = dataset.QualityGate ?? new WallTruthQualityGate(),
            Properties = dataset.Properties ?? new Dictionary<string, string>()
        };
    }

    public BenchmarkManifest ToBenchmarkManifest(string sourcePath, string? fixtureId = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("A source path is required.", nameof(sourcePath));
        }

        var dataset = Validate(this);
        var targets = dataset.Entries
            .Where(entry => entry.LabelKind == WallTruthLabelKind.Wall && entry.CenterLine is not null)
            .Select(entry => new BenchmarkDetectionTarget
            {
                Id = entry.Id,
                PageNumber = entry.PageNumber,
                Bounds = entry.Bounds ?? BoundsFor(entry.CenterLine!.Value, entry.ThicknessDrawingUnits),
                CenterLine = entry.CenterLine,
                MaxLineDistance = entry.MaxLineDistance,
                MaxEndpointDistance = entry.MaxEndpointDistance,
                MaxAngularDifferenceDegrees = entry.MaxAngularDifferenceDegrees,
                MinLengthOverlapRatio = entry.MinLengthOverlapRatio,
                WallType = entry.WallType,
                Label = entry.Label,
                Evidence = entry.Evidence
            })
            .ToArray();

        return new BenchmarkManifest
        {
            Name = dataset.Name ?? "Wall truth benchmark",
            Fixtures =
            [
                new BenchmarkFixture
                {
                    Id = string.IsNullOrWhiteSpace(fixtureId) ? "wall-truth" : fixtureId.Trim(),
                    Name = dataset.Name,
                    SourcePath = sourcePath,
                    Expectations = new BenchmarkExpectations
                    {
                        WallMetrics = new BenchmarkDetectorMetricExpectations
                        {
                            Targets = targets,
                            MinRecall = dataset.QualityGate.MinRecall,
                            MinPrecision = dataset.CompleteTruthSet ? dataset.QualityGate.MinPrecision : null,
                            CompleteTruthSet = dataset.CompleteTruthSet
                        }
                    },
                    Properties = new Dictionary<string, string>
                    {
                        ["wallTruthSchemaVersion"] = CurrentSchemaVersion,
                        ["wallTruthVersion"] = dataset.Version ?? string.Empty,
                        ["sourceFingerprint"] = dataset.SourceFingerprint ?? string.Empty
                    }
                }
            ]
        };
    }

    private static void ValidateEntry(WallTruthEntry entry, IReadOnlySet<int> pageNumbers)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            throw new ArgumentException("Every wall truth entry requires an id.");
        }

        if (entry.PageNumber <= 0)
        {
            throw new ArgumentException($"Wall truth entry '{entry.Id}' requires a positive pageNumber.");
        }

        if (pageNumbers.Count > 0 && !pageNumbers.Contains(entry.PageNumber))
        {
            throw new ArgumentException(
                $"Wall truth entry '{entry.Id}' references page {entry.PageNumber}, which is not declared in pages.");
        }

        if (entry.LabelKind == WallTruthLabelKind.Wall)
        {
            if (entry.CenterLine is null || entry.CenterLine.Value.Length <= 0)
            {
                throw new ArgumentException($"Wall truth entry '{entry.Id}' requires a non-empty centerLine.");
            }
        }
        else if (entry.CenterLine is null
                 && (entry.Bounds is null || entry.Bounds.Value.Width <= 0 || entry.Bounds.Value.Height <= 0))
        {
            throw new ArgumentException(
                $"Not-wall truth entry '{entry.Id}' requires either centerLine or positive bounds.");
        }

        if (entry.ThicknessDrawingUnits is <= 0)
        {
            throw new ArgumentException($"Wall truth entry '{entry.Id}' thicknessDrawingUnits must be positive.");
        }

        if (entry.MaxLineDistance <= 0
            || entry.MaxEndpointDistance <= 0
            || entry.MaxAngularDifferenceDegrees <= 0
            || entry.MinLengthOverlapRatio is <= 0 or > 1)
        {
            throw new ArgumentException($"Wall truth entry '{entry.Id}' contains invalid geometry tolerances.");
        }
    }

    private static void ValidateQualityGate(WallTruthQualityGate gate)
    {
        var ratios = new[]
        {
            gate.MinRecall,
            gate.MinPrecision,
            gate.MinLengthWeightedRecall,
            gate.MinMajorWallRecall,
            gate.MinExteriorWallRecall,
            gate.MinWallTypeAccuracy
        };
        if (ratios.Any(value => value is < 0 or > 1))
        {
            throw new ArgumentException("Wall truth quality-gate ratios must be between 0 and 1.");
        }

        if (gate.MaxMeanLineDistance < 0
            || gate.MaxMeanEndpointDistance < 0
            || gate.MaxNotWallViolationCount < 0)
        {
            throw new ArgumentException("Wall truth quality-gate maximums cannot be negative.");
        }
    }

    private static void RequireConstant(string actual, string expected, string fieldName)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Wall truth {fieldName} must be '{expected}'.");
        }
    }

    private static PlanRect BoundsFor(PlanLineSegment line, double? thickness)
    {
        var half = Math.Max(0.5, (thickness ?? 1) / 2.0);
        var left = Math.Min(line.Start.X, line.End.X) - half;
        var top = Math.Min(line.Start.Y, line.End.Y) - half;
        var right = Math.Max(line.Start.X, line.End.X) + half;
        var bottom = Math.Max(line.Start.Y, line.End.Y) + half;
        return new PlanRect(left, top, right - left, bottom - top);
    }
}

public sealed record WallTruthPage(
    int PageNumber,
    double Width,
    double Height);

public sealed record WallTruthEntry
{
    public string Id { get; init; } = string.Empty;

    public int PageNumber { get; init; }

    public WallTruthLabelKind LabelKind { get; init; } = WallTruthLabelKind.Wall;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlanLineSegment? CenterLine { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlanRect? Bounds { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WallType? WallType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ThicknessDrawingUnits { get; init; }

    public WallTruthImportance Importance { get; init; } = WallTruthImportance.Standard;

    public double MaxLineDistance { get; init; } = 4.0;

    public double MaxEndpointDistance { get; init; } = 8.0;

    public double MaxAngularDifferenceDegrees { get; init; } = 4.0;

    public double MinLengthOverlapRatio { get; init; } = 0.75;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reviewer { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ReviewedAt { get; init; }

    public IReadOnlyList<string> SourceDetectionIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}

public sealed record WallTruthQualityGate
{
    public double MinRecall { get; init; } = 0.90;

    public double MinPrecision { get; init; } = 0.85;

    public double MinLengthWeightedRecall { get; init; } = 0.94;

    public double MinMajorWallRecall { get; init; } = 0.98;

    public double MinExteriorWallRecall { get; init; } = 0.96;

    public double MinWallTypeAccuracy { get; init; } = 0.90;

    public double MaxMeanLineDistance { get; init; } = 4.0;

    public double MaxMeanEndpointDistance { get; init; } = 8.0;

    public int MaxNotWallViolationCount { get; init; }
}

public static class WallTruthJsonSerializer
{
    public static string Serialize(WallTruthDataset dataset, bool writeIndented = true)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        return JsonSerializer.Serialize(WallTruthDataset.Validate(dataset), CreateOptions(writeIndented));
    }

    public static async ValueTask WriteAsync(
        WallTruthDataset dataset,
        Stream output,
        bool writeIndented = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(output);
        await JsonSerializer.SerializeAsync(
                output,
                WallTruthDataset.Validate(dataset),
                CreateOptions(writeIndented),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
