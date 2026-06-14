namespace OpenPlanTrace;

internal sealed class PipelineDiagnosticsBuilder
{
    private readonly List<PipelineStageReport> _stageReports = new();
    private readonly List<PlanDiagnostic> _messages = new();
    private IReadOnlyList<PipelineArtifactSnapshot> _artifactInventory = Array.Empty<PipelineArtifactSnapshot>();

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public PipelineExecutionPlan ExecutionPlan { get; private set; } = PipelineExecutionPlan.Empty;

    public int MessageCount => _messages.Count;

    public void Add(PlanDiagnostic diagnostic) => _messages.Add(diagnostic);

    public void AddStageReport(PipelineStageReport report) => _stageReports.Add(report);

    public void SetExecutionPlan(PipelineExecutionPlan executionPlan)
    {
        ArgumentNullException.ThrowIfNull(executionPlan);

        ExecutionPlan = executionPlan;
    }

    public void SetArtifactInventory(IReadOnlyList<PipelineArtifactSnapshot> artifactInventory)
    {
        ArgumentNullException.ThrowIfNull(artifactInventory);

        _artifactInventory = artifactInventory;
    }

    public IReadOnlyList<PlanDiagnostic> MessagesSince(int startIndex) =>
        _messages.Skip(startIndex).ToArray();

    public PipelineDiagnostics Build() =>
        new(
            StartedAt,
            DateTimeOffset.UtcNow,
            _stageReports.ToArray(),
            _messages.ToArray(),
            ExecutionPlan,
            _artifactInventory);
}
