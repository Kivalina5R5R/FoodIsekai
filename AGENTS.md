# FoodIsekaiZ editing rules

## Preserve existing work

- Treat the current working tree and the current Unity scene as the source of truth.
- Before editing, inspect `git status` and the relevant diff. Existing changes belong to the user and must be preserved.
- Never restore, reset, checkout, or regenerate unrelated scene sections from `HEAD`.
- Make narrow, target-specific edits. Do not use broad replacements against repeated Unity YAML fields such as `m_Sprite`, `m_Color`, or `m_Text` without object-specific context.
- After editing, verify the requested object and field counts, inspect the diff, and run `git diff --check`.

## Unity scene and runtime ownership

- Treat serialized scene values and runtime-written values as separate sources of truth. If a component is updated by a script at runtime, update that script as part of the requested format change.
- Preserve the authored UI hierarchy and visual decisions unless the user explicitly asks to replace them.
- Current UI decisions to preserve:
  - `CustomerPanel1` through `CustomerPanel6` use a child `BG Order` Image.
  - Customer panels have no legacy root Image background behind `BG Order`.
  - Customer order status uses the Image component backed by the assigned `F1-Meat` through `F5-Drink` prefabs, not a Text label.
  - `TeamScore` displays the formatted number only, without a `TEAM SCORE` label.
- Do not overwrite a scene file with a stale in-memory Unity scene. If Unity has unsaved scene changes, keep file edits limited to the requested serialized fields and flag a reload/save conflict instead of reverting the scene.
- Runtime scripts may update live game data only: score text, timers, status text, status sprites, and runtime visibility. They must not assign authored colors, fonts, transforms, anchors, sizes, sprites used as design defaults, sibling order, camera layout, or other Scene styling.
- Never restore or recreate a manually removed display object such as `Title`; preserve its current active state and hierarchy unless the user explicitly requests that object.
