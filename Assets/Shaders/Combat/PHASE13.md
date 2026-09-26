# Phase 13: Range visibility controls

The two independent range buttons now show MY RANGE / ON or OFF and ENEMY RANGE / ON or OFF. Dark panels and restrained green/red corners replace colour-only status. Pointer and keyboard focus brighten the corners; press changes their thickness. The existing button geometry, callbacks and local visibility flags remain authoritative. Added graphics never intercept input; no new shader or material is needed.

Disabling one side also clears an already-visible hover preview belonging to that side. Previously the per-side setters cleared only timed warning groups, allowing a hover preview to remain visible while its toggle was off. The manager now records the hover preview's side and clears only a matching one. Other-side previews are preserved. This affects display only, not range calculations, damage, character animation or beat timing.
