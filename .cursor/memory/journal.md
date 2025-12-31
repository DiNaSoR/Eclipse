## 2025-12-30
- Added a Familiars subtab mock layout in Docs/DesignMock/index.html, including the active familiar card, quick actions, box management, binding grid, and advanced commands.
- Refined Familiars tab styling with a new layout, accents, and a lightweight reveal animation, plus responsive stacking for narrow screens.

## 2025-12-30
- Implemented the Familiars tab UI layout in Services/CharacterMenuService.cs with a custom panel (active familiar card, action rows, bind grid, advanced actions) and data-driven updates.
- Updated tab visibility logic to show the new familiars panel instead of text entries and wired progress/stat display from familiar data.
## 2025-12-31
- Tuned Familiars UI sizing in Services/CharacterMenuService.cs with tighter font scales, spacing, and overflow handling plus game-sprite backgrounds/dividers to prevent text overlap.
- Updated Docs/DesignMock/index.html Familiars styling to match in-game sprite assets (Window_Box, Window_Box_Background, SlotFrame_Smaller, progress bar) with smaller typography.
- Redesigned the Familiars mock layout in Docs/DesignMock/index.html into a 2x2 grid (Active/Quick/Bind/Box), added a box selector with a familiar list, and introduced icon-heavy headers.
- Ported the 2x2 Familiars layout into Services/CharacterMenuService.cs with card headers, iconized action rows, a dropdown-style box selector, and a box list placeholder hooked to active familiar data.
- Reworked the Familiars mock so box management spans the right column through the advanced row, with the advanced list in the left column and a shared divider row.
- Reorganized the Familiars mock into left/right columns (Active/Bind/Advanced left, Quick/Box right) with a vertical ActionSlotDivider split.
- Swapped Familiars header backgrounds to Act_BG and container dividers to Divider_Horizontal, plus applied Act_BG headers and Divider_Horizontal container lines to the Stat Bonuses and Professions sections.
- Applied the Familiars left/right column layout in-game with a vertical ActionSlotDivider, card-wrapped Quick Actions, and a flexible Box Management column stretch.
- Updated in-game Familiars/Stat Bonuses/Professions headers to use Act_BG and section dividers to use Divider_Horizontal.
- Refined the in-game Familiars tab styling in Services/CharacterMenuService.cs with smaller typography, iconized headers/action rows, command pills, a dropdown-style box selector, and a placeholder current-box list, plus updated colors to match the HTML mock.\n- Built the Release configuration to produce the updated Eclipse.dll.
- Added client-side parsing for familiar box/chat replies in Services/DataService.cs and hooked it in Patches/ClientChatSystemPatch.cs so box selection and current box contents can update live.\n- Darkened Familiars header backgrounds and adjusted bind grid spacing in Services/CharacterMenuService.cs to avoid overlap, plus auto-requested box data on a cooldown when the tab opens.\n- Built the Release configuration for the updated Eclipse.dll.
- Made Familiars box entries update instantly with active familiar level changes, added refresh-on-change, removed command text from familiar rows/headers, tightened right-column spacing, darkened header tint, and adjusted bind grid padding in Services/CharacterMenuService.cs.\n- Built Release after updates.
- Replaced missing Familiars icon sprite names with manifest-backed options and hid header/action icons when sprites fail to resolve to eliminate white placeholder boxes in Services/CharacterMenuService.cs.
- Built Release after the Familiars icon fix.
- Made Current Box familiar rows clickable (smartbind/summon via `.fam sb`) and changed the vertical column divider to participate in layout so columns respect it in Services/CharacterMenuService.cs.
- Built Release after enabling clickable box rows and divider layout changes.
- Synced `Docs/DesignMock/index.html` Familiars with the latest in-game UI changes (layout divider as a real element, removed visible command text, updated missing action icons, and made Current Box rows buttons).
- Fixed Current Box row clicks to use slot-based binding (`.fam b #`) with a delayed unbind->bind routine, eliminating name-based smartbind ambiguity (Skeleton vs Skeleton Crossbow) in `Services/CharacterMenuService.cs`.
- Added a functional Box selector dropdown (toggles list, selects via `.fam cb`, refreshes via `.fam l`) and forced the Current Box list to always render 10 slots (empty rows disabled) in `Services/CharacterMenuService.cs`.
- Expanded the HUD sprite allowlist so Familiars headers/dividers/icons resolve from game assets (no white placeholder boxes) in `Services/HUD/Shared/HudData.cs`.
- Updated `Docs/DesignMock/index.html` Familiars to match: smaller typography, 80% column divider height, and 10-slot Current Box list with empty placeholders.
