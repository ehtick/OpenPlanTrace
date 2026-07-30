namespace OpenPlanTrace;

internal sealed class CrossDomainStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    public string Name => "cross-domain-structural-fusion";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        foreach (var candidate in context.Candidates.Drafts
                     .Where(candidate => candidate.HasCorroboratedFragmentAxisEvidence))
        {
            candidate.ReduceContextOnlyPenalty(
                -0.08,
                "fragment continuity, wall graph, occupied structural territory, and opposite room loops jointly corroborate the wall axis");
        }
    }
}
