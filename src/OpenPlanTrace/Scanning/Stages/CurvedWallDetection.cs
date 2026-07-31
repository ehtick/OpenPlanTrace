namespace OpenPlanTrace;

internal static class CurvedWallDetection
{
    private const int ArcSampleCount = 65;
    private const int MaximumArcObservationsPerPage = 1200;
    private const int MaximumPolylinePointCount = 96;

    public static IReadOnlyList<CurvedWallCandidate> Detect(
        PlanPage page,
        SheetRegion mainRegion,
        ScanContext context)
    {
        var observations = CollectObservations(page, mainRegion, context)
            .Select((item, index) => item with { Index = index })
            .OrderBy(item => item.SourceId, StringComparer.Ordinal)
            .Take(MaximumArcObservationsPerPage)
            .ToArray();
        if (observations.Length < 2)
        {
            return Array.Empty<CurvedWallCandidate>();
        }

        var curveContextLines = CollectCurveContextLines(page, context.Options).ToArray();
        var pairs = BuildPairs(observations, page, context.Options, curveContextLines)
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Overlap.SweepMagnitude * pair.CenterlineRadius)
            .ThenBy(pair => pair.First.SourceId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Second.SourceId, StringComparer.Ordinal)
            .ToArray();
        var consumed = new HashSet<int>();
        var result = new List<CurvedWallCandidate>();

        foreach (var pair in pairs)
        {
            if (consumed.Contains(pair.First.Index) || consumed.Contains(pair.Second.Index))
            {
                continue;
            }

            consumed.Add(pair.First.Index);
            consumed.Add(pair.Second.Index);

            var center = new PlanPoint(
                (pair.First.Center.X + pair.Second.Center.X) / 2.0,
                (pair.First.Center.Y + pair.Second.Center.Y) / 2.0);
            var bounds = ArcBounds(
                center,
                pair.CenterlineRadius,
                pair.Overlap.StartAngle,
                pair.Overlap.Sweep,
                pair.Thickness / 2.0);
            var sourceIds = new[] { pair.First.SourceId, pair.Second.SourceId }
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var sourceKind = pair.First.SourceKind == pair.Second.SourceKind
                ? pair.First.SourceKind == ArcObservationSource.NativeArc
                    ? CurvedWallSourceKind.NativeArcPair
                    : CurvedWallSourceKind.PolylineArcPair
                : CurvedWallSourceKind.MixedArcPair;
            var confidence = new Confidence(Math.Clamp(
                0.58
                + (pair.Overlap.Ratio * 0.20)
                + (pair.CenterAgreement * 0.10)
                + (pair.RadialAgreement * 0.08)
                + (sourceKind == CurvedWallSourceKind.NativeArcPair ? 0.04 : 0),
                0.58,
                0.94));
            var scaleGroup = context.Calibration.SelectMeasurementScaleGroup(
                page.Number,
                bounds,
                mainRegion.Id);
            var id = $"page:{page.Number}:curved-wall:{result.Count + 1:000}";

            var candidate = new CurvedWallCandidate(
                id,
                page.Number,
                center,
                pair.CenterlineRadius,
                pair.Overlap.StartAngle,
                pair.Overlap.Sweep,
                pair.Thickness,
                bounds,
                mainRegion.Id,
                sourceKind,
                pair.Overlap.Ratio,
                Math.Max(pair.First.RadialError, pair.Second.RadialError),
                ReadyForCoordinatePlacement: false,
                ExcludedFromLinearTopology: true,
                confidence,
                RequiresReview: true,
                sourceIds,
                new[]
                {
                    "paired concentric curved wall-face geometry",
                    $"centerline radius {pair.CenterlineRadius:0.###} drawing units",
                    $"face separation {pair.Thickness:0.###} drawing units",
                    $"angular overlap ratio {pair.Overlap.Ratio:0.###}",
                    $"sweep {pair.Overlap.Sweep * 180.0 / Math.PI:0.###} degrees",
                    $"maximum radial fit error {Math.Max(pair.First.RadialError, pair.Second.RadialError):0.###} drawing units",
                    "preserved as circular-arc evidence; excluded from the line-only wall graph until mixed path topology is available",
                    "curve was not replaced by tangent extensions or an invented corner"
                })
            {
                RadiusMillimeters = context.Calibration.ToMillimeters(pair.CenterlineRadius, scaleGroup),
                ThicknessMillimeters = context.Calibration.ToMillimeters(pair.Thickness, scaleGroup),
                ArcLengthMeters = context.Calibration.ToMeters(pair.Overlap.SweepMagnitude * pair.CenterlineRadius, scaleGroup),
                MeasurementScaleGroupId = scaleGroup?.Id
            };
            AddOrReplaceDuplicate(result, candidate);
        }

