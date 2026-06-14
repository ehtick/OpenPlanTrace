namespace OpenPlanTrace;

public sealed record PipelineExecutionWave(
    int Level,
    IReadOnlyList<string> Stages,
    IReadOnlyList<PlanArtifactKind> Reads,
    IReadOnlyList<PlanArtifactKind> Writes,
    IReadOnlyList<PlanArtifactKind> WriteConflictArtifacts,
    IReadOnlyList<PipelineWaveDependency> IntraWaveDependencies,
    bool IsParallelCandidate,
    IReadOnlyList<string> DirectDownstreamStages,
    IReadOnlyList<PlanArtifactKind> DownstreamReadArtifacts)
{
    public int StageCount => Stages.Count;

    public bool HasWriteConflicts => WriteConflictArtifacts.Count > 0;

    public bool HasIntraWaveDependencies => IntraWaveDependencies.Count > 0;

    public int DirectDownstreamStageCount => DirectDownstreamStages.Count;

    public string ParallelReadiness =>
        IsParallelCandidate
            ? "Ready"
            : StageCount <= 1
                ? "SingleStage"
                : HasWriteConflicts
                    ? "WriteConflict"
                    : HasIntraWaveDependencies
                        ? "IntraWaveDependency"
                        : "Sequential";

    public string RecommendedExecutionMode => IsParallelCandidate ? "Parallel" : "Sequential";

    public IReadOnlyList<string> SchedulingReasons
    {
        get
        {
            var reasons = new List<string>();
            if (IsParallelCandidate)
            {
                reasons.Add("Wave has multiple stages, no write conflicts, and no intra-wave dependencies.");
            }
            else if (StageCount <= 1)
            {
                reasons.Add("Wave contains a single stage.");
            }

            if (HasWriteConflicts)
            {
                reasons.Add($"Wave has write conflicts on {string.Join(", ", WriteConflictArtifacts)}.");
            }

            if (HasIntraWaveDependencies)
            {
                reasons.Add("Wave contains stages that read artifacts written by another stage in the same wave.");
            }

            if (reasons.Count == 0)
            {
                reasons.Add("Wave should run sequentially until a scheduler proves it is safe to parallelize.");
            }

            if (DirectDownstreamStages.Count > 0)
            {
                reasons.Add($"Wave output feeds {DirectDownstreamStages.Count} later stage(s): {string.Join(", ", DirectDownstreamStages.Take(8))}.");
            }

            return reasons;
        }
    }
}

public sealed record PipelineWaveDependency(
    string Stage,
    string DependsOnStage,
    IReadOnlyList<PlanArtifactKind> Artifacts);
