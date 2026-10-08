using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace PMM_VIPCore;

public class BridgeConfig : BasePluginConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;

    // Empty = the player's own menu from !menu. Otherwise a MenuType name: PanoramaMenu, PanoramaWasdMenu, ChatMenu ...
    [JsonPropertyName("ForceMenuType")] public string ForceMenuType { get; set; } = "";

    // Plugins (assembly names) whose plain CSS ChatMenu / CenterHtmlMenu are moved to MenuManager.
    // "VIP_*" = every VIPCore module. VIPCore itself is caught through VipApi.CreateMenu anyway.
    [JsonPropertyName("InterceptPlugins")]
    public List<string> InterceptPlugins { get; set; } = new() { "VIPCore", "VIP_*" };

    // !vip: Toggle features become MenuManager toggles ("[On] Bhop") instead of "Bhop [Enabled]".
    [JsonPropertyName("MainToggles")] public bool MainToggles { get; set; } = true;

    // Toggle in !vip: show "Bhop: Enabled" as a MenuManager toast instead of a chat line (panorama menus only).
    [JsonPropertyName("ToggleNotify")] public bool ToggleNotify { get; set; } = true;

    // Selectable features whose sub menu is shown as a drop-down right inside !vip (panorama menus only).
    // Only list features whose OnSelectItem just opens a menu (Fov, Tag). Never Respawn and the like:
    // the bridge calls OnSelectItem every time !vip is drawn to read the choices.
    [JsonPropertyName("InlineSelectFeatures")]
    public List<string> InlineSelectFeatures { get; set; } = new() { "Fov", "Tag" };

    [JsonPropertyName("InlineSelectMax")] public int InlineSelectMax { get; set; } = 16;

    // Sub menus opened from !vip (or from another VIP menu) get a "Back" row.
    [JsonPropertyName("BackButton")] public bool BackButton { get; set; } = true;

    // After a row in a sub menu is picked (Fov 110, Tag ...) go back to the parent menu, so it shows the new value.
    [JsonPropertyName("ReturnToParentAfterSelect")] public bool ReturnToParentAfterSelect { get; set; } = true;

    [JsonPropertyName("ExitButton")] public bool ExitButton { get; set; } = true;

    // Remove chat colour codes ({green}, \x04 ...) from titles and rows. Not applied to ChatMenu.
    [JsonPropertyName("StripColors")] public bool StripColors { get; set; } = true;

    [JsonPropertyName("Debug")] public bool Debug { get; set; } = false;

    // Own VIP panorama (pmm_vip.xml) for players whose MenuManager menu is PanoramaMenu.
    [JsonPropertyName("Paint")] public PaintOptions Paint { get; set; } = new();
}

