# PMM_VIPCore — install

[English](#english) · [Русский](#русский)

## English

### PMM_VIPCore 0.1.5 — install

Bridge: menus of VIPCore are shown through PanoramaMenuManager. Supported: VIPCore 2.x (partiusfabaa, 2.0.0) and
the older VIPCore 1.x (thesamefabius, v1.3.3 / release recompile3) — the bridge finds out which one is installed.
VIPCore and its modules are not rebuilt — everything goes through Harmony, as in PMM_GG1MapChooser.

#### Requirements
- CounterStrikeSharp 1.0.378 or newer (.NET 10)
- VIPCore (original `plugins/VIPCore` + `shared/VipCoreApi`) and any `VIP_*` modules
- MenuManagerCS2 (PanoramaMenuManager) 1.2.03 and its client addon (WeaponsMenu "Panorama digits" mode: 1.2.05)

VIP_Fov and VIP_Tag must be the **original** builds (thesamefabius release), not the ones from the PMM fork: the fork
builds call `VipFeatureBase.OnMenuSelect`, which the original VipCoreApi does not have (`MissingMethodException`).
They are not part of this archive.

The self-built fork `PMM_VIPCore_partiusfabaa` (v1.3.3-pmm4) is not needed any more — put the original `VIPCore.dll` back.

#### Server
```
plugins/PMM_VIPCore/PMM_VIPCore.dll
plugins/PMM_VIPCore/harmony/0Harmony.dll
plugins/PMM_VIPCore/lang/ru.json, en.json
configs/plugins/PMM_VIPCore/PMM_VIPCore.json
```
- `0Harmony.dll` only in `harmony/`. If PMM_GG1MapChooser is installed, the copy it already loaded is reused.
- Do not put `MenuManagerApi.dll` or `CounterStrikeSharp.API.dll` into the plugin folder.
- Restart the server completely after an install or update (`css_plugins reload` does not unload Harmony).
- Check: `css_pmm_vip` in the server console. Expect `active=True`, `VipCoreApi.CreateMenu` in `patched`,
  `painter=True` and 2 languages in `translations`.

#### VIPCore 2.x: `MenuType`
`configs/plugins/VIPCore/vip_core.json` of VIPCore 2 has `"MenuType"`. The bridge takes over the VIP menus only for
the values listed in `CoreMenuTypes` of our config (default `center`, `pmm`, `panorama`):
- `"MenuType": "center"` (VIPCore's default) — `!vip`, its sub menus, Fov / Tag, WeaponsMenu and the VIP admin menu
  open as PMM_VIPCore menus (VIP panorama for PanoramaMenu players);
- `"MenuType": "chat"` / `"console"` — VIPCore shows its own menus, the bridge does not touch them.
`css_pmm_vip` shows `vipcore=… (2.x)`, `menuType=` and `takeover=True/False`.
Use the VIP modules built for VIPCore 2 (VIP_Fov, VIP_Tag, VIP_WeaponsMenu … from the same release).

#### Client: VIP panorama (addon)
Copy the content of `Content-addonmanager/` into the root of the addon that already carries the MenuManager panorama
(MultiAddonManager) and rebuild the addon:
```
panorama/layout/custom_game/pmm_vip.xml
panorama/styles/custom_game/pmm_vip.css
panorama/styles/custom_game/pmm_vip_theme.css
```
Players have to reconnect after a new addon version.

The panel is drawn for players who picked **PanoramaMenu** (mouse) in `!menu`. Other menu types (WASD, chat, CS:GO)
get the standard MenuManager menu. Exception: the WeaponsMenu panel is shown to everyone (it has its own mouse / WASD / digits
modes, see below). Without the addon on the client nothing appears: set `Paint.Enabled: false` then.

On screen:
- header: nickname, VIP group (colour per group), VIP expiry ("until 12.11.2026 · 34 d." / "forever");
- 3×4 tiles (12 per page): icon, name, the value from `vip.json` ("120 HP", "x1.2"), a switch /
  a value with a list / "▶ USE" / a lock "No access";
- a click on Fov / Tag opens a 4×4 value picker;
- module sub menus are an 8-row list with "Back"; WeaponsMenu has its own compact panel (below);
- status line bottom left, pages bottom centre, auto-close countdown bottom right; the page is remembered per menu;
- a module's own menu (WeaponsMenu) closes after a pick and has no "Back", as in CSS.

Layout generator: `tools/gen_layout.py` (icons, palettes, tile count — ids and classes must match `Paint.cs`).

#### WeaponsMenu (VIP_WeaponsMenu)
The weapons menu of the original VIP_WeaponsMenu (daffyy) gets a compact panel (6 rows, `WeaponsMenu.Position`)
with a gear ⚙ in the header, left of ×. The gear and the chat command **`!swm`** open the settings
(only for VIP players with the `WeaponsMenu` feature):

| Row | What it does |
|---|---|
| Ask every time whether to save the set? | on (default) — "Save this set?" after every pick; off — never asked again (kept over map changes and restarts), the set is saved here |
| Open the weapon set menu automatically | off — the menu does not pop up on spawn; take a set with "Pick a set now" |
| Pick a set now | opens the weapons list (the module checks alive / buy zone / buy time / round itself) |
| Save set «X» | saves the set taken last (when it was not saved in the prompt) |
| Delete the saved set | the weapons menu shows up again on the next spawn (the same as the module's `!vwmenu`) |
| Menu control | opens the list Panorama mouse / Panorama WASD / Panorama digits, a pick sets the mode at once. Digits: the first pick runs the number keys check |

Controls of the panel:
- mouse — clicks (the mouse is captured while the panel is open);
- WASD — W/S move, E take, A/D page, R settings (in the settings: back / close); the mouse stays with the game;
- digits — 1–6 take, 7 settings / back, 8/9 page, 0 close. Needs PMM Core 1.2.05: the first switch opens the
  MenuManager number keys check (the bind line is printed to the console), the mode is set after it is passed.

Player settings are kept per SteamID in `plugins/PMM_VIPCore/weaponsmenu_players.json`. Defaults for new players —
`WeaponsMenu.DefaultAskSave`, `DefaultEveryRound`, `DefaultMode` (`Auto` = from the player's `!menu`).
The panel needs the 0.1.4 addon files (`pmm_vip.xml`, `pmm_vip.css`, `pmm_vip_theme.css`): rebuild the addon.

#### Colours (`Paint.Theme`)
Panorama cannot take colours from the server, so the colours live in a separate file `pmm_vip_theme.css`:
1. change the colours in `Paint.Theme` (`#rrggbb` or `#rrggbbaa`, `aa` = opacity);
2. the plugin writes `plugins/PMM_VIPCore/pmm_vip_theme.css` on load (or on `css_pmm_vip css`);
3. copy it to `panorama/styles/custom_game/` of the addon and rebuild the addon.

| Key | What |
|---|---|
| `PanelTop`, `PanelBottom`, `PanelBorder`, `Separator` | panel background (top to bottom gradient), border, line under the header |
| `Nick`, `Muted` | nickname; VIP expiry and the clock icon |
| `CloseBg`, `CloseHover`, `CloseText` | the × button |
| `TileBg`, `TileBorder`, `TileHover`, `TileName`, `TileDesc` | tiles, picker buttons, sub menu rows |
| `IconBox`, `Icon` | icon box and icon of a disabled feature |
| `SwitchOff`, `KnobOff`, `KnobOn` | switch off / knob off / knob on (an enabled switch uses the group colour) |
| `ButtonBg`, `ButtonHover`, `ButtonText`, `DotOff` | "Back", page arrows, page dots |
| `ToastOk*`, `ToastWarn*` | status line (Bg, Border, Text, Icon) |
| `Palettes` | group colours: name → `Accent` (main: enabled switch, badge, group, borders), `Light`, `Dark`, `OnAccent` (text on the badge). Add your own names and use them in `GroupPalette` / `DefaultPalette` |

A new palette works only after the updated `pmm_vip_theme.css` is in the addon.

#### Names and texts (`lang/ru.json`, `lang/en.json`)
All texts of the bridge are in `plugins/PMM_VIPCore/lang/<language>.json` (`ru.json`, `en.json`; add your own
language as a new file, e.g. `de.json`). The files ship with the plugin; on every start the plugin adds the keys a new
version brings, keys that are already there are never changed. Changes are picked up on the fly.
- feature key (`Health`, `ShowDamage`, `SoundDMG`, `Tag` …) — tile name;
- `tag.Disable`, `fov.Disable` and any module keys — labels in the value lists;
- `pmm_vip.*` — panel texts (`pmm_vip.back`, `pmm_vip.enabled`, `pmm_vip.forever` …), `pmm_vip.wm.*` — WeaponsMenu settings;
- `pmm_vip.title` — `!vip` title (`{0}` = group), `pmm_vip.value.<Feature>` — value label (`{0} HP`).
Language = the player's CS2 language; if a key is missing — `en`, then `Paint.Texts` / `Paint.ValueFormat` of the config.

Up to 0.1.4 the texts were in the `PMM_VIPCore` section of
`configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json`. That section is still read (after `lang/`),
so old edits keep working, but it is no longer filled in; move your changes to `lang/*.json`.

Keys:
- feature keys (`Health`, `ShowDamage`, `WeaponsMenu` …) — tile names;
- `fov.*`, `tag.*`, `bhop.*`, `respawn.*`, `vip.*`, `viptest.*`, `buyteamweapon.*`, `weaponsmenu.*` — texts of VIP modules
  in menus and chat (`{0}` = value from the module, `{blue}`, `{red}`, `{green}`, `{orange}`, `{default}` = chat colours);
- `pmm_vip.*` — panel texts, `pmm_vip.value.<Feature>` — value label.

These module keys win over the VIPCore `lang` files. Module texts use the language CSS has at that moment
(the server language in timers, e.g. the WeaponsMenu menu on spawn); set `ModuleLanguage` (`"ru"`) to force one.

#### What is intercepted
| What | How |
|---|---|
| `!vip` | VIPCore 2: `Services.MenuManager.OpenVipMenu(player)`, VIPCore 1: `VipCore.CreateMenu(player)` + `VipCoreApi.CreateMenu(title)` → MenuManager menu |
| Toggle features in `!vip` | switches, change in place, the page does not reset |
| "Bhop: Enabled" in chat | status line / `Notify` toast (panorama only) |
| Fov, Tag | value list right inside `!vip` (panorama only), the new value shows at once |
| Module sub menus through `CreateMenu` | MenuManager menu with "Back" to `!vip` |
| Modules with their own `ChatMenu` / `CenterHtmlMenu` (VIP_Tag, VIP_WeaponsMenu) | CSS hooks `ChatMenu.Open`, `CenterHtmlMenu.Open`, `MenuManager.OpenChatMenu/OpenCenterHtmlMenu` and, as a fallback, `ChatMenuInstance/CenterHtmlMenuInstance.Display`; only for plugins from `InterceptPlugins` |
| Module texts (`GetTranslatedText`) | from `lang/<language>.json` when the key is there |

Menus of other plugins are not touched: a CSS menu is intercepted only when a plugin from `InterceptPlugins` opened it.

#### Settings (`configs/plugins/PMM_VIPCore/PMM_VIPCore.json`)
The file is JSON with comments (`//`): every key is explained right in it. CounterStrikeSharp reads only `.json`,
so the name stays `PMM_VIPCore.json`. No comma after the last item of a block.

| Key | Default | What it does |
|---|---|---|
| `Enabled` | `true` | `false` — VIPCore draws its own menu |
| `ForceMenuType` | `""` | empty — the player's menu from `!menu`; otherwise `PanoramaMenu`, `PanoramaWasdMenu`, `ChatMenu` … |
| `InterceptPlugins` | `["VIPCore","VIP_*"]` | whose CSS menus move to MenuManager (`*` at the end = prefix). Empty — no CSS hooks |
| `MainToggles` | `true` | toggle features as switches |
| `ToggleNotify` | `true` | toggle message as a status line / toast instead of chat |
| `InlineSelectFeatures` | `["Fov","Tag"]` | Selectable features shown as a value list. Only features whose OnSelectItem just opens a menu. **Do not add Respawn** — the bridge calls OnSelectItem every time `!vip` is drawn |
| `InlineSelectMax` | `16` | more items — a normal row with a sub menu |
| `BackButton` | `true` | "Back" in sub menus |
| `ReturnToParentAfterSelect` | `true` | after a pick in a sub menu go back to the parent menu |
| `ExitButton` | `true` | exit button |
| `StripColors` | `true` | remove chat colour codes from titles and rows (not for ChatMenu) |
| `Debug` | `false` | log opens / intercepts |
| `SkipDisabledMenus` | `true` | a module menu where every row is disabled is not opened (WeaponsMenu before the round of its sets) |
| `ModuleLanguage` | `""` | language of module texts from `lang/`: empty = the player's language (VIPCore 2) / current CSS language, or e.g. `ru` |
| `CoreMenuTypes` | `["center","pmm","panorama"]` | VIPCore 2: `MenuType` values of `vip_core.json` for which the bridge shows the VIP menus. Other values (`chat`, `console`) — VIPCore's own menus |
| `AutoCloseSeconds` | `20` | module menus that pop up by themselves (WeaponsMenu on spawn) close after N seconds, countdown in the panel. `0` = off |
| `AutoCloseVipSeconds` | `0` | the same for `!vip` and its sub menus. `0` = off |
| `AutoCloseWarnSeconds` | `5` | the countdown turns red for the last N seconds |
| `WeaponsMenu.Enabled` | `true` | compact panel, settings and `!swm` for VIP_WeaponsMenu. `false` — its menu is a normal module sub menu |
| `WeaponsMenu.Commands` | `["css_swm"]` | commands that open the settings (`css_swm` = `!swm`) |
| `WeaponsMenu.Compact` | `true` | compact panel; `false` — the wide VIP panel (6 rows per page) |
| `WeaponsMenu.Position` | `Left` | position of the compact panel: `Left` / `Center` / `Right` |
| `WeaponsMenu.DefaultAskSave` | `true` | "Ask every time whether to save the set?" for players without own settings |
| `WeaponsMenu.DefaultEveryRound` | `true` | "Open the weapon set menu automatically" for players without own settings |
| `WeaponsMenu.DefaultMode` | `Auto` | `Auto` (from `!menu`), `Mouse`, `Wasd`, `Digits` |
| `WeaponsMenu.Module` / `Feature` | `VIP_WeaponsMenu` / `WeaponsMenu` | module assembly name and its VIPCore feature key |
| `Paint.Enabled` | `true` | own VIP panorama (needs the addon) |
| `Paint.Position` | `Center` | `Left` / `Center` / `Right` |
| `Paint.GroupPalette` | VIPBRONZE/SILVER/GOLD | group → palette from `Paint.Theme.Palettes`. Not listed — by the group name, else `DefaultPalette` |
| `Paint.Icons` | all standard modules | feature key → icon (`ic-*` of `pmm_vip.css`: `health`, `exojump`, `taser`, `zoom_in` …). Missing — `DefaultIcon` |
| `Paint.ValueFormat` | Health → `{0} HP` … | fallback format of the `vip.json` value (main one — `pmm_vip.value.*` in the translations file) |
| `Paint.DateFormat` | `dd.MM.yyyy` | VIP expiry date format |
| `Paint.Texts` | RU | fallback panel texts when the translations file has no key |
| `Paint.OpenDelay` / `InputDelay` | `0.2` | delay of the first draw and of the mouse capture (the chat command frame must not be touched) |
| `Paint.Theme` | dark + gold | panel and group colours, see "Colours" |

## Русский

### PMM_VIPCore 0.1.5 — установка

Мост: меню VIPCore показываются через PanoramaMenuManager. Поддерживаются VIPCore 2.x (partiusfabaa, 2.0.0) и
старый VIPCore 1.x (thesamefabius, v1.3.3 / релиз recompile3) — мост сам определяет, какой стоит.
VIPCore и модули не пересобираются — всё через Harmony, как в PMM_GG1MapChooser.

#### Что нужно
- CounterStrikeSharp 1.0.378 или новее (.NET 10)
- VIPCore (оригинальный `plugins/VIPCore` + `shared/VipCoreApi`) и любые модули `VIP_*`
- MenuManagerCS2 (PanoramaMenuManager) 1.2.03 и его клиентский аддон (режим WeaponsMenu «Панорама цифры» — 1.2.05)

VIP_Fov и VIP_Tag нужны **оригинальные** (из релиза thesamefabius), не из PMM-форка: форковые вызывают
`VipFeatureBase.OnMenuSelect`, которого нет в оригинальном VipCoreApi (`MissingMethodException`). В архив они не входят.

Самосборный форк `PMM_VIPCore_partiusfabaa` (v1.3.3-pmm4) больше не нужен — верните оригинальный `VIPCore.dll`.

#### Сервер
```
plugins/PMM_VIPCore/PMM_VIPCore.dll
plugins/PMM_VIPCore/harmony/0Harmony.dll
plugins/PMM_VIPCore/lang/ru.json, en.json
configs/plugins/PMM_VIPCore/PMM_VIPCore.json
```
- `0Harmony.dll` только в `harmony/`. Если стоит PMM_GG1MapChooser, используется уже загруженная копия.
- Не кладите в папку плагина `MenuManagerApi.dll` и `CounterStrikeSharp.API.dll`.
- После установки — полный перезапуск сервера (`css_plugins reload` не снимает Harmony).
- Проверка: `css_pmm_vip` в консоли сервера. Должно быть `active=True` и в `patched` — `VipCoreApi.CreateMenu`.

#### VIPCore 2.x: `MenuType`
В `configs/plugins/VIPCore/vip_core.json` у VIPCore 2 есть `"MenuType"`. Мост берёт VIP-меню на себя только при
значениях из `CoreMenuTypes` нашего конфига (по умолчанию `center`, `pmm`, `panorama`):
- `"MenuType": "center"` (по умолчанию у VIPCore) — `!vip`, его подменю, Fov / Tag, WeaponsMenu и админ-меню VIP
  открываются как меню PMM_VIPCore (VIP-панорама для игроков с PanoramaMenu);
- `"MenuType": "chat"` / `"console"` — VIPCore показывает свои меню, мост их не трогает.
`css_pmm_vip` показывает `vipcore=… (2.x)`, `menuType=` и `takeover=True/False`.
Модули VIP нужны собранные под VIPCore 2 (VIP_Fov, VIP_Tag, VIP_WeaponsMenu … из того же релиза).

#### Клиент: VIP-панорама (аддон)
Скопируйте содержимое `Content-addonmanager/` в корень того же аддона, где лежит панорама MenuManager
(MultiAddonManager), и пересоберите аддон:
```
panorama/layout/custom_game/pmm_vip.xml
panorama/styles/custom_game/pmm_vip.css
panorama/styles/custom_game/pmm_vip_theme.css
```
После новой версии аддона игрокам нужно перезайти.

Панорама рисуется для игроков, у которых в `!menu` выбрано **PanoramaMenu** (мышь). У остальных типов
(WASD, чат, CS:GO) — обычное меню MenuManager. Исключение — панель WeaponsMenu: она показывается всем (у неё свои режимы
мышь / WASD / цифры, см. ниже). Без аддона на клиенте меню не появится: тогда `Paint.Enabled: false`.

Что на экране:
- шапка: ник, группа VIP (цвет по группе), срок VIP («до 12.11.2026 · 34 дн.» / «навсегда»);
- плитки 3×4 (12 на страницу): иконка, название, значение из `vip.json` («120 HP», «x1.2»), тумблер /
  значение со списком / «▶ ИСП.» / замок «Нет доступа»;
- клик по Fov / Tag — выбор значения плитками 4×4;
- подменю модулей — списком по 8 строк, с «Назад»; у WeaponsMenu своя компактная панель (ниже);
- статус переключения внизу слева, листание внизу по центру, отсчёт автозакрытия внизу справа; страница запоминается для каждого меню;
- собственное меню модуля (WeaponsMenu) закрывается после выбора и не имеет «Назад», как в CSS.

Генератор разметки: `tools/gen_layout.py` (иконки, палитры, число плиток —
id и классы должны совпадать с `Paint.cs`).

#### WeaponsMenu (VIP_WeaponsMenu)
Меню оружия оригинального VIP_WeaponsMenu (daffyy) показывается в компактной панели (6 строк, `WeaponsMenu.Position`)
с шестерёнкой ⚙ в шапке слева от ×. Шестерёнка и команда в чат **`!swm`** открывают настройки
(только для VIP с функцией `WeaponsMenu`):

| Строка | Что делает |
|---|---|
| Спрашивать каждый раз о сохранении набора? | вкл. (по умолчанию) — «Сохранить этот набор?» после каждого выбора; выкл. — больше не спрашивает никогда (и после смены карты, и после перезапуска), набор сохраняется здесь |
| Автоматический вызов меню выбора комплекта оружий | выкл. — меню не всплывает при спавне; комплект берётся через «Выбрать комплект сейчас» |
| Выбрать комплект сейчас | открывает список оружия (жив ли игрок, зона и время закупки, раунд — проверяет сам модуль) |
| Сохранить комплект «X» | сохраняет последний взятый комплект (если в вопросе ответили «Нет» или вопрос не показывался) |
| Удалить сохранённый комплект | при следующем спавне меню оружия снова появится (то же, что `!vwmenu` модуля) |
| Вид меню | открывает список Панорама мышка / Панорама WASD / Панорама цифры, выбор сразу ставит режим. Цифры: при первом выборе — проверка цифр |

Управление панелью:
- мышка — клики (мышь захватывается, пока панель открыта);
- WASD — W/S выбор, E взять, A/D страница, R настройки (в настройках — назад / закрыть); мышь остаётся игре;
- цифры — 1–6 взять, 7 настройки / назад, 8/9 страница, 0 закрыть. Нужен PMM Core 1.2.05: при первом переключении
  открывается проверка цифр MenuManager (строка биндов — в консоли), режим включается после её прохождения.

Настройки игроков хранятся по SteamID в `plugins/PMM_VIPCore/weaponsmenu_players.json`. Значения для новых игроков —
`WeaponsMenu.DefaultAskSave`, `DefaultEveryRound`, `DefaultMode` (`Auto` — по `!menu` игрока).
Панели нужны файлы аддона 0.1.4 (`pmm_vip.xml`, `pmm_vip.css`, `pmm_vip_theme.css`): пересоберите аддон.

#### Цвета (`Paint.Theme`)
Панорама не принимает цвета с сервера, поэтому цвета живут в отдельном файле `pmm_vip_theme.css`:
1. меняете цвета в `Paint.Theme` конфига (`#rrggbb` или `#rrggbbaa`, `aa` — прозрачность);
2. плагин при загрузке (или `css_pmm_vip css`) пишет `plugins/PMM_VIPCore/pmm_vip_theme.css`;
3. копируете его в `panorama/styles/custom_game/` аддона и пересобираете аддон.

| Ключ | Что |
|---|---|
| `PanelTop`, `PanelBottom`, `PanelBorder`, `Separator` | фон панели (градиент сверху вниз), рамка, линия под шапкой |
| `Nick`, `Muted` | ник; срок VIP и иконка часов |
| `CloseBg`, `CloseHover`, `CloseText` | кнопка × |
| `TileBg`, `TileBorder`, `TileHover`, `TileName`, `TileDesc` | плитки, кнопки выбора, строки подменю |
| `IconBox`, `Icon` | квадрат и иконка у выключенной функции |
| `SwitchOff`, `KnobOff`, `KnobOn` | ползунок выключен / кружок выкл. / кружок вкл. (фон включённого — цвет группы) |
| `ButtonBg`, `ButtonHover`, `ButtonText`, `DotOff` | «Назад», стрелки страниц, точки страниц |
| `ToastOk*`, `ToastWarn*` | строка статуса (Bg, Border, Text, Icon) |
| `Palettes` | цвета групп: имя → `Accent` (главный: включённый ползунок, бейдж, группа, рамки), `Light`, `Dark`, `OnAccent` (текст на бейдже). Можно добавлять свои имена и указывать их в `GroupPalette` / `DefaultPalette` |

Новая палитра работает только после того, как обновлённый `pmm_vip_theme.css` попал в аддон.

#### Названия и тексты (`lang/ru.json`, `lang/en.json`)
Все тексты моста лежат в `plugins/PMM_VIPCore/lang/<язык>.json` (`ru.json`, `en.json`; свой язык — новым файлом,
например `de.json`). Файлы идут вместе с плагином; при каждом запуске плагин дописывает ключи новой версии,
уже существующие ключи не меняются. Изменения подхватываются на лету.
- ключ функции (`Health`, `ShowDamage`, `SoundDMG`, `Tag` …) — название плитки;
- `tag.Disable`, `fov.Disable` и любые ключи модулей — подписи в списках выбора;
- `pmm_vip.*` — тексты панели (`pmm_vip.back`, `pmm_vip.enabled`, `pmm_vip.forever` …), `pmm_vip.wm.*` — настройки WeaponsMenu;
- `pmm_vip.title` — заголовок `!vip` (`{0}` = группа), `pmm_vip.value.<Функция>` — подпись значения (`{0} HP`).
Язык — язык игрока в CS2 (`!lang`), если ключа нет — `en`, затем `Paint.Texts` / `Paint.ValueFormat` конфига.

До 0.1.4 тексты лежали в разделе `PMM_VIPCore` файла
`configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json`. Этот раздел по-прежнему читается (после `lang/`),
старые правки работают, но больше не дополняется; перенесите свои правки в `lang/*.json`.

Ключи:
- ключи функций (`Health`, `ShowDamage`, `WeaponsMenu` …) — названия плиток;
- `fov.*`, `tag.*`, `bhop.*`, `respawn.*`, `vip.*`, `viptest.*`, `buyteamweapon.*`, `weaponsmenu.*` — тексты VIP-модулей
  в меню и в чате (`{0}` — значение от модуля, `{blue}`, `{red}`, `{green}`, `{orange}`, `{default}` — цвета чата);
- `pmm_vip.*` — тексты панели, `pmm_vip.value.<Функция>` — подпись значения.

Ключи модулей важнее файлов `lang` VIPCore. Тексты модулей берутся на языке, который CSS использует в этот момент
(в таймерах — язык сервера, например меню WeaponsMenu при спавне); `ModuleLanguage` (`"ru"`) задаёт язык принудительно.

#### Что перехватывается
| Что | Как |
|---|---|
| `!vip` | VIPCore 2: `Services.MenuManager.OpenVipMenu(player)`, VIPCore 1: `VipCore.CreateMenu(player)` + `VipCoreApi.CreateMenu(title)` → меню MenuManager |
| Toggle-фичи в `!vip` | тумблеры `AddToggle` (`[Вкл] Bhop`), переключаются на месте, страница не сбрасывается |
| «Bhop: Включено» в чат | тост `Notify` (только панорама) |
| Fov, Tag | выпадающий список `AddSelect` прямо в `!vip` (только панорама), новое значение видно сразу |
| Подменю модулей через `CreateMenu` (Fov в чат-режиме и т.п.) | меню MenuManager с кнопкой «Назад» в `!vip` |
| Модули с собственным `ChatMenu` / `CenterHtmlMenu` (VIP_Tag, VIP_WeaponsMenu) | хуки `ChatMenu.Open`, `CenterHtmlMenu.Open`, `MenuManager.OpenChatMenu/OpenCenterHtmlMenu` CSS и запасной `ChatMenuInstance/CenterHtmlMenuInstance.Display`; только для плагинов из `InterceptPlugins` |
| Тексты модулей (`GetTranslatedText`) | из `lang/<язык>.json`, если там есть ключ |

Меню других плагинов не трогаются: CSS-меню перехватывается только если его открыл плагин из `InterceptPlugins`.

#### Настройки (`configs/plugins/PMM_VIPCore/PMM_VIPCore.json`)
Файл — JSON с комментариями (`//`): каждый ключ описан прямо в нём. CounterStrikeSharp читает только `.json`,
поэтому имя остаётся `PMM_VIPCore.json`. После последнего элемента блока запятую не ставить.

| Ключ | По умолчанию | Что делает |
|---|---|---|
| `Enabled` | `true` | `false` — VIPCore рисует своё меню |
| `ForceMenuType` | `""` | пусто — меню игрока из `!menu`; иначе `PanoramaMenu`, `PanoramaWasdMenu`, `ChatMenu` … |
| `InterceptPlugins` | `["VIPCore","VIP_*"]` | чьи CSS-меню переносить в MenuManager (`*` в конце — префикс). Пусто — хуки CSS не ставятся |
| `MainToggles` | `true` | toggle-фичи как тумблеры |
| `ToggleNotify` | `true` | сообщение о переключении — тостом, а не в чат |
| `InlineSelectFeatures` | `["Fov","Tag"]` | Selectable-фичи, которые показываются выпадающим списком. Только те, чей OnSelectItem просто открывает меню. **Не добавляйте Respawn** — мост вызывает OnSelectItem при каждой отрисовке `!vip` |
| `InlineSelectMax` | `16` | больше пунктов — обычная строка с подменю |
| `BackButton` | `true` | «Назад» в подменю |
| `ReturnToParentAfterSelect` | `true` | после выбора в подменю вернуться в родительское меню |
| `ExitButton` | `true` | кнопка выхода |
| `StripColors` | `true` | убрать цветовые коды чата из заголовков и строк (кроме ChatMenu) |
| `Debug` | `false` | лог открытий / перехватов |
| `SkipDisabledMenus` | `true` | меню модуля, где все строки недоступны, не открывается (WeaponsMenu до раунда наборов) |
| `ModuleLanguage` | `""` | язык текстов модулей из `lang/`: пусто — язык игрока (VIPCore 2) / текущий язык CSS, или например `ru` |
| `CoreMenuTypes` | `["center","pmm","panorama"]` | VIPCore 2: значения `MenuType` из `vip_core.json`, при которых мост показывает VIP-меню. Другие (`chat`, `console`) — свои меню VIPCore |
| `AutoCloseSeconds` | `20` | всплывающие меню модулей (WeaponsMenu при спавне) закрываются через N секунд, отсчёт в панели. `0` — выкл. |
| `AutoCloseVipSeconds` | `0` | то же для `!vip` и его подменю. `0` — выкл. |
| `AutoCloseWarnSeconds` | `5` | последние N секунд отсчёт красный |
| `WeaponsMenu.Enabled` | `true` | компактная панель, настройки и `!swm` для VIP_WeaponsMenu. `false` — его меню как обычное подменю модуля |
| `WeaponsMenu.Commands` | `["css_swm"]` | команды, открывающие настройки (`css_swm` = `!swm`) |
| `WeaponsMenu.Compact` | `true` | компактная панель; `false` — широкая VIP-панель (6 строк на страницу) |
| `WeaponsMenu.Position` | `Left` | где компактная панель: `Left` / `Center` / `Right` |
| `WeaponsMenu.DefaultAskSave` | `true` | «Спрашивать каждый раз о сохранении набора?» для игроков без своих настроек |
| `WeaponsMenu.DefaultEveryRound` | `true` | «Автоматический вызов меню выбора комплекта оружий» для игроков без своих настроек |
| `WeaponsMenu.DefaultMode` | `Auto` | `Auto` (по `!menu`), `Mouse`, `Wasd`, `Digits` |
| `WeaponsMenu.Module` / `Feature` | `VIP_WeaponsMenu` / `WeaponsMenu` | имя сборки модуля и ключ его функции в VIPCore |
| `Paint.Enabled` | `true` | своя VIP-панорама (нужен аддон) |
| `Paint.Position` | `Center` | `Left` / `Center` / `Right` |
| `Paint.GroupPalette` | VIPBRONZE/SILVER/GOLD | группа → палитра из `Paint.Theme.Palettes`. Нет в списке — по названию группы, иначе `DefaultPalette` |
| `Paint.Icons` | для всех стандартных модулей | ключ фичи → иконка (`ic-*` из `pmm_vip.css`: `health`, `exojump`, `taser`, `zoom_in` …). Нет — `DefaultIcon` |
| `Paint.ValueFormat` | Health → `{0} HP` … | запасной формат значения из `vip.json` (основной — `pmm_vip.value.*` в файле переводов) |
| `Paint.DateFormat` | `dd.MM.yyyy` | формат даты окончания VIP |
| `Paint.Texts` | RU | запасные надписи панели, если в файле переводов нет ключа |
| `Paint.OpenDelay` / `InputDelay` | `0.2` | задержка первой отрисовки и захвата мыши (кадр команды чата трогать нельзя) |
| `Paint.Theme` | тёмный + золото | цвета панели и групп, см. «Цвета» |
