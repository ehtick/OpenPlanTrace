namespace OpenPlanTrace;

public sealed record PipelineStagePlan(
    int Order,
    string Stage,
    PipelineStageKind Kind,
    int ExecutionWave,
    bool IsParallelCandidate,
    int DependencyLevel,
    int PreferredDependencyLevel,
    IReadOnlyList<PlanArtifactKind> Reads,
    IReadOnlyList<PlanArtifactKind> OptionalReads,
    IReadOnlyList<PlanArtifactKind> Writes,
    IReadOnlyList<PlanArtifactKind> AvailableBefore,
    IReadOnlyList<PlanArtifactKind> SatisfiedReads,
    IReadOnlyList<PlanArtifactKind> MissingRequiredReads,
    IReadOnlyList<PlanArtifactKind> SatisfiedOptionalReads,
    IReadOnlyList<PlanArtifactKind> MissingOptionalReads)
{
    public bool IsDependencyReady => MissingRequiredReads.Count == 0;
}
