using System.Globalization;
using System.Text.Json.Serialization;

namespace OpenPlanTrace;

public sealed record PipelineArtifactSnapshot
{
    [JsonConstructor]
    public PipelineArtifactSnapshot(
        PlanArtifactKind artifact,
        int count,
        string? stateKey = null,
        int revision = 0,
        IReadOnlyList<string>? evidence = null)
    {
        var normalizedCount = Math.Max(0, count);
        var normalizedRevision = revision > 0 ? revision : CreateRevision(artifact, normalizedCount);
        var normalizedStateKey = string.IsNullOrWhiteSpace(stateKey)
            ? CreateStateKey(artifact, normalizedCount, normalizedRevision)
            : stateKey.Trim();

        Artifact = artifact;
        Count = normalizedCount;
        StateKey = normalizedStateKey;
        Revision = normalizedRevision;
        Evidence = evidence is { Count: > 0 }
            ? evidence.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray()
            : EvidenceFor(artifact, normalizedCount, normalizedStateKey, normalizedRevision);
    }

    public PlanArtifactKind Artifact { get; init; }

    public int Count { get; init; }

    public string StateKey { get; init; }

    public int Revision { get; init; }

    public IReadOnlyList<string> Evidence { get; init; }

    private static string CreateStateKey(
        PlanArtifactKind artifact,
        int count,
        int revision) =>
        $"{artifact}:{count.ToString(CultureInfo.InvariantCulture)}:{revision.ToString("x8", CultureInfo.InvariantCulture)}";

    private static int CreateRevision(PlanArtifactKind artifact, int count)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            var hash = offsetBasis;
            foreach (var character in artifact.ToString())
            {
                hash ^= character;
                hash *= prime;
            }

            hash ^= (uint)count;
            hash *= prime;
            var revision = (int)(hash & 0x7fffffff);
            return revision == 0 ? 1 : revision;
        }
    }

    private static IReadOnlyList<string> EvidenceFor(
        PlanArtifactKind artifact,
        int count,
        string stateKey,
        int revision) =>
        new[]
        {
            $"artifact kind {artifact}",
            $"artifact count {count.ToString(CultureInfo.InvariantCulture)}",
            $"state key {stateKey}",
            $"revision {revision.ToString(CultureInfo.InvariantCulture)}"
        };
}

public sealed record PipelineStageRuntimeReadiness(
    bool RequiredReadsHaveData,
    bool HasEmptyRequiredReads,
    IReadOnlyList<PlanArtifactKind> NonEmptyRequiredReads,
    IReadOnlyList<PlanArtifactKind> EmptyRequiredReads,
    bool OptionalReadsHaveData,
    bool HasEmptyOptionalReads,
    IReadOnlyList<PlanArtifactKind> NonEmptyOptionalReads,
    IReadOnlyList<PlanArtifactKind> EmptyOptionalReads,
    IReadOnlyList<string> Evidence)
{
    public static PipelineStageRuntimeReadiness Empty { get; } = new(
        true,
        false,
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>(),
        true,
        false,
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>(),
        new[] { "runtime readiness unavailable" });

    public static PipelineStageRuntimeReadiness From(
        PipelineStageMetadata metadata,
        IReadOnlyList<PipelineArtifactSnapshot> inputArtifacts)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(inputArtifacts);

        var counts = inputArtifacts
            .GroupBy(artifact => artifact.Artifact)
            .ToDictionary(group => group.Key, group => group.Max(artifact => artifact.Count));
        var requiredReads = Normalize(metadata.Reads);
        var optionalReads = Normalize(metadata.OptionalReads);
        var nonEmptyRequiredReads = NonEmpty(requiredReads, counts);
        var emptyRequiredReads = EmptyReads(requiredReads, counts);
        var nonEmptyOptionalReads = NonEmpty(optionalReads, counts);
        var emptyOptionalReads = EmptyReads(optionalReads, counts);
        var evidence = new List<string>
        {
            $"required runtime reads with data {nonEmptyRequiredReads.Length}/{requiredReads.Length}",
            $"optional runtime reads with data {nonEmptyOptionalReads.Length}/{optionalReads.Length}"
        };

        if (emptyRequiredReads.Length > 0)
        {
            evidence.Add($"empty required runtime reads: {string.Join(",", emptyRequiredReads.Select(artifact => artifact.ToString()))}");
        }

        if (emptyOptionalReads.Length > 0)
        {
            evidence.Add($"empty optional runtime reads: {string.Join(",", emptyOptionalReads.Select(artifact => artifact.ToString()))}");
        }

        return new PipelineStageRuntimeReadiness(
            emptyRequiredReads.Length == 0,
            emptyRequiredReads.Length > 0,
            nonEmptyRequiredReads,
            emptyRequiredReads,
            optionalReads.Length == 0 || emptyOptionalReads.Length == 0,
            emptyOptionalReads.Length > 0,
            nonEmptyOptionalReads,
            emptyOptionalReads,
            evidence);
    }

    private static PlanArtifactKind[] NonEmpty(
        IReadOnlyList<PlanArtifactKind> artifacts,
        IReadOnlyDictionary<PlanArtifactKind, int> counts) =>
        artifacts
            .Where(artifact => counts.TryGetValue(artifact, out var count) && count > 0)
            .ToArray();

    private static PlanArtifactKind[] EmptyReads(
        IReadOnlyList<PlanArtifactKind> artifacts,
        IReadOnlyDictionary<PlanArtifactKind, int> counts) =>
        artifacts
            .Where(artifact => !counts.TryGetValue(artifact, out var count) || count == 0)
            .ToArray();

    private static PlanArtifactKind[] Normalize(IEnumerable<PlanArtifactKind> artifacts) =>
        artifacts
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .Distinct()
            .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
            .ToArray();
}