public class PaintOptions
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("LayoutPath")] public string LayoutPath { get; set; } = "panorama/layout/custom_game/pmm_vip.xml";
    // Left / Center / Right
    [JsonPropertyName("Position")] public string Position { get; set; } = "Center";
    // Delay before the first draw (a HUD update in the same frame as a chat command can get the player kicked).
    [JsonPropertyName("OpenDelay")] public float OpenDelay { get; set; } = 0.2f;
    [JsonPropertyName("InputDelay")] public float InputDelay { get; set; } = 0.2f;
    [JsonPropertyName("ToastSeconds")] public float ToastSeconds { get; set; } = 2.5f;
    // Date format of the VIP expiry in the header.
    [JsonPropertyName("DateFormat")] public string DateFormat { get; set; } = "dd.MM.yyyy";

    // VIP group -> palette: gold, silver, bronze, platinum, emerald, ruby, sapphire, amethyst.
    // Groups not listed: a palette whose name is part of the group name (VIPGOLD -> gold), else DefaultPalette.
    [JsonPropertyName("GroupPalette")]
    public Dictionary<string, string> GroupPalette { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("DefaultPalette")] public string DefaultPalette { get; set; } = "gold";

    // Feature key -> icon (an ic-* class of pmm_vip.css). Missing -> DefaultIcon.
    [JsonPropertyName("Icons")]
    public Dictionary<string, string> Icons { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Antiflash"] = "flashbang", ["Armor"] = "armor_helmet", ["ArmorRegen"] = "shield", ["Bhop"] = "exojump",
        ["BuyTeamWeapon"] = "shoppingcart", ["CustomDefaultAmmo"] = "bullet", ["DamageMultiplier"] = "kill",
        ["DecoyTp"] = "decoy", ["DefuseKit"] = "defuser", ["Defuser"] = "defuser", ["FastReload"] = "bullet_burst",
        ["Fov"] = "zoom_in", ["Gravity"] = "parachute", ["Grenades"] = "grenadepack", ["Health"] = "health",
        ["HealthRegen"] = "health", ["Healthshot"] = "healthshot", ["InfiniteAmmo"] = "ammobox", ["Items"] = "inventory",
        ["Jumps"] = "exojump", ["Killscreen"] = "camera", ["Money"] = "dollar_sign", ["NoFallDamage"] = "parachute",
        ["RainbowModel"] = "colorwheel", ["ResetDeaths"] = "refresh", ["Respawn"] = "undo", ["ShowDamage"] = "stats",
        ["SmokeColor"] = "smokegrenade", ["SoundDMG"] = "sound_3", ["Speed"] = "fast", ["Tag"] = "clantag",
        ["TeammatesHeal"] = "teamplayer", ["Vampirism"] = "kill_headshot", ["WeaponsMenu"] = "loadout", ["Zeus"] = "taser",
        ["endurance"] = "timer", ["exp_multiplier"] = "xp_rank", ["fastdefuse"] = "defuser", ["fastplant"] = "planted_c4",
        ["flags"] = "key",
    };
    [JsonPropertyName("DefaultIcon")] public string DefaultIcon { get; set; } = "star";

    // Feature key -> format of its vip.json value under the name ({0} = value). Not listed: the value as is.
    [JsonPropertyName("ValueFormat")]
    public Dictionary<string, string> ValueFormat { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Health"] = "{0} HP", ["Armor"] = "{0} брони", ["Speed"] = "x{0}", ["Gravity"] = "x{0}", ["Money"] = "${0}",
        ["Jumps"] = "+{0}", ["Vampirism"] = "{0}%", ["DamageMultiplier"] = "x{0}", ["HealthRegen"] = "+{0} HP",
        ["ArmorRegen"] = "+{0}", ["Healthshot"] = "{0} шт.", ["exp_multiplier"] = "x{0}",
    };

    [JsonPropertyName("Texts")] public PaintTexts Texts { get; set; } = new();

    // Colours of the panel. Panorama cannot take colours from the server: the plugin writes
    // pmm_vip_theme.css from this section, the file goes into the addon (see INSTALL.md).
    [JsonPropertyName("Theme")] public ThemeOptions Theme { get; set; } = new();
}

// Every colour is #rrggbb or #rrggbbaa (aa = opacity, ff = solid).
public class ThemeOptions
{
    [JsonPropertyName("PanelTop")] public string PanelTop { get; set; } = "#19171cf5";
    [JsonPropertyName("PanelBottom")] public string PanelBottom { get; set; } = "#0d0c10f5";
    [JsonPropertyName("PanelBorder")] public string PanelBorder { get; set; } = "#3a2f1c";
    [JsonPropertyName("Separator")] public string Separator { get; set; } = "#ffffff1a";
    [JsonPropertyName("Nick")] public string Nick { get; set; } = "#ffffff";
    [JsonPropertyName("Muted")] public string Muted { get; set; } = "#a7a29a";
    [JsonPropertyName("CloseBg")] public string CloseBg { get; set; } = "#ffffff0d";
    [JsonPropertyName("CloseHover")] public string CloseHover { get; set; } = "#c93642";
    [JsonPropertyName("CloseText")] public string CloseText { get; set; } = "#ffffffcc";
    [JsonPropertyName("TileBg")] public string TileBg { get; set; } = "#ffffff08";
    [JsonPropertyName("TileBorder")] public string TileBorder { get; set; } = "#ffffff12";
    [JsonPropertyName("TileHover")] public string TileHover { get; set; } = "#ffffff12";
    [JsonPropertyName("TileName")] public string TileName { get; set; } = "#ece8e0";
    [JsonPropertyName("TileDesc")] public string TileDesc { get; set; } = "#8e897f";
    [JsonPropertyName("IconBox")] public string IconBox { get; set; } = "#ffffff0d";
    [JsonPropertyName("Icon")] public string Icon { get; set; } = "#d8d4cc";
    [JsonPropertyName("SwitchOff")] public string SwitchOff { get; set; } = "#ffffff1f";
    [JsonPropertyName("KnobOff")] public string KnobOff { get; set; } = "#9a958c";
    [JsonPropertyName("KnobOn")] public string KnobOn { get; set; } = "#ffffff";
    [JsonPropertyName("ButtonBg")] public string ButtonBg { get; set; } = "#ffffff0f";
    [JsonPropertyName("ButtonHover")] public string ButtonHover { get; set; } = "#ffffff22";
    [JsonPropertyName("ButtonText")] public string ButtonText { get; set; } = "#dddddd";
    [JsonPropertyName("DotOff")] public string DotOff { get; set; } = "#ffffff26";
    [JsonPropertyName("ToastOkBg")] public string ToastOkBg { get; set; } = "#1f2a1b";
    [JsonPropertyName("ToastOkBorder")] public string ToastOkBorder { get; set; } = "#6fbf5a66";
    [JsonPropertyName("ToastOkText")] public string ToastOkText { get; set; } = "#d6efcd";
    [JsonPropertyName("ToastOkIcon")] public string ToastOkIcon { get; set; } = "#8fe07a";
    [JsonPropertyName("ToastWarnBg")] public string ToastWarnBg { get; set; } = "#2a1b1b";
    [JsonPropertyName("ToastWarnBorder")] public string ToastWarnBorder { get; set; } = "#d9606066";
    [JsonPropertyName("ToastWarnText")] public string ToastWarnText { get; set; } = "#f3d0d0";
    [JsonPropertyName("ToastWarnIcon")] public string ToastWarnIcon { get; set; } = "#ff8a8a";

