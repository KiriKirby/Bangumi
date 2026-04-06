# Bangumi Agent Log

## Current Mission

- Keep the existing `UWP + WinUI 2` architecture.
- Rebuild the app UI toward the `Bili.Uwp` desktop shell style inside the current UWP codebase.
- Use these repositories as the long-term feature and API references:
  - `https://github.com/bangumi/api`
  - `https://github.com/czy0729/Bangumi`
- Use `https://github.com/Richasy/Bili.Uwp` as the direct UI shell and title bar reference.
- Implement the richer client experience gradually inside the current codebase instead of migrating to WinUI 3.

## Product Direction

- UI priority is higher than feature completion for the current phase.
- Left sidebar is the first-level navigation and should be built directly from `Bili.Uwp` desktop navigation patterns and file structure, not from ad hoc re-implementation.
- Top secondary selector is the second-level navigation for the active first-level section and should map to `czy0729/Bangumi` screen groups.
- Search, message entry, and avatar live in the title bar area and should visually follow `Bili.Uwp` `AppTitleBar`.
- Unfinished pages must navigate to stable placeholder UI instead of crashing, throwing resource errors, or closing the app.

## Constraints

- Do not migrate away from `UWP`.
- Do not replace `WinUI 2`.
- Prefer the latest WinUI 2 control styles and Fluent title bar / navigation patterns that can still run in this UWP app.
- Keep existing behavior working where possible while shell UI is being replaced incrementally.
- Do not invent extra wrapper layers, oversized spacing, or additional container hierarchy when `Bili.Uwp` already has a concrete implementation to copy.

## References

- Bangumi official API: `https://github.com/bangumi/api`
- czy0729 Bangumi client: `https://github.com/czy0729/Bangumi`
- Bili UWP shell reference: `https://github.com/Richasy/Bili.Uwp`
- Microsoft Learn `NavigationView` guidance: use left navigation for prominent top-level categories and adapt the pane behavior.
- Microsoft Learn `Title bar customization` guidance: custom title bars in UWP can host search and app commands.
- Microsoft Learn `TabView` guidance for WinUI 2: latest WinUI 2 styles should be preferred for modern rounded visuals.
- Microsoft Learn `Pivot` guidance: not preferred as a visible Windows 11 pattern, but it still provides swipe behavior; use carefully when swipe interaction is required.
- Local checked-out references for direct copying / inspection:
  - `_references/Bili.Uwp`
  - `_references/czy0729.Bangumi`

## Completed Setup And Fixes

- Installed `.NET SDK 8.0`.
- Installed `Visual Studio Community 2022` with UWP / WinUI build support.
- Installed Windows 10 SDK `10.0.19041.0`.
- Verified the solution can be built locally with MSBuild.
- Installed the official Codex CLI runtime for the user's Visual Studio plugin.

## Completed Code Changes

### OAuth / Runtime

- Filled the Bangumi OAuth app credentials in `Bangumi/Common/Constants.cs`.
- Fixed the OAuth authorize URL so `redirect_uri` is always included.
- Confirmed the app builds after the OAuth fix.

### Performance

- Reduced repeated list scans in `Bangumi/ViewModels/ProgressViewModel.cs`.
- Materialized repeated enumerable access in `Bangumi.Api/Services/BgmApi.cs`.

### Navigation

- Reworked the old top tabs so the current home area can use a real swipeable container.
- Added `Bangumi/Views/HomePage.xaml` and `Bangumi/Views/HomePage.xaml.cs`.
- Updated `Bangumi/MainPage.xaml.cs` so top nav clicks target `HomePage` tab selection instead of separate page swaps.
- Updated detail navigation in progress, collection, and calendar pages to navigate through the root frame.

### Shell UI Rewrite

- Switched `App.xaml` to `XamlControlsResources` `Version2` so the app picks up newer WinUI 2 control styles.
- Replaced the old `MainPage` top-navigation shell with a custom left-sidebar shell.
- Moved search, refresh, message entry, network indicator, and avatar into the custom title bar area.
- Rebuilt `MainPage` primary navigation around fixed first-level entries and a `Pivot` content surface for second-level sections.
- Added `Bangumi/Views/ShellPlaceholderPage.xaml` and `Bangumi/Views/ShellPlaceholderPage.xaml.cs` for unfinished modules during the shell rewrite.
- Updated `ShellPlaceholderPage` to use self-contained resources so placeholder screens do not depend on shell-only resource keys.
- Current section mapping is structural first:
  - `首页` hosts `ProgressPage` under the first secondary tab.
  - `时间线` hosts `CalendarPage` under the first secondary tab.
  - `收藏` hosts `CollectionPage` under the first secondary tab.
  - Discovery / Rakuen / Group / Messages / Profile sections currently route unfinished destinations into placeholders.
