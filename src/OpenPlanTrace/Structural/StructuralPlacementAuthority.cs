namespace OpenPlanTrace;

internal enum StructuralPlacementAuthorityKind
{
    ReviewOnly,
    ContextSupported,
    Corroborated,
    Independent
}

internal readonly record struct StructuralPlacementAuthority(
    StructuralPlacementAuthorityKind Kind,
    bool IsAbsoluteBlock,
    string Reason)
{
    public bool CanSelect => Kind != StructuralPlacementAuthorityKind.ReviewOnly;
}

internal static class StructuralPlacementAuthorityEvaluator
{
    public static StructuralPlacementAuthority Evaluate(
        StructuralWallCandidate candidate)
    {
        if (!candidate.IsEligible)
        {
            return ReviewOnly(
                "candidate geometry is invalid or below the structural eligibility threshold",
                isAbsoluteBlock: true);
        }

        if (candidate.HasAbsoluteBlockingEvidence)
        {
            return ReviewOnly(
                "absolute structural evidence blocks coordinate placement",
                isAbsoluteBlock: true);
        }

        var hasIndependentSupport =
            candidate.HasIndependentWallBodyEvidence
            || candidate.HasAcceptedPlacementReadyWallBodyEvidence;
        var hasPlacementReadyCorroboration = candidate.HasCrossDomainWallBodyEvidence;
        var hasOpeningHostedReviewCorroboration =
            candidate.HasOpeningHostedReviewWallBodyCorroboration;
        var hasCorroboratedSupport =
            hasPlacementReadyCorroboration
            || hasOpeningHostedReviewCorroboration;
        if (candidate.HasStrongNegativeEvidence
            && !hasIndependentSupport
            && !hasCorroboratedSupport)
        {
            return ReviewOnly(
                "strong semantic or contextual negative evidence lacks independent wall-body support");
        }

        if (hasIndependentSupport)
        {
            return new StructuralPlacementAuthority(
                StructuralPlacementAuthorityKind.Independent,
                IsAbsoluteBlock: false,
                "independent wall-body evidence authorizes structural placement");
        }

        if (hasCorroboratedSupport)
        {
            return new StructuralPlacementAuthority(
                StructuralPlacementAuthorityKind.Corroborated,
                IsAbsoluteBlock: false,
                hasPlacementReadyCorroboration
                    ? "cross-domain wall-body, topology, and room evidence authorize structural placement"
                    : "explicit opening-host, graph, and multi-room evidence authorize canonical review selection without coordinate-placement readiness");
        }

        return new StructuralPlacementAuthority(
            StructuralPlacementAuthorityKind.ContextSupported,
            IsAbsoluteBlock: false,
            "clean preliminary geometry may be strengthened by structural context");
    }

    public static bool CanParticipateInRecoveryBundle(
        StructuralWallCandidate candidate)
    {
        var authority = Evaluate(candidate);
        return authority.CanSelect
            && (authority.Kind is
                    StructuralPlacementAuthorityKind.Independent
                        or StructuralPlacementAuthorityKind.Corroborated
                || candidate.WasAcceptedByPreliminaryPipeline
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.WallGraph)
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell)
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.OpeningHost));
    }

    private static StructuralPlacementAuthority ReviewOnly(
        string reason,
        bool isAbsoluteBlock = false) =>
        new(
            StructuralPlacementAuthorityKind.ReviewOnly,
            isAbsoluteBlock,
            reason);
}