public sealed record PipelineStageOutputReadiness(
    bool DeclaredOutputsHaveData,
    bool HasEmptyDeclaredOutputs,
    IReadOnlyList<PlanArtifactKind> NonEmptyDeclaredOutputs,
    IReadOnlyList<PlanArtifactKind> EmptyDeclaredOutputs,
    IReadOnlyList<PlanArtifactKind> ChangedDeclaredOutputs,
    IReadOnlyList<PlanArtifactKind> UnchangedDeclaredOutputs,
    bool HasUndeclaredChanges,
    IReadOnlyList<PlanArtifactKind> UndeclaredChangedArtifacts,
    IReadOnlyList<string> Evidence)
{
    public static PipelineStageOutputReadiness Empty { get; } = new(
        true,
        false,
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>(),
        false,
        Array.Empty<PlanArtifactKind>(),
        new[] { "output readiness unavailable" });

    [JsonIgnore]
    public bool IsAvailable =>
        !Evidence.SequenceEqual(Empty.Evidence, StringComparer.Ordinal)
        || NonEmptyDeclaredOutputs.Count > 0
        || EmptyDeclaredOutputs.Count > 0
        || ChangedDeclaredOutputs.Count > 0
        || UnchangedDeclaredOutputs.Count > 0
        || UndeclaredChangedArtifacts.Count > 0
        || HasEmptyDeclaredOutputs
        || HasUndeclaredChanges;

    public static PipelineStageOutputReadiness From(
        PipelineStageMetadata metadata,
        IReadOnlyList<PipelineArtifactSnapshot> outputArtifacts,
        IReadOnlyList<PipelineArtifactDelta> artifactDeltas,
        PipelineStageContract contract)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(outputArtifacts);
        ArgumentNullException.ThrowIfNull(artifactDeltas);
        ArgumentNullException.ThrowIfNull(contract);

        var declaredWrites = Normalize(metadata.Writes);
        var outputCounts = outputArtifacts
            .GroupBy(artifact => artifact.Artifact)
            .ToDictionary(group => group.Key, group => group.Max(artifact => artifact.Count));
        var changedArtifacts = artifactDeltas
            .Where(delta => delta.Changed)
            .Select(delta => delta.Artifact)
            .ToHashSet();
        var nonEmptyDeclaredOutputs = declaredWrites
            .Where(artifact => outputCounts.TryGetValue(artifact, out var count) && count > 0)
            .ToArray();
        var emptyDeclaredOutputs = declaredWrites
            .Where(artifact => !outputCounts.TryGetValue(artifact, out var count) || count == 0)
            .ToArray();
        var changedDeclaredOutputs = declaredWrites
            .Where(changedArtifacts.Contains)
            .ToArray();
        var unchangedDeclaredOutputs = declaredWrites
            .Where(artifact => !changedArtifacts.Contains(artifact))
            .ToArray();
        var undeclaredChangedArtifacts = Normalize(contract.UndeclaredChangedArtifacts);
        var evidence = new List<string>
        {
            $"declared outputs with data {nonEmptyDeclaredOutputs.Length}/{declaredWrites.Length}",
            $"changed declared outputs {changedDeclaredOutputs.Length}/{declaredWrites.Length}"
        };

        if (emptyDeclaredOutputs.Length > 0)
        {
            evidence.Add($"empty declared outputs: {string.Join(",", emptyDeclaredOutputs.Select(artifact => artifact.ToString()))}");
        }

        if (undeclaredChangedArtifacts.Length > 0)
        {
            evidence.Add($"undeclared changed artifacts: {string.Join(",", undeclaredChangedArtifacts.Select(artifact => artifact.ToString()))}");
        }

        return new PipelineStageOutputReadiness(
            emptyDeclaredOutputs.Length == 0,
            emptyDeclaredOutputs.Length > 0,
            nonEmptyDeclaredOutputs,
            emptyDeclaredOutputs,
            changedDeclaredOutputs,
            unchangedDeclaredOutputs,
            undeclaredChangedArtifacts.Length > 0,
            undeclaredChangedArtifacts,
            evidence);
    }

    private static PlanArtifactKind[] Normalize(IEnumerable<PlanArtifactKind> artifacts) =>
        artifacts
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .Distinct()
            .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
            .ToArray();
}