- Command-line build verification is currently limited by local SDK / task-host issues; prioritize runtime-safe placeholders and shell stability during this phase.

### Latest Shell Direction

- The shell must use local `_references/Bili.Uwp` UI files as the direct baseline, especially:
  - `_references/Bili.Uwp/src/App/Controls/App/AppTitleBar.xaml`
  - `_references/Bili.Uwp/src/App/Controls/App/DesktopNavigationView.xaml`
  - related styles and spacing decisions from the same shell area
- Do not treat `Bili.Uwp` as inspiration only; copy its structure, proportions, collapse behavior, and spacing as literally as practical inside the current UWP project.
- Left navigation should follow `Bili.Uwp` responsive behavior:
  - when the window becomes narrower, the pane may collapse into the hamburger form if that is how `Bili.Uwp` behaves
  - do not force a permanently expanded pane if that diverges from `Bili.Uwp`
- Top secondary navigation should stay visible and be treated as the second-level information architecture.
- UI look and spacing should be copied from local `_references/Bili.Uwp` files, especially:
  - `src/App/Controls/App/AppTitleBar.xaml`
  - `src/App/Controls/App/DesktopNavigationView.xaml`
- Function list, page grouping, and icons / material sources should be copied from local `_references/czy0729.Bangumi`.
- If a feature is not implemented, ship a clickable placeholder instead of wiring to unstable code.
- `SettingsPage` should not be opened directly during the current UI phase if it is unstable; route settings through shell placeholders first.
- The shell should prioritize `Bili.Uwp` proportions:
  - custom title bar height near `48`
  - search box width and proportion should match `Bili.Uwp` and stay stable instead of resizing with text length
  - sidebar width, collapse threshold, and overall spacing should match `Bili.Uwp`
  - fewer custom outer containers and less self-invented empty space
  - less rounded / less oversized content containers unless `Bili.Uwp` already does so
- Current responsive shell baseline:
  - `NarrowWindowThresholdWidth = 740`
  - `MediumWindowThresholdWidth = 920`
  - narrow state should collapse the pane into the hamburger interaction
  - narrow state should hide the `Bangumi` title text and keep only the logo
  - search box should shrink with the title bar layout when the available width drops below its wide-state width
  - current shell search width baseline is `520`, medium state shrinks to `420`, narrow state shrinks to `250`
  - page refresh should live inside the page content header area instead of the global title bar
- The current placeholder tree should cover not only `首页 / 时间线 / 收藏 / 发现 / 超展开 / 我的`, but also:
  - `小组`
  - `消息`
  - `小圣杯`
  - `工具`
  - `设置`
- Secondary navigation should explicitly mirror local `_references/czy0729.Bangumi/src/screens` groups wherever possible, even before functionality exists.

### Current Shell Freeze

- The shell is now frozen as a custom `SplitView + custom title bar + custom sidebar + right-side Pivot content shell`.
- Treat the current shell frame as design-locked.
- Do not change title bar layout, sidebar layout, right content frame layout, spacing, shadow layering, or shell color tokens unless the user explicitly asks to change the shell frame again.
- Future UI work should happen inside pages first; do not casually rework the outer shell.

### Current Shell Architecture

- `Bangumi/MainPage.xaml` is the source of truth for shell visuals and spacing.
- `Bangumi/MainPage.xaml.cs` is the source of truth for shell responsive behavior, sidebar state switching, and right-content header collapse behavior.
- `Bangumi/App.xaml` is the source of truth for shell palette and shadow tokens.
- The shell must remain:
  - custom title bar on row `0`
  - title-bar shadow strip on row `1`
  - `SplitView` shell body on row `1`
  - custom left pane inside `SplitView.Pane`
  - custom right content frame inside `SplitView.Content`

### Current Shell Visual Direction

