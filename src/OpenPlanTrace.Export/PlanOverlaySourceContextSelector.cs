namespace OpenPlanTrace.Export;

internal static class PlanOverlaySourceContextSelector
{
    public static IReadOnlyList<PlanPrimitive> Select(
        PlanPage page,
        SvgOverlayRenderOptions options,
        PlanRect focusBounds)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);

        var limit = Math.Max(0, options.MaxSourceContextPrimitives);
        if (limit == 0 || page.Primitives.Count == 0)
        {
            return Array.Empty<PlanPrimitive>();
        }

        var supported = page.Primitives
            .Select((primitive, index) => new SourceContextPrimitive(primitive, index))
            .Where(item => IsSupported(item.Primitive))
            .ToArray();
        if (supported.Length <= limit || focusBounds.IsEmpty)
        {
            return supported
                .Take(limit)
                .Select(item => item.Primitive)
                .ToArray();
        }

        var pageBounds = new PlanRect(0, 0, page.Size.Width, page.Size.Height);
        var normalizedFocus = focusBounds.ClampTo(pageBounds);
        if (normalizedFocus.IsEmpty)
        {
            normalizedFocus = focusBounds;
        }

        var nearPadding = Math.Max(24.0, Math.Min(page.Size.Width, page.Size.Height) * 0.08);
        var nearFocus = normalizedFocus.Inflate(nearPadding).ClampTo(pageBounds);

        return supported
            .OrderBy(item => Priority(item.Primitive.Bounds, normalizedFocus, nearFocus))
            .ThenBy(item => DistanceToFocus(item.Primitive.Bounds, normalizedFocus))
            .ThenBy(item => item.Index)
            .Take(limit)
            .Select(item => item.Primitive)
            .ToArray();
    }

    public static int SupportedPrimitiveCount(PlanPage page) =>
        page.Primitives.Count(IsSupported);

    public static PlanRect ResolveFocusBounds(
        PlanScanResult result,
        PlanPage page,
        SvgOverlayRenderOptions options,
        PlanRect viewportBounds)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);

        var pageBounds = new PlanRect(0, 0, page.Size.Width, page.Size.Height);
        var topologyBounds = PlanRect.Union(
            WallTopologySpanVisibility.BuildVisibleTopologySpans(result, page.Number, options)
                .Select(span => span.Bounds)
                .Concat(
                    options.IncludeReviewOnlyWallTopologySpans
                        ? WallTopologySpanVisibility.BuildHiddenNonPlacementTopologySpans(result, page.Number, options)
                            .Select(span => span.Bounds)
                        : Array.Empty<PlanRect>()));

        if (!topologyBounds.IsEmpty)
        {
            return topologyBounds
                .Inflate(Math.Max(24.0, options.ViewportPaddingDrawingUnits))
                .ClampTo(pageBounds);
        }

        var floorplanRegionBounds = PlanRect.Union(
            result.SheetRegions
                .Where(region => region.PageNumber == page.Number && region.Kind == RegionKind.MainFloorPlan)
                .Select(region => region.Bounds));
        if (!floorplanRegionBounds.IsEmpty)
        {
            return floorplanRegionBounds
                .Inflate(Math.Max(12.0, options.ViewportPaddingDrawingUnits))
                .ClampTo(pageBounds);
        }

        return viewportBounds.IsEmpty
            ? pageBounds
            : viewportBounds.ClampTo(pageBounds);
    }

    private static bool IsSupported(PlanPrimitive primitive) =>
        primitive is LinePrimitive
        or PolylinePrimitive
        or RectanglePrimitive
        or ArcPrimitive;

    private static int Priority(PlanRect bounds, PlanRect focus, PlanRect nearFocus)
    {
        if (bounds.IsEmpty)
        {
            return 3;
        }

        if (bounds.Intersects(focus, tolerance: 0.5) || focus.Contains(bounds.Center, tolerance: 0.5))
        {
            return 0;
        }

        if (!nearFocus.IsEmpty
            && (bounds.Intersects(nearFocus, tolerance: 0.5) || nearFocus.Contains(bounds.Center, tolerance: 0.5)))
        {
            return 1;
        }

        return 2;
    }

    private static double DistanceToFocus(PlanRect bounds, PlanRect focus)
    {
        if (bounds.IsEmpty || focus.IsEmpty)
        {
            return double.MaxValue;
        }

        if (bounds.Intersects(focus))
        {
            return 0;
        }

        var dx = bounds.Right < focus.Left
            ? focus.Left - bounds.Right
            : bounds.Left > focus.Right
                ? bounds.Left - focus.Right
                : 0;
        var dy = bounds.Bottom < focus.Top
            ? focus.Top - bounds.Bottom
            : bounds.Top > focus.Bottom
                ? bounds.Top - focus.Bottom
                : 0;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private sealed record SourceContextPrimitive(PlanPrimitive Primitive, int Index);
}