public sealed record PipelineArtifactChange(
    PlanArtifactKind Artifact,
    int BeforeCount,
    int AfterCount)
{
    public int Delta => AfterCount - BeforeCount;

    public PipelineArtifactDeltaKind ChangeKind => PipelineArtifactDeltaKindExtensions.FromCounts(BeforeCount, AfterCount);
}

public sealed record PipelineArtifactDelta(
    PlanArtifactKind Artifact,
    int BeforeCount,
    int AfterCount,
    bool IsDeclaredWrite)
{
    public static IReadOnlyList<PipelineArtifactDelta> FromCounts(
        IReadOnlyDictionary<PlanArtifactKind, int> before,
        IReadOnlyDictionary<PlanArtifactKind, int> after,
        IEnumerable<PlanArtifactKind> declaredWrites,
        IEnumerable<PlanArtifactKind> changedArtifacts)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(declaredWrites);
        ArgumentNullException.ThrowIfNull(changedArtifacts);

        var declaredWriteSet = declaredWrites
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .ToHashSet();

        return declaredWriteSet
            .Concat(changedArtifacts)
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .Distinct()
            .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
            .Select(artifact => new PipelineArtifactDelta(
                artifact,
                before.TryGetValue(artifact, out var beforeCount) ? beforeCount : 0,
                after.TryGetValue(artifact, out var afterCount) ? afterCount : 0,
                declaredWriteSet.Contains(artifact)))
            .ToArray();
    }

    public int Delta => AfterCount - BeforeCount;

    public bool Changed => Delta != 0;

    public bool WasPresent => BeforeCount > 0;

    public bool IsPresent => AfterCount > 0;

    public bool IsEmptyDeclaredOutput => IsDeclaredWrite && AfterCount == 0;

    public PipelineArtifactDeltaKind ChangeKind => PipelineArtifactDeltaKindExtensions.FromCounts(BeforeCount, AfterCount);
}

public enum PipelineArtifactDeltaKind
{
    Unchanged,
    Created,
    Increased,
    Decreased,
    Removed
}

internal static class PipelineArtifactDeltaKindExtensions
{
    public static PipelineArtifactDeltaKind FromCounts(int beforeCount, int afterCount)
    {
        if (beforeCount == afterCount)
        {
            return PipelineArtifactDeltaKind.Unchanged;
        }

        if (beforeCount == 0 && afterCount > 0)
        {
            return PipelineArtifactDeltaKind.Created;
        }

        if (beforeCount > 0 && afterCount == 0)
        {
            return PipelineArtifactDeltaKind.Removed;
        }

        return afterCount > beforeCount
            ? PipelineArtifactDeltaKind.Increased
            : PipelineArtifactDeltaKind.Decreased;
    }
}
