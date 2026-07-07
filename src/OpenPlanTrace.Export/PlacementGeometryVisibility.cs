namespace OpenPlanTrace.Export;

internal static class PlacementGeometryVisibility
{
    public static bool ShouldShowPlacementGraphEdge(
        PlacementWallGraphEdgeExport edge,
        SvgOverlayRenderOptions options) =>
        options.IncludeExcludedPlacementGeometry
        || !edge.ExcludedFromStructuralTopology;

    public static bool ShouldShowWallBodyFootprint(
        WallBodyFootprint footprint,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? evidenceAssessment,
        SvgOverlayRenderOptions options) =>
        options.IncludeExcludedPlacementGeometry
        || !IsExcludedWallBodyFootprint(footprint, component, evidenceAssessment);

    public static bool IsExcludedWallBodyFootprint(
        WallBodyFootprint footprint,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? evidenceAssessment)
    {
        var trustedExteriorShellContinuityFragment =
            WallPlacementReadinessEvaluator.IsTrustedExteriorShellContinuityFragment(
                footprint.SourceWall,
                component,
                evidenceAssessment);
        var trustedRoomBoundaryIsolatedFragment =
            WallPlacementReadinessEvaluator.IsTrustedRoomBoundaryIsolatedFragment(
                footprint.SourceWall,
                component,
                evidenceAssessment);
        var trustedRoomBoundaryIsolatedExteriorWall =
            WallPlacementReadinessEvaluator.IsTrustedRoomBoundaryIsolatedExteriorWall(
                footprint.SourceWall,
                component,
                evidenceAssessment);
        var trustedRecoveredRoomBoundaryObjectLikeWall =
            WallPlacementReadinessEvaluator.IsTrustedRecoveredRoomBoundaryObjectLikeWall(
                footprint.SourceWall,
                component,
                evidenceAssessment);
        var trustedObjectLikeLongCleanFragmentInterior =
            WallPlacementContextGuards.IsTrustedObjectLikeLongCleanFragmentInteriorWallBody(
                footprint.SourceWall,
                component,
                evidenceAssessment);

        return (WallEvidenceExportHelpers.IsExcludedFromStructuralTopology(component, evidenceAssessment)
                && !trustedRecoveredRoomBoundaryObjectLikeWall
                && !trustedObjectLikeLongCleanFragmentInterior)
            || (!trustedExteriorShellContinuityFragment
                && !trustedRoomBoundaryIsolatedFragment
                && !trustedRoomBoundaryIsolatedExteriorWall
                && !trustedRecoveredRoomBoundaryObjectLikeWall
                && !trustedObjectLikeLongCleanFragmentInterior
                && !WallTopologySpanVisibility.IsPlacementReadyStructuralSpan(component, evidenceAssessment));
    }
}
