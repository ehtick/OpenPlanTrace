namespace OpenPlanTrace;

public sealed record PipelineDiagnostics
{
    public PipelineDiagnostics(
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<PipelineStageReport> stageReports,
        IReadOnlyList<PlanDiagnostic> messages)
        : this(startedAt, completedAt, stageReports, messages, PipelineExecutionPlan.Empty)
    {
    }

    public PipelineDiagnostics(
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<PipelineStageReport> stageReports,
        IReadOnlyList<PlanDiagnostic> messages,
        PipelineExecutionPlan executionPlan)
        : this(
            startedAt,
            completedAt,
            stageReports,
            messages,
            executionPlan,
            Array.Empty<PipelineArtifactSnapshot>())
    {
    }

    public PipelineDiagnostics(
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<PipelineStageReport> stageReports,
        IReadOnlyList<PlanDiagnostic> messages,
        PipelineExecutionPlan executionPlan,
        IReadOnlyList<PipelineArtifactSnapshot> artifactInventory)
    {
        ArgumentNullException.ThrowIfNull(stageReports);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(executionPlan);
        ArgumentNullException.ThrowIfNull(artifactInventory);

        StartedAt = startedAt;
        CompletedAt = completedAt;
        StageReports = stageReports;
        Messages = messages;
        ExecutionPlan = executionPlan;
        ArtifactInventory = artifactInventory;
    }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }

    public IReadOnlyList<PipelineStageReport> StageReports { get; init; }

    public IReadOnlyList<PlanDiagnostic> Messages { get; init; }

    public PipelineExecutionPlan ExecutionPlan { get; init; }

    public IReadOnlyList<PipelineArtifactSnapshot> ArtifactInventory { get; init; }

    public TimeSpan Duration => CompletedAt - StartedAt;

    public bool HasErrors => Messages.Any(message => message.Severity == DiagnosticSeverity.Error);

    public int InfoCount => Messages.Count(message => message.Severity == DiagnosticSeverity.Info);

    public int WarningCount => Messages.Count(message => message.Severity == DiagnosticSeverity.Warning);

    public int ErrorCount => Messages.Count(message => message.Severity == DiagnosticSeverity.Error);
}
