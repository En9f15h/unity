# Phase 8: Result presentation

`GameResultManager` retains the authored victory, defeat and opponent-left sprites,
result logic, audio calls and return-button action. `ResultPresentation` adds:

- A 0.34-second unscaled fade and 18-unit settling motion; no time-scale changes.
- Gold victory, muted red defeat and cool neutral draw/disconnect ornaments.
- One short highlight using `Combat/Result Emblem UI`, a UI shader that soft-keys
  the authored emblems' near-black backgrounds while retaining their colours.
- A centred draw title when no authored emblem is available.
- A panel that fits smaller canvas sizes with margins.

The modal backdrop blocks gameplay clicks immediately, independently of the panel
alpha. Its final opacity is 0.52, confined to the result overlay. All decorations
are non-interactive. Repeated results reuse the existing hierarchy. Destroying the
manager releases its overlay, including its UI material instance.

No animation clips, combat beat lengths, damage resolution, map ordering,
post-processing settings, scene assets or graphics API settings are changed.

Validation entry points:

- `ResultPresentationValidation.BuildProbe`, then launch with `-result-probe`
  and `-shader-probe-output <directory>` for actual scene integration.
- `ResultPresentationValidation.BuildGame` for a regular player without probes.
- Both build methods accept `-result-output <directory>`.

The probe covers seven selection-to-battle routes, four result states, repeated
updates, paused-time presentation, responsive bounds and return-to-selection.
Screenshots use offscreen rendering; they do not measure GPU frame performance.
