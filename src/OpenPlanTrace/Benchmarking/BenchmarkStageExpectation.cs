namespace OpenPlanTrace;

public sealed record BenchmarkStageExpectation
{
    public string Stage { get; init; } = string.Empty;

    public double? MaxDurationMilliseconds { get; init; }

    public int? MaxDiagnostics { get; init; }

    public int? MaxWarnings { get; init; }

    public int? MaxErrors { get; init; }

    public bool? RequireDependencyReady { get; init; }

    public int? MaxMissingRequiredReads { get; init; }

    public int? MaxMissingOptionalReads { get; init; }

    public bool? RequireRuntimeRequiredReadsHaveData { get; init; }

    public bool? RequireRuntimeOptionalReadsHaveData { get; init; }

    public int? MaxEmptyRequiredRuntimeReads { get; init; }

    public int? MaxEmptyOptionalRuntimeReads { get; init; }

    public bool? RequireWritesOnlyDeclaredArtifacts { get; init; }

    public int? MaxUndeclaredChangedArtifacts { get; init; }

    public int? MaxEmptyDeclaredOutputs { get; init; }

    public IReadOnlyList<BenchmarkStageArtifactExpectation> ArtifactExpectations { get; init; } =
        Array.Empty<BenchmarkStageArtifactExpectation>();
}

public sealed record BenchmarkStageArtifactExpectation
{
    public PlanArtifactKind Artifact { get; init; } = PlanArtifactKind.Unknown;

    public int? MinBeforeCount { get; init; }

    public int? MaxBeforeCount { get; init; }

    public int? MinAfterCount { get; init; }

    public int? MaxAfterCount { get; init; }

    public int? MinDelta { get; init; }

    public int? MaxDelta { get; init; }

    public bool? RequireChanged { get; init; }
}
