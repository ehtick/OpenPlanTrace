namespace OpenPlanTrace;

public sealed record BenchmarkCaseIssueSummary(
    string Code,
    DiagnosticSeverity Severity,
    string Stage,
    string Scope,
    int Count,
    string Message,
    IReadOnlyList<int> PageNumbers,
    double? MaxConfidence,
    int SourcePrimitiveCount,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyDictionary<string, string> Properties);

public sealed record BenchmarkStageSummary(
    string Stage,
    double DurationMilliseconds,
    int InputCount,
    int OutputCount,
    int DiagnosticCount,
    int InfoCount,
    int WarningCount,
    int ErrorCount,
    string DisplayName,
    string Kind,
    int DependencyLevel,
    int PreferredDependencyLevel,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> OptionalReads,
    IReadOnlyList<string> Writes,
    IReadOnlyList<string> Capabilities,
    bool IsDependencyReady,
    IReadOnlyList<string> MissingRequiredReads,
    IReadOnlyList<string> MissingOptionalReads,
    IReadOnlyList<PipelineArtifactSnapshot> InputArtifacts,
    IReadOnlyList<PipelineArtifactSnapshot> OutputArtifacts,
    IReadOnlyList<PipelineArtifactChange> ChangedArtifacts,
    IReadOnlyList<PipelineArtifactDelta> ArtifactDeltas)
{
    public PipelineStageRuntimeReadiness RuntimeReadiness { get; init; } = PipelineStageRuntimeReadiness.Empty;

    public PipelineStageOutputReadiness OutputReadiness { get; init; } = PipelineStageOutputReadiness.Empty;

    public PipelineStageContract Contract { get; init; } = PipelineStageContract.Empty;
}
