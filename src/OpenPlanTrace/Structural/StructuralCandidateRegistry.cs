namespace OpenPlanTrace;

internal sealed class StructuralCandidateRegistry
{
    private readonly Dictionary<string, CandidateDraft> _candidates = new(StringComparer.Ordinal);

    public IReadOnlyCollection<CandidateDraft> Drafts => _candidates.Values;

    public CandidateDraft Add(
        string id,
        int pageNumber,
        PlanLineSegment centerLine,
        double thickness,
        WallType wallType,
        Confidence confidence,
        StructuralCandidateOrigin origins,
        bool isEligible,
        bool wasAccepted,
        IEnumerable<string>? sourceWallIds = null,
        IEnumerable<string>? sourceWallGraphEdgeIds = null,
        IEnumerable<string>? sourceWallComponentIds = null,
        IEnumerable<string>? sourcePrimitiveIds = null,
        IEnumerable<string>? sourceRoomIds = null,
        IEnumerable<string>? sourceOpeningIds = null,
        IEnumerable<string>? evidence = null)
    {
        if (!_candidates.TryGetValue(id, out var candidate))
        {
            candidate = new CandidateDraft(
                id,
                pageNumber,
                StructuralGeometry.Canonicalize(centerLine),
                thickness,
                wallType,
                confidence,
                origins,
                isEligible,
                wasAccepted);
            _candidates.Add(id, candidate);
        }
        else
        {
            candidate.Merge(
                centerLine,
                thickness,
                wallType,
                confidence,
                origins,
                isEligible,
                wasAccepted);
        }

        candidate.SourceWallIds.UnionWith(sourceWallIds ?? Array.Empty<string>());
        candidate.SourceWallGraphEdgeIds.UnionWith(sourceWallGraphEdgeIds ?? Array.Empty<string>());
        candidate.SourceWallComponentIds.UnionWith(
            sourceWallComponentIds ?? Array.Empty<string>());
        candidate.SourcePrimitiveIds.UnionWith(sourcePrimitiveIds ?? Array.Empty<string>());
        candidate.SourceRoomIds.UnionWith(sourceRoomIds ?? Array.Empty<string>());
        candidate.SourceOpeningIds.UnionWith(sourceOpeningIds ?? Array.Empty<string>());
        candidate.Evidence.UnionWith(evidence ?? Array.Empty<string>());
        return candidate;
    }

    public bool TryGet(string id, out CandidateDraft candidate) =>
        _candidates.TryGetValue(id, out candidate!);

    public IReadOnlyList<CandidateDraft> FindCompatible(
        int pageNumber,
        PlanLineSegment line,
        StructuralSolverOptions options,
        double minimumOverlapRatio = 0.45)
    {
        var angleTolerance = options.AngleToleranceDegrees * Math.PI / 180.0;
        return _candidates.Values
            .Where(candidate => candidate.PageNumber == pageNumber)
            .Where(candidate => StructuralGeometry.AreParallel(candidate.CenterLine, line, angleTolerance))
            .Where(candidate =>
                StructuralGeometry.PerpendicularDistance(candidate.CenterLine, line)
                <= Math.Max(options.AxisTolerance, candidate.Thickness / 2.0))
            .Where(candidate =>
                StructuralGeometry.OverlapRatio(candidate.CenterLine, line) >= minimumOverlapRatio)
            .OrderBy(candidate => candidate.HasStrongNegativeEvidence)
            .ThenByDescending(candidate => candidate.CurrentUnaryScore)
            .ThenByDescending(candidate => StructuralGeometry.OverlapRatio(candidate.CenterLine, line))
            .ThenBy(candidate => StructuralGeometry.PerpendicularDistance(candidate.CenterLine, line))
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<StructuralWallCandidate> Build()
    {
        return _candidates.Values
            .Select(candidate => candidate.Build())
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Bounds.Y)
            .ThenBy(candidate => candidate.Bounds.X)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
    }

    internal sealed class CandidateDraft
    {
        private readonly Dictionary<string, StructuralEvidenceSignal> _signals = new(StringComparer.Ordinal);

