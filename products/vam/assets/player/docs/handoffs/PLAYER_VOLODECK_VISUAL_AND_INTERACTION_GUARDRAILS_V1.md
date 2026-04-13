# Player Volodeck Visual And Interaction Guardrails V1

## Purpose

Volodeck is powerful, but it can also create false confidence.

This note exists to stop the common failure mode where a witness looks "good
enough" only because the framing was weak, the capture was blank, or the proof
surface was not actually exercising the same contract that VaM will use.

## Hard rule

Volodeck is the harness.

That means:

1. do not bloat player or Meta runtime components with Volodeck-only hooks
2. do not add Unity-only control paths that VaM will never use
3. do not call a surface "good" just because the capture did not reveal an
   obvious flaw

## Goodhart warning

LLM review is especially vulnerable to "looks fine because nothing is obviously
wrong."

That is not an acceptable visual pass condition.

A Volodeck witness only counts when it was intentionally framed to answer a
specific question:

1. orientation
2. edge quality and icon crispness
3. control placement
4. interaction truth

## Required witness packet before operator testing

Every candidate surface or shell should produce all of the following before the
operator is asked to test it:

1. a contextual scene preview
2. a tighter surface preview
3. capture metadata for the tighter surface preview
4. a receipt that records the exact command, project, and output paths

For the current Meta video-player proof, that packet now comes from:

1. `products/vam/assets/player/scripts/Build-MetaVideoPlayerProofVolodeckWitness.ps1`
2. `products/vam/assets/player/unity/ghost_training_export_clone/Assets/Editor/GhostMetaUiSetBootstrap.cs`

## Visual pass criteria

### Context shot

The contextual scene preview is for placement and orientation.

It passes only if:

1. the resource is large enough in frame to judge its overall orientation
2. the surface is not a tiny island in a blank background
3. the camera still leaves enough surrounding space to read the shape and stance

It fails if:

1. the subject is too far out
2. the image is blank or nearly blank
3. the framing hides the real orientation question

### Surface shot

The tighter surface preview is for fidelity and control layout.

It passes only if:

1. readable labels are upright and oriented correctly
2. icons are large enough to judge edge quality
3. the surface is cropped tightly enough to inspect details
4. the crop still preserves enough context to understand layout relationships

It fails if:

1. it is too zoomed in to understand the layout
2. it is too far out to judge icon/text quality
3. it trims away important edges or alignment clues

## Interaction pass criteria

A Volodeck interaction witness must use the same exposed control contract that
VaM will use.

That means:

1. use the authored proof surface
2. use the same action and value ids that export with the control surface
3. do not invent a hidden proof-only control API

Interaction is only considered truly proven when the harness can show:

1. action input
2. visible state response
3. state readback parity where applicable

Important current boundary:

1. Volodeck can honestly prove shell orientation, control layout, and exposed
   contract shape right now
2. Volodeck should not be described as exact VaM-internal interaction emulation
   while the live Halo-to-VaM chaining seam is still unavailable
3. when that gap exists, the witness packet should explicitly separate:
   - Volodeck visual/control preflight
   - player-backed raw or packaged artifact proof
   - live VaM interaction proof

Examples:

1. `play/pause` should visibly toggle playback state
2. a scrub control should move the timeline and the displayed state should track
3. hover or focus visuals should only count if they match the intended real
   interaction seam

## Timing/orientation proof rule

If the question is about a specific movie frame or a known landmark moment,
such as a logo appearing at a known time:

1. the witness must intentionally seek or otherwise land on that moment
2. the capture must be judged against that exact orientation question
3. do not assume looped playback or `playOnStart` is enough

If the repo does not yet contain a deterministic timing hook for that witness,
record that gap explicitly instead of pretending the proof already exists.

## Current known lessons

1. A wide scene shot can be technically correct and still be useless if the
   subject is too small in frame.
2. A tighter surface capture can be the honest visual judge for orientation and
   edge quality even when the wider shot is weak.
3. Snapshot-derived witnesses are not sufficient for judging authored
   interactive visual fidelity when an authored Volodeck proof exists.
4. Context and surface captures should be treated as complementary, not
   interchangeable.

## Current Meta video-player witness

Current witness command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\projects\fa\products\vam\assets\player\scripts\Build-MetaVideoPlayerProofVolodeckWitness.ps1 -RepoRoot C:\projects\fa
```

Current outputs:

1. `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/video_player_proof_scene_preview.png`
2. `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/video_player_proof_surface_preview.png`
3. `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/video_player_proof_surface_preview.meta.json`
4. `products/vam/assets/player/build/meta_video_player_proof_volodeck_witness/meta_video_player_proof_volodeck_witness_receipt.json`

## Resume rule if a thread freezes

1. open this file
2. open `PLAYER_VOLODECK_PARITY_BOUNDARY_V1.md`
3. inspect the latest witness images before making claims about quality
4. confirm both context and surface captures exist
5. only then decide whether the next step is:
   - visual polish
   - interaction wiring
   - timing/orientation proof
