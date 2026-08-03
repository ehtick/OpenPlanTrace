# Curved Wall Design

## Current Status

OpenPlanTrace `0.12.003` implements the first conservative mixed-path step.
It collects native arcs and fitted PDF polyline arcs before line-only wall
filtering, pairs compatible concentric faces, and exports exact review evidence
as `curvedWalls`. Scan v72 and structure v2 also expose those candidates beside
canonical straight runs as discriminated structural paths.

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
sampled `LineString` compatibility view and carries the canonical parameters,
structural path ID, connected straight-path provenance, and an approximation
marker. Mixed junctions use standards-valid `MultiPoint` geometry containing
both unchanged source endpoints; their midpoint is an explicit advisory
property, never substituted into source geometry.

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

## Mixed Structural Path Contract

The implemented `openplantrace.structural-path-topology.v1` contract supports:

- `Line`: start and end points;
- `CircularArc`: center, radius, start angle, signed sweep, exact endpoints,
  bounds, and arc length; and
- endpoint-only `Tangent` or `Corner` relations between one line and one arc.

Every structural path carries wall type, thickness, confidence, placement
readiness, page/millimeter coordinates, source IDs, evidence, and a stable path
identity. Relations preserve both source endpoint coordinates and expose only an
advisory midpoint. Existing line-only consumers can continue to consume `Line`
paths, and placement output remains unchanged.

Contract v1 connects path endpoint to path endpoint only. It deliberately does
not invent a relation when an arc endpoint meets the interior of an unsplit
straight run, as seen in the public curved-plan regression. That case needs a
future parameterized path-location reference carrying the straight-path
parameter and exact projected/source points.

`EllipticalArc` and `Spline` remain future discriminators. They require exact
source parameters plus a declared bounded-error compatibility polyline before
they can enter this contract.

## Next Detection And Topology Work

1. Distinguish structural concentric faces from radial floor, exhibit, stair,
   furniture, and symbol patterns using connected straight-wall context.
2. Add parameterized arc-endpoint-to-line-interior and arc-to-arc relations,
   then promote only globally supported curves into placement-capable mixed wall
   graph paths.
3. Host curved openings by arc-length intervals, normalized parameters, and
   local tangent/normal vectors.
4. Solve rooms from mixed line/curve boundaries. Circular area contributions
   can be integrated analytically; general curves need bounded-error geometry.
5. Add elliptical and spline evidence only after circular topology is stable.

## Accuracy Gates

Placement-ready curved support requires reviewed cases for circular,
elliptical, spline, line-to-arc tangent, line-to-arc corner, concentric
double-face, single-centerline, curved opening, door-swing, radial stair,
compact symbol, and dense radial-pattern geometry.

Required metrics are centerline distance, radius/parameter error, endpoint
error, length error, thickness error, duplicate overlap, room closure, opening
interval error, false-curve rate, and runtime. Until those gates pass,
`curvedWalls` is exact evidence for review, not placement geometry.