    // Group colours. Name -> Accent (main colour: switch, badge, group, borders), Light (highlight),
    // Dark (gradient end), OnAccent (text on the badge / group label). Add your own names freely
    // and use them in GroupPalette / DefaultPalette.
    [JsonPropertyName("Palettes")]
    public Dictionary<string, PaletteColors> Palettes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gold"] = new("#f2c35b", "#ffe39a", "#a8701a", "#24190a"),
        ["silver"] = new("#c9d2dc", "#f2f6fa", "#7d8894", "#1b2026"),
        ["bronze"] = new("#d8915a", "#f3c39b", "#8a4f22", "#26150a"),
        ["platinum"] = new("#9fe3e0", "#e2fbfa", "#4f9a97", "#0f2423"),
        ["emerald"] = new("#5fd38d", "#b7f5cf", "#24804a", "#0b2415"),
        ["ruby"] = new("#ef5a6f", "#ffb0bb", "#9a2133", "#2a0a10"),
        ["sapphire"] = new("#5f9cf2", "#b9d6ff", "#2a5aa8", "#0b1830"),
        ["amethyst"] = new("#b07cf2", "#ddc4ff", "#6a3bb0", "#1d0f30"),
    };
}

public class PaletteColors
{
    public PaletteColors() { }
    public PaletteColors(string accent, string light, string dark, string onAccent)
    {
        Accent = accent;
        Light = light;
        Dark = dark;
        OnAccent = onAccent;
    }

    [JsonPropertyName("Accent")] public string Accent { get; set; } = "#f2c35b";
    [JsonPropertyName("Light")] public string Light { get; set; } = "#ffe39a";
    [JsonPropertyName("Dark")] public string Dark { get; set; } = "#a8701a";
    [JsonPropertyName("OnAccent")] public string OnAccent { get; set; } = "#24190a";
}

public class PaintTexts
{
    [JsonPropertyName("Brand")] public string Brand { get; set; } = "VIP";
    [JsonPropertyName("Forever")] public string Forever { get; set; } = "навсегда";
    [JsonPropertyName("Until")] public string Until { get; set; } = "до";
    // {0} = days left
    [JsonPropertyName("DaysLeft")] public string DaysLeft { get; set; } = "· {0} дн.";
    [JsonPropertyName("Enabled")] public string Enabled { get; set; } = "Включено";
    [JsonPropertyName("Disabled")] public string Disabled { get; set; } = "Выключено";
    [JsonPropertyName("NoAccess")] public string NoAccess { get; set; } = "Нет доступа";
    [JsonPropertyName("Use")] public string Use { get; set; } = "▶ ИСП.";
    [JsonPropertyName("Back")] public string Back { get; set; } = "‹ НАЗАД";
    [JsonPropertyName("PickHint")] public string PickHint { get; set; } = "Выбери значение";
    [JsonPropertyName("ListHint")] public string ListHint { get; set; } = "";
    // {0} = page, {1} = pages
    [JsonPropertyName("Page")] public string Page { get; set; } = "{0} / {1}";
}
