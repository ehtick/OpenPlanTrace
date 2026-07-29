namespace OpenPlanTrace;

public sealed record StructuralSolverOptions
{
    public bool Enabled { get; init; } = true;

    public double MinimumCandidateLength { get; init; } = 3;

    public double AxisTolerance { get; init; } = 3;

    public double EndpointTolerance { get; init; } = 4;

    public double AngleToleranceDegrees { get; init; } = 3;

    public double MaximumContinuationGap { get; init; } = 12;

    public double DuplicateOverlapRatio { get; init; } = 0.78;

    public double MinimumDominantWallLengthRatio { get; init; } = 1.35;

    public double MaximumDominantWallScoreDeficit { get; init; } = 0.15;

    public double MaximumDominantWallConfidenceDeficit { get; init; } = 0.15;

    public int MaximumRoomBoundaryAlternativesPerEdge { get; init; } = 4;

    public double MinimumConsiderationScore { get; init; } = -0.10;

    public double InitialSelectionScore { get; init; } = 0.08;

    public int MaximumOptimizationPasses { get; init; } = 8;

    public double ObjectiveImprovementTolerance { get; init; } = 0.0001;

    public double RoomClosureBonus { get; init; } = 0.85;

    public double ExteriorContinuityBonus { get; init; } = 0.16;

    public double HardConflictPenalty { get; init; } = 1.50;
}
