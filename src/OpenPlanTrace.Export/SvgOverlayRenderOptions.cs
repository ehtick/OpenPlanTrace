namespace OpenPlanTrace.Export;

public enum SvgOverlayRenderProfile
{
    Full,
    StructuralReview
}

public sealed record SvgOverlayRenderOptions
{
    public SvgOverlayRenderProfile Profile { get; init; } = SvgOverlayRenderProfile.Full;

    public bool IncludeLegend { get; init; } = true;

    public bool IncludeDiagnostics { get; init; } = true;

    public bool IncludeRegions { get; init; } = true;

    public bool IncludeDimensions { get; init; } = true;

    public bool IncludeAnnotations { get; init; } = true;

    public bool IncludeGridAxes { get; init; } = true;

    public bool IncludeGridBaySpacings { get; init; } = true;

    public bool IncludeWalls { get; init; } = true;

    public bool IncludeWallComponents { get; init; } = true;

    public bool IncludeWallNodes { get; init; } = true;

    public bool IncludeRooms { get; init; } = true;

    public bool IncludeRoomClusters { get; init; } = true;

    public bool IncludeRoomAdjacency { get; init; } = true;

    public bool IncludeOpenings { get; init; } = true;

    public bool IncludeObjects { get; init; } = true;

    public bool IncludeObjectAggregates { get; init; } = true;

    public bool IncludeSurfacePatterns { get; init; } = true;

    public bool IncludeRoutingLayer { get; init; }

    public string BackgroundColor { get; init; } = "#ffffff";

    public static SvgOverlayRenderOptions ForProfile(SvgOverlayRenderProfile profile) =>
        profile switch
        {
            SvgOverlayRenderProfile.StructuralReview => new SvgOverlayRenderOptions
            {
                Profile = SvgOverlayRenderProfile.StructuralReview,
                IncludeWallComponents = false,
                IncludeWallNodes = false,
                IncludeRoomClusters = false,
                IncludeRoomAdjacency = false,
                IncludeObjects = false,
                IncludeObjectAggregates = false,
                IncludeRoutingLayer = false
            },
            _ => new SvgOverlayRenderOptions()
            {
                IncludeRoutingLayer = true
            }
        };

    public static bool TryParseProfile(string value, out SvgOverlayRenderProfile profile)
    {
        switch (NormalizeProfile(value))
        {
            case "full":
            case "debug":
            case "all":
                profile = SvgOverlayRenderProfile.Full;
                return true;
            case "structural":
            case "structuralreview":
            case "structuralreviewoverlay":
            case "review":
            case "geometry":
                profile = SvgOverlayRenderProfile.StructuralReview;
                return true;
            default:
                profile = SvgOverlayRenderProfile.Full;
                return false;
        }
    }

    public static string ProfileName(SvgOverlayRenderProfile profile) =>
        profile switch
        {
            SvgOverlayRenderProfile.StructuralReview => "structural-review",
            _ => "full"
        };

    private static string NormalizeProfile(string value) =>
        string.Concat(
            (value ?? string.Empty)
            .Where(char.IsLetterOrDigit))
            .ToLowerInvariant();
}
