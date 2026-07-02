namespace OpenPlanTrace;

internal sealed class PipelineDiagnosticsBuilder
{
    private readonly List<PipelineStageReport> _stageReports = new();
    private readonly List<DiagnosticEntry> _messages = new();
    private IReadOnlyList<PipelineArtifactSnapshot> _artifactInventory = Array.Empty<PipelineArtifactSnapshot>();
    private int _nextMessageSequence;

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public PipelineExecutionPlan ExecutionPlan { get; private set; } = PipelineExecutionPlan.Empty;

    public int MessageCount => _messages.Count;

    public int MessageCursor => _nextMessageSequence;

    public void Add(PlanDiagnostic diagnostic) => _messages.Add(new DiagnosticEntry(_nextMessageSequence++, diagnostic));

    public int RemoveWhere(Predicate<PlanDiagnostic> predicate) =>
        _messages.RemoveAll(entry => predicate(entry.Diagnostic));

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

    public IReadOnlyList<PlanDiagnostic> MessagesSince(int startSequence) =>
        _messages
            .Where(entry => entry.Sequence >= startSequence)
            .Select(entry => entry.Diagnostic)
            .ToArray();

    public PipelineDiagnostics Build() =>
        new(
            StartedAt,
            DateTimeOffset.UtcNow,
            _stageReports.ToArray(),
            _messages.Select(entry => entry.Diagnostic).ToArray(),
            ExecutionPlan,
            _artifactInventory);

    private sealed record DiagnosticEntry(int Sequence, PlanDiagnostic Diagnostic);
}