        return result;
    }

    private static IEnumerable<ArcObservation> CollectObservations(
        PlanPage page,
        SheetRegion mainRegion,
        ScanContext context)
    {
        for (var index = 0; index < page.Primitives.Count; index++)
        {
            var primitive = page.Primitives[index];
            if (!mainRegion.Bounds.Intersects(primitive.Bounds)
                || IsExcludedCurveLayer(primitive, context))
            {
                continue;
            }

            var sourceId = context.PrimitiveId(page.Number, index, primitive);
            switch (primitive)
            {
                case ArcPrimitive arc:
                {
                    var observation = FromNativeArc(index, sourceId, arc, page, context.Options);
                    if (observation is not null)
                    {
                        yield return observation;
                    }

                    break;
                }
                case PolylinePrimitive polyline:
                    foreach (var observation in FromPolyline(
                                 index,
                                 sourceId,
                                 polyline,
                                 page,
                                 context.Options))
                    {
                        yield return observation;
                    }

                    break;
            }
        }
    }

    private static bool IsExcludedCurveLayer(PlanPrimitive primitive, ScanContext context)
    {
        var layerName = primitive.Source.Layer ?? primitive.Layer;
        var sourceFormat = primitive.Source.SourceFormat;
        var layer = string.IsNullOrWhiteSpace(layerName)
            ? null
            : context.LayerAnalysis.Find(layerName, sourceFormat)
                ?? context.LayerAnalysis.Find(layerName);
        return layer?.LikelyCategory is LayerCategory.Door
            or LayerCategory.Window
            or LayerCategory.Dimension
            or LayerCategory.Text
            or LayerCategory.Grid
            or LayerCategory.Equipment
            or LayerCategory.Electrical
            or LayerCategory.HVAC
            or LayerCategory.Plumbing
            or LayerCategory.FireSafety
            or LayerCategory.SurfacePattern;
    }

    private static ArcObservation? FromNativeArc(
        int index,
        string sourceId,
        ArcPrimitive arc,
        PlanPage page,
        ScannerOptions options)
    {
        if (!IsPlausibleStructuralArc(
                arc.Center,
                arc.Radius,
                arc.SweepAngleRadians,
                radialError: 0,
                page,
                options))
        {
            return null;
        }

        return new ArcObservation(
            index,
            sourceId,
            arc.Center,
            arc.Radius,
            arc.StartAngleRadians,
            arc.SweepAngleRadians,
            0,
            ArcObservationSource.NativeArc,
            IsFilled(arc));
    }

    private static IEnumerable<ArcObservation> FromPolyline(
        int index,
        string sourceId,
        PolylinePrimitive polyline,
        PlanPage page,
        ScannerOptions options)
    {
        if (polyline.Points.Count < 5
            || polyline.Points.Count > MaximumPolylinePointCount)
        {
            yield break;
        }

        var points = DeduplicateConsecutivePoints(polyline.Points).ToList();
        if (polyline.Closed
            && points.Count > 1
            && points[0].DistanceTo(points[^1]) <= 0.02)
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 5)
        {
            yield break;
        }

        var fits = new List<ArcFit>();
        if (polyline.Closed)
        {
            AddCurvedSubchainFits(points, fits);
        }
        else
        {
            AddFit(points, 0, points.Count, fits);
            for (var trimStart = 0; trimStart <= 3; trimStart++)
            {
                for (var trimEnd = 0; trimEnd <= 3; trimEnd++)
                {
                    if (trimStart + trimEnd == 0
                        || points.Count - trimStart - trimEnd < 5)
                    {
                        continue;
                    }

                    AddFit(points, trimStart, points.Count - trimEnd, fits);
                }
            }
        }

        var accepted = fits
            .Where(item => IsPlausibleStructuralArc(
                item.Center,
                item.Radius,
                item.Sweep,
                item.RadialError,
                page,
                options))
            .OrderByDescending(item => item.ArcLength)
            .ThenBy(item => item.RadialError)
            .ToArray();
        var emitted = new List<ArcFit>();
        foreach (var fit in accepted)
        {
            if (emitted.Any(existing =>
                    existing.Center.DistanceTo(fit.Center) <= Math.Max(0.5, fit.Radius * 0.01)
                    && Math.Abs(existing.Radius - fit.Radius) <= Math.Max(0.5, fit.Radius * 0.01)))
            {
                continue;
            }

            emitted.Add(fit);
            yield return new ArcObservation(
                index,
                sourceId,
                fit.Center,
                fit.Radius,
                fit.StartAngle,
                fit.Sweep,
                fit.RadialError,
                ArcObservationSource.PolylineFit,
                IsFilled(polyline));
            if (emitted.Count >= 3)
            {
                yield break;
            }
        }
    }

    private static void AddCurvedSubchainFits(
        IReadOnlyList<PlanPoint> points,
        ICollection<ArcFit> fits)
    {
        if (points.Count < 5)
        {
            return;
        }

        var largestCornerIndex = 0;
        var largestCorner = double.NegativeInfinity;
        for (var index = 0; index < points.Count; index++)
        {
            var previous = points[(index - 1 + points.Count) % points.Count];
            var current = points[index];
            var next = points[(index + 1) % points.Count];
            var turn = Math.Abs(TurnAngle(previous, current, next));
            if (turn > largestCorner)
            {
                largestCorner = turn;
                largestCornerIndex = index;
            }
        }

        var ordered = Enumerable.Range(0, points.Count)
            .Select(offset => points[(largestCornerIndex + offset) % points.Count])
            .ToArray();
        var runStartVertex = -1;
        var runSign = 0;
        for (var vertex = 1; vertex < ordered.Length - 1; vertex++)
        {
            var turn = TurnAngle(ordered[vertex - 1], ordered[vertex], ordered[vertex + 1]);
            var valid = Math.Abs(turn) >= 0.0002 && Math.Abs(turn) <= 0.50;
            var sign = Math.Sign(turn);
            if (valid && (runStartVertex < 0 || sign == runSign))
            {
                if (runStartVertex < 0)
                {
                    runStartVertex = vertex;
                    runSign = sign;
                }

                continue;
            }

            AddCurvedRun(runStartVertex, vertex - 1, ordered, fits);
            runStartVertex = valid ? vertex : -1;
            runSign = valid ? sign : 0;
        }

        AddCurvedRun(runStartVertex, ordered.Length - 2, ordered, fits);
    }

    private static void AddCurvedRun(
        int startVertex,
        int endVertex,
        IReadOnlyList<PlanPoint> points,
        ICollection<ArcFit> fits)
    {
        if (startVertex < 1 || endVertex - startVertex + 1 < 3)
        {
            return;
        }

        AddFit(points, startVertex - 1, endVertex + 2, fits);
    }

    private static double TurnAngle(PlanPoint previous, PlanPoint current, PlanPoint next)
    {
        var incoming = (current - previous).Normalize();
        var outgoing = (next - current).Normalize();
        return Math.Atan2(incoming.Cross(outgoing), incoming.Dot(outgoing));
    }

    private static void AddFit(
        IReadOnlyList<PlanPoint> points,
        int start,
        int end,
        ICollection<ArcFit> fits)
    {
        var slice = points.Skip(start).Take(end - start).ToArray();
        if (TryFitArc(slice, out var fit))
        {
            fits.Add(fit);
        }
    }

    private static IReadOnlyList<PlanPoint> DeduplicateConsecutivePoints(
        IReadOnlyList<PlanPoint> points)
    {
        var result = new List<PlanPoint>(points.Count);
        foreach (var point in points)
        {
            if (double.IsFinite(point.X)
                && double.IsFinite(point.Y)
                && (result.Count == 0 || result[^1].DistanceTo(point) > 0.02))
            {
                result.Add(point);
            }
        }

        return result;
    }

    private static bool TryFitArc(
        IReadOnlyList<PlanPoint> points,
        out ArcFit fit)
    {
        fit = default!;
        var first = points[0];
        var middle = points[points.Count / 2];
        var last = points[^1];
        if (!TryFitCircle(first, middle, last, out var center, out var radius))
        {
            return false;
        }

        var radialError = points.Max(point => Math.Abs(point.DistanceTo(center) - radius));
        var startAngle = Math.Atan2(first.Y - center.Y, first.X - center.X);
        var middleAngle = Math.Atan2(middle.Y - center.Y, middle.X - center.X);
        var endAngle = Math.Atan2(last.Y - center.Y, last.X - center.X);
        var sweep = ResolveSweepThroughMiddle(startAngle, middleAngle, endAngle);
        if (Math.Abs(sweep) < Math.PI / 15.0
            || Math.Abs(sweep) > Math.PI * 1.95
            || !AnglesProgressMonotonically(points, center, sweep))
        {
            return false;
        }

        var polylineLength = 0.0;
        for (var index = 1; index < points.Count; index++)
        {
            polylineLength += points[index - 1].DistanceTo(points[index]);
        }

        var arcLength = Math.Abs(sweep) * radius;
        var lengthRatio = arcLength <= 0 ? 0 : polylineLength / arcLength;
        if (lengthRatio < 0.82 || lengthRatio > 1.18)
        {
            return false;
        }

        fit = new ArcFit(
            center,
            radius,
            startAngle,
            sweep,
            radialError,
            arcLength);
        return true;
    }

    private static bool AnglesProgressMonotonically(
        IReadOnlyList<PlanPoint> points,
        PlanPoint center,
        double sweep)
    {
        var expectedSign = Math.Sign(sweep);
        var violations = 0;
        for (var index = 1; index < points.Count; index++)
        {
            var previous = Math.Atan2(
                points[index - 1].Y - center.Y,
                points[index - 1].X - center.X);
            var current = Math.Atan2(
                points[index].Y - center.Y,
                points[index].X - center.X);
            var delta = NormalizeSignedAngle(current - previous);
            if (Math.Abs(delta) > Math.PI / 2.0 || Math.Sign(delta) != expectedSign)
            {
                violations++;
            }
        }

        return violations <= Math.Max(0, (points.Count - 1) / 8);
    }

    private static bool IsPlausibleStructuralArc(
        PlanPoint center,
        double radius,
        double sweep,
        double radialError,
        PlanPage page,
        ScannerOptions options)
    {
        var pageDiagonal = Math.Sqrt(
            (page.Size.Width * page.Size.Width)
            + (page.Size.Height * page.Size.Height));
        var arcLength = Math.Abs(sweep) * radius;
        var maximumError = Math.Max(
            0.55,
            Math.Min(
                Math.Max(options.WallSnapTolerance, 2.5),
                radius * 0.025));
        return double.IsFinite(center.X)
            && double.IsFinite(center.Y)
            && double.IsFinite(radius)
            && radius >= Math.Max(4, options.MinWallPairSeparation * 2.0)
            && radius <= pageDiagonal * 5.0
            && arcLength >= options.MinWallLength * 1.15
            && Math.Abs(sweep) >= Math.PI / 15.0
            && Math.Abs(sweep) <= Math.PI * 1.95
            && radialError <= maximumError;
    }

    private static IEnumerable<ArcPair> BuildPairs(
        IReadOnlyList<ArcObservation> observations,
        PlanPage page,
        ScannerOptions options,
        IReadOnlyList<PlanLineSegment> curveContextLines)
    {
        var pageDiagonal = Math.Sqrt(
            (page.Size.Width * page.Size.Width)
            + (page.Size.Height * page.Size.Height));
        for (var firstIndex = 0; firstIndex < observations.Count; firstIndex++)
        {
            var first = observations[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < observations.Count; secondIndex++)
            {
                var second = observations[secondIndex];
                if (string.Equals(first.SourceId, second.SourceId, StringComparison.Ordinal))
                {
                    continue;
                }

                var thickness = Math.Abs(first.Radius - second.Radius);
                if (thickness < Math.Max(1.0, options.MinWallPairSeparation * 0.55)
                    || thickness > Math.Max(options.MaxWallPairSeparation, options.DefaultWallThickness * 6.0))
                {
                    continue;
                }

                var centerDistance = first.Center.DistanceTo(second.Center);
                var centerTolerance = Math.Max(
                    options.WallSnapTolerance * 1.5,
                    Math.Min(pageDiagonal * 0.01, Math.Min(first.Radius, second.Radius) * 0.025));
                if (centerDistance > centerTolerance
                    || !TryAngularOverlap(first, second, out var overlap)
                    || overlap.Ratio < Math.Max(0.62, options.MinWallPairOverlapRatio))
                {
                    continue;
                }

                var centerlineRadius = (first.Radius + second.Radius) / 2.0;
                if (overlap.SweepMagnitude * centerlineRadius < options.MinWallLength * 1.15)
                {
                    continue;
                }

                var hasFilledBodyEvidence = first.IsFilled || second.IsFilled;
                var isNativeArcPair = first.SourceKind == ArcObservationSource.NativeArc
                    && second.SourceKind == ArcObservationSource.NativeArc;
                var isPolylineArcPair = first.SourceKind == ArcObservationSource.PolylineFit
                    && second.SourceKind == ArcObservationSource.PolylineFit;
                var minimumUnfilledFaceSeparation = Math.Max(
                    options.MinWallPairSeparation * 1.5,
                    options.DefaultWallThickness * 0.75);
                if (!hasFilledBodyEvidence
                    && !isNativeArcPair
                    && thickness < minimumUnfilledFaceSeparation)
                {
                    continue;
                }

                var smallCircularSymbolRadius = Math.Max(
                    options.DefaultWallThickness * 6.0,
                    options.MinWallLength * 2.0);
                if (isPolylineArcPair
                    && !hasFilledBodyEvidence
                    && overlap.SweepMagnitude >= Math.PI * 1.25
                    && centerlineRadius < smallCircularSymbolRadius)
                {
                    continue;
                }

                var pairCenter = new PlanPoint(
                    (first.Center.X + second.Center.X) / 2.0,
                    (first.Center.Y + second.Center.Y) / 2.0);
                if (HasDenseRadialSpokeFan(
                        curveContextLines,
                        pairCenter,
                        centerlineRadius,
                        thickness,
                        overlap,
                        options))
                {
                    continue;
                }

                var centerAgreement = 1.0 - Math.Clamp(centerDistance / Math.Max(0.001, centerTolerance), 0, 1);
                var fitTolerance = Math.Max(0.55, centerlineRadius * 0.025);
                var fitError = Math.Max(first.RadialError, second.RadialError);
                var radialAgreement = 1.0 - Math.Clamp(fitError / fitTolerance, 0, 1);
                var score = overlap.Ratio * 0.62
                    + centerAgreement * 0.22
                    + radialAgreement * 0.16;
                yield return new ArcPair(
                    first,
                    second,
                    centerlineRadius,
                    thickness,
                    overlap,
                    centerAgreement,
                    radialAgreement,
                    score);
            }
        }
    }

    private static IEnumerable<PlanLineSegment> CollectCurveContextLines(
        PlanPage page,
        ScannerOptions options)
    {
        var pageDiagonal = Math.Sqrt(
            (page.Size.Width * page.Size.Width)
            + (page.Size.Height * page.Size.Height));
        var minimumLength = Math.Max(2.0, options.MinWallLength * 0.4);
        var maximumLength = pageDiagonal * 0.24;
        foreach (var primitive in page.Primitives)
        {
            switch (primitive)
            {
                case LinePrimitive line when line.Segment.Length >= minimumLength
                                             && line.Segment.Length <= maximumLength:
                    yield return line.Segment;
                    break;
                case PolylinePrimitive { Points.Count: >= 2 and <= 4 } polyline:
                    for (var index = 1; index < polyline.Points.Count; index++)
                    {
                        var segment = new PlanLineSegment(
                            polyline.Points[index - 1],
                            polyline.Points[index]);
                        if (segment.Length >= minimumLength && segment.Length <= maximumLength)
                        {
                            yield return segment;
                        }
                    }

                    break;
            }
        }
    }

    private static bool HasDenseRadialSpokeFan(
        IReadOnlyList<PlanLineSegment> lines,
        PlanPoint center,
        double centerlineRadius,
        double thickness,
        AngularOverlap overlap,
        ScannerOptions options)
    {
        const int minimumSpokeCount = 4;
        var minimumLength = Math.Max(
            options.MinWallLength * 0.5,
            thickness * 2.0);
        var maximumLength = Math.Max(
            minimumLength * 1.5,
            centerlineRadius * 1.4);
        var minimumMidpointRadius = centerlineRadius * 0.55;
        var maximumMidpointRadius = centerlineRadius * 1.55;
        var spokeCount = 0;

        foreach (var line in lines)
        {
            if (line.Length < minimumLength || line.Length > maximumLength)
            {
                continue;
            }

            var radial = line.Midpoint - center;
            var midpointRadius = radial.Length;
            if (midpointRadius < minimumMidpointRadius
                || midpointRadius > maximumMidpointRadius)
            {
                continue;
            }

            var midpointAngle = Math.Atan2(radial.Y, radial.X);
            if (!ContainsAngle(overlap.StartAngle, overlap.Sweep, midpointAngle)
                || Math.Abs(line.Vector.Normalize().Dot(radial.Normalize())) < 0.94)
            {
                continue;
            }

            spokeCount++;
            if (spokeCount >= minimumSpokeCount)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAngularOverlap(
        ArcObservation first,
        ArcObservation second,
        out AngularOverlap overlap)
    {
        var reference = Math.Abs(first.Sweep) <= Math.Abs(second.Sweep)
            ? first
            : second;
        var other = ReferenceEquals(reference, first) ? second : first;
        var inside = new bool[ArcSampleCount];
        for (var index = 0; index < ArcSampleCount; index++)
        {
            var parameter = index / (double)(ArcSampleCount - 1);
            var angle = reference.StartAngle + (reference.Sweep * parameter);
            inside[index] = ContainsAngle(other.StartAngle, other.Sweep, angle);
        }

        var bestStart = -1;
        var bestEnd = -1;
        var currentStart = -1;
        for (var index = 0; index <= inside.Length; index++)
        {
            var current = index < inside.Length && inside[index];
            if (current && currentStart < 0)
            {
                currentStart = index;
            }
            else if (!current && currentStart >= 0)
            {
                var end = index - 1;
                if (end - currentStart > bestEnd - bestStart)
                {
                    bestStart = currentStart;
                    bestEnd = end;
                }

                currentStart = -1;
            }
        }

        if (bestStart < 0 || bestEnd <= bestStart)
        {
            overlap = default!;
            return false;
        }

        var startParameter = bestStart / (double)(ArcSampleCount - 1);
        var endParameter = bestEnd / (double)(ArcSampleCount - 1);
        var sweep = reference.Sweep * (endParameter - startParameter);
        overlap = new AngularOverlap(
            reference.StartAngle + (reference.Sweep * startParameter),
            sweep,
            Math.Abs(endParameter - startParameter));
        return true;
    }

    private static bool ContainsAngle(double start, double sweep, double angle)
    {
        const double tolerance = 0.012;
        return sweep >= 0
            ? NormalizePositiveAngle(angle - start) <= sweep + tolerance
            : NormalizePositiveAngle(start - angle) <= -sweep + tolerance;
    }

    private static bool IsFilled(PlanPrimitive primitive) =>
        primitive.Source.Properties.TryGetValue("isFilled", out var value)
        && bool.TryParse(value, out var isFilled)
        && isFilled;

    private static void AddOrReplaceDuplicate(
        IList<CurvedWallCandidate> candidates,
        CurvedWallCandidate candidate)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            var existing = candidates[index];
            var radiusTolerance = Math.Max(0.75, candidate.CenterlineRadius * 0.012);
            var thicknessTolerance = Math.Max(0.75, candidate.Thickness * 0.16);
            if (existing.Center.DistanceTo(candidate.Center) > radiusTolerance
                || Math.Abs(existing.CenterlineRadius - candidate.CenterlineRadius) > radiusTolerance
                || Math.Abs(existing.Thickness - candidate.Thickness) > thicknessTolerance
                || ArcCoverageOverlapRatio(existing, candidate) < 0.72)
            {
                continue;
            }

            var existingLength = Math.Abs(existing.SweepAngleRadians) * existing.CenterlineRadius;
            var candidateLength = Math.Abs(candidate.SweepAngleRadians) * candidate.CenterlineRadius;
            var selected = candidateLength > existingLength ? candidate : existing;
            var sources = existing.SourcePrimitiveIds
                .Concat(candidate.SourcePrimitiveIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var evidence = selected.Evidence
                .Concat(new[] { "overlapping or direction-reversed arc observations were collapsed to one physical curved wall candidate" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            candidates[index] = selected with
            {
                Id = existing.Id,
                SourcePrimitiveIds = sources,
                Evidence = evidence
            };
            return;
        }

        candidates.Add(candidate);
    }

    private static double ArcCoverageOverlapRatio(
        CurvedWallCandidate first,
        CurvedWallCandidate second)
    {
        const int sampleCount = 97;
        var shorter = Math.Abs(first.SweepAngleRadians) <= Math.Abs(second.SweepAngleRadians)
            ? first
            : second;
        var longer = ReferenceEquals(shorter, first) ? second : first;
        var inside = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            var parameter = index / (double)(sampleCount - 1);
            var angle = shorter.StartAngleRadians + (shorter.SweepAngleRadians * parameter);
            if (ContainsAngle(longer.StartAngleRadians, longer.SweepAngleRadians, angle))
            {
                inside++;
            }
        }

        return inside / (double)sampleCount;
    }

    private static PlanRect ArcBounds(
        PlanPoint center,
        double radius,
        double start,
        double sweep,
        double padding)
    {
        var points = Enumerable.Range(0, ArcSampleCount)
            .Select(index =>
            {
                var parameter = index / (double)(ArcSampleCount - 1);
                var angle = start + (sweep * parameter);
                return new PlanPoint(
                    center.X + (Math.Cos(angle) * radius),
                    center.Y + (Math.Sin(angle) * radius));
            })
            .ToArray();
        var left = points.Min(point => point.X) - padding;
        var top = points.Min(point => point.Y) - padding;
        var right = points.Max(point => point.X) + padding;
        var bottom = points.Max(point => point.Y) + padding;
        return PlanRect.FromEdges(left, top, right, bottom);
    }

    private static bool TryFitCircle(
        PlanPoint a,
        PlanPoint b,
        PlanPoint c,
        out PlanPoint center,
        out double radius)
    {
        center = default;
        radius = 0;
        var d = 2 * (
            (a.X * (b.Y - c.Y))
            + (b.X * (c.Y - a.Y))
            + (c.X * (a.Y - b.Y)));
        if (Math.Abs(d) <= 0.001)
        {
            return false;
        }

        var a2 = (a.X * a.X) + (a.Y * a.Y);
        var b2 = (b.X * b.X) + (b.Y * b.Y);
        var c2 = (c.X * c.X) + (c.Y * c.Y);
        center = new PlanPoint(
            ((a2 * (b.Y - c.Y)) + (b2 * (c.Y - a.Y)) + (c2 * (a.Y - b.Y))) / d,
            ((a2 * (c.X - b.X)) + (b2 * (a.X - c.X)) + (c2 * (b.X - a.X))) / d);
        radius = center.DistanceTo(a);
        return double.IsFinite(radius) && radius > 0.001;
    }

    private static double ResolveSweepThroughMiddle(
        double start,
        double middle,
        double end)
    {
        var counterClockwise = NormalizePositiveAngle(end - start);
        var middleCounterClockwise = NormalizePositiveAngle(middle - start);
        return middleCounterClockwise <= counterClockwise
            ? counterClockwise
            : -(Math.PI * 2.0 - counterClockwise);
    }

    private static double NormalizePositiveAngle(double angle)
    {
        var normalized = angle % (Math.PI * 2.0);
        return normalized < 0 ? normalized + (Math.PI * 2.0) : normalized;
    }

    private static double NormalizeSignedAngle(double angle)
    {
        var normalized = NormalizePositiveAngle(angle);
        return normalized > Math.PI ? normalized - (Math.PI * 2.0) : normalized;
    }

    private enum ArcObservationSource
    {
        NativeArc,
        PolylineFit
    }

    private sealed record ArcObservation(
        int Index,
        string SourceId,
        PlanPoint Center,
        double Radius,
        double StartAngle,
        double Sweep,
        double RadialError,
        ArcObservationSource SourceKind,
        bool IsFilled);

    private sealed record ArcFit(
        PlanPoint Center,
        double Radius,
        double StartAngle,
        double Sweep,
        double RadialError,
        double ArcLength);

    private sealed record AngularOverlap(
        double StartAngle,
        double Sweep,
        double Ratio)
    {
        public double SweepMagnitude => Math.Abs(Sweep);
    }

    private sealed record ArcPair(
        ArcObservation First,
        ArcObservation Second,
        double CenterlineRadius,
        double Thickness,
        AngularOverlap Overlap,
        double CenterAgreement,
        double RadialAgreement,
        double Score);
}
