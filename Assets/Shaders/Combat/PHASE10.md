# Phase 10: Reserved action slots

ActionSlot now hides the legacy white lock overlay and displays a muted skill
echo with a linked-diamond marker. The original sprite selection precedence is
preserved: authored continuation sprite, action icon, then source prefab image.
No sprite leaves the link marker visible without reusing a previous icon.

ActionContinuationPresentation creates its three graphics only on first use and
reuses them. The image and marker use standard UI materials, accept no raycasts,
and do not create another DraggableItem or action record. The root ignores layout.
Clearing the reservation hides its root and clears its sprite. Phase 9 placement
confirmation remains layered above it.

Slot cost, acceptance, claim rules, character animations, beat timing, scene
assets and graphics settings are unchanged. The old serialized visual references
remain in place for recoverability.

The existing PlanningPresentationValidation.BuildProbe / -planning-probe route
now checks reserved-slot display, input isolation, occupied-slot rejection,
missing artwork, clearing and reuse alongside the previous interaction checks.
