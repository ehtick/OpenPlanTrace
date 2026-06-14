namespace OpenPlanTrace;

public sealed record PipelineRerunPlan(
    string PlanId,
    string DisplayName,
    IReadOnlyList<PlanArtifactKind> ChangedArtifacts,
    IReadOnlyList<PlanArtifactKind> ChangedSourceArtifacts,
    IReadOnlyList<string> DirectConsumerStages,
    IReadOnlyList<string> RerunStages,
    IReadOnlyList<int> RerunWaves,
    IReadOnlyList<PlanArtifactKind> AffectedArtifacts,
    int FirstRerunWave,
    int LastRerunWave,
    int RerunStageCount,
    int AffectedArtifactCount,
    string RecommendedExecutionMode,
    IReadOnlyList<string> Evidence)
{
    public bool HasWork => RerunStageCount > 0;
}
