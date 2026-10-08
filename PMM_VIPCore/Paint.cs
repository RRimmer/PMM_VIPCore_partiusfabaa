using System.Collections;
using System.Runtime.CompilerServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace PMM_VIPCore;

// VIP panorama (pmm_vip.xml): MenuManager offers every PanoramaMenu to its painters (pmm:paint);
// menus the bridge built for VIPCore are drawn here instead of the standard MenuManager list.
// Server -> client: dialog variables + classes per player only (see tools/gen_layout.py for ids).
public partial class Plugin : IPmmMenuPainter
{
    private const int TilesPerPage = 12, Cols = 3, ChipsPerPage = 16, RowsPerPage = 8, Dots = 8;
    private const string RootId = "vip-root";

    // Must match tools/gen_layout.py
    private static readonly HashSet<string> IconNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "armor_helmet", "armor", "kevlar", "helmet", "healthshot", "defuser", "flashbang", "smokegrenade", "hegrenade",
        "molotov", "decoy", "grenadepack", "taser", "ak47", "awp", "deagle", "knife", "ammobox", "c4", "planted_c4",
        "assaultsuit", "customplayer", "health", "shield", "fast", "exojump", "parachute", "dollar_sign", "coin_stack",
        "star", "gift", "random", "clantag", "zoom_in", "camera", "bullet", "bullet_burst", "kill", "kill_headshot",
        "refresh", "undo", "timer", "hourglass", "stats", "key", "teamplayer", "colorwheel", "loadout", "inventory",
        "xp_rank", "sound_3", "crosshair", "bomb", "buyzone", "shoppingcart", "leader", "trophy", "settings", "clock",
        "lock", "check", "warning",
    };

    private sealed class PaintView
    {
        public Shown Shown = null!;
        public string Title = "";
        public bool IsMain;
        public Action<CCSPlayerController>? Back;
        public List<Entry> Entries = new();
    }

    private sealed class PaintSlot
    {
        public PaintView? View;
        public bool Open;
        public int Token;
        public int Page;
        public int Pick = -1;          // entry index whose value picker is shown
        public int PickPage;
        public bool Captured;
        public int ToastToken;
        public readonly Dictionary<string, int> Pages = new(StringComparer.Ordinal);   // title -> page
        public readonly Dictionary<string, bool> Cls = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Vars = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> OneHot = new(StringComparer.Ordinal);

        public void Forget()
        {
            Cls.Clear();
            Vars.Clear();
            OneHot.Clear();
            Captured = false;
        }
    }

    private readonly ConditionalWeakTable<IMenu, PaintView> _views = new();
    private readonly PaintSlot[] _paint = Enumerable.Range(0, 65).Select(_ => new PaintSlot()).ToArray();
    private static readonly PluginCapability<IPmmPaintRegistry?> PaintCapability = new("pmm:paint");
    private IPmmPaintRegistry? _registry;
    private CCSCustomHudLayout? _vipLayout;
    private bool _layoutWarned;

    private PaintTexts Tx => Config.Paint.Texts;

    // ---------------- lifecycle ----------------

    private void PaintLoad()
    {
        RegisterListener<Listeners.OnCustomHudClicked>(OnVipClick);
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _vipLayout = null;
            foreach (var ps in _paint)
            {
                ps.Open = false;
                ps.View = null;
                ps.Forget();
            }
        });
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            if (slot is < 0 or >= 65) return;
            var ps = _paint[slot];
            ps.Open = false;
            ps.View = null;
            ps.Token++;
            ps.Pages.Clear();
        });
        // Reconnect into the same slot keeps the previous player's per-player state: hide it again.
        RegisterListener<Listeners.OnClientPutInServer>(slot =>
        {
            if (slot is < 0 or >= 65) return;
            _paint[slot].Forget();
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null && p.IsValid && _vipLayout is { IsValid: true })
            {
                Cls(p, RootId, "vip-hidden", true);
                _vipLayout.SetInputCaptureEnabled(p, false);
            }
        });
        // Round restart may remove the entity: forget open menus so MenuManager does not think they are shown.
        AddTimer(0.5f, () =>
        {
            if (_vipLayout is { IsValid: true }) return;
            if (_vipLayout != null) _vipLayout = null;
            foreach (var ps in _paint)
                if (ps.Open)
                {
                    ps.Open = false;
                    ps.View = null;
                    ps.Forget();
                }
        }, TimerFlags.REPEAT);
    }

    private void RegisterPainter()
    {
        if (_registry != null || !Config.Paint.Enabled) return;
        try { _registry = PaintCapability.Get(); } catch { _registry = null; }
        if (_registry == null)
        {
            Logger.LogWarning("MenuManager has no pmm:paint (needs 1.2.03 PMM build): VIP menus use the standard panorama list");
            return;
        }
        _registry.Add(this);
        Logger.LogInformation("VIP panorama registered ({Layout})", Config.Paint.LayoutPath);
    }

    private void UnregisterPainter()
    {
        try { _registry?.Remove(this); } catch { }
        _registry = null;
    }

    // ---------------- IPmmMenuPainter ----------------

    public bool TryPaint(CCSPlayerController player, IMenu menu)
    {
        if (!player.IsValid || player.IsBot || player.Slot is < 0 or >= 65) return false;
        var ps = _paint[player.Slot];
        if (!Config.Paint.Enabled || !Active || !_views.TryGetValue(menu, out var view))
        {
            // Another plugin's menu replaces ours.
            if (ps.Open) Hide(player);
            return false;
        }
        if (!EnsureVipLayout())
        {
            if (!_layoutWarned) Logger.LogWarning("custom_hud_layout {Path} was not created, VIP menus use the standard list", Config.Paint.LayoutPath);
            _layoutWarned = true;
            return false;
        }

        bool wasOpen = ps.Open;
        if (ps.View != null) ps.Pages[ps.View.Title] = ps.Page;
        ps.View = view;
        ps.Pick = -1;
        ps.PickPage = 0;
        ps.Page = ps.Pages.TryGetValue(view.Title, out int page) ? page : 0;
        ps.Open = true;
        int token = ++ps.Token;

        // Never draw in the frame of the chat command (!vip): CS2 then kicks with NETWORK_DISCONNECT.
        Later(wasOpen ? 0f : Config.Paint.OpenDelay, () =>
        {
            if (ps.Token != token || !ps.Open || !player.IsValid) return;
            Draw(player);
            if (ps.Captured) return;
            Later(Config.Paint.InputDelay, () =>
            {
                if (ps.Token == token && ps.Open && player.IsValid) Capture(player, true);
            });
        });
        return true;
    }

    public void Close(CCSPlayerController player)
    {
        if (player.IsValid && player.Slot is >= 0 and < 65 && _paint[player.Slot].Open) Hide(player);
    }

    public bool IsOpen(CCSPlayerController player)
    {
        return player.IsValid && player.Slot is >= 0 and < 65 && _paint[player.Slot].Open;
    }

    // ---------------- clicks ----------------

    private void OnVipClick(CCSPlayerController player, CCSCustomHudLayout layout, string buttonId)
    {
        if (_vipLayout == null || layout.Handle != _vipLayout.Handle || !player.IsValid || player.Slot is < 0 or >= 65) return;
        var ps = _paint[player.Slot];
        var view = ps.View;
        if (!ps.Open || view == null) return;

        try
        {
            switch (buttonId)
            {
                case "vip-close":
                    Hide(player);
                    return;
                case "vip-prev":
                case "vip-next":
                {
                    int step = buttonId == "vip-next" ? 1 : -1;
                    if (ps.Pick >= 0)
                    {
                        int pages = Pages(view.Entries[ps.Pick].Choices.Length, ChipsPerPage);
                        ps.PickPage = Math.Clamp(ps.PickPage + step, 0, pages - 1);
                    }
                    else
                    {
                        int pages = Pages(view.Entries.Count, view.IsMain ? TilesPerPage : RowsPerPage);
                        ps.Page = Math.Clamp(ps.Page + step, 0, pages - 1);
                        ps.Pages[view.Title] = ps.Page;
                    }
                    Draw(player);
                    return;
                }
                case "vip-back":
                    if (ps.Pick >= 0)
                    {
                        ps.Pick = -1;
                        Draw(player);
                        return;
                    }
                    if (view.Back != null)
                    {
                        ps.Page = 0;   // this sub menu opens on page 1 next time
                        view.Back(player);
                    }
                    return;
            }

            if (TryIndex(buttonId, "vt-", out int t) && view.IsMain && ps.Pick < 0)
            {
                ClickEntry(player, ps, view, ps.Page * TilesPerPage + t);
                return;
            }
            if (TryIndex(buttonId, "vl-", out int r) && !view.IsMain && ps.Pick < 0)
            {
                ClickEntry(player, ps, view, ps.Page * RowsPerPage + r);
                return;
            }
            if (TryIndex(buttonId, "vp-", out int c) && ps.Pick >= 0 && ps.Pick < view.Entries.Count)
            {
                var e = view.Entries[ps.Pick];
                int choice = ps.PickPage * ChipsPerPage + c;
                if (choice >= e.Choices.Length) return;
                ps.Pick = -1;
                e.Pick?.Invoke(player, choice);
                if (StillShowing(ps, view, player))
                {
                    e.Value = e.Choices[choice];
                    Draw(player);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "VIP panorama click {Button} failed", buttonId);
        }
    }

    private void ClickEntry(CCSPlayerController player, PaintSlot ps, PaintView view, int index)
    {
        if (index < 0 || index >= view.Entries.Count) return;
        var e = view.Entries[index];
        if (e.Disabled)
        {
            Toast(player, $"{e.Label} — {T(player, "no_access", Tx.NoAccess)}", true);
            return;
        }

        if (e.Kind == Kind.Select)
        {
            ps.Pick = index;
            int cur = Array.FindIndex(e.Choices, x => x.Equals(e.Value, StringComparison.OrdinalIgnoreCase));
            ps.PickPage = cur > 0 ? cur / ChipsPerPage : 0;
            Draw(player);
            return;
        }

        e.Click(player);
        if (!StillShowing(ps, view, player)) return;   // a sub menu / new !vip was painted, or the menu closed

        if (e.Kind == Kind.Toggle && e.Key != "")
            e.On = ReadState(player, e.Key) == "Enabled";
        Draw(player);
    }

    private static bool StillShowing(PaintSlot ps, PaintView view, CCSPlayerController player)
        => player.IsValid && ps.Open && ReferenceEquals(ps.View, view);

    private string ReadState(CCSPlayerController p, string key)
    {
        var user = (Get(_core, "Users") as IDictionary)?[p.SteamID];
        return (Get(user, "FeatureState") as IDictionary)?[key]?.ToString() ?? "";
    }

    // ---------------- drawing ----------------

    private void Draw(CCSPlayerController player)
    {
        var ps = _paint[player.Slot];
        var view = ps.View;
        if (view == null || _vipLayout is not { IsValid: true }) return;

        DrawHeader(player);

        string mode = ps.Pick >= 0 && ps.Pick < view.Entries.Count ? "pick" : view.IsMain ? "main" : "list";
        OneHot(player, RootId, "view", "view-" + mode);
        Cls(player, RootId, "has-back", mode == "pick" || view.Back != null);
        string pos = Config.Paint.Position.ToLowerInvariant() switch { "left" => "pos-left", "right" => "pos-right", _ => "" };
        OneHot(player, RootId, "pos", pos);
        Var(player, RootId, "vip_use", T(player, "use", Tx.Use));
        Var(player, RootId, "vip_back", T(player, "back", Tx.Back));

        int page, pages;
        switch (mode)
        {
            case "main":
                pages = Pages(view.Entries.Count, TilesPerPage);
                page = ps.Page = Math.Clamp(ps.Page, 0, pages - 1);
                DrawTiles(player, view.Entries, page);
                break;
            case "pick":
            {
                var e = view.Entries[ps.Pick];
                pages = Pages(e.Choices.Length, ChipsPerPage);
                page = ps.PickPage = Math.Clamp(ps.PickPage, 0, pages - 1);
                Var(player, RootId, "vip_sub_title", e.Label);
                Var(player, RootId, "vip_sub_hint", T(player, "pick_hint", Tx.PickHint));
                OneHot(player, "vip-sub-ic", "icon", "ic-" + Icon(e.Key));
                DrawChips(player, e, page);
                break;
            }
            default:
                pages = Pages(view.Entries.Count, RowsPerPage);
                page = ps.Page = Math.Clamp(ps.Page, 0, pages - 1);
                Var(player, RootId, "vip_sub_title", view.Title);
                Var(player, RootId, "vip_sub_hint", T(player, "list_hint", Tx.ListHint));
                DrawRows(player, view.Entries, page);
                break;
        }

        Cls(player, RootId, "has-pages", pages > 1);
        Var(player, RootId, "vip_page", Fmt(T(player, "page", Tx.Page), page + 1, pages));
        for (int i = 0; i < Dots; i++)
        {
            Cls(player, "vip-dot-" + i, "slot-off", i >= pages);
            Cls(player, "vip-dot-" + i, "a", i == page);
        }

        Cls(player, RootId, "vip-hidden", false);
    }

    private void DrawHeader(CCSPlayerController player)
    {
        var user = (Get(_core, "Users") as IDictionary)?[player.SteamID];
        string group = Get(user, "group") as string ?? "";
        long expires = Get(user, "expires") switch { int i => i, long l => l, _ => 0 };

        Var(player, RootId, "vip_brand", T(player, "brand", Tx.Brand));
        Var(player, RootId, "vip_nick", player.PlayerName ?? "");
        Var(player, RootId, "vip_group", group);
        OneHot(player, RootId, "pal", "grp-" + Palette(group));

        if (expires <= 0)
        {
            Var(player, RootId, "vip_exp_pre", T(player, "forever", Tx.Forever));
            Var(player, RootId, "vip_exp_date", "");
            Var(player, RootId, "vip_exp_left", "");
            return;
        }
        var until = DateTimeOffset.FromUnixTimeSeconds(expires);
        long days = (long)Math.Ceiling((until - DateTimeOffset.UtcNow).TotalDays);
        string date;
        try { date = until.ToLocalTime().ToString(Config.Paint.DateFormat); }
        catch (FormatException) { date = until.ToLocalTime().ToString("dd.MM.yyyy"); }
        Var(player, RootId, "vip_exp_pre", T(player, "until", Tx.Until));
        Var(player, RootId, "vip_exp_date", date);
        Var(player, RootId, "vip_exp_left", days > 0 ? Fmt(T(player, "days_left", Tx.DaysLeft), days) : "");
    }

    private void DrawTiles(CCSPlayerController player, List<Entry> entries, int page)
    {
        int first = page * TilesPerPage;
        int onPage = Math.Max(0, Math.Min(TilesPerPage, entries.Count - first));
        for (int r = 0; r < TilesPerPage / Cols; r++)
            Cls(player, "vip-gr-" + r, "slot-off", r * Cols >= onPage);

        for (int i = 0; i < TilesPerPage; i++)
        {
            string id = "vt-" + i;
            if (i >= onPage)
            {
                Cls(player, id, "slot-off", true);
                continue;
            }
            var e = entries[first + i];
            string kind = e.Kind switch
            {
                Kind.Toggle => "k-toggle",
                Kind.Select => "k-select",
                Kind.Action when e.Key != "" => "k-action",
                _ => "k-plain",
            };
            string desc = e.Disabled ? T(player, "no_access", Tx.NoAccess)
                : e.Desc != "" ? e.Desc
                : e.Kind == Kind.Toggle ? (e.On ? T(player, "enabled", Tx.Enabled) : T(player, "disabled", Tx.Disabled))
                : "";

            Cls(player, id, "slot-off", false);
            OneHot(player, id, "kind", kind);
            Cls(player, id, "on", e.Kind == Kind.Toggle && e.On && !e.Disabled);
            Cls(player, id, "locked", e.Disabled);
            OneHot(player, "vt-ic-" + i, "icon", "ic-" + Icon(e.Key));
            Var(player, id, "vtn" + i, e.Label);
            Var(player, id, "vts" + i, desc);
            Var(player, id, "vtv" + i, e.Value);
        }
    }

    private void DrawChips(CCSPlayerController player, Entry e, int page)
    {
        int first = page * ChipsPerPage;
        int onPage = Math.Max(0, Math.Min(ChipsPerPage, e.Choices.Length - first));
        for (int r = 0; r < ChipsPerPage / 4; r++)
            Cls(player, "vip-cr-" + r, "slot-off", r * 4 >= onPage);
        for (int i = 0; i < ChipsPerPage; i++)
        {
            string id = "vp-" + i;
            if (i >= onPage)
            {
                Cls(player, id, "slot-off", true);
                continue;
            }
            string choice = e.Choices[first + i];
            Cls(player, id, "slot-off", false);
            Cls(player, id, "sel", choice.Equals(e.Value, StringComparison.OrdinalIgnoreCase));
            Var(player, id, "vpc" + i, choice);
        }
    }

    private void DrawRows(CCSPlayerController player, List<Entry> entries, int page)
    {
        int first = page * RowsPerPage;
        int onPage = Math.Max(0, Math.Min(RowsPerPage, entries.Count - first));
        for (int i = 0; i < RowsPerPage; i++)
        {
            string id = "vl-" + i;
            if (i >= onPage)
            {
                Cls(player, id, "slot-off", true);
                continue;
            }
            var e = entries[first + i];
            Cls(player, id, "slot-off", false);
            Cls(player, id, "dis", e.Disabled);
            Var(player, id, "vlr" + i, e.Label);
        }
    }

    private void Hide(CCSPlayerController player)
    {
        if (player.Slot is < 0 or >= 65) return;
        var ps = _paint[player.Slot];
        if (ps.View != null) ps.Pages[ps.View.Title] = ps.Page;
        ps.Open = false;
        ps.View = null;
        ps.Pick = -1;
        ps.Token++;
        if (!player.IsValid || _vipLayout is not { IsValid: true }) return;
        Capture(player, false);
        Cls(player, RootId, "toast-on", false);
        Cls(player, RootId, "vip-hidden", true);
    }

    // Short status line in the footer (toggle result, "no access").
    private bool Toast(CCSPlayerController player, string text, bool warn)
    {
        if (!player.IsValid || player.Slot is < 0 or >= 65) return false;
        var ps = _paint[player.Slot];
        if (!ps.Open || _vipLayout is not { IsValid: true }) return false;
        int token = ++ps.ToastToken;
        Var(player, RootId, "vip_toast", text);
        Cls(player, RootId, "toast-warn", warn);
        Cls(player, RootId, "toast-on", true);
        Later(Math.Max(0.5f, Config.Paint.ToastSeconds), () =>
        {
            if (ps.ToastToken == token && player.IsValid && _vipLayout is { IsValid: true })
                Cls(player, RootId, "toast-on", false);
        });
        return true;
    }

    private void Capture(CCSPlayerController player, bool on)
    {
        var ps = _paint[player.Slot];
        if (_vipLayout is not { IsValid: true } || !player.IsValid) return;
        if (ps.Captured == on) return;
        ps.Captured = on;
        _vipLayout.SetInputCaptureEnabled(player, on);
    }

    private bool EnsureVipLayout()
    {
        if (_vipLayout is { IsValid: true }) return true;
        _vipLayout = null;
        var created = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
        if (created == null || created.Handle == IntPtr.Zero) return false;
        created.StrLayout = Config.Paint.LayoutPath;
        created.DispatchSpawn();
        if (!created.IsValid) return false;
        _vipLayout = created;
        _layoutWarned = false;
        foreach (var ps in _paint) ps.Forget();
        return true;
    }

    // ---------------- sends (cached per player) ----------------

    private void Cls(CCSPlayerController player, string panel, string cls, bool on)
    {
        var layout = _vipLayout;
        if (layout is not { IsValid: true } || !player.IsValid) return;
        var ps = _paint[player.Slot];
        string key = panel + "\u0001" + cls;
        if (ps.Cls.TryGetValue(key, out bool cur) && cur == on) return;
        ps.Cls[key] = on;
        layout.SetHasClassForPlayer(player, panel, cls, on);
    }

    private void Var(CCSPlayerController player, string panel, string name, string value)
    {
        var layout = _vipLayout;
        if (layout is not { IsValid: true } || !player.IsValid) return;
        var ps = _paint[player.Slot];
        string key = panel + "\u0001" + name;
        if (ps.Vars.TryGetValue(key, out var cur) && cur == value) return;
        ps.Vars[key] = value;
        layout.SetDialogVariableStringForPlayer(player, panel, name, value);
    }

    // One class out of a group (view-*, grp-*, ic-*, k-*): the previous one is removed.
    // Classes authored in pmm_vip.xml count as already set.
    private void OneHot(CCSPlayerController player, string panel, string group, string cls)
    {
        var ps = _paint[player.Slot];
        string key = panel + "\u0001" + group;
        if (!ps.OneHot.TryGetValue(key, out var prev))
        {
            // First send for this panel: clear the whole group. A reconnect into the same slot keeps the
            // previous player's per-player classes, so "authored" is not guaranteed. Icons are too many to clear.
            prev = Authored(panel, group);
            if (GroupClasses(group) is { } all)
                foreach (var other in all)
                    if (other != cls) Cls(player, panel, other, false);
        }
        else if (prev == cls) return;
        if (!string.IsNullOrEmpty(prev) && prev != cls) Cls(player, panel, prev, false);
        if (!string.IsNullOrEmpty(cls)) Cls(player, panel, cls, true);
        ps.OneHot[key] = cls;
    }

    private string[]? GroupClasses(string group) => group switch
    {
        "view" => new[] { "view-main", "view-pick", "view-list" },
        "pal" => PaletteNames().Select(p => "grp-" + p).ToArray(),
        "kind" => new[] { "k-toggle", "k-select", "k-action", "k-plain" },
        "pos" => new[] { "pos-left", "pos-right" },
        _ => null,
    };

    // Palettes of Paint.Theme (the classes exist only if pmm_vip_theme.css in the addon is up to date).
    private IEnumerable<string> PaletteNames() =>
        Config.Paint.Theme.Palettes.Keys.Select(k => k.ToLowerInvariant()).Where(ThemeCss.IsName);

    // Panel text: translation file (pmm_vip.<key>, player's language) -> Paint.Texts.
    private string T(CCSPlayerController player, string key, string fallback)
        => _tr?.Get(VipTranslations.Culture(player), "pmm_vip." + key) ?? fallback;

    private static string Authored(string panel, string group) => group switch
    {
        "view" => "view-main",
        "pal" => "grp-gold",
        "kind" => "k-toggle",
        "icon" => "ic-star",
        _ => "",
    };

    // ---------------- helpers ----------------

    private string Palette(string group)
    {
        var names = PaletteNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Config.Paint.GroupPalette.TryGetValue(group, out var p) && names.Contains(p)) return p.ToLowerInvariant();
        foreach (var name in names.OrderByDescending(n => n.Length))
            if (group.Contains(name, StringComparison.OrdinalIgnoreCase)) return name;
        if (names.Contains(Config.Paint.DefaultPalette)) return Config.Paint.DefaultPalette.ToLowerInvariant();
        return names.FirstOrDefault() ?? "gold";
    }

    private string Icon(string key)
    {
        if (key != "" && Config.Paint.Icons.TryGetValue(key, out var icon) && IconNames.Contains(icon)) return icon.ToLowerInvariant();
        return IconNames.Contains(Config.Paint.DefaultIcon) ? Config.Paint.DefaultIcon.ToLowerInvariant() : "star";
    }

    private static int Pages(int count, int per) => Math.Max(1, (count + per - 1) / per);

    private static bool TryIndex(string id, string prefix, out int index)
    {
        index = -1;
        return id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id.AsSpan(prefix.Length), out index) && index >= 0;
    }

    private static string Fmt(string format, params object[] args)
    {
        try { return string.Format(format, args); }
        catch (FormatException) { return format; }
    }

    private void Later(float delay, Action action)
    {
        if (delay <= 0f) Server.NextFrame(action);
        else AddTimer(delay, action);
    }
}
