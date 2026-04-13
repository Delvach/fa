# FrameAngel Unity Editor Bridge

Drop-in Unity Editor package that exposes the first bounded HTTP host behind
Kitchen Sync `unity_adapter`.

Target baseline:

- Unity `2022.3`

Live proof on this machine:

- Unity `2022.3.62f3`
- project `C:\Users\ben\Documents\3d\vam\holodeck\test2022`
- host `http://127.0.0.1:8797`
- Kitchen Sync `unity_adapter` connected

Current slice:

- `GET /v1/health`
- `GET /v1/capabilities`
- `GET /v1/state`
- `POST /v1/command`

Implemented commands:

- `observe.selection`
- `observe.scene_context`
- `observe.prefab_context`
- `observe.object_children`
- `observe.object_bounds`
- `observe.workspace_state`
- `capture.scene_view`
- `capture.game_view`
- `capture.camera`
- `capture.orbit_view`
- `capture.section_view`
- `capture.multicam_rig`
- `bridge.refresh_package`
- `scene.workspace_reset`
- `scene.group_root_upsert`
- `scene.primitive_upsert`
- `scene.rounded_rect_prism_upsert`
- `scene.particle_system_upsert`
- `scene.crt_glass_upsert`
- `scene.crt_cabinet_upsert`
- `scene.seat_shell_upsert`
- `scene.object_duplicate`
- `scene.object_delete`
- `scene.object_get_state`
- `asset.texture_import_local`
- `scene.material_style_upsert`
- `asset.innerpiece.inspect_selection`
- `asset.innerpiece.export_selection`
- `asset.innerpiece.export_project_asset`
- `asset.innerpiece.capture_preview`
- `asset.innerpiece.get_last_export`

Guardrails:

- bounded primitive workspace only
- explicit capture only
- no arbitrary API invoke
- no hidden movement of user-owned cameras
- workspace mutation disabled while prefab stage is open
- no broad `unity.api.invoke`
- local package edits can auto-queue `AssetDatabase.Refresh()` while the bridge is running

## Install into a Unity project

Add the package as a local package from:

- `C:\projects\fa\products\vam\assets\player\unity_editor_bridge\current`

Then open:

- `FrameAngel/Unity Bridge/Open Control Panel`

From the control panel you can:

- start or stop the local bridge
- enable or disable auto-start
- enable or disable package auto-refresh on source changes
- queue a manual package refresh without touching the mouse
- review the current endpoint

Important:

- adding the package does not automatically start the bridge
- after install, click `Start` or enable `Auto Start`

Default endpoint:

- `http://127.0.0.1:8797`

Default capture root:

- `<UnityProject>\Library\FrameAngelUnityBridge\Captures`

## Kitchen Sync contract

Kitchen Sync owns:

- routing
- policy
- health/state/capability polling

This Unity package owns:

- real Unity Editor inspection
- explicit SceneView / GameView capture
- named camera capture
- temporary orbit-angle capture around a chosen target root without touching user-owned cameras
- canonical six-view bundle capture
- bounded primitive workspace mutation and inspection
- bounded particle-system authoring with textured particle materials and readback
- grouped shell blockout roots with child-parented shell parts
- single-object rounded-rectangle prism authoring and bounded duplication
- bounded CRT-style curved glass authoring for retro screens
- bounded CRT cabinet authoring with a front frame section, optional front screen recess, and tapered rear shell
- bounded economy seat shell authoring for original cabin-study seatbacks with embedded screen bays
- local texture import plus bounded material/style application for shell finishes and procedural placeholders
- InnerPiece asset inspection and hybrid package export
- bridge-driven package refresh so the running editor can pick up bridge source changes without focus clicks
- the bounded HTTP command surface consumed by `unity_adapter`

## Compatibility and troubleshooting

- the current package is for Unity `2022.3`
- do not assume a Unity `2018` project is a valid target just because it has a
  `Packages/manifest.json`
- if Kitchen Sync shows `connection refused`, first check whether the bridge
  window says `Status = Running`
- if the package is installed but the `FrameAngel` menu does not appear, the
  Unity project is likely not compiling cleanly enough for the editor tooling to
  load

## Validation build

This repo includes a source-validation project at:

- `validation/FrameAngel.UnityEditorBridge.Validation.csproj`

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Validate-UnityEditorBridge.ps1
```
