# Unity Editor Bridge Contract

This subtree inherits the repo-root authority from:

- `C:\projects\fa\AGENTS.md`

If this file conflicts with repo-root canon, the repo-root canon wins.

This subtree is the clean home for the reusable FrameAngel Unity editor bridge
package.

## Purpose

Keep the bridge package portable and tool-shaped:

1. source-only package contents live here
2. Unity product projects reference this package as a local dependency
3. product-specific scenes, prefabs, and authored assets do not move here

## Boundaries

Keep in this subtree:

1. `package.json`
2. `Editor/`
3. `Runtime/`
4. validation scripts and the validation project
5. package-local docs

Do not move here:

1. product Unity scenes or prefabs from `C:\projects\fa`
2. operator data
3. deployed player assets
4. repo-external historical bridge copies

## Validation

Primary local validation entrypoint:

1. `C:\projects\fa\products\vam\assets\player\unity_editor_bridge\current\Validate-UnityEditorBridge.ps1`

When changing the bridge, prefer validating here before assuming downstream
Unity projects are at fault.

## Migration rule

If a Unity project still points at an old local package path, patch the
project manifest to reference:

1. `C:\projects\fa\products\vam\assets\player\unity_editor_bridge\current`

Do not reintroduce `C:\projects\frameangel` or older recovery roots as the
landing zone for active bridge work.
