# Project Memo

Last updated: 2025-12-31

## V Rising Mod – Familiars (current truth)

- Ownership:
  - UI: `Services/CharacterMenuService.cs`
  - Box/chat updates: `Services/DataService.cs` + `Patches/ClientChatSystemPatch.cs`
  - Sprite allowlist: `Services/HUD/Shared/HudData.cs`

- Familiars UI is owned by `Services/CharacterMenuService.cs` (single owner; avoid parallel UI implementations).

- Familiars box live updates are driven by chat parsing:
  - Parsing: `Services/DataService.cs`
  - Hook/wiring: `Patches/ClientChatSystemPatch.cs`

- The Current Box list is intentionally fixed to 10 slots:
  - Always render 10 rows
  - Empty rows are disabled placeholders (consistent layout)

- Icon/sprite stability rules:
  - Use manifest-backed sprite names where possible
  - If a sprite fails to resolve, hide the icon to avoid white placeholder boxes
  - HUD sprite allowlist must include required sprites for headers/dividers/icons:
    - `Services/HUD/Shared/HudData.cs`

- Binding must be slot-based (not name-based) to avoid ambiguity:
  - Prefer `.fam b #` with delayed unbind→bind routine when needed

---

## Eclipse modular architecture (current truth)

- Ownership:
  - HUD subsystem: `Services/HUD/*`
  - Character menu subsystem: `Services/CharacterMenu/*`
  - Shared UI factory: `Services/CharacterMenu/Shared/UIFactory.cs`

- HUD subsystem lives under `Services/HUD/*` with clear ownership boundaries:
  - Interfaces (`Interfaces/*`), base classes (`Base/*`), managers/orchestrator/integration, shared utilities/config/data, and concrete components.

- Character menu subsystem lives under `Services/CharacterMenu/*`:
  - Tabs implement `ICharacterMenuTab` and typically derive from `CharacterMenuTabBase`.
  - Shared UI creation helpers live in `Services/CharacterMenu/Shared/UIFactory.cs`.
  - Orchestrator + integration manage lifecycle and wiring.

- Backwards compatibility policy:
  - Public APIs remain stable; legacy nested static classes delegate to extracted manager classes.

- Il2Cpp constraints to remember:
  - Avoid ambiguous `Object.Destroy()` → prefer `UnityEngine.Object.Destroy()`.
  - Avoid `new RectOffset(left,right,top,bottom)`; set properties explicitly.

- Old monolithic sources are archived at:
  - `Docs/Archive/OldServices/*`
