namespace OpenPlanTrace;

public sealed record PipelinePlanIssue(
    string Code,
    DiagnosticSeverity Severity,
    string Stage,
    string Message,
    IReadOnlyList<PlanArtifactKind> Artifacts);
