# Curved Wall Design

## Current Status

OpenPlanTrace `0.12.002` implements the first conservative circular-wall step.
It collects native arcs and fitted PDF polyline arcs before line-only wall
filtering, pairs compatible concentric faces, and exports exact review evidence
as `curvedWalls`.

Each candidate includes:

- center and centerline radius;
- start angle and signed sweep in radians;
- exact start/end points and arc length;
- wall thickness from face separation;
- page bounds and calibrated measurements when available;
- native/polyline/mixed source kind;
- angular overlap, radial fit error, confidence, source IDs, and evidence; and
- explicit `readyForCoordinatePlacement: false` and
  `excludedFromLinearTopology: true` safety flags.

The SVG viewer renders these candidates as magenta dashed review curves with a
white halo. Native JSON preserves the exact circular parameters. GeoJSON uses a
sampled `LineString` compatibility view and carries the canonical parameters
plus an approximation marker in feature properties.

## Geometric Rule

A curved wall must never be reconstructed by extending two straight tangents
until they meet and pulling the corner toward a median point. That invents an
apex, changes length, breaks offsets, and gives downstream placement engines
false geometry.

For a circular wall represented by inner and outer faces:

- verify concentricity and compatible angular intervals;
- use `(innerRadius + outerRadius) / 2` as centerline radius;
- use `outerRadius - innerRadius` as thickness;
- preserve center, start angle, signed sweep, and endpoints;
- retain both face source IDs and supporting evidence; and
- compute exact arc length as `abs(sweepAngle) * centerlineRadius`.

For ellipses, splines, and general curves, preserve a parametric source curve
when the loader exposes one. A polyline export must declare its approximation
tolerance and remain a compatibility view rather than canonical geometry.

## Current Safety Gates

The initial detector rejects or defers:

- door, window, dimension, text, grid, equipment, MEP, and surface-pattern
  layers;
- incompatible centers, radial separation, overlap, or fit error;
- thin unfilled fitted pairs without credible wall-face separation;
- compact unfilled fitted rings with high sweep that resemble symbols; and
- circular candidates crossed by a dense radial stair/detail spoke fan.

Overlapping or direction-reversed observations collapse to one physical curve.
The same source path cannot pair with itself. Every accepted curve remains
review-only, so a false candidate cannot affect rooms, routing, or placement.

## Target Structural Path Contract

The next architecture step is a discriminated structural path contract:

- `Line`: start and end points;
- `CircularArc`: center, radius, start angle, sweep angle, and direction;
- `EllipticalArc`: center, axes, rotation, and parameter interval;
- `Spline`: degree, control points, knots, weights, and parameter interval; and
- optional derived polyline plus its approximation tolerance.

Every structural path carries wall type, thickness, confidence, placement
readiness, page/millimeter coordinates, source IDs, evidence, and a stable path
identity. Existing line-only consumers can continue to consume `Line` paths.

## Next Detection And Topology Work

1. Add line-to-arc endpoint and tangent/corner relations without smoothing real
   architectural corners.
2. Distinguish structural concentric faces from radial floor, exhibit, stair,
   furniture, and symbol patterns using connected straight-wall context.
3. Promote only globally supported curves into mixed wall graph paths.
4. Host curved openings by arc-length intervals, normalized parameters, and
   local tangent/normal vectors.
5. Solve rooms from mixed line/curve boundaries. Circular area contributions
   can be integrated analytically; general curves need bounded-error geometry.
6. Add elliptical and spline evidence only after circular topology is stable.

## Accuracy Gates

Placement-ready curved support requires reviewed cases for circular,
elliptical, spline, line-to-arc tangent, line-to-arc corner, concentric
double-face, single-centerline, curved opening, door-swing, radial stair,
compact symbol, and dense radial-pattern geometry.

Required metrics are centerline distance, radius/parameter error, endpoint
error, length error, thickness error, duplicate overlap, room closure, opening
interval error, false-curve rate, and runtime. Until those gates pass,
`curvedWalls` is exact evidence for review, not placement geometry.