- Use Win10-style hard-edged shell surfaces.
- Large shell frames and page-level containers must stay square.
- Title bar, left pane, and right content must read as three layered slabs.
- Preserve the current pink-accent dark theme.
- Preserve the current downward title-bar shadow and left-edge content shadow.
- Remove blue default accents anywhere they appear; treat them as bugs.

### Current Shell Palette Freeze

- window background: `#151014`
- chrome background: `#33252D`
- title bar background: `#33252D`
- left pane background: `#21171C`
- main gradient top: `#2D2026`
- main gradient mid: `#24191F`
- main gradient bottom: `#171115`
- content surface: `#2F2229`
- raised surface: `#392A32`
- pressed / recessed surface: `#1B1317`
- border: `#30FFFFFF`
- top separator baseline token: `#45FFFFFF`
- primary text: `#FFF8F4F6`
- secondary text: `#AEF8F4F6`
- accent pink: `#F091B2`
- accent light pink: `#F6A5C0`
- accent dark 1: `#C87090`
- accent dark 2: `#9E526C`
- accent dark 3: `#6F384A`

### Current Shadow Freeze

- title bar drop shadow start: `#78000000`
- title bar drop shadow end: `#00000000`
- main content left-edge shadow start: `#94000000`
- main content left-edge shadow mid: `#32000000`
- main content left-edge shadow end: `#00000000`
- title-bar shadow strip height: `10`
- main content left-edge shadow width: `14`

### Current Typography Freeze

- Entire app UI text is restricted to exactly three fixed size levels and no others.
- small text: `12`
  - baseline reference: sidebar section headers such as `导航` and `专题`
- medium text: `14`
  - baseline reference: sidebar item labels such as `首页` and `时间线`
- large text: `34`
  - baseline reference: right-side main section title
- Do not introduce extra font sizes for shell, page headers, tabs, cards, metadata, placeholder text, or settings text unless this typography rule is explicitly revised first.

### Current Title Bar Freeze

- root title-bar row height: `48`
- title bar top offset: `Margin="0,-1,0,0"`
- title bar right reserved caption-space column: `172`
- title bar must stay visually flush to the top edge; no visible black top border
- bright separator line under the title bar must stay removed
- command buttons:
  - base title-bar button size: `32 x 32`
  - hamburger button expanded / narrow shell height: `48`
  - hamburger button compact width: `56`
  - hamburger icon size: `18`
- app brand block expanded state:
  - margin: `8,0,18,0`
  - spacing: `8`
  - app icon: `18 x 18`
  - app name text size: `14`
- app brand block compact state:
  - margin: `6,0,8,0`
  - spacing: `6`
  - app icon: `20 x 20`
  - app name text must collapse
- right command group:
  - margin: `0,0,8,0`
  - spacing: `10`
  - must remain close to caption buttons without overlap
- avatar button:
  - ellipse size: `26 x 26`
- global search box:
  - host margin: `0,4,0,4`
  - height: `32`
  - font size: `14`
  - internal horizontal padding: `8`
  - border thickness: `1`
  - must remain square and hard-edged
  - must not steal focus on app startup or when the pane opens
- global search width rules:
  - minimal shell state: host width `0`
  - compact state: `max(220, min(300, width - 560))`
  - expanded state: `min(460, max(320, width * 0.21))`

### Current Left Pane Freeze

- `SplitView.OpenPaneLength`: `212`
- `SplitView.CompactPaneLength`: `56`
- left pane uses `ShellPaneBrush`
- left pane must remain custom-rendered, not reverted to stock `NavigationView`
- pane search host:
  - margin: `8,12,8,4`
  - search box height: `32`
- main scroll host:
  - top stack padding in XAML base: `0,8,0,0`
- footer host XAML base margin: `0,0,0,8`

### Current Sidebar Typography and Item Geometry Freeze

- section header style:
  - margin: `18,12,0,10`
  - font size: `12`
  - font weight: `SemiBold`
- item text style:
  - font size: `14`
  - font weight: `Normal`
- navigation button base style:
  - height setter: `42`
  - margin setter: `0,1,0,1`
  - padding setter: `0`
  - hard-edged template, no corner radius
- actual item content grid geometry:
  - right padding: `20`
  - indicator column width: `4`
  - icon column width:
    - `50` for `首页`
    - `48` for all other items
  - text column width: `*` in expanded mode, `0` in compact icon mode