        public CandidateDraft(
            string id,
            int pageNumber,
            PlanLineSegment centerLine,
            double thickness,
            WallType wallType,
            Confidence confidence,
            StructuralCandidateOrigin origins,
            bool isEligible,
            bool wasAccepted)
        {
            Id = id;
            PageNumber = pageNumber;
            CenterLine = centerLine;
            Thickness = thickness;
            WallType = wallType;
            Confidence = confidence;
            Origins = origins;
            IsEligible = isEligible;
            WasAcceptedByPreliminaryPipeline = wasAccepted;
        }

        public string Id { get; }

        public int PageNumber { get; }

        public PlanLineSegment CenterLine { get; private set; }

        public double Thickness { get; private set; }

        public WallType WallType { get; private set; }

        public Confidence Confidence { get; private set; }

        public StructuralCandidateOrigin Origins { get; private set; }

        public bool IsEligible { get; private set; }

        public bool WasAcceptedByPreliminaryPipeline { get; private set; }

        public double DrawingLength => CenterLine.Length;

        public PlanRect Bounds =>
            CenterLine.Bounds.Inflate(Math.Max(Thickness / 2.0, 0.5));

        public HashSet<string> SourceWallIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SourceWallGraphEdgeIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SourceWallComponentIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SourcePrimitiveIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SourceRoomIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SourceOpeningIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Evidence { get; } = new(StringComparer.Ordinal);

        public bool HasStrongNegativeEvidence =>
            _signals.Values.Any(signal => signal.IsStrongBlockingSemanticNegative);

        public bool HasBlockingSemanticEvidence =>
            _signals.Values.Any(signal => signal.IsBlockingSemanticNegative);

        public bool HasIndependentWallBodyEvidence =>
            _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.WallBody
                && signal.Weight >= 0.30);

