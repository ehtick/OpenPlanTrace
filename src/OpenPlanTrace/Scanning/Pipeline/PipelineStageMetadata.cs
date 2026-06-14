namespace OpenPlanTrace;

public sealed record PipelineStageMetadata(
    string Stage,
    string DisplayName,
    PipelineStageKind Kind,
    IReadOnlyList<PlanArtifactKind> Reads,
    IReadOnlyList<PlanArtifactKind> Writes,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<PlanArtifactKind> OptionalReads)
{
    public PipelineStageMetadata(
        string stage,
        string displayName,
        PipelineStageKind kind,
        IReadOnlyList<PlanArtifactKind> reads,
        IReadOnlyList<PlanArtifactKind> writes,
        IReadOnlyList<string> capabilities)
        : this(stage, displayName, kind, reads, writes, capabilities, Array.Empty<PlanArtifactKind>())
    {
    }
}
