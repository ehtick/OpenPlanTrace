namespace OpenPlanTrace;

internal sealed class TopologyStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    public string Name => "preliminary-topology-evidence";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        var candidatesByWallId = context.Candidates.Drafts
            .SelectMany(candidate => candidate.SourceWallIds.Select(wallId => (WallId: wallId, Candidate: candidate)))
            .GroupBy(item => item.WallId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Candidate).Distinct().ToArray(),
                StringComparer.Ordinal);
        var territoryByCandidateId = BuildTerritoryAssessments(
            context,
            candidatesByWallId);
        AddTerritorySignals(context, territoryByCandidateId);
        var edgeCandidateIds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var edge in context.Source.WallGraph.Edges
                     .OrderBy(edge => edge.PageNumber)
                     .ThenBy(edge => edge.Id, StringComparer.Ordinal))
        {
            if (!candidatesByWallId.TryGetValue(edge.WallId, out var candidates))
            {
                continue;
            }

            edgeCandidateIds[edge.Id] = candidates.Select(candidate => candidate.Id).ToArray();
            foreach (var candidate in candidates)
            {
                candidate.SourceWallGraphEdgeIds.Add(edge.Id);
                candidate.AddOrigin(StructuralCandidateOrigin.WallGraph);
                var excludedFromStructuralTerritory =
                    TerritoryFor(territoryByCandidateId, candidate.Id).IsExcludedUnanchored;
                var graphWeight = candidate.HasRejectedWallEvidence
                    ? -0.12
                    : excludedFromStructuralTerritory
                        ? 0
                        : 0.18;
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:graph:{edge.Id}",
                        StructuralEvidenceSignalKind.ExistingGraph,
                        graphWeight,
                        edge.Id,
                        candidate.HasRejectedWallEvidence
                            ? $"preliminary wall graph edge {edge.Id} conflicts with final wall rejection"
                            : excludedFromStructuralTerritory
                                ? $"preliminary wall graph edge {edge.Id} retained as provenance without structural support because its component was excluded from structural topology"
                            : $"represented by preliminary wall graph edge {edge.Id}",
                        candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
            }
        }

        foreach (var node in context.Source.WallGraph.Nodes
                     .OrderBy(node => node.PageNumber)
                     .ThenBy(node => node.Id, StringComparer.Ordinal))
        {
            var incidentEdges = context.Source.WallGraph.Edges
                .Where(edge =>
                    string.Equals(edge.FromNodeId, node.Id, StringComparison.Ordinal)
                    || string.Equals(edge.ToNodeId, node.Id, StringComparison.Ordinal))
                .ToArray();
            var candidateIds = incidentEdges
                .Where(edge => edgeCandidateIds.ContainsKey(edge.Id))
                .SelectMany(edge => edgeCandidateIds[edge.Id])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (candidateIds.Length == 0)
            {
                continue;
            }

            context.Junctions.Add(
                new StructuralJunctionCandidate(
                    $"structural-junction:{node.Id}",
                    node.PageNumber,
                    node.Position,
                    node.Kind,
                    candidateIds,
                    new[] { node.Id },
                    node.Confidence,
                    node.Evidence
                        .Append($"preliminary junction supports {candidateIds.Length} structural candidate(s)")
                        .ToArray()));

            foreach (var candidateId in candidateIds)
            {
                if (!context.Candidates.TryGet(candidateId, out var candidate))
                {
                    continue;
                }

                var excludedFromStructuralTerritory =
                    TerritoryFor(territoryByCandidateId, candidate.Id).IsExcludedUnanchored;
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:junction:{node.Id}",
                        StructuralEvidenceSignalKind.Junction,
                        candidate.HasRejectedWallEvidence || excludedFromStructuralTerritory
                            ? 0
                            : Math.Min(0.12, 0.05 + (node.Degree * 0.015)),
                        node.Id,
                        candidate.HasRejectedWallEvidence
                            ? $"preliminary node {node.Id} retained as provenance but not positive support after final wall rejection"
                            : excludedFromStructuralTerritory
                                ? $"preliminary node {node.Id} retained as provenance without support because it belongs only to an excluded structural island"
                            : $"connected to preliminary wall node {node.Id} with degree {node.Degree}",
                        candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
            }
        }
    }

    private static IReadOnlyDictionary<string, CandidateTerritoryAssessment> BuildTerritoryAssessments(
        StructuralEvidenceBuildContext context,
        IReadOnlyDictionary<string, StructuralCandidateRegistry.CandidateDraft[]> candidatesByWallId)
    {
        var componentsByWallId = context.Source.WallGraph.Components
            .SelectMany(component => component.WallIds.Select(wallId => (WallId: wallId, Component: component)))
            .GroupBy(item => item.WallId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.Component)
                    .DistinctBy(component => component.Id, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var roomContexts =
            RoomBoundaryStructuralEvidenceProducer.ClassifyRoomContexts(context.Source.Rooms);
        var trustedRoomIds = roomContexts
            .Where(item => item.Value == StructuralRoomLoopContext.Indoor)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var openingHostWallIds = context.Source.Openings
            .Where(opening =>
                StructuralOpeningSupport
                    .Assess(opening, trustedRoomIds)
                    .HasTrustedRoomContext)
            .SelectMany(opening => opening.HostWallIds
                .Concat(opening.AdjacentWallIds)
                .Append(opening.WallId ?? string.Empty))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var directAnchorCandidateIds = context.Candidates.Drafts
            .Where(candidate =>
                HasDirectTerritoryAnchor(
                    candidate,
                    trustedRoomIds,
                    openingHostWallIds))
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var anchoredComponentIds = context.Source.WallGraph.Components
            .Where(component => component.WallIds
                .Where(candidatesByWallId.ContainsKey)
                .SelectMany(wallId => candidatesByWallId[wallId])
                .Any(candidate => directAnchorCandidateIds.Contains(candidate.Id)))
            .Select(component => component.Id)
            .ToHashSet(StringComparer.Ordinal);
        var componentsByCandidateId = context.Candidates.Drafts.ToDictionary(
            candidate => candidate.Id,
            candidate => candidate.SourceWallIds
                .Where(componentsByWallId.ContainsKey)
                .SelectMany(wallId => componentsByWallId[wallId])
                .DistinctBy(component => component.Id, StringComparer.Ordinal)
                .OrderBy(component => component.Id, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
        var territorySeeds = context.Candidates.Drafts
            .Where(candidate => !candidate.HasBlockingSemanticEvidence)
            .Where(candidate =>
                directAnchorCandidateIds.Contains(candidate.Id)
                || componentsByCandidateId[candidate.Id].Any(component =>
                    !IsExcludedStructuralComponent(component)
                    || anchoredComponentIds.Contains(component.Id)))
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var excludedCandidateIds = componentsByCandidateId
            .Where(item =>
                item.Value.Length > 0
                && item.Value.All(IsExcludedStructuralComponent))
            .Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var structurallyConnectedCandidateIds = ExpandStructuralTerritory(
            context.Candidates.Drafts,
            territorySeeds,
            excludedCandidateIds,
            context.Options);

        return context.Candidates.Drafts.ToDictionary(
            candidate => candidate.Id,
            candidate =>
            {
                var components = componentsByCandidateId[candidate.Id];
                var excludedOnly = components.Length > 0
                    && components.All(IsExcludedStructuralComponent);
                var coherentComponent = components.Any(component =>
                    !IsExcludedStructuralComponent(component));
                var anchored = directAnchorCandidateIds.Contains(candidate.Id)
                    || components.Any(component => anchoredComponentIds.Contains(component.Id))
                    || structurallyConnectedCandidateIds.Contains(candidate.Id);
                return new CandidateTerritoryAssessment(
                    HasPositiveSupport: coherentComponent || anchored,
                    IsExcludedUnanchored: excludedOnly && !anchored,
                    ComponentIds: components.Select(component => component.Id).ToArray(),
                    ComponentKinds: components
                        .Select(component => component.Kind)
                        .Distinct()
                        .Order()
                        .ToArray());
            },
            StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> ExpandStructuralTerritory(
        IReadOnlyCollection<StructuralCandidateRegistry.CandidateDraft> candidates,
        IReadOnlySet<string> seedCandidateIds,
        IReadOnlySet<string> excludedCandidateIds,
        StructuralSolverOptions options)
    {
        var values = candidates
            .Where(candidate => candidate.IsEligible)
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var supported = seedCandidateIds.ToHashSet(StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var candidate in values.Where(candidate =>
                         !supported.Contains(candidate.Id)
                         && !excludedCandidateIds.Contains(candidate.Id)
                         && !candidate.HasBlockingSemanticEvidence))
            {
                if (!values
                    .Where(other =>
                        supported.Contains(other.Id)
                        && other.PageNumber == candidate.PageNumber)
                    .Any(other => HasPlausibleStructuralConnection(
                        candidate,
                        other,
                        options)))
                {
                    continue;
                }

                supported.Add(candidate.Id);
                changed = true;
            }
        }

        var trustedStructuralCandidates = values
            .Where(candidate =>
                supported.Contains(candidate.Id)
                && !excludedCandidateIds.Contains(candidate.Id))
            .ToArray();
        foreach (var candidate in values.Where(candidate =>
                     excludedCandidateIds.Contains(candidate.Id)
                     && !supported.Contains(candidate.Id)
                     && !candidate.HasBlockingSemanticEvidence
                     && !candidate.HasReviewWallEvidence
                     && candidate.HasIndependentWallBodyEvidence))
        {
            if (trustedStructuralCandidates.Any(trusted =>
                    trusted.PageNumber == candidate.PageNumber
                    && HasPlausibleStructuralConnection(
                        candidate,
                        trusted,
                        options)))
            {
                supported.Add(candidate.Id);
            }
        }

        return supported;
    }

    private static bool HasPlausibleStructuralConnection(
        StructuralCandidateRegistry.CandidateDraft candidate,
        StructuralCandidateRegistry.CandidateDraft supported,
        StructuralSolverOptions options)
    {
        var angleTolerance = Math.Max(
            options.AngleToleranceDegrees * 2.0,
            8.0) * Math.PI / 180.0;
        var angle = StructuralGeometry.AngleDifference(
            candidate.CenterLine,
            supported.CenterLine);
        if (angle <= angleTolerance)
        {
            return StructuralGeometry.PerpendicularDistance(
                    candidate.CenterLine,
                    supported.CenterLine) <= options.AxisTolerance
                && StructuralGeometry.ProjectedGap(
                    candidate.CenterLine,
                    supported.CenterLine) <= options.MaximumContinuationGap;
        }

        var tolerance = Math.Max(
            options.EndpointTolerance,
            Math.Min(
                options.EndpointTolerance * 2.0,
                options.AxisTolerance
                + (Math.Min(candidate.Thickness, supported.Thickness) / 2.0)));
        return StructuralGeometry.EndpointDistance(
                candidate.CenterLine,
                supported.CenterLine) <= tolerance
            || StructuralGeometry.IsPointNearInterior(
                candidate.CenterLine.Start,
                supported.CenterLine,
                tolerance)
            || StructuralGeometry.IsPointNearInterior(
                candidate.CenterLine.End,
                supported.CenterLine,
                tolerance)
            || StructuralGeometry.IsPointNearInterior(
                supported.CenterLine.Start,
                candidate.CenterLine,
                tolerance)
            || StructuralGeometry.IsPointNearInterior(
                supported.CenterLine.End,
                candidate.CenterLine,
                tolerance);
    }

    private static void AddTerritorySignals(
        StructuralEvidenceBuildContext context,
        IReadOnlyDictionary<string, CandidateTerritoryAssessment> territoryByCandidateId)
    {
        foreach (var candidate in context.Candidates.Drafts)
        {
            var territory = TerritoryFor(territoryByCandidateId, candidate.Id);
            if (territory.HasPositiveSupport && !candidate.HasRejectedWallEvidence)
            {
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:structural-territory",
                        StructuralEvidenceSignalKind.StructuralTerritory,
                        0.06,
                        string.Join(",", territory.ComponentIds),
                        territory.ComponentIds.Count == 0
                            ? "candidate is directly anchored to trusted room, opening, or exterior-shell evidence"
                            : $"candidate belongs to coherent or anchored structural component(s) {string.Join(",", territory.ComponentIds)}",
                        candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
            }

            if (!territory.IsExcludedUnanchored)
            {
                continue;
            }

            candidate.AddSignal(
                new StructuralEvidenceSignal(
                    $"signal:{candidate.Id}:isolated-structural-island",
                    StructuralEvidenceSignalKind.IsolatedStructuralIsland,
                    -1.35,
                    string.Join(",", territory.ComponentIds),
                    $"candidate belongs only to excluded {string.Join("/", territory.ComponentKinds)} component(s) without trusted room, explicit opening, coherent wall-chain, or source-backed shell support",
                    candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
        }
    }

    private static bool HasDirectTerritoryAnchor(
        StructuralCandidateRegistry.CandidateDraft candidate,
        IReadOnlySet<string> trustedRoomIds,
        IReadOnlySet<string> openingHostWallIds)
    {
        if (candidate.SourceRoomIds.Any(trustedRoomIds.Contains)
            || candidate.SourceWallIds.Any(openingHostWallIds.Contains)
            || candidate.HasTrustedRoomConfirmedWallEvidence)
        {
            return true;
        }

        return candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell)
            && candidate.HasTrustedExteriorShellEvidence;
    }

    private static bool IsExcludedStructuralComponent(WallGraphComponent component) =>
        component.ExcludedFromStructuralTopology
        || component.Kind is WallGraphComponentKind.ObjectLikeIsland
            or WallGraphComponentKind.IsolatedFragment;

    private static CandidateTerritoryAssessment TerritoryFor(
        IReadOnlyDictionary<string, CandidateTerritoryAssessment> territoryByCandidateId,
        string candidateId) =>
        territoryByCandidateId.TryGetValue(candidateId, out var territory)
            ? territory
            : CandidateTerritoryAssessment.Empty;

    private sealed record CandidateTerritoryAssessment(
        bool HasPositiveSupport,
        bool IsExcludedUnanchored,
        IReadOnlyList<string> ComponentIds,
        IReadOnlyList<WallGraphComponentKind> ComponentKinds)
    {
        public static CandidateTerritoryAssessment Empty { get; } =
            new(
                HasPositiveSupport: false,
                IsExcludedUnanchored: false,
                Array.Empty<string>(),
                Array.Empty<WallGraphComponentKind>());
    }
}
