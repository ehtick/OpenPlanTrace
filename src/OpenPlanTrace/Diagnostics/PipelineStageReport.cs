namespace OpenPlanTrace;

public sealed record PipelineStageReport
{
    public PipelineStageReport(
        string stage,
        TimeSpan duration,
        int inputCount,
        int outputCount,
        int diagnosticCount = 0,
        int infoCount = 0,
        int warningCount = 0,
        int errorCount = 0)
        : this(
            stage,
            duration,
            inputCount,
            outputCount,
            diagnosticCount,
            infoCount,
            warningCount,
            errorCount,
            PipelineStageMetadataCatalog.Get(stage),
            Array.Empty<PipelineArtifactSnapshot>(),
            Array.Empty<PipelineArtifactSnapshot>(),
            Array.Empty<PipelineArtifactChange>(),
            Array.Empty<PipelineArtifactDelta>())
    {
    }

    public PipelineStageReport(
        string stage,
        TimeSpan duration,
        int inputCount,
        int outputCount,
        int diagnosticCount,
        int infoCount,
        int warningCount,
        int errorCount,
        PipelineStageMetadata metadata)
        : this(
            stage,
            duration,
            inputCount,
            outputCount,
            diagnosticCount,
            infoCount,
            warningCount,
            errorCount,
            metadata,
            Array.Empty<PipelineArtifactSnapshot>(),
            Array.Empty<PipelineArtifactSnapshot>(),
            Array.Empty<PipelineArtifactChange>(),
            Array.Empty<PipelineArtifactDelta>())
    {
    }

    public PipelineStageReport(
        string stage,
        TimeSpan duration,
        int inputCount,
        int outputCount,
        int diagnosticCount,
        int infoCount,
        int warningCount,
        int errorCount,
        PipelineStageMetadata metadata,
        IReadOnlyList<PipelineArtifactSnapshot> inputArtifacts,
        IReadOnlyList<PipelineArtifactSnapshot> outputArtifacts,
        IReadOnlyList<PipelineArtifactChange> changedArtifacts)
        : this(
            stage,
            duration,
            inputCount,
            outputCount,
            diagnosticCount,
            infoCount,
            warningCount,
            errorCount,
            metadata,
            inputArtifacts,
            outputArtifacts,
            changedArtifacts,
            BuildArtifactDeltas(metadata, outputArtifacts, changedArtifacts))
    {
    }

    public PipelineStageReport(
        string stage,
        TimeSpan duration,
        int inputCount,
        int outputCount,
        int diagnosticCount,
        int infoCount,
        int warningCount,
        int errorCount,
        PipelineStageMetadata metadata,
        IReadOnlyList<PipelineArtifactSnapshot> inputArtifacts,
        IReadOnlyList<PipelineArtifactSnapshot> outputArtifacts,
        IReadOnlyList<PipelineArtifactChange> changedArtifacts,
        IReadOnlyList<PipelineArtifactDelta> artifactDeltas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(inputArtifacts);
        ArgumentNullException.ThrowIfNull(outputArtifacts);
        ArgumentNullException.ThrowIfNull(changedArtifacts);
        ArgumentNullException.ThrowIfNull(artifactDeltas);

        Stage = stage;
        Duration = duration;
        InputCount = inputCount;
        OutputCount = outputCount;
        DiagnosticCount = diagnosticCount;
        InfoCount = infoCount;
        WarningCount = warningCount;
        ErrorCount = errorCount;
        Metadata = metadata;
        InputArtifacts = inputArtifacts;
        OutputArtifacts = outputArtifacts;
        ChangedArtifacts = changedArtifacts;
        ArtifactDeltas = artifactDeltas;
        RuntimeReadiness = PipelineStageRuntimeReadiness.From(metadata, inputArtifacts);
        var contract = PipelineStageContract.From(
            metadata.Writes,
            changedArtifacts.Select(artifact => artifact.Artifact));
        Contract = contract;
        OutputReadiness = PipelineStageOutputReadiness.From(
            metadata,
            outputArtifacts,
            artifactDeltas,
            contract);
    }

    public string Stage { get; init; }

    public TimeSpan Duration { get; init; }

    public int InputCount { get; init; }

    public int OutputCount { get; init; }

    public int DiagnosticCount { get; init; }

    public int InfoCount { get; init; }

    public int WarningCount { get; init; }

    public int ErrorCount { get; init; }

    public PipelineStageMetadata Metadata { get; init; }

    public IReadOnlyList<PipelineArtifactSnapshot> InputArtifacts { get; init; }

    public IReadOnlyList<PipelineArtifactSnapshot> OutputArtifacts { get; init; }

    public IReadOnlyList<PipelineArtifactChange> ChangedArtifacts { get; init; }

    public IReadOnlyList<PipelineArtifactDelta> ArtifactDeltas { get; init; }

    public PipelineStageRuntimeReadiness RuntimeReadiness { get; init; }

    public PipelineStageOutputReadiness OutputReadiness { get; init; }

    public PipelineStageContract Contract { get; init; }

    private static IReadOnlyList<PipelineArtifactDelta> BuildArtifactDeltas(
        PipelineStageMetadata metadata,
        IReadOnlyList<PipelineArtifactSnapshot> outputArtifacts,
        IReadOnlyList<PipelineArtifactChange> changedArtifacts)
    {
        var declaredWrites = metadata.Writes
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .ToHashSet();
        var outputCounts = outputArtifacts.ToDictionary(artifact => artifact.Artifact, artifact => artifact.Count);
        var changeByArtifact = changedArtifacts.ToDictionary(artifact => artifact.Artifact);

        return declaredWrites
            .Concat(changedArtifacts.Select(artifact => artifact.Artifact))
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .Distinct()
            .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
            .Select(artifact =>
            {
                var change = changeByArtifact.GetValueOrDefault(artifact);
                var afterCount = change?.AfterCount
                    ?? (outputCounts.TryGetValue(artifact, out var outputCount) ? outputCount : 0);
                var beforeCount = change?.BeforeCount ?? afterCount;

                return new PipelineArtifactDelta(
                    artifact,
                    beforeCount,
                    afterCount,
                    declaredWrites.Contains(artifact));
            })
            .ToArray();
    }
}
