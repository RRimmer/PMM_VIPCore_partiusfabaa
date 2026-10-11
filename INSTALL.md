# PMM_VIPCore — install

[English](#english) · [Русский](#русский)

## English

### PMM_VIPCore 0.1.3 — install

Bridge: menus of the original VIPCore (thesamefabius, v1.3.3 / release recompile3) are shown through PanoramaMenuManager.
VIPCore and its modules are not rebuilt — everything goes through Harmony, as in PMM_GG1MapChooser.

#### Requirements
- CounterStrikeSharp 1.0.376 or newer (.NET 10)
- VIPCore (original `plugins/VIPCore` + `shared/VipCoreApi`) and any `VIP_*` modules
- MenuManagerCS2 (PanoramaMenuManager) 1.2.03 and its client addon

VIP_Fov and VIP_Tag must be the **original** builds (thesamefabius release), not the ones from the PMM fork: the fork
builds call `VipFeatureBase.OnMenuSelect`, which the original VipCoreApi does not have (`MissingMethodException`).
They are not part of this archive.

The self-built fork `PMM_VIPCore_partiusfabaa` (v1.3.3-pmm4) is not needed any more — put the original `VIPCore.dll` back.

#### Server
```
plugins/PMM_VIPCore/PMM_VIPCore.dll
plugins/PMM_VIPCore/harmony/0Harmony.dll
configs/plugins/PMM_VIPCore/PMM_VIPCore.json
```
- `0Harmony.dll` only in `harmony/`. If PMM_GG1MapChooser is installed, the copy it already loaded is reused.
- Do not put `MenuManagerApi.dll` or `CounterStrikeSharp.API.dll` into the plugin folder.
- Restart the server completely after an install or update (`css_plugins reload` does not unload Harmony).
- Check: `css_pmm_vip` in the server console. Expect `active=True`, `VipCoreApi.CreateMenu` in `patched`,
  `painter=True` and 2 languages in `translations`.

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
get the standard MenuManager menu. Without the addon on the client nothing appears: set `Paint.Enabled: false` then.

On screen:
- header: nickname, VIP group (colour per group), VIP expiry ("until 12.11.2026 · 34 d." / "forever");
- 3×4 tiles (12 per page): icon, name, the value from `vip.json` ("120 HP", "x1.2"), a switch /
  a value with a list / "▶ USE" / a lock "No access";
- a click on Fov / Tag opens a 4×4 value picker;
- module sub menus (WeaponsMenu etc.) are an 8-row list with "Back";
- status line bottom left, pages bottom centre, auto-close countdown bottom right; the page is remembered per menu;
- a module's own menu (WeaponsMenu) closes after a pick and has no "Back", as in CSS.

Layout generator: `tools/gen_layout.py` (icons, palettes, tile count — ids and classes must match `Paint.cs`).

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

#### Names and texts (MenuManager_Modules_Translation.json)
On the first start the plugin adds a `PMM_VIPCore` section (ru / en) to
`configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json` — the same file MenuManager uses.
Missing keys are added on every start, existing ones are never changed; changes are picked up on the fly.
- feature key (`Health`, `ShowDamage`, `SoundDMG`, `Tag` …) — tile name;
- `tag.Disable`, `fov.Disable` and any module keys — labels in the value lists;
- `pmm_vip.*` — panel texts (`pmm_vip.back`, `pmm_vip.enabled`, `pmm_vip.forever` …);
- `pmm_vip.title` — `!vip` title (`{0}` = group), `pmm_vip.value.<Feature>` — value label (`{0} HP`).
Language = the player's CS2 language; if a key is missing — `en`, then `Paint.Texts` / `Paint.ValueFormat` of the config.

#### Translations to add by hand (only if they did not appear automatically)
On every start the plugin adds the missing keys of the `PMM_VIPCore` section to
`configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json` by itself (keys that are already there are not changed).
**Only if** the section or some keys did not appear (file is read-only, the JSON has an error, the server log shows
`Translations: could not add …`), add this block to the root object of the file by hand
(put a comma after the previous section):

```json
"PMM_VIPCore": {
    "ru": {
      "pmm_vip.brand": "VIP",
      "pmm_vip.forever": "навсегда",
      "pmm_vip.until": "до",
      "pmm_vip.days_left": "· {0} дн.",
      "pmm_vip.enabled": "Включено",
      "pmm_vip.disabled": "Выключено",
      "pmm_vip.no_access": "Нет доступа",
      "pmm_vip.use": "▶ ИСП.",
      "pmm_vip.back": "‹ НАЗАД",
      "pmm_vip.pick_hint": "Выбери значение",
      "pmm_vip.page": "{0} / {1}",
      "pmm_vip.title": "VIP меню — {0}",
      "pmm_vip.closes_in": "Закроется через {0} с",
      "Antiflash": "Анти-флеш",
      "Armor": "Броня",
      "ArmorRegen": "Реген брони",
      "Bhop": "Банихоп",
      "BuyTeamWeapon": "Оружие другой команды",
      "CustomDefaultAmmo": "Патроны",
      "DamageMultiplier": "Множитель урона",
      "DecoyTp": "Телепорт-граната",
      "DefuseKit": "Набор сапёра",
      "Defuser": "Набор сапёра",
      "FastReload": "Быстрая перезарядка",
      "Fov": "Поле зрения (FOV)",
      "Gravity": "Гравитация",
      "Grenades": "Гранаты",
      "Health": "Здоровье",
      "HealthRegen": "Реген здоровья",
      "Healthshot": "Медшприц",
      "InfiniteAmmo": "Бесконечные патроны",
      "Items": "Предметы",
      "Jumps": "Доп. прыжки",
      "Killscreen": "Эффект убийства",
      "Money": "Деньги",
      "NoFallDamage": "Без урона от падения",
      "RainbowModel": "Радужная модель",
      "ResetDeaths": "Сброс смертей",
      "Respawn": "Возрождение",
      "ShowDamage": "Показ урона",
      "SmokeColor": "Цветной дым",
      "SoundDMG": "Звук попадания",
      "Speed": "Скорость",
      "Tag": "Клан-тег",
      "TeammatesHeal": "Лечение союзников",
      "Vampirism": "Вампиризм",
      "WeaponsMenu": "Меню оружия",
      "Zeus": "Zeus",
      "endurance": "Выносливость",
      "exp_multiplier": "Множитель опыта",
      "fastdefuse": "Быстрое разминирование",
      "fastplant": "Быстрая установка",
      "flags": "Флаги",
      "fov.Disable": "Выкл.",
      "tag.Disable": "Без тега",
      "fov.On": "Вы установили {blue}FOV {default}на {red}{0}",
      "fov.Off": "Вы выключили {blue}FOV",
      "tag.On": "Вы установили {blue}тег {default}на {red}{0}",
      "tag.Off": "Вы выключили {blue}тег",
      "bhop.Activated": "Банихоп активирован!",
      "bhop.TimeToActivation": "Банихоп будет активен через {red}{0}{default} сек.",
      "respawn.Limit": "Лимит возрождений исчерпан!",
      "respawn.InTeam": "Вы должны быть в команде!",
      "respawn.IsAlive": "Вы должны быть мертвы!",
      "respawn.Success": "Вы успешно возродились!",
      "vip.NoAccess": "У вас нет доступа к этой команде!",
      "vip.AlreadyVipPrivileges": "У вас уже есть VIP-привилегия.",
      "vip.ResetDeaths": "Ваши смерти сброшены!",
      "viptest.YouCanNoLongerTakeTheVip": "Вы больше не можете взять VIP-Test",
      "viptest.RetakenThrough": "Взять VIP-Test повторно можно через: {0}",
      "viptest.SuccessfullyPassed": "Вы взяли VIP-Test! Заканчивается через: {0}",
      "buyteamweapon.round_start": "Можно купить оружие другой команды: {green}{0}",
      "buyteamweapon.in_buy_zone": "Вы должны быть в зоне закупки!",
      "buyteamweapon.no_money": "Недостаточно денег!",
      "weaponsmenu.title": "Выберите набор оружия",
      "weaponsmenu.fromround": "[С {0} раунда]",
      "weaponsmenu.wanttosave": "Сохранить этот набор?",
      "weaponsmenu.wanttosave.yes": "Да",
      "weaponsmenu.wanttosave.no": "Нет",
      "weaponsmenu.saved": "Набор сохранён, вы будете получать его автоматически!",
      "weaponsmenu.notsaved": "Набор не сохранён, в следующем раунде можно выбрать снова.",
      "weaponsmenu.resetinfo.reset": "Набор сброшен, выберите новый в следующем раунде!",
      "weaponsmenu.resetinfo.info": "Хотите сменить набор? Команда {orange}!vwmenu{default} — выбор в следующем раунде.",
      "pmm_vip.value.Health": "{0} HP",
      "pmm_vip.value.Armor": "{0} брони",
      "pmm_vip.value.Speed": "x{0}",
      "pmm_vip.value.Gravity": "x{0}",
      "pmm_vip.value.Money": "${0}",
      "pmm_vip.value.Jumps": "+{0}",
      "pmm_vip.value.Vampirism": "{0}%",
      "pmm_vip.value.DamageMultiplier": "x{0}",
      "pmm_vip.value.HealthRegen": "+{0} HP",
      "pmm_vip.value.ArmorRegen": "+{0}",
      "pmm_vip.value.Healthshot": "{0} шт.",
      "pmm_vip.value.exp_multiplier": "x{0}"
    },
    "en": {
      "pmm_vip.brand": "VIP",
      "pmm_vip.forever": "forever",
      "pmm_vip.until": "until",
      "pmm_vip.days_left": "· {0} d.",
      "pmm_vip.enabled": "Enabled",
      "pmm_vip.disabled": "Disabled",
      "pmm_vip.no_access": "No access",
      "pmm_vip.use": "▶ USE",
      "pmm_vip.back": "‹ BACK",
      "pmm_vip.pick_hint": "Pick a value",
      "pmm_vip.page": "{0} / {1}",
      "pmm_vip.title": "VIP menu — {0}",
      "pmm_vip.closes_in": "Closes in {0}s",
      "Antiflash": "Anti-Flash",
      "Armor": "Armor",
      "ArmorRegen": "Armor Regen",
      "Bhop": "BunnyHop",
      "BuyTeamWeapon": "Enemy Team Weapons",
      "CustomDefaultAmmo": "Ammo",
      "DamageMultiplier": "Damage Multiplier",
      "DecoyTp": "Teleport Grenade",
      "DefuseKit": "Defuse Kit",
      "Defuser": "Defuse Kit",
      "FastReload": "Fast Reload",
      "Fov": "Field of View",
      "Gravity": "Gravity",
      "Grenades": "Grenades",
      "Health": "Health",
      "HealthRegen": "Health Regen",
      "Healthshot": "Healthshot",
      "InfiniteAmmo": "Infinite Ammo",
      "Items": "Items",
      "Jumps": "Extra Jumps",
      "Killscreen": "Kill Effect",
      "Money": "Money",
      "NoFallDamage": "No Fall Damage",
      "RainbowModel": "Rainbow Model",
      "ResetDeaths": "Reset Deaths",
      "Respawn": "Respawn",
      "ShowDamage": "Show Damage",
      "SmokeColor": "Smoke Color",
      "SoundDMG": "Hit Sound",
      "Speed": "Speed",
      "Tag": "Clan Tag",
      "TeammatesHeal": "Heal Teammates",
      "Vampirism": "Vampirism",
      "WeaponsMenu": "Weapons Menu",
      "Zeus": "Zeus",
      "endurance": "Endurance",
      "exp_multiplier": "XP Multiplier",
      "fastdefuse": "Fast Defuse",
      "fastplant": "Fast Plant",
      "flags": "Flags",
      "fov.Disable": "Off",
      "tag.Disable": "No tag",
      "fov.On": "You have set {blue}FOV {default}to {red}{0}",
      "fov.Off": "You've disabled the {blue}FOV",
      "tag.On": "You have set {blue}Tag {default}to {red}{0}",
      "tag.Off": "You've disabled the {blue}Tag",
      "bhop.Activated": "BunnyHop activated!",
      "bhop.TimeToActivation": "BunnyHop will be active in {red}{0}{default}s.",
      "respawn.Limit": "Respawn limit reached!",
      "respawn.InTeam": "You must be in a team!",
      "respawn.IsAlive": "You must be dead!",
      "respawn.Success": "Respawn successful!",
      "vip.NoAccess": "You don't have access to this command!",
      "vip.AlreadyVipPrivileges": "You already have VIP privileges.",
      "vip.ResetDeaths": "Your deaths are reset!",
      "viptest.YouCanNoLongerTakeTheVip": "You can no longer take the VIP Test.",
      "viptest.RetakenThrough": "The VIP test can be retaken in: {0}.",
      "viptest.SuccessfullyPassed": "You have taken the VIP test! Ends in: {0}.",
      "buyteamweapon.round_start": "You can buy the other team's weapons: {green}{0}",
      "buyteamweapon.in_buy_zone": "You must be in the buy zone!",
      "buyteamweapon.no_money": "Not enough money!",
      "weaponsmenu.title": "Select weapons package",
      "weaponsmenu.fromround": "[From round {0}]",
      "weaponsmenu.wanttosave": "Do you want to save this set?",
      "weaponsmenu.wanttosave.yes": "Yes",
      "weaponsmenu.wanttosave.no": "No",
      "weaponsmenu.saved": "Your free set has been saved, you will receive it when possible!",
      "weaponsmenu.notsaved": "Your free set was not saved, you can choose a new one next round.",
      "weaponsmenu.resetinfo.reset": "Your free set has been reset, choose a new one next round!",
      "weaponsmenu.resetinfo.info": "Want another set? Use {orange}!vwmenu{default} to choose a new one next round.",
      "pmm_vip.value.Health": "{0} HP",
      "pmm_vip.value.Armor": "{0} armor",
      "pmm_vip.value.Speed": "x{0}",
      "pmm_vip.value.Gravity": "x{0}",
      "pmm_vip.value.Money": "${0}",
      "pmm_vip.value.Jumps": "+{0}",
      "pmm_vip.value.Vampirism": "{0}%",
      "pmm_vip.value.DamageMultiplier": "x{0}",
      "pmm_vip.value.HealthRegen": "+{0} HP",
      "pmm_vip.value.ArmorRegen": "+{0}",
      "pmm_vip.value.Healthshot": "{0} pcs",
      "pmm_vip.value.exp_multiplier": "x{0}"
    }
  }
```

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
| `!vip` | `VipCore.CreateMenu(player)` + `VipCoreApi.CreateMenu(title)` → MenuManager menu |
| Toggle features in `!vip` | switches, change in place, the page does not reset |
| "Bhop: Enabled" in chat | status line / `Notify` toast (panorama only) |
| Fov, Tag | value list right inside `!vip` (panorama only), the new value shows at once |
| Module sub menus through `CreateMenu` | MenuManager menu with "Back" to `!vip` |
| Modules with their own `ChatMenu` / `CenterHtmlMenu` (VIP_Tag, VIP_WeaponsMenu) | CSS hooks `ChatMenu.Open`, `CenterHtmlMenu.Open`, `MenuManager.OpenChatMenu/OpenCenterHtmlMenu` and, as a fallback, `ChatMenuInstance/CenterHtmlMenuInstance.Display`; only for plugins from `InterceptPlugins` |
| Module texts (`GetTranslatedText`) | from `MenuManager_Modules_Translation.json` when the key is there |

Menus of other plugins are not touched: a CSS menu is intercepted only when a plugin from `InterceptPlugins` opened it.

#### Settings (`configs/plugins/PMM_VIPCore/PMM_VIPCore.json`)
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
| `ModuleLanguage` | `""` | language of module texts from the translations file: empty = current CSS language, or e.g. `ru` |
| `AutoCloseSeconds` | `20` | module menus that pop up by themselves (WeaponsMenu on spawn) close after N seconds, countdown in the panel. `0` = off |
| `AutoCloseVipSeconds` | `0` | the same for `!vip` and its sub menus. `0` = off |
| `AutoCloseWarnSeconds` | `5` | the countdown turns red for the last N seconds |
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

### PMM_VIPCore 0.1.3 — установка

Мост: меню оригинального VIPCore (thesamefabius, v1.3.3 / релиз recompile3) показываются через PanoramaMenuManager.
VIPCore и модули не пересобираются — всё через Harmony, как в PMM_GG1MapChooser.

#### Что нужно
- CounterStrikeSharp 1.0.376 или новее (.NET 10)
- VIPCore (оригинальный `plugins/VIPCore` + `shared/VipCoreApi`) и любые модули `VIP_*`
- MenuManagerCS2 (PanoramaMenuManager) 1.2.03 и его клиентский аддон

VIP_Fov и VIP_Tag нужны **оригинальные** (из релиза thesamefabius), не из PMM-форка: форковые вызывают
`VipFeatureBase.OnMenuSelect`, которого нет в оригинальном VipCoreApi (`MissingMethodException`). В архив они не входят.

Самосборный форк `PMM_VIPCore_partiusfabaa` (v1.3.3-pmm4) больше не нужен — верните оригинальный `VIPCore.dll`.

#### Сервер
```
plugins/PMM_VIPCore/PMM_VIPCore.dll
plugins/PMM_VIPCore/harmony/0Harmony.dll
configs/plugins/PMM_VIPCore/PMM_VIPCore.json
```
- `0Harmony.dll` только в `harmony/`. Если стоит PMM_GG1MapChooser, используется уже загруженная копия.
- Не кладите в папку плагина `MenuManagerApi.dll` и `CounterStrikeSharp.API.dll`.
- После установки — полный перезапуск сервера (`css_plugins reload` не снимает Harmony).
- Проверка: `css_pmm_vip` в консоли сервера. Должно быть `active=True` и в `patched` — `VipCoreApi.CreateMenu`.

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
(WASD, чат, CS:GO) — обычное меню MenuManager. Без аддона на клиенте меню не появится: тогда `Paint.Enabled: false`.

Что на экране:
- шапка: ник, группа VIP (цвет по группе), срок VIP («до 12.11.2026 · 34 дн.» / «навсегда»);
- плитки 3×4 (12 на страницу): иконка, название, значение из `vip.json` («120 HP», «x1.2»), тумблер /
  значение со списком / «▶ ИСП.» / замок «Нет доступа»;
- клик по Fov / Tag — выбор значения плитками 4×4;
- подменю модулей (WeaponsMenu и т.п.) — списком по 8 строк, с «Назад»;
- статус переключения внизу слева, листание внизу по центру, отсчёт автозакрытия внизу справа; страница запоминается для каждого меню;
- собственное меню модуля (WeaponsMenu) закрывается после выбора и не имеет «Назад», как в CSS.

Генератор разметки: `tools/gen_layout.py` (иконки, палитры, число плиток —
id и классы должны совпадать с `Paint.cs`).

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

#### Названия и тексты (MenuManager_Modules_Translation.json)
При первом запуске плагин добавляет раздел `PMM_VIPCore` (ru / en) в
`configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json` — тот же файл, что у MenuManager.
Недостающие ключи дописываются при каждом запуске, существующие не трогаются; изменения подхватываются на лету.
- ключ функции (`Health`, `ShowDamage`, `SoundDMG`, `Tag` …) — название плитки;
- `tag.Disable`, `fov.Disable` и любые ключи модулей — подписи в списках выбора;
- `pmm_vip.*` — тексты панели (`pmm_vip.back`, `pmm_vip.enabled`, `pmm_vip.forever` …);
- `pmm_vip.title` — заголовок `!vip` (`{0}` = группа), `pmm_vip.value.<Функция>` — подпись значения (`{0} HP`).
Язык — язык игрока в CS2 (`!lang`), если ключа нет — `en`, затем `Paint.Texts` / `Paint.ValueFormat` конфига.

#### Переводы, которые нужно дописать вручную (только если не появились сами)
При каждом запуске плагин сам дописывает недостающие ключи раздела `PMM_VIPCore` в
`configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json` (уже существующие ключи не меняются).
**Только если** раздел или часть ключей не появились (файл только для чтения, ошибка в JSON, в логе сервера
`Translations: could not add …`), добавьте этот блок в корневой объект файла вручную
(не забудьте запятую после предыдущего раздела):

```json
"PMM_VIPCore": {
    "ru": {
      "pmm_vip.brand": "VIP",
      "pmm_vip.forever": "навсегда",
      "pmm_vip.until": "до",
      "pmm_vip.days_left": "· {0} дн.",
      "pmm_vip.enabled": "Включено",
      "pmm_vip.disabled": "Выключено",
      "pmm_vip.no_access": "Нет доступа",
      "pmm_vip.use": "▶ ИСП.",
      "pmm_vip.back": "‹ НАЗАД",
      "pmm_vip.pick_hint": "Выбери значение",
      "pmm_vip.page": "{0} / {1}",
      "pmm_vip.title": "VIP меню — {0}",
      "pmm_vip.closes_in": "Закроется через {0} с",
      "Antiflash": "Анти-флеш",
      "Armor": "Броня",
      "ArmorRegen": "Реген брони",
      "Bhop": "Банихоп",
      "BuyTeamWeapon": "Оружие другой команды",
      "CustomDefaultAmmo": "Патроны",
      "DamageMultiplier": "Множитель урона",
      "DecoyTp": "Телепорт-граната",
      "DefuseKit": "Набор сапёра",
      "Defuser": "Набор сапёра",
      "FastReload": "Быстрая перезарядка",
      "Fov": "Поле зрения (FOV)",
      "Gravity": "Гравитация",
      "Grenades": "Гранаты",
      "Health": "Здоровье",
      "HealthRegen": "Реген здоровья",
      "Healthshot": "Медшприц",
      "InfiniteAmmo": "Бесконечные патроны",
      "Items": "Предметы",
      "Jumps": "Доп. прыжки",
      "Killscreen": "Эффект убийства",
      "Money": "Деньги",
      "NoFallDamage": "Без урона от падения",
      "RainbowModel": "Радужная модель",
      "ResetDeaths": "Сброс смертей",
      "Respawn": "Возрождение",
      "ShowDamage": "Показ урона",
      "SmokeColor": "Цветной дым",
      "SoundDMG": "Звук попадания",
      "Speed": "Скорость",
      "Tag": "Клан-тег",
      "TeammatesHeal": "Лечение союзников",
      "Vampirism": "Вампиризм",
      "WeaponsMenu": "Меню оружия",
      "Zeus": "Zeus",
      "endurance": "Выносливость",
      "exp_multiplier": "Множитель опыта",
      "fastdefuse": "Быстрое разминирование",
      "fastplant": "Быстрая установка",
      "flags": "Флаги",
      "fov.Disable": "Выкл.",
      "tag.Disable": "Без тега",
      "fov.On": "Вы установили {blue}FOV {default}на {red}{0}",
      "fov.Off": "Вы выключили {blue}FOV",
      "tag.On": "Вы установили {blue}тег {default}на {red}{0}",
      "tag.Off": "Вы выключили {blue}тег",
      "bhop.Activated": "Банихоп активирован!",
      "bhop.TimeToActivation": "Банихоп будет активен через {red}{0}{default} сек.",
      "respawn.Limit": "Лимит возрождений исчерпан!",
      "respawn.InTeam": "Вы должны быть в команде!",
      "respawn.IsAlive": "Вы должны быть мертвы!",
      "respawn.Success": "Вы успешно возродились!",
      "vip.NoAccess": "У вас нет доступа к этой команде!",
      "vip.AlreadyVipPrivileges": "У вас уже есть VIP-привилегия.",
      "vip.ResetDeaths": "Ваши смерти сброшены!",
      "viptest.YouCanNoLongerTakeTheVip": "Вы больше не можете взять VIP-Test",
      "viptest.RetakenThrough": "Взять VIP-Test повторно можно через: {0}",
      "viptest.SuccessfullyPassed": "Вы взяли VIP-Test! Заканчивается через: {0}",
      "buyteamweapon.round_start": "Можно купить оружие другой команды: {green}{0}",
      "buyteamweapon.in_buy_zone": "Вы должны быть в зоне закупки!",
      "buyteamweapon.no_money": "Недостаточно денег!",
      "weaponsmenu.title": "Выберите набор оружия",
      "weaponsmenu.fromround": "[С {0} раунда]",
      "weaponsmenu.wanttosave": "Сохранить этот набор?",
      "weaponsmenu.wanttosave.yes": "Да",
      "weaponsmenu.wanttosave.no": "Нет",
      "weaponsmenu.saved": "Набор сохранён, вы будете получать его автоматически!",
      "weaponsmenu.notsaved": "Набор не сохранён, в следующем раунде можно выбрать снова.",
      "weaponsmenu.resetinfo.reset": "Набор сброшен, выберите новый в следующем раунде!",
      "weaponsmenu.resetinfo.info": "Хотите сменить набор? Команда {orange}!vwmenu{default} — выбор в следующем раунде.",
      "pmm_vip.value.Health": "{0} HP",
      "pmm_vip.value.Armor": "{0} брони",
      "pmm_vip.value.Speed": "x{0}",
      "pmm_vip.value.Gravity": "x{0}",
      "pmm_vip.value.Money": "${0}",
      "pmm_vip.value.Jumps": "+{0}",
      "pmm_vip.value.Vampirism": "{0}%",
      "pmm_vip.value.DamageMultiplier": "x{0}",
      "pmm_vip.value.HealthRegen": "+{0} HP",
      "pmm_vip.value.ArmorRegen": "+{0}",
      "pmm_vip.value.Healthshot": "{0} шт.",
      "pmm_vip.value.exp_multiplier": "x{0}"
    },
    "en": {
      "pmm_vip.brand": "VIP",
      "pmm_vip.forever": "forever",
      "pmm_vip.until": "until",
      "pmm_vip.days_left": "· {0} d.",
      "pmm_vip.enabled": "Enabled",
      "pmm_vip.disabled": "Disabled",
      "pmm_vip.no_access": "No access",
      "pmm_vip.use": "▶ USE",
      "pmm_vip.back": "‹ BACK",
      "pmm_vip.pick_hint": "Pick a value",
      "pmm_vip.page": "{0} / {1}",
      "pmm_vip.title": "VIP menu — {0}",
      "pmm_vip.closes_in": "Closes in {0}s",
      "Antiflash": "Anti-Flash",
      "Armor": "Armor",
      "ArmorRegen": "Armor Regen",
      "Bhop": "BunnyHop",
      "BuyTeamWeapon": "Enemy Team Weapons",
      "CustomDefaultAmmo": "Ammo",
      "DamageMultiplier": "Damage Multiplier",
      "DecoyTp": "Teleport Grenade",
      "DefuseKit": "Defuse Kit",
      "Defuser": "Defuse Kit",
      "FastReload": "Fast Reload",
      "Fov": "Field of View",
      "Gravity": "Gravity",
      "Grenades": "Grenades",
      "Health": "Health",
      "HealthRegen": "Health Regen",
      "Healthshot": "Healthshot",
      "InfiniteAmmo": "Infinite Ammo",
      "Items": "Items",
      "Jumps": "Extra Jumps",
      "Killscreen": "Kill Effect",
      "Money": "Money",
      "NoFallDamage": "No Fall Damage",
      "RainbowModel": "Rainbow Model",
      "ResetDeaths": "Reset Deaths",
      "Respawn": "Respawn",
      "ShowDamage": "Show Damage",
      "SmokeColor": "Smoke Color",
      "SoundDMG": "Hit Sound",
      "Speed": "Speed",
      "Tag": "Clan Tag",
      "TeammatesHeal": "Heal Teammates",
      "Vampirism": "Vampirism",
      "WeaponsMenu": "Weapons Menu",
      "Zeus": "Zeus",
      "endurance": "Endurance",
      "exp_multiplier": "XP Multiplier",
      "fastdefuse": "Fast Defuse",
      "fastplant": "Fast Plant",
      "flags": "Flags",
      "fov.Disable": "Off",
      "tag.Disable": "No tag",
      "fov.On": "You have set {blue}FOV {default}to {red}{0}",
      "fov.Off": "You've disabled the {blue}FOV",
      "tag.On": "You have set {blue}Tag {default}to {red}{0}",
      "tag.Off": "You've disabled the {blue}Tag",
      "bhop.Activated": "BunnyHop activated!",
      "bhop.TimeToActivation": "BunnyHop will be active in {red}{0}{default}s.",
      "respawn.Limit": "Respawn limit reached!",
      "respawn.InTeam": "You must be in a team!",
      "respawn.IsAlive": "You must be dead!",
      "respawn.Success": "Respawn successful!",
      "vip.NoAccess": "You don't have access to this command!",
      "vip.AlreadyVipPrivileges": "You already have VIP privileges.",
      "vip.ResetDeaths": "Your deaths are reset!",
      "viptest.YouCanNoLongerTakeTheVip": "You can no longer take the VIP Test.",
      "viptest.RetakenThrough": "The VIP test can be retaken in: {0}.",
      "viptest.SuccessfullyPassed": "You have taken the VIP test! Ends in: {0}.",
      "buyteamweapon.round_start": "You can buy the other team's weapons: {green}{0}",
      "buyteamweapon.in_buy_zone": "You must be in the buy zone!",
      "buyteamweapon.no_money": "Not enough money!",
      "weaponsmenu.title": "Select weapons package",
      "weaponsmenu.fromround": "[From round {0}]",
      "weaponsmenu.wanttosave": "Do you want to save this set?",
      "weaponsmenu.wanttosave.yes": "Yes",
      "weaponsmenu.wanttosave.no": "No",
      "weaponsmenu.saved": "Your free set has been saved, you will receive it when possible!",
      "weaponsmenu.notsaved": "Your free set was not saved, you can choose a new one next round.",
      "weaponsmenu.resetinfo.reset": "Your free set has been reset, choose a new one next round!",
      "weaponsmenu.resetinfo.info": "Want another set? Use {orange}!vwmenu{default} to choose a new one next round.",
      "pmm_vip.value.Health": "{0} HP",
      "pmm_vip.value.Armor": "{0} armor",
      "pmm_vip.value.Speed": "x{0}",
      "pmm_vip.value.Gravity": "x{0}",
      "pmm_vip.value.Money": "${0}",
      "pmm_vip.value.Jumps": "+{0}",
      "pmm_vip.value.Vampirism": "{0}%",
      "pmm_vip.value.DamageMultiplier": "x{0}",
      "pmm_vip.value.HealthRegen": "+{0} HP",
      "pmm_vip.value.ArmorRegen": "+{0}",
      "pmm_vip.value.Healthshot": "{0} pcs",
      "pmm_vip.value.exp_multiplier": "x{0}"
    }
  }
```

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
| `!vip` | `VipCore.CreateMenu(player)` + `VipCoreApi.CreateMenu(title)` → меню MenuManager |
| Toggle-фичи в `!vip` | тумблеры `AddToggle` (`[Вкл] Bhop`), переключаются на месте, страница не сбрасывается |
| «Bhop: Включено» в чат | тост `Notify` (только панорама) |
| Fov, Tag | выпадающий список `AddSelect` прямо в `!vip` (только панорама), новое значение видно сразу |
| Подменю модулей через `CreateMenu` (Fov в чат-режиме и т.п.) | меню MenuManager с кнопкой «Назад» в `!vip` |
| Модули с собственным `ChatMenu` / `CenterHtmlMenu` (VIP_Tag, VIP_WeaponsMenu) | хуки `ChatMenu.Open`, `CenterHtmlMenu.Open`, `MenuManager.OpenChatMenu/OpenCenterHtmlMenu` CSS и запасной `ChatMenuInstance/CenterHtmlMenuInstance.Display`; только для плагинов из `InterceptPlugins` |
| Тексты модулей (`GetTranslatedText`) | из `MenuManager_Modules_Translation.json`, если там есть ключ |

Меню других плагинов не трогаются: CSS-меню перехватывается только если его открыл плагин из `InterceptPlugins`.

#### Настройки (`configs/plugins/PMM_VIPCore/PMM_VIPCore.json`)
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
| `ModuleLanguage` | `""` | язык текстов модулей из файла переводов: пусто — текущий язык CSS, или например `ru` |
| `AutoCloseSeconds` | `20` | всплывающие меню модулей (WeaponsMenu при спавне) закрываются через N секунд, отсчёт в панели. `0` — выкл. |
| `AutoCloseVipSeconds` | `0` | то же для `!vip` и его подменю. `0` — выкл. |
| `AutoCloseWarnSeconds` | `5` | последние N секунд отсчёт красный |
| `Paint.Enabled` | `true` | своя VIP-панорама (нужен аддон) |
| `Paint.Position` | `Center` | `Left` / `Center` / `Right` |
| `Paint.GroupPalette` | VIPBRONZE/SILVER/GOLD | группа → палитра из `Paint.Theme.Palettes`. Нет в списке — по названию группы, иначе `DefaultPalette` |
| `Paint.Icons` | для всех стандартных модулей | ключ фичи → иконка (`ic-*` из `pmm_vip.css`: `health`, `exojump`, `taser`, `zoom_in` …). Нет — `DefaultIcon` |
| `Paint.ValueFormat` | Health → `{0} HP` … | запасной формат значения из `vip.json` (основной — `pmm_vip.value.*` в файле переводов) |
| `Paint.DateFormat` | `dd.MM.yyyy` | формат даты окончания VIP |
| `Paint.Texts` | RU | запасные надписи панели, если в файле переводов нет ключа |
| `Paint.OpenDelay` / `InputDelay` | `0.2` | задержка первой отрисовки и захвата мыши (кадр команды чата трогать нельзя) |
| `Paint.Theme` | тёмный + золото | цвета панели и групп, см. «Цвета» |
