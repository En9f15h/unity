# Phase 9: Planning interaction feedback

ActionDragSource now passes its existing can-use and can-drag decisions to a
presentation component. A small corner frame fades in on hover or selection,
thickens on press and fades out on exit. The frame is gold for available actual
actions and cool blue for actions that can currently only be dragged for claims.
Locked planning and inactive palette sources suppress the frame.

Successful ActionSlot placement shows a 0.32-second confirmation. Multi-slot
actions also mark their continuation slot with a softer cool frame. Removing or
dragging the action clears its old confirmation. Repeated placements reuse it.

PlanningFocusFrame uses one MaskableGraphic mesh and the standard UI material.
It neither adds a custom shader nor replaces existing icon materials. It ignores
layout and raycasts and does not resize, move or recolour the authored icon.
Timing is unscaled and confined to UI feedback. No character animation, beat,
combat resolution, slot acceptance or claim rule is modified.

PlanningPresentationValidation.BuildProbe plus -planning-probe runs actual
selection-to-game routes and interaction checks in an isolated Player.
BuildGame creates an ordinary Player without the diagnostic compilation symbol.
Both methods accept -planning-output to choose their artifact directory.
