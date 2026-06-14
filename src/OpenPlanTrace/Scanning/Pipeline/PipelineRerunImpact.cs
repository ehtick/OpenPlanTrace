namespace OpenPlanTrace;

public sealed record PipelineRerunImpact(
    PlanArtifactKind Artifact,
    bool IsSourceArtifact,
    string ImpactScope,
    IReadOnlyList<string> ProducerStages,
    IReadOnlyList<string> DirectConsumerStages,
    IReadOnlyList<string> AffectedStages,
    IReadOnlyList<PlanArtifactKind> AffectedArtifacts,
    int FirstAffectedWave,
    int AffectedStageCount,
    IReadOnlyList<string> Evidence)
{
    public bool HasImpact => AffectedStageCount > 0;
}
