# Public PDF Evaluation

This report records the first broad, external PDF sweep for OpenPlanTrace
`0.12.001`. It is an engineering evaluation, not a certified accuracy
benchmark. No reviewed ground-truth wall set exists for these files, so wall
counts and internal confidence scores must not be presented as precision or
recall.

## Corpus

The local sweep used 13 publicly downloadable architectural PDFs containing 63
pages. The source PDFs, generated JSON, and screenshots are deliberately kept
under the ignored `artifacts/` directory and are not redistributed by this
repository.

| Case | Pages | Source character | Result |
| --- | ---: | --- | --- |
| Open farmhouse drawing set | 25 | Multi-sheet vector set | Stopped manually after unacceptable time and memory growth; page-at-a-time triage is required. |
| Historic house plan | 1 | Image-only historic scan | Completed; no structural walls recovered. |
| USDA two-bedroom farmhouse | 4 | Hybrid/raster publication | Completed; no structural walls recovered. |
| USDA farm cottage | 3 | Image-heavy publication | Completed; no structural walls recovered. |
| USDA five-bedroom house | 5 | Image-heavy publication | Completed; no structural walls recovered. |
| Typical farmhouse layout | 1 | Image-only plan | Completed; no structural walls recovered. |
| Modern private-house drawing set | 12 | Dense vector drawing set | Completed; focused floorplan page used for visual wall QA. |
| Apartment plan 11 | 1 | Sparse downloadable plan | Completed; no structural walls recovered. |
| Apartment plan 21 | 1 | Sparse downloadable plan | Completed; no structural walls recovered. |
| Apartment plan 25 | 1 | Sparse downloadable plan | Completed; no structural walls recovered. |
| Apartment plan 31 | 1 | Sparse downloadable plan | Completed; two incomplete exterior runs recovered. |
| Apartment plan 34 | 1 | Sparse downloadable plan | Completed; two incomplete exterior runs and one room recovered. |
| Municipal house-type set | 7 | Large multi-sheet publication | Stopped manually after excessive memory growth; streaming is required. |

Eleven files completed and two exposed unacceptable whole-document resource
growth. A completed scan does not mean a correct scan.

## Visual Finding

The focused vector page initially promoted long, regular floor-finish strokes
into 82 canonical wall runs. A new plan-scale parallel-pattern check now:

- recognizes regularly repeated, deep parallel families across a plan region;
- suppresses only the repeated family rather than every crossing primitive;
- preserves perpendicular structural crossings and source-backed exterior
  shell anchors; and
- records explicit surface-pattern evidence and diagnostics.

The focused page now produces 32 canonical runs, including six exterior runs.
The large stripe field is gone and the right exterior shell is restored. The
result is visibly cleaner, but small shell gaps and short tails remain and must
stay reviewable.

## Capability Gaps

1. Raster and image-only plans need a real OCR/vectorization adapter. The core
   has an adapter contract but the default CLI does not ship a model or invent
   geometry when none was extracted.
2. Large multi-page vector sets need page classification, per-page execution,
   bounded intermediate artifacts, and streaming export before they are safe
   production inputs.
3. Sparse or hybrid pages need source-readiness checks that distinguish "scan
   completed" from "structural geometry was actually available."
4. External plans need reviewed wall truth before they can contribute real
   precision, recall, placement-error, or room-closure metrics.

## Source Notes

The sweep included openly licensed and public-domain Wikimedia/USDA material,
the publicly downloadable Clearline architecture sample, Vassar apartment-plan
downloads, and an official Limerick house-type publication. Public download
does not itself grant redistribution rights, which is why the original files
and rendered pages remain local and ignored.

Source pages used for reproducibility:

- [Farmhouse Drawing Set (CC BY-SA 3.0)](https://commons.wikimedia.org/wiki/File:Farmhouse_Drawing_Set_V-001.pdf)
- [Historic House plans (public domain)](https://commons.wikimedia.org/wiki/File:House_plans.pdf)
- [USDA two-bedroom farmhouse](https://commons.wikimedia.org/wiki/File:Floor_plan_of_a_two_bedroom_farmhouse_(IA_CAT31371087).pdf)
- [USDA farm cottage](https://commons.wikimedia.org/wiki/File:Floor_plan_of_a_farm_cottage_(IA_CAT31371084).pdf)
- [USDA five-bedroom house](https://commons.wikimedia.org/wiki/File:Floor_plan_of_a_5-bedroom_house_(IA_CAT31371083).pdf)
- [Clearline architecture example](https://clearline.com.ua/en/exampleArchitect/)
- [Vassar apartment details](https://offices.vassar.edu/faculty-housing/apartment-details/91-raymond-ave/)
- [Limerick House Types](https://mypoint.limerick.ie/en/system/files/materials/378/House%20Types.pdf)

## Next Evaluation Gate

Add reviewed wall truth for at least one page in each source class: clean
vector, dense vector, hybrid, raster, curved-wall, rotated/non-orthogonal, and
large multi-page. Report centerline distance, endpoint error, length-weighted
precision/recall, exterior continuity, duplicate overlap, and room closure.
