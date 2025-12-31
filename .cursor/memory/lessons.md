## 2025-12-31
- Familiar box row actions: never use name-based `.fam sb <name>` from UI clicks; SmartBind uses substring matching and names are not unique (e.g., "Skeleton" vs "Skeleton Crossbow"), causing the wrong familiar to activate. Always use slot indices from `.fam l` and send `.fam b <slot>` (or `.fam ub` then delayed `.fam b <slot>` when switching).
- HUD sprite lookups: new UI sprites only resolve if the sprite name is included in `Services/HUD/Shared/HudData.cs` `SpriteNames` (otherwise Images show as white placeholders). Add required sprite names when introducing UI assets.
