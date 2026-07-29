using System.Runtime.CompilerServices;

namespace OpenPlanTrace.Export;

internal static class PlanPlacementExportCache
{
    private static readonly ConditionalWeakTable<PlanScanResult, Lazy<PlanPlacementExport>> Cache = new();

    public static PlanPlacementExport GetOrCreate(PlanScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Cache.GetValue(
                result,
                static scan => new Lazy<PlanPlacementExport>(
                    () => PlanPlacementExport.CreateUncached(scan),
                    LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;
    }
}
