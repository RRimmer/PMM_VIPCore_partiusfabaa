using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Logging;

namespace PMM_VIPCore;

// Texts of the VIP menus from MenuManager's file
// configs/plugins/MenuManagerCore/MenuManager_Modules_Translation.json (plugin -> language -> key -> text).
// The bridge adds its own "PMM_VIPCore" section once; after that the file belongs to the admin.
// Lookup: whole text first (feature key "Health", "tag.Disable", "pmm_vip.back"), then tokens with . or _
// inside a longer text, the same way MenuManager does it for its own menus.
internal sealed class VipTranslations
{
    public const string Section = "PMM_VIPCore";
    private const string FileName = "MenuManager_Modules_Translation.json";

    private static readonly Regex Token = new(
        @"(?<![A-Za-z0-9_.])([A-Za-z][A-Za-z0-9_.]*[_.][A-Za-z0-9_.]*)(?![A-Za-z0-9_.])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger _log;
    private readonly string _path;
    private DateTime _writtenAt = DateTime.MinValue;
    private Dictionary<string, Dictionary<string, string>> _byLanguage = new(StringComparer.OrdinalIgnoreCase);

    public VipTranslations(string moduleDirectory, ILogger log)
    {
        _log = log;
        _path = Path.GetFullPath(Path.Combine(moduleDirectory, "..", "..", "configs", "plugins", "MenuManagerCore", FileName));
    }

    public string FilePath => _path;
    public int Languages
    {
        get
        {
            ReloadIfChanged();
            return _byLanguage.Count;
        }
    }

    // Adds the PMM_VIPCore section, or the keys / languages it is missing (a new version brings new keys).
    // Keys that are already there are never changed: the admin's texts stay. Other sections stay as they are.
    public void EnsureSection()
    {
        try
        {
            JsonObject root;
            if (File.Exists(_path))
            {
                var parsed = JsonNode.Parse(File.ReadAllText(_path), documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
                if (parsed is not JsonObject obj)
                {
                    _log.LogWarning("Translations: {Path} is not a JSON object, section not added", _path);
                    return;
                }
                root = obj;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                root = new JsonObject();
            }

            bool created = root[Section] is not JsonObject;
            if (created) root[Section] = new JsonObject();
            var section = (JsonObject)root[Section]!;
            int added = 0;
            foreach (var (lang, map) in VipDefaults.Texts)
            {
                if (section[lang] is not JsonObject o)
                {
                    o = new JsonObject();
                    section[lang] = o;
                }
                foreach (var (k, v) in map)
                {
                    if (o.ContainsKey(k)) continue;
                    o[k] = v;
                    added++;
                }
            }
            if (added == 0) return;

            File.WriteAllText(_path, root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            _log.LogInformation("Translations: {Count} keys {What} section {Section} of {Path}",
                added, created ? "written to the new" : "added to the", Section, _path);
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Translations: could not add the {Section} section to {Path}", Section, _path);
        }
    }

    private void ReloadIfChanged()
    {
        if (!File.Exists(_path)) return;
        DateTime written = File.GetLastWriteTimeUtc(_path);
        if (written == _writtenAt) return;
        _writtenAt = written;

        var next = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            // Our own section first: a key that another plugin's section also has must not override VIP texts.
            var modules = doc.RootElement.EnumerateObject()
                .OrderBy(m => m.Name.Equals(Section, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
            foreach (var module in modules)
            {
                if (module.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var language in module.Value.EnumerateObject())
                {
                    if (language.Value.ValueKind != JsonValueKind.Object) continue;
                    if (!next.TryGetValue(language.Name, out var map))
                        next[language.Name] = map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var phrase in language.Value.EnumerateObject())
                    {
                        if (phrase.Value.ValueKind != JsonValueKind.String) continue;
                        string? value = phrase.Value.GetString();
                        if (!string.IsNullOrEmpty(value)) map.TryAdd(phrase.Name, value);
                    }
                }
            }
            _byLanguage = next;
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Translations: could not read {Path}", _path);
        }
    }

    public static CultureInfo Culture(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return CultureInfo.InvariantCulture;
        try { return player.GetLanguage(); }
        catch { return CultureInfo.InvariantCulture; }
    }

    // Exact key, or null.
    public string? Get(CultureInfo culture, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        ReloadIfChanged();
        foreach (string name in Candidates(culture))
            if (_byLanguage.TryGetValue(name, out var map) && map.TryGetValue(key, out var value))
                return value;
        return null;
    }

    // Whole text, then tokens inside it.
    public string Apply(CultureInfo culture, string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var whole = Get(culture, text);
        if (whole != null) return whole;
        if (!Token.IsMatch(text)) return text;
        return Token.Replace(text, m => Get(culture, m.Value) ?? m.Value);
    }

    private static IEnumerable<string> Candidates(CultureInfo culture)
    {
        if (!string.IsNullOrEmpty(culture.Name)) yield return culture.Name;
        if (!string.IsNullOrEmpty(culture.Parent.Name) && culture.Parent.Name != culture.Name) yield return culture.Parent.Name;
        if (!string.IsNullOrEmpty(culture.TwoLetterISOLanguageName) && culture.TwoLetterISOLanguageName != "iv")
            yield return culture.TwoLetterISOLanguageName;
        yield return "en";
    }
}

// What the PMM_VIPCore section starts with. Feature keys = VIPCore feature names (vip.json),
// pmm_vip.* = texts of the VIP panel, pmm_vip.value.<Feature> = format of the vip.json value ({0}).
internal static class VipDefaults
{
    public static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["ru"] = new()
        {
            ["pmm_vip.brand"] = "VIP",
            ["pmm_vip.forever"] = "навсегда",
            ["pmm_vip.until"] = "до",
            ["pmm_vip.days_left"] = "· {0} дн.",
            ["pmm_vip.enabled"] = "Включено",
            ["pmm_vip.disabled"] = "Выключено",
            ["pmm_vip.no_access"] = "Нет доступа",
            ["pmm_vip.use"] = "▶ ИСП.",
            ["pmm_vip.back"] = "‹ НАЗАД",
            ["pmm_vip.pick_hint"] = "Выбери значение",
            ["pmm_vip.page"] = "{0} / {1}",
            ["pmm_vip.title"] = "VIP меню — {0}",
            ["pmm_vip.closes_in"] = "Закроется через {0} с",

            // WeaponsMenu: !swm settings, compact panel, key hints (0.1.4)
            ["pmm_vip.wm.settings_title"] = "Настройки меню оружия",
            ["pmm_vip.wm.ask_each_time"] = "Спрашивать каждый раз о сохранении набора?",
            ["pmm_vip.wm.auto_menu"] = "Автоматический вызов меню выбора комплекта оружий",
            ["pmm_vip.wm.mode_title"] = "Вид меню",
            ["pmm_vip.wm.mode_need_check"] = "нужна проверка цифр",
            ["pmm_vip.wm.mode_need_core"] = "Нужен PMM Core 1.2.05 на сервере",
            ["pmm_vip.wm.open_now"] = "Выбрать комплект сейчас",
            ["pmm_vip.wm.save"] = "Сохранить комплект",
            ["pmm_vip.wm.save_last"] = "Сохранить комплект «{0}»",
            ["pmm_vip.wm.forget"] = "Удалить сохранённый комплект",
            ["pmm_vip.wm.mode"] = "Вид меню",
            ["pmm_vip.wm.mode_mouse"] = "Панорама мышка",
            ["pmm_vip.wm.mode_wasd"] = "Панорама WASD",
            ["pmm_vip.wm.mode_digits"] = "Панорама цифры",
            ["pmm_vip.wm.mode_set"] = "Вид меню: {0}",
            ["pmm_vip.wm.saved"] = "Комплект сохранён и будет выдаваться автоматически",
            ["pmm_vip.wm.saved_auto"] = "Комплект сохранён и выдаётся автоматически. Удалите его, чтобы выбрать другой",
            ["pmm_vip.wm.already_saved"] = "Комплект уже сохранён",
            ["pmm_vip.wm.nothing_to_save"] = "Сначала возьмите комплект в меню оружия",
            ["pmm_vip.wm.nothing_saved"] = "Нет сохранённого комплекта",
            ["pmm_vip.wm.forgot"] = "Сохранённый комплект удалён",
            ["pmm_vip.wm.not_now"] = "Сейчас взять комплект нельзя: вы мертвы, вне зоны закупки, время закупки вышло или комплект ещё недоступен",
            ["pmm_vip.wm.unavailable"] = "VIP_WeaponsMenu не загружен",
            ["pmm_vip.wm.no_access"] = "{red}[VIP]{default} Меню оружия вам недоступно",
            ["pmm_vip.wm.hint"] = "{gold}[VIP]{default} Меню оружия и сохранение комплекта: {green}{0}",
            ["pmm_vip.wm.keys_check"] = "{gold}[VIP]{default} Нажмите по очереди 1 … 0. Строка с биндами — в консоли",
            ["pmm_vip.wm.keys_fail"] = "Проверка цифр не пройдена, вид меню не изменён",
            ["pmm_vip.keys_wasd"] = "W/S — выбор · E — взять · A/D — стр. · R — настройки",
            ["pmm_vip.keys_wasd_set"] = "W/S — выбор · E — изменить · R — назад",
            ["pmm_vip.keys_digits"] = "1–6 — взять · 7 — настройки · 8/9 — стр. · 0 — закрыть",
            ["pmm_vip.keys_digits_set"] = "1–6 — изменить · 7 — назад · 0 — закрыть",

            ["Antiflash"] = "Анти-флеш",
            ["Armor"] = "Броня",
            ["ArmorRegen"] = "Реген брони",
            ["Bhop"] = "Банихоп",
            ["BuyTeamWeapon"] = "Оружие другой команды",
            ["CustomDefaultAmmo"] = "Патроны",
            ["DamageMultiplier"] = "Множитель урона",
            ["DecoyTp"] = "Телепорт-граната",
            ["DefuseKit"] = "Набор сапёра",
            ["Defuser"] = "Набор сапёра",
            ["FastReload"] = "Быстрая перезарядка",
            ["Fov"] = "Поле зрения (FOV)",
            ["Gravity"] = "Гравитация",
            ["Grenades"] = "Гранаты",
            ["Health"] = "Здоровье",
            ["HealthRegen"] = "Реген здоровья",
            ["Healthshot"] = "Медшприц",
            ["InfiniteAmmo"] = "Бесконечные патроны",
            ["Items"] = "Предметы",
            ["Jumps"] = "Доп. прыжки",
            ["Killscreen"] = "Эффект убийства",
            ["Money"] = "Деньги",
            ["NoFallDamage"] = "Без урона от падения",
            ["RainbowModel"] = "Радужная модель",
            ["ResetDeaths"] = "Сброс смертей",
            ["Respawn"] = "Возрождение",
            ["ShowDamage"] = "Показ урона",
            ["SmokeColor"] = "Цветной дым",
            ["SoundDMG"] = "Звук попадания",
            ["Speed"] = "Скорость",
            ["Tag"] = "Клан-тег",
            ["TeammatesHeal"] = "Лечение союзников",
            ["Vampirism"] = "Вампиризм",
            ["WeaponsMenu"] = "Меню оружия",
            ["Zeus"] = "Zeus",
            ["endurance"] = "Выносливость",
            ["exp_multiplier"] = "Множитель опыта",
            ["fastdefuse"] = "Быстрое разминирование",
            ["fastplant"] = "Быстрая установка",
            ["flags"] = "Флаги",

            ["fov.Disable"] = "Выкл.",
            ["tag.Disable"] = "Без тега",

            // texts of VIP modules (VIPCore GetTranslatedText); {colour} tags as in VIPCore lang files
            ["fov.On"] = "Вы установили {blue}FOV {default}на {red}{0}",
            ["fov.Off"] = "Вы выключили {blue}FOV",
            ["tag.On"] = "Вы установили {blue}тег {default}на {red}{0}",
            ["tag.Off"] = "Вы выключили {blue}тег",
            ["bhop.Activated"] = "Банихоп активирован!",
            ["bhop.TimeToActivation"] = "Банихоп будет активен через {red}{0}{default} сек.",
            ["respawn.Limit"] = "Лимит возрождений исчерпан!",
            ["respawn.InTeam"] = "Вы должны быть в команде!",
            ["respawn.IsAlive"] = "Вы должны быть мертвы!",
            ["respawn.Success"] = "Вы успешно возродились!",
            ["vip.NoAccess"] = "У вас нет доступа к этой команде!",
            ["vip.AlreadyVipPrivileges"] = "У вас уже есть VIP-привилегия.",
            ["vip.ResetDeaths"] = "Ваши смерти сброшены!",
            ["viptest.YouCanNoLongerTakeTheVip"] = "Вы больше не можете взять VIP-Test",
            ["viptest.RetakenThrough"] = "Взять VIP-Test повторно можно через: {0}",
            ["viptest.SuccessfullyPassed"] = "Вы взяли VIP-Test! Заканчивается через: {0}",
            ["buyteamweapon.round_start"] = "Можно купить оружие другой команды: {green}{0}",
            ["buyteamweapon.in_buy_zone"] = "Вы должны быть в зоне закупки!",
            ["buyteamweapon.no_money"] = "Недостаточно денег!",
            ["weaponsmenu.title"] = "Выберите набор оружия",
            ["weaponsmenu.fromround"] = "[С {0} раунда]",
            ["weaponsmenu.wanttosave"] = "Сохранить этот набор?",
            ["weaponsmenu.wanttosave.yes"] = "Да",
            ["weaponsmenu.wanttosave.no"] = "Нет",
            ["weaponsmenu.saved"] = "Набор сохранён, вы будете получать его автоматически!",
            ["weaponsmenu.notsaved"] = "Набор не сохранён, в следующем раунде можно выбрать снова.",
            ["weaponsmenu.resetinfo.reset"] = "Набор сброшен, выберите новый в следующем раунде!",
            ["weaponsmenu.resetinfo.info"] = "Хотите сменить набор? Команда {orange}!vwmenu{default} — выбор в следующем раунде.",

            ["pmm_vip.value.Health"] = "{0} HP",
            ["pmm_vip.value.Armor"] = "{0} брони",
            ["pmm_vip.value.Speed"] = "x{0}",
            ["pmm_vip.value.Gravity"] = "x{0}",
            ["pmm_vip.value.Money"] = "${0}",
            ["pmm_vip.value.Jumps"] = "+{0}",
            ["pmm_vip.value.Vampirism"] = "{0}%",
            ["pmm_vip.value.DamageMultiplier"] = "x{0}",
            ["pmm_vip.value.HealthRegen"] = "+{0} HP",
            ["pmm_vip.value.ArmorRegen"] = "+{0}",
            ["pmm_vip.value.Healthshot"] = "{0} шт.",
            ["pmm_vip.value.exp_multiplier"] = "x{0}",
        },
        ["en"] = new()
        {
            ["pmm_vip.brand"] = "VIP",
            ["pmm_vip.forever"] = "forever",
            ["pmm_vip.until"] = "until",
            ["pmm_vip.days_left"] = "· {0} d.",
            ["pmm_vip.enabled"] = "Enabled",
            ["pmm_vip.disabled"] = "Disabled",
            ["pmm_vip.no_access"] = "No access",
            ["pmm_vip.use"] = "▶ USE",
            ["pmm_vip.back"] = "‹ BACK",
            ["pmm_vip.pick_hint"] = "Pick a value",
            ["pmm_vip.page"] = "{0} / {1}",
            ["pmm_vip.title"] = "VIP menu — {0}",
            ["pmm_vip.closes_in"] = "Closes in {0}s",

            // WeaponsMenu: !swm settings, compact panel, key hints (0.1.4)
            ["pmm_vip.wm.settings_title"] = "Weapons menu settings",
            ["pmm_vip.wm.ask_each_time"] = "Ask every time whether to save the set?",
            ["pmm_vip.wm.auto_menu"] = "Open the weapon set menu automatically",
            ["pmm_vip.wm.mode_title"] = "Menu control",
            ["pmm_vip.wm.mode_need_check"] = "keys check needed",
            ["pmm_vip.wm.mode_need_core"] = "Needs PMM Core 1.2.05 on the server",
            ["pmm_vip.wm.open_now"] = "Pick a set now",
            ["pmm_vip.wm.save"] = "Save the set",
            ["pmm_vip.wm.save_last"] = "Save set «{0}»",
            ["pmm_vip.wm.forget"] = "Delete the saved set",
            ["pmm_vip.wm.mode"] = "Menu control",
            ["pmm_vip.wm.mode_mouse"] = "Panorama mouse",
            ["pmm_vip.wm.mode_wasd"] = "Panorama WASD",
            ["pmm_vip.wm.mode_digits"] = "Panorama digits",
            ["pmm_vip.wm.mode_set"] = "Menu control: {0}",
            ["pmm_vip.wm.saved"] = "Set saved, it will be given automatically",
            ["pmm_vip.wm.saved_auto"] = "A set is saved and given automatically. Delete it to pick another one",
            ["pmm_vip.wm.already_saved"] = "A set is already saved",
            ["pmm_vip.wm.nothing_to_save"] = "Take a set from the weapons menu first",
            ["pmm_vip.wm.nothing_saved"] = "No saved set",
            ["pmm_vip.wm.forgot"] = "Saved set deleted",
            ["pmm_vip.wm.not_now"] = "You cannot take a set now: dead, out of the buy zone, buy time is over or no set yet",
            ["pmm_vip.wm.unavailable"] = "VIP_WeaponsMenu is not loaded",
            ["pmm_vip.wm.no_access"] = "{red}[VIP]{default} The weapons menu is not available to you",
            ["pmm_vip.wm.hint"] = "{gold}[VIP]{default} Weapons menu and saving a set: {green}{0}",
            ["pmm_vip.wm.keys_check"] = "{gold}[VIP]{default} Press 1 … 0 one by one. The bind line is in the console",
            ["pmm_vip.wm.keys_fail"] = "Number keys check failed, menu control not changed",
            ["pmm_vip.keys_wasd"] = "W/S — move · E — take · A/D — page · R — settings",
            ["pmm_vip.keys_wasd_set"] = "W/S — move · E — change · R — back",
            ["pmm_vip.keys_digits"] = "1–6 — take · 7 — settings · 8/9 — page · 0 — close",
            ["pmm_vip.keys_digits_set"] = "1–6 — change · 7 — back · 0 — close",

            ["Antiflash"] = "Anti-Flash",
            ["Armor"] = "Armor",
            ["ArmorRegen"] = "Armor Regen",
            ["Bhop"] = "BunnyHop",
            ["BuyTeamWeapon"] = "Enemy Team Weapons",
            ["CustomDefaultAmmo"] = "Ammo",
            ["DamageMultiplier"] = "Damage Multiplier",
            ["DecoyTp"] = "Teleport Grenade",
            ["DefuseKit"] = "Defuse Kit",
            ["Defuser"] = "Defuse Kit",
            ["FastReload"] = "Fast Reload",
            ["Fov"] = "Field of View",
            ["Gravity"] = "Gravity",
            ["Grenades"] = "Grenades",
            ["Health"] = "Health",
            ["HealthRegen"] = "Health Regen",
            ["Healthshot"] = "Healthshot",
            ["InfiniteAmmo"] = "Infinite Ammo",
            ["Items"] = "Items",
            ["Jumps"] = "Extra Jumps",
            ["Killscreen"] = "Kill Effect",
            ["Money"] = "Money",
            ["NoFallDamage"] = "No Fall Damage",
            ["RainbowModel"] = "Rainbow Model",
            ["ResetDeaths"] = "Reset Deaths",
            ["Respawn"] = "Respawn",
            ["ShowDamage"] = "Show Damage",
            ["SmokeColor"] = "Smoke Color",
            ["SoundDMG"] = "Hit Sound",
            ["Speed"] = "Speed",
            ["Tag"] = "Clan Tag",
            ["TeammatesHeal"] = "Heal Teammates",
            ["Vampirism"] = "Vampirism",
            ["WeaponsMenu"] = "Weapons Menu",
            ["Zeus"] = "Zeus",
            ["endurance"] = "Endurance",
            ["exp_multiplier"] = "XP Multiplier",
            ["fastdefuse"] = "Fast Defuse",
            ["fastplant"] = "Fast Plant",
            ["flags"] = "Flags",

            ["fov.Disable"] = "Off",
            ["tag.Disable"] = "No tag",

            ["fov.On"] = "You have set {blue}FOV {default}to {red}{0}",
            ["fov.Off"] = "You've disabled the {blue}FOV",
            ["tag.On"] = "You have set {blue}Tag {default}to {red}{0}",
            ["tag.Off"] = "You've disabled the {blue}Tag",
            ["bhop.Activated"] = "BunnyHop activated!",
            ["bhop.TimeToActivation"] = "BunnyHop will be active in {red}{0}{default}s.",
            ["respawn.Limit"] = "Respawn limit reached!",
            ["respawn.InTeam"] = "You must be in a team!",
            ["respawn.IsAlive"] = "You must be dead!",
            ["respawn.Success"] = "Respawn successful!",
            ["vip.NoAccess"] = "You don't have access to this command!",
            ["vip.AlreadyVipPrivileges"] = "You already have VIP privileges.",
            ["vip.ResetDeaths"] = "Your deaths are reset!",
            ["viptest.YouCanNoLongerTakeTheVip"] = "You can no longer take the VIP Test.",
            ["viptest.RetakenThrough"] = "The VIP test can be retaken in: {0}.",
            ["viptest.SuccessfullyPassed"] = "You have taken the VIP test! Ends in: {0}.",
            ["buyteamweapon.round_start"] = "You can buy the other team's weapons: {green}{0}",
            ["buyteamweapon.in_buy_zone"] = "You must be in the buy zone!",
            ["buyteamweapon.no_money"] = "Not enough money!",
            ["weaponsmenu.title"] = "Select weapons package",
            ["weaponsmenu.fromround"] = "[From round {0}]",
            ["weaponsmenu.wanttosave"] = "Do you want to save this set?",
            ["weaponsmenu.wanttosave.yes"] = "Yes",
            ["weaponsmenu.wanttosave.no"] = "No",
            ["weaponsmenu.saved"] = "Your free set has been saved, you will receive it when possible!",
            ["weaponsmenu.notsaved"] = "Your free set was not saved, you can choose a new one next round.",
            ["weaponsmenu.resetinfo.reset"] = "Your free set has been reset, choose a new one next round!",
            ["weaponsmenu.resetinfo.info"] = "Want another set? Use {orange}!vwmenu{default} to choose a new one next round.",

            ["pmm_vip.value.Health"] = "{0} HP",
            ["pmm_vip.value.Armor"] = "{0} armor",
            ["pmm_vip.value.Speed"] = "x{0}",
            ["pmm_vip.value.Gravity"] = "x{0}",
            ["pmm_vip.value.Money"] = "${0}",
            ["pmm_vip.value.Jumps"] = "+{0}",
            ["pmm_vip.value.Vampirism"] = "{0}%",
            ["pmm_vip.value.DamageMultiplier"] = "x{0}",
            ["pmm_vip.value.HealthRegen"] = "+{0} HP",
            ["pmm_vip.value.ArmorRegen"] = "+{0}",
            ["pmm_vip.value.Healthshot"] = "{0} pcs",
            ["pmm_vip.value.exp_multiplier"] = "x{0}",
        },
    };
}
