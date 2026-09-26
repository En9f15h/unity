# Phase 12: Ready button feedback

ReadyButtonPresentation observes the same manager phase used by the countdown. The authored ready image, Button callback, colours, interactability and hit-area geometry are preserved. During planning, pointer/keyboard focus fades in gold corners; pressed corners widen. Submission changes to a cool WAITING / PLAN LOCKED badge with one 0.28-second confirmation highlight. Resolution shows RESOLVING / ACTIONS IN PLAY, synchronization shows SYNC, and results hide the added presentation.

The badge sits inside the button's central area because Ready is baked into both character UI sprites. Added graphics have raycastTarget=false. Focus and confirmation use unscaled UI time. This component neither submits actions nor changes Button listeners or game state. Existing phase, deadline, networking, character animation and beat rules remain authoritative.
