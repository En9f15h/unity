# Phase 11: Battle phase countdown

BattleCountdownPresentation presents the existing TurnPlanningManager deadline as PLAN, WAIT (local plan submitted), RESOLVE, or SYNC. The legacy timer Text remains the manager's serialized reference. Its child view replaces the dedicated gray backing at runtime, inside the original timer bounds. A shared HUD background is never disabled.

The remaining-time rule is unchanged: ceil(max(0, remaining)). The bar uses the same shared remaining/duration values. Only an unsubmitted plan in its final five seconds gets amber text and a single 0.22-second, 7% scale pulse per changing second. Submitted plans keep a calm cool tint. Resolution has no numeric countdown; results hide the complete view.

All added graphics are non-interactive and use standard UI materials. No scene, prefab, character animation, BPM, damage, deadline, networking, shader or DirectX setting is changed. The pulse uses unscaled UI time and never drives battle timing.
