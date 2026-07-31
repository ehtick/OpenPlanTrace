# Curved Wall Design

## Status

OpenPlanTrace preserves native DXF arcs as `ArcPrimitive`, approximates PDF
Bezier paths as source polylines, and uses arcs when recognizing door swings.
Canonical structural walls are currently straight `PlanLineSegment` runs.
Native curved structural walls are therefore not yet placement-ready and must
not be advertised as implemented.

## Geometric Rule

A curved wall must never be reconstructed by extending two straight tangents
until they meet and then pulling the corner toward a median point. That creates
an invented apex, changes wall length, breaks offsets, and gives downstream
placement engines false geometry.

For a circular wall represented by inner and outer faces:

- verify that the faces are concentric and have compatible angular intervals;
- use `(innerRadius + outerRadius) / 2` as the canonical centerline radius;
- use `outerRadius - innerRadius` as wall thickness;
- preserve center, start angle, sweep angle, sweep direction, and endpoints;
- retain both face source IDs and any supporting primitive IDs; and
- compute exact arc length as `abs(sweepAngle) * centerlineRadius`.

For ellipses, splines, and general curves, preserve a parametric source curve
when the loader exposes one. When a consumer supports only line segments,
export an explicitly derived tessellation with a declared maximum chord or
Hausdorff error. Tessellation is a compatibility view, not the canonical wall.

## Proposed Contract

Add a discriminated structural path contract instead of overloading a straight
segment:

- `Line`: start and end points;
- `CircularArc`: center, radius, start angle, sweep angle, and direction;
- `EllipticalArc`: center, axes, rotation, and parameter interval;
- `Spline`: degree, control points, knots, weights, and parameter interval;
- optional derived polyline plus its approximation tolerance.

Every curved run must also carry wall type, thickness, confidence, placement
readiness, page and millimeter coordinates, source IDs, evidence, and a stable
curve identity. Existing straight consumers can continue to use `Line` paths.

## Detection Pipeline

1. Collect native arcs, Beziers, splines, and curved polyline candidates before
   line-only wall filtering destroys their continuity.
2. Reject door swings, sanitary fixtures, furniture, symbols, dimensions, and
   small decorative curves using scale, radius, sweep, endpoint, layer, nearby
   wall, and repeated-symbol evidence.
3. Pair compatible offset curves. Circular candidates use concentricity,
   radial separation, angular overlap, and tangent support. General curves use
   bounded offset-distance consistency.
4. Build the median curve and estimate thickness from robust face separation.
5. Join line and curve endpoints with tangent-aware nodes. Preserve corners
   when tangents genuinely differ; do not smooth architectural corners.
6. Solve rooms from mixed line/curve boundaries and retain rejected candidates
   for diagnostics.

## Openings And Rooms

Openings hosted on curves use an arc-length interval, not an X/Y projection.
Export start and end distances along the host curve, normalized parameters,
world coordinates, local tangent/normal vectors, width, and operation. This
lets a downstream engine place a door or window at the exact curved-wall
location and orientation.

Room boundaries must accept mixed line and curve edges. Circular boundary area
can be integrated analytically; general curves may use a bounded-error polygon
only when the approximation tolerance is recorded. Junction, closure, and
adjacency checks operate on shared curve intervals and endpoint tolerances.

## Accuracy Gates

Curved-wall support is not complete until the corpus includes reviewed cases
for circular, elliptical, spline, line-to-arc tangent, line-to-arc corner,
concentric double-face, single-centerline, curved opening, and false door-swing
geometry. Required metrics are centerline distance, radius/parameter error,
endpoint error, length error, thickness error, duplicate overlap, room closure,
and opening interval error.

The first implementation should target native circular DXF arcs. PDF Bezier
and spline reconstruction should follow only after the circular path contract,
graph topology, exporter compatibility, and regression metrics are stable.