        public bool HasAcceptedPlacementReadyWallBodyEvidence =>
            WasAcceptedByPreliminaryPipeline
            && !HasReviewWallEvidence
            && !HasRejectedWallEvidence
            && _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.AcceptedWall
                && signal.Weight > 0)
            && _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.WallBody
                && signal.Weight >= 0.16);

        public bool HasCorroboratableFragmentAxisEvidence =>
            _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.FragmentAxisContinuity
                && signal.Weight >= 0.16);

        public bool HasCorroboratedFragmentAxisEvidence =>
            HasCorroboratableFragmentAxisEvidence
            && Origins.HasFlag(StructuralCandidateOrigin.WallGraph)
            && Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary)
            && _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.ExistingGraph
                && signal.Weight > 0)
            && _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.StructuralTerritory
                && signal.Weight > 0)
            && _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary
                && signal.Weight >= 0.08)
            && !_signals.Values.Any(signal =>
                signal.Weight <= -0.45
                && signal.Kind is
                    StructuralEvidenceSignalKind.DoorOrOpeningSymbol
                        or StructuralEvidenceSignalKind.SurfacePattern
                        or StructuralEvidenceSignalKind.RepeatedDetailPattern
                        or StructuralEvidenceSignalKind.DimensionOrAnnotation
                        or StructuralEvidenceSignalKind.ObjectOrFixture
                        or StructuralEvidenceSignalKind.UnsupportedOblique
                        or StructuralEvidenceSignalKind.IsolatedStructuralIsland
                        or StructuralEvidenceSignalKind.UnoccupiedShellExtension
                        or StructuralEvidenceSignalKind.WallBodyThicknessOutlier);

        public bool HasRejectedWallEvidence =>
            _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.RejectedWall
                && signal.Weight < 0);

        public bool HasReviewWallEvidence =>
            _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.ReviewWall
                && signal.Weight < 0);

        public bool HasDimensionOrAnnotationEvidence =>
            _signals.Values.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.DimensionOrAnnotation
                && signal.Weight < 0);

        public bool HasExplicitFilledWallBodyEvidence =>
            Evidence.Any(item =>
                item.Contains(
                    "filled closed vector wall body",
                    StringComparison.OrdinalIgnoreCase)
                || item.Contains(
                    "filled wall-solid primitive",
                    StringComparison.OrdinalIgnoreCase));

        public bool HasTrustedExteriorShellEvidence =>
            WallType == WallType.Exterior
            && (SourceWallIds.Any(wallId =>
                    wallId.Contains(
                        "wall-exterior-shell-source-backed:",
                        StringComparison.Ordinal))
                || Evidence.Any(item =>
                    item.Contains(
                        "source-backed exterior shell",
                        StringComparison.OrdinalIgnoreCase)
                    || item.Contains(
                        "trusted long isolated exterior shell promoted",
                        StringComparison.OrdinalIgnoreCase)
                    || item.Contains(
                        "trusted long main exterior shell promoted",
                        StringComparison.OrdinalIgnoreCase)));

        public bool HasTrustedRoomConfirmedWallEvidence =>
            WasAcceptedByPreliminaryPipeline
            && HasIndependentWallBodyEvidence
            && Evidence.Any(item =>
                item.Contains(
                    "room-confirmed wall body promoted to placement-ready",
                    StringComparison.OrdinalIgnoreCase)
                || item.Contains(
                    "room-confirmed isolated wall graph fragment kept placement-ready",
                    StringComparison.OrdinalIgnoreCase));

        public bool HasParallelFaceEvidence =>
            Evidence.Any(item =>
                item.Contains(
                    "parallel wall-face",
                    StringComparison.OrdinalIgnoreCase));

        public double CurrentUnaryScore =>
            Math.Round(
                Math.Clamp(_signals.Values.Sum(signal => signal.Weight) - 0.16, -2, 2),
                6,
                MidpointRounding.AwayFromZero);

        public void Merge(
            PlanLineSegment centerLine,
            double thickness,
            WallType wallType,
            Confidence confidence,
            StructuralCandidateOrigin origins,
            bool isEligible,
            bool wasAccepted)
        {
            if (confidence.Value > Confidence.Value)
            {
                CenterLine = StructuralGeometry.Canonicalize(centerLine);
                Confidence = confidence;
            }

            Thickness = Math.Max(Thickness, thickness);
            if (WallType == WallType.Unknown || wallType == WallType.Exterior)
            {
                WallType = wallType;
            }

            Origins |= origins;
            IsEligible |= isEligible;
            WasAcceptedByPreliminaryPipeline |= wasAccepted;
        }

        public void AddSignal(StructuralEvidenceSignal signal)
        {
            _signals[signal.Id] = signal;
            Evidence.Add(signal.Description);
            SourcePrimitiveIds.UnionWith(signal.SourcePrimitiveIds);
        }

        public void ReduceContextOnlyPenalty(
            double weight,
            string description)
        {
            var signalIds = _signals.Values
                .Where(signal =>
                    signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary
                    && signal.Weight < weight)
                .Select(signal => signal.Id)
                .ToArray();
            foreach (var signalId in signalIds)
            {
                _signals[signalId] = _signals[signalId] with
                {
                    Weight = weight,
                    Description = description
                };
            }

            if (signalIds.Length > 0)
            {
                Evidence.Add(description);
            }
        }

        public void AddOrigin(StructuralCandidateOrigin origin) =>
            Origins |= origin;

        public StructuralWallCandidate Build()
        {
            var signals = _signals.Values
                .OrderBy(signal => signal.Kind)
                .ThenBy(signal => signal.SourceId, StringComparer.Ordinal)
                .ThenBy(signal => signal.Id, StringComparer.Ordinal)
                .ToArray();
            return new StructuralWallCandidate(
                Id,
                PageNumber,
                CenterLine,
                Thickness,
                WallType,
                Confidence,
                Origins,
                IsEligible,
                WasAcceptedByPreliminaryPipeline,
                CurrentUnaryScore,
                SourceWallIds.Order(StringComparer.Ordinal).ToArray(),
                SourceWallGraphEdgeIds.Order(StringComparer.Ordinal).ToArray(),
                SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray(),
                SourceRoomIds.Order(StringComparer.Ordinal).ToArray(),
                SourceOpeningIds.Order(StringComparer.Ordinal).ToArray(),
                signals,
                Evidence.Order(StringComparer.Ordinal).ToArray())
            {
                SourceWallComponentIds =
                    SourceWallComponentIds.Order(StringComparer.Ordinal).ToArray()
            };
        }
    }
}
