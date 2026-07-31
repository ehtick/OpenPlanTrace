# Public PDF Evaluation

This report records the public PDF stress sweep for OpenPlanTrace `0.12.002`.
It is an engineering evaluation, not a certified accuracy benchmark. The files
do not have reviewed wall truth, so detector counts and confidence values must
not be presented as precision or recall.

## Corpus

The general sweep used 13 publicly downloadable architectural PDFs containing
63 pages. Three additional public sources supplied focused curved, angled, and
filled-wall pages. Source PDFs, generated JSON, and screenshots remain under
the ignored `artifacts/` directory and are not redistributed by this repository.

| Case | Pages | Difficulty / source character | Result |
| --- | ---: | --- | --- |
| Open farmhouse drawing set | 25 | Large multi-sheet vector | Stopped manually after unacceptable whole-document time and memory growth; page-at-a-time triage is required. |
| Historic house plan | 1 | Raster | Completed; no structural vectors recovered. |
| USDA two-bedroom farmhouse | 4 | Hybrid/raster | Completed; no structural vectors recovered. |
| USDA farm cottage | 3 | Hybrid/raster | Completed; no structural vectors recovered. |
| USDA five-bedroom house | 5 | Hybrid/raster | Completed; no structural vectors recovered. |
| Typical farmhouse layout | 1 | Raster | Completed; no structural vectors recovered. |
| Modern private-house drawing set | 12 | **Extreme** filled-wall/detail stress | Focused page recovered a continuous source-backed exterior axis through three window assemblies; ultra-black wall-body median visually passed at 4x. |
| Apartment plans 11, 21, and 25 | 3 | Sparse vector | Completed; no structural walls recovered. |
| Apartment plan 31 | 1 | Sparse vector | Completed; two incomplete exterior runs recovered. |
| Apartment plan 34 | 1 | Sparse vector | Completed; two incomplete exterior runs and one room recovered. |
| Municipal house-type set | 7 | Large multi-sheet publication | Stopped manually after excessive whole-document memory growth; streaming is required. |
| Vanna Venturi focused plan | 1 focused page | 45-degree and circular geometry | One circular face pair retained with 0.174 drawing-unit maximum radial fit error; a 44.56-degree paired wall survived as review-only evidence where mixed topology was incomplete. |
| Los Gatos curved residence | 2 focused pages | Large-radius residential curves | Two real curved construction pairs survived stair/detail suppression on the reviewed main page; circular stair spokes did not become curved walls. |
| Pennsylvania museum plan | 1 focused page | **Extreme** radial/detail density | Full scan completed in about 12.2 minutes. A direct real-page retest removed one compact 257-degree equipment/detail ring while retaining 16 review candidates around the central curved assembly. |

Eleven general-corpus files completed and two exposed unacceptable
whole-document resource growth. A completed scan does not mean a correct scan.

## Implemented Findings

- Plan-scale orthogonal and parallel finish patterns remain excluded from wall
  reconstruction while perpendicular structural crossings are preserved.
- Two regular crossing diagonal families are exported as a non-structural
  surface pattern instead of becoming a forest of 45-degree walls.
- Axis-aligned and rotated filled polygons can recover a wall-body centerline.
- Canonical straight runs can use corroborated paired faces plus filled geometry
  to anchor axis and thickness to the physical wall-body median.
- Compatible circular faces are paired before line-only filtering, preserving
  exact center, centerline radius, start angle, signed sweep, thickness, source
  IDs, and radial error.
- Circular evidence remains review-only and excluded from placement/topology.
  No tangent intersection or artificial corner is created.
- Compact unfilled high-sweep rings and dense radial stair fans are rejected as
  likely symbols/details while large residential curves remain visible.

## Measured Checks

The focused filled-wall page produced 518 raw wall candidates and 30 selected
canonical runs in about 11.7 seconds. Its reviewed 667.094-unit top exterior
run was centered at `y = 349.895`, used a 22.677-unit physical thickness, and
remained continuous through three window assemblies.

The focused Vanna Venturi page produced one reviewable circular candidate with
a 44.880-unit centerline radius, 3.055-unit thickness, 48.530-degree sweep, and
0.940 confidence. The reviewed Los Gatos main page retained two large-radius
circular candidates with approximately 18-19 drawing-unit thickness.

The museum page is a performance and ambiguity stress case, not a readiness
success: its 257,192 source primitives required roughly 12.2 minutes, and some
concentric exhibit/floor geometry still resembles structural curved walls.
Those candidates remain unable to enter placement output.

## Capability Gaps

1. Raster and image-only plans need a real OCR/vectorization adapter. The core
   has an adapter contract, but the default CLI does not ship a model or invent
   geometry when none was extracted.
2. Large multi-page vector sets need page classification, bounded page-level
   execution, streaming export, and lower-allocation candidate indexing.
3. Curves need mixed line/curve graph nodes, structural-vs-radial-pattern
   arbitration, curved opening intervals, and curved room boundaries before
   coordinate placement can be enabled.
4. External plans need reviewed wall truth before they can contribute real
   centerline error, precision, recall, room closure, or opening-host metrics.

## Source Notes

Public download does not itself grant redistribution rights, which is why
original files and rendered pages stay local and ignored.

- [Farmhouse Drawing Set (CC BY-SA 3.0)](https://commons.wikimedia.org/wiki/File:Farmhouse_Drawing_Set_V-001.pdf)
- [Historic House plans (public domain)](https://commons.wikimedia.org/wiki/File:House_plans.pdf)
- [USDA two-bedroom farmhouse](https://commons.wikimedia.org/wiki/File:Floor_plan_of_a_two_bedroom_farmhouse_(IA_CAT31371087).pdf)
- [USDA farm cottage](https://commons.wikimedia.org/wiki/File:Floor_plan_of_a_farm_cottage_(IA_CAT31371084).pdf)
- [USDA five-bedroom house](https://commons.wikimedia.org/wiki/File:Floor_plan_of_a_5-bedroom_house_(IA_CAT31371083).pdf)
- [Clearline architecture example](https://clearline.com.ua/en/exampleArchitect/)
- [Vassar apartment details](https://offices.vassar.edu/faculty-housing/apartment-details/91-raymond-ave/)
- [Limerick House Types](https://mypoint.limerick.ie/en/system/files/materials/378/House%20Types.pdf)
- [Vanna Venturi study PDF](https://libstore.ugent.be/fulltxt/RUG01/002/224/711/RUG01-002224711_2015_0001_AC.pdf)
- [Los Gatos development plans](https://www.losgatosca.gov/DocumentCenter/View/41484/Development-Plans---0-Mireval-Rd-532-25-027)
- [Pennsylvania museum bid drawings](https://www.pa.gov/content/dam/copapwp-pagov/en/dgs/documents/design-and-construction/bidding/2026-document/946-12%20p6%20addendum%205.pdf)

## Next Evaluation Gate

Add reviewed wall truth for clean vector, dense vector, hybrid, raster,
curved-wall, rotated/non-orthogonal, filled-wall, and large multi-page classes.
Report centerline distance, endpoint and radius error, length-weighted
precision/recall, exterior continuity, duplicate overlap, room closure, and
runtime independently for each source class.
