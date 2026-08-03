using System.Globalization;

namespace OpenPlanTrace;

internal sealed class StructuralPathTopologyStage : IPipelineStage
{
    public string Name => "structural-path-topology";

    public ValueTask ExecuteAsync(
        ScanContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        context.StructuralPathTopology = MixedStructuralPathTopologyBuilder.Build(
            context.StructuralPlanSolution,
            context.CurvedWallCandidates,
            context.Walls,
            context.Calibration,
            context.Options.StructuralSolver);

        var topology = context.StructuralPathTopology;
        context.AddDiagnostic(
            "structural_path_topology.built",
            DiagnosticSeverity.Info,
            Name,
            $"Built {topology.Metrics.LinePathCount} line path(s), {topology.Metrics.CircularArcPathCount} circular-arc path(s), and {topology.Metrics.JunctionCount} review-only mixed junction(s).",
            confidence: topology.Metrics.RejectedCurvedCandidateCount == 0
                ? Confidence.High
                : Confidence.Medium,
            scope: DiagnosticScope.Document,
            sourcePrimitiveIds: topology.Paths.SelectMany(path => path.SourcePrimitiveIds),
            properties: new Dictionary<string, string>
            {
                ["contractVersion"] = topology.ContractVersion,
                ["linePathCount"] = topology.Metrics.LinePathCount.ToString(CultureInfo.InvariantCulture),
                ["circularArcPathCount"] = topology.Metrics.CircularArcPathCount.ToString(CultureInfo.InvariantCulture),
                ["junctionCount"] = topology.Metrics.JunctionCount.ToString(CultureInfo.InvariantCulture),
                ["tangentJunctionCount"] = topology.Metrics.TangentJunctionCount.ToString(CultureInfo.InvariantCulture),
                ["cornerJunctionCount"] = topology.Metrics.CornerJunctionCount.ToString(CultureInfo.InvariantCulture),
                ["connectedCurvedPathCount"] = topology.Metrics.ConnectedCurvedPathCount.ToString(CultureInfo.InvariantCulture),
                ["unconnectedCurvedPathCount"] = topology.Metrics.UnconnectedCurvedPathCount.ToString(CultureInfo.InvariantCulture),
                ["rejectedCurvedCandidateCount"] = topology.Metrics.RejectedCurvedCandidateCount.ToString(CultureInfo.InvariantCulture),
                ["sourceGeometryMutated"] = bool.FalseString
            });

        if (topology.Metrics.RejectedCurvedCandidateCount > 0)
        {
            context.AddDiagnostic(
                "structural_path_topology.invalid_curves_rejected",
                DiagnosticSeverity.Warning,
                Name,
                $"Rejected {topology.Metrics.RejectedCurvedCandidateCount} invalid circular-arc candidate(s) from mixed structural topology.",
                confidence: Confidence.High,
                scope: DiagnosticScope.Document,
                properties: new Dictionary<string, string>
                {
                    ["rejectedCurvedCandidateCount"] = topology.Metrics.RejectedCurvedCandidateCount.ToString(CultureInfo.InvariantCulture)
                });
        }

        return ValueTask.CompletedTask;
    }
}
