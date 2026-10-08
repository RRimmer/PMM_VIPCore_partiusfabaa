# PMM_VIPCore — changelog

[English](#english) · [Русский](#русский)

## English

### 0.1.1 — 2026-10-08
- Panel colours in the config: `Paint.Theme` (background, border, tiles, text, switch, buttons, status line) and
  group palettes `Paint.Theme.Palettes` (add your own). The plugin writes `pmm_vip_theme.css` on load and on
  `css_pmm_vip css`; the file goes into the addon next to `pmm_vip.css`.
- Feature names, choice labels (`tag.Disable` …) and panel texts come from
  `MenuManager_Modules_Translation.json` (section `PMM_VIPCore`, ru / en, added automatically) in the player's language.
  Fixed: Tag showed `tag.Disable`; ShowDamage / SoundDMG / TeammatesHeal had no names.
- VIP_Fov / VIP_Tag are no longer shipped: use the original builds.

### 0.1.0 — 2026-10-08
- Own VIP panorama `pmm_vip.xml` (dark + gold), drawn through MenuManager's `pmm:paint` for PanoramaMenu players.
  - Header: nickname, VIP group (palette per group), VIP expiry.
  - 3×4 tiles with CS2 icons, the value from `vip.json`, a switch / a choice / "USE" / "No access".
  - Fov / Tag values are picked from 4×4 tiles. Module sub menus are an 8-row list with "Back".
  - Status line inside the panel. The page is remembered per menu, also after "Back".
- Config: `Paint` section (icons, value formats, group palettes, texts, position).
- `css_pmm_vip` shows the panel state.

### 0.0.1 — 2026-10-08
- First version. Harmony bridge for the original VIPCore (thesamefabius) without rebuilding the core or the modules.
- `VipCoreApi.CreateMenu` returns the bridge's menu object, `Open()` goes to MenuManager (`menu:nfcore`).
- `!vip`: toggle features become switches, the toggle message is a toast instead of chat; Fov / Tag are drop-downs.
- Switches and drop-downs change in place: `!vip` is not reopened and does not jump back to page 1
  (MenuManager forgets the page on every `Open`, so the bridge toggles the feature itself with the current state
  instead of VIPCore's callback, which keeps the state from the moment the menu was built).
- Module sub menus get "Back" and return to the parent menu after a pick.
- Plugins from `InterceptPlugins` (default `VIPCore`, `VIP_*`): their `ChatMenu` / `CenterHtmlMenu` also open in MenuManager.
- The log names a module built against another VipCoreApi (`MissingMethodException`, e.g. VIP_Fov / VIP_Tag from the old PMM fork).
- Server command `css_pmm_vip [repatch]`.

## Русский

### 0.1.1 — 2026-10-08
- Цвета панели в конфиге: `Paint.Theme` (фон, рамка, плитки, текст, ползунок, кнопки, статус) и палитры групп
  `Paint.Theme.Palettes` (можно добавлять свои). Плагин пишет `pmm_vip_theme.css` при загрузке и по `css_pmm_vip css`,
  файл кладётся в аддон рядом с `pmm_vip.css`.
- Названия функций, подписи списков (`tag.Disable` …) и тексты панели берутся из
  `MenuManager_Modules_Translation.json` (раздел `PMM_VIPCore`, ru / en, добавляется сам) по языку игрока.
  Исправлено: Tag показывал `tag.Disable`; ShowDamage / SoundDMG / TeammatesHeal без названий.
- VIP_Fov / VIP_Tag больше не входят в архив: нужны оригинальные сборки.

### 0.1.0 — 2026-10-08
- Своя VIP-панорама `pmm_vip.xml` (тёмный + золото), рисуется через `pmm:paint` MenuManager для PanoramaMenu.
  - Шапка: ник, группа VIP (палитра по группе), срок VIP.
  - Плитки 3×4 с иконками из клиента CS2, значением из `vip.json`, тумблером / выбором / «ИСП.» / «Нет доступа».
  - Выбор значения Fov / Tag — плитками 4×4. Подменю модулей — список по 8 строк с «Назад».
  - Статус переключения в самой панели. Страница запоминается для каждого меню, в том числе после «Назад».
- Конфиг: секция `Paint` (иконки, форматы значений, палитры групп, тексты, позиция).
- `css_pmm_vip` показывает состояние панорамы.

### 0.0.1 — 2026-10-08
- Первая версия. Harmony-мост для оригинального VIPCore (thesamefabius) без пересборки ядра и модулей.
- `VipCoreApi.CreateMenu` возвращает свой объект меню, `Open()` уходит в MenuManager (`menu:nfcore`).
- `!vip`: toggle-фичи — тумблеры, переключение — тост вместо чата; Fov / Tag — выпадающий список.
- Тумблер и выпадающий список меняются на месте: `!vip` не открывается заново и не сбрасывается на 1-ю страницу
  (MenuManager при каждом `Open` забывает страницу, поэтому мост переключает фичу сам, с актуальным состоянием,
  а не через callback VIPCore, который хранит состояние на момент открытия меню).
- Подменю модулей получают «Назад», после выбора возвращают в родительское меню.
- Плагины из `InterceptPlugins` (по умолчанию `VIPCore`, `VIP_*`): их `ChatMenu` / `CenterHtmlMenu` тоже открываются через MenuManager.
- Мост пишет в лог, какой модуль собран не под тот VipCoreApi (`MissingMethodException`, например VIP_Fov / VIP_Tag из старого PMM-форка).
- Команда сервера `css_pmm_vip [repatch]`.