- item icon sizes:
  - `首页`: `24 x 24`
  - all other sidebar items: `20 x 20`
  - compact-mode code path normalizes icon element size to `22 x 22`
- selection indicator:
  - width: `4`
  - height: `21`
  - vertical alignment: center
  - one fixed shared size for every item

### Current Sidebar Responsive Behavior Freeze

- three shell size states must remain:
  - minimal: width `< 620`
  - compact: width `>= 620` and `< 900`
  - expanded: width `>= 900`
- minimal state:
  - `SplitView.DisplayMode = Overlay`
  - pane open state driven by `_isTemporaryOverlayOpen`
  - title-bar search host hidden
  - pane search host visible
- compact state:
  - `SplitView.DisplayMode = CompactInline`
  - pane open state driven by `_isCompactPaneTemporarilyOpen`
  - hamburger visible
  - app name hidden
  - sidebar rendered in compact icon mode unless temporarily expanded
- expanded state:
  - `SplitView.DisplayMode = Inline`
  - pane always open
  - app name visible

### Current Expanded Sidebar State Freeze

- sidebar buttons:
  - width: `212`
  - height: `38`
  - margin: `0`
- sidebar top padding: `3`
- footer bottom margin: `3`
- separator top margin: `5`
- section headers visible
- item text visible
- selected state uses `SidebarSelectedBrush`
- selected state must win over plain hover
- selected hover must animate inside the selected gradient from left to right
- plain hover must remain subtle and high-transparency

### Current Compact Sidebar State Freeze

- sidebar root width: `56`
- sidebar buttons:
  - width: `56`
  - height: `38`
  - margin: `0,3,0,3`
- sidebar top padding: `8`
- separator top margin: `6`
- footer bottom margin: `2`
- section headers hidden
- item text hidden
- compact-state tooltip must show each item label
- compact-state selected visuals:
  - keep left indicator bar
  - keep compact selected gradient
  - no separate extra selection shadow unless explicitly requested
- compact top icon rail:
  - hamburger width: `56`
  - hamburger height: `48`
  - hamburger icon size: `18`
  - app icon must sit to the right of hamburger with current compact brand block margin `6,0,8,0`

### Current Sidebar Divider / Hover / Selection Freeze

- divider lines must stay thin and relatively transparent
- divider look must stay recessed / inset
- plain hover brush: subtle, very high transparency
- selected state:
  - gradient fill only
  - do not add extra selection shadow unless explicitly requested again
- selection indicator bars must use one fixed shared length for every item

### Current Right Content Frame Freeze

- right content host outer margin: `20,0,0,0`
- top gradient background height: `112`
- title area:
  - container height: `62`
  - container margin: `0,0,0,10`
  - title text top margin: `18`
  - title text size: `34`
  - title text weight: `Bold`
- secondary navigation:
  - control: `Pivot`
  - must remain left-aligned
  - control padding: `0`
  - header item padding: `0,0,20,4`
  - header item margin: `0`
  - header item min width: `0`
  - header item horizontal content alignment: `Left`
  - header item vertical content alignment: `Center`
  - header text size: `14`
  - header text weight: `Normal`
  - header text vertical alignment: `Bottom`
- right content area vertical rhythm:
  - title top margin is independent and larger than the rest
  - title-to-tab spacing is controlled by title container bottom margin `10`
  - tab indicator-to-content spacing is controlled by content host top margin `0`
  - these values are design-locked for the current shell frame
- right content host:
  - frame wrapper top margin: `0`
  - content should continue filling downward to the bottom of the window
  - content must start at the top-left of the available content region
  - right content shell itself must not add any extra internal left / top / right / bottom padding beyond the values listed above
  - shell-level right spacing is `0`; the right edge should visually run to the window edge
- current title collapse behavior:
  - collapse distance: `62`
  - collapse formula multiplier: `1.7`
  - title transform clamp: `12`
  - title opacity fade multiplier: `2.2`
- do not reintroduce a separate opaque header overlay under the tabs
- the unnecessary dark masking effect under the secondary navigation must stay minimized

### Current Shell Behavior Freeze

- `首页`, `时间线`, and `收藏` must immediately load their real first tab pages:
  - `ProgressPage`
  - `CalendarPage`
  - `CollectionPage`
