using System.Runtime.CompilerServices;

namespace OpenPlanTrace.Export;

internal static class PlanStructureExportCache
{
    private static readonly ConditionalWeakTable<PlanScanResult, Lazy<PlanStructureExport>> Cache = new();

    public static PlanStructureExport GetOrCreate(PlanScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Cache.GetValue(
                result,
                static scan => new Lazy<PlanStructureExport>(
                    () => PlanStructureExport.CreateUncached(scan),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
    }
}
