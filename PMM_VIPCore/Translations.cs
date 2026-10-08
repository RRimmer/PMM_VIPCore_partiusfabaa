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

    // Adds the PMM_VIPCore section when the file does not have it yet. Other sections stay as they are.
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
                if (root.ContainsKey(Section)) return;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                root = new JsonObject();
            }

            var section = new JsonObject();
            foreach (var (lang, map) in VipDefaults.Texts)
            {
                var o = new JsonObject();
                foreach (var (k, v) in map) o[k] = v;
                section[lang] = o;
            }
            root[Section] = section;
            File.WriteAllText(_path, root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            _log.LogInformation("Translations: section {Section} added to {Path}", Section, _path);
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