- unfinished destinations must continue routing into `ShellPlaceholderPage`
- `Settings` stays inside the shell as a stable placeholder section until a full settings rewrite is ready
- `我的` navigation entry must remain avatar-based in both logged-in and logged-out states
- shell footer items without a matching `czy0729/Bangumi` image asset may use official Microsoft / WinUI symbols
- clicking an already selected left sidebar primary item must do nothing:
  - no section rebuild
  - no page reload
  - no reset back to the first secondary tab
- right secondary sections use this loading behavior:
  - first navigation into an uninitialized tab frame uses `EntranceNavigationTransitionInfo`
  - already initialized tab frames use `SuppressNavigationTransitionInfo`
  - do not reintroduce the old zoom / drill-in content animation for Pivot content loading
- left-sidebar primary-section switching keeps the whole right content block animation:
  - animated target: title + secondary navigation + current content as one block
  - background must stay static
  - duration: `380ms`
  - easing: `CubicEase / EaseOut`
  - direction:
    - lower section index => block enters from above
    - higher section index => block enters from below

### Current Existing Page Outer-Frame Freeze

- existing real content pages and placeholder pages must align to the shell content origin with no extra outer margin or padding
- page-level outer host rules currently frozen as:
  - `ProgressPage` root `AdaptiveGridView.Padding = 0`
  - `CalendarPage` zoomed-in `AdaptiveGridView.Padding = 0`
  - `CalendarPage` zoomed-out `ListView.Padding = 0`
  - `CollectionPage` zoomed-in `AdaptiveGridView.Padding = 0`
  - `CollectionPage` zoomed-out `ListView.Padding = 0`
  - `CollectionPage` `TypeCombobox.Margin = 0`
  - `ShellPlaceholderPage` root `StackPanel.Margin = 0`
- card internals may still have their own local padding; this freeze only applies to page-level outer alignment inside the shell

### Shell Change Policy

- From this point forward, the current shell frame is considered approved and frozen.
- Do not change any shell-frame values above unless:
  - the user explicitly asks to change that exact shell area, or
  - a compile/runtime bug forces a minimal corrective change
- If shell-frame changes become necessary, update this `AGENT.md` section immediately so it remains the exact source of truth.

### Current Build / Run Baseline

- The project now compiles successfully in `Debug | x64`.
- The default project platform has been switched to `x64` to align with the user's local installed package state.
- Debug packaging should avoid full multi-architecture bundle behavior during the current UI iteration phase.
- If deployment conflicts appear, first check whether another installed package with the same identity but a different architecture already exists on the machine.

## Current UI Rewrite Plan

1. Keep the app always runnable by routing unfinished destinations to `ShellPlaceholderPage`.
2. Rebuild the shell using direct `Bili.Uwp` desktop patterns and file-level structure instead of ad hoc approximations.
3. Keep the left navigation, top bar, search box proportion, and responsive collapse behavior visually as close as possible to `Bili.Uwp`, unless the user explicitly asks for a structural deviation.
4. Expand the second-level tabs until the visible information architecture matches the local `czy0729/Bangumi` screen tree closely.
5. Continue replacing shell icons / image elements with assets sourced from `czy0729/Bangumi`, with Microsoft / WinUI assets only as fallback when `czy0729/Bangumi` has no equivalent.
6. Only after the shell and placeholder tree are stable, replace placeholders with real pages incrementally.
7. Maintain and expand a checked screen-route index so every `czy0729/Bangumi` page-level screen is accounted for in the UWP shell.

## Page Index Tracking

- Current imported page-level checklist file:
  - `Docs/Czy0729BangumiScreenIndex.md`
- This file is the running source of truth for route coverage during the placeholder phase.
- Every page-level screen from local `_references/czy0729.Bangumi/src/screens` should eventually map to:
  - a real UWP page, or
  - a stable placeholder reachable from the shell
- Shared composition directories such as `_base`, `_icon`, and `_item` are not shell destinations, but they still matter as context when copying page structure and visual language.

## Notes

- OAuth secrets are configured in source right now because the current project expects constants; avoid publishing them to a public repository without moving them to a safer configuration path.
- This file should be updated whenever major environment, architecture, navigation, or UI decisions are made.
