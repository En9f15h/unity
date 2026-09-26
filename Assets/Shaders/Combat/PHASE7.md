# Attack range presentation

`Combat/Range Telegraph` and the shared Resources material are selected automatically by
`AttackRangePreviewManager`. Missing or unsupported material retains the original sprites.
No scene or prefab migration is required.

- Lower-opacity fill keeps the underlying character/background visible.
- Corner emphasis and border segmentation retain the logical tile grid.
- Enemy ranges add diagonal hatching and dashed borders, so overlaps are readable by shape
  as well as the existing ally/enemy colors. Authored renderer colors remain the source tint.
- Two small bottom markers converge during the existing timed warning window. Hover ranges
  have no countdown. Turning off border scale pulsing does not disable the timed markers.
- The shader uses one shared material for every range layer; per-renderer values use an MPB.
  Renderer count stays at two per cell, matching the previous implementation.

The warning's existing start, duration and post-hit fade remain controlled by the original
battle flow. The markers describe that warning window; no animation, beat, movement or hit
resolution timing is written by this presentation. The 60 FPS / 1 second character clip
contract and animation midpoint remain unchanged. Hit keeps its authored length.

## Validation

`RangePresentationValidation.RunBatch` runs the real offline selection-to-battle routes for
all seven maps, checking bounds, shared materials, ally/enemy patterns, hover state, timed
progress, independent toggles, fade cleanup and invalid ranges. Three new maps receive
hover and overlap images. `BuildProbe` produces a non-development test Player; run with
`-range-probe -shader-probe-output <directory>`. `BuildGame` builds the normal scene list
without the diagnostic definition. `-range-output <directory>` selects editor/build output.

These are simulated offline peer/readiness checks, not a full two-client online match.
