using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace PMM_VIPCore;

// Menus of VIP_WeaponsMenu (daffyy): List = "pick a set", Save = "save this set?", Settings = our !swm view.
internal enum WmKind { None, List, Save, Settings, Mode }

// How the player drives the WeaponsMenu panel.
internal enum WmInput { Mouse, Wasd, Digits }

// Source object of the settings view: its rows are built by the bridge (BuildWmSettings), Open is never used.
internal sealed class WmSettingsMenu : BaseMenu
{
    public WmSettingsMenu(string title) : base(title) { }
    public override void Open(CCSPlayerController player) { }
}

// VIP_WeaponsMenu without rebuilding it:
//  - its menus go to the compact VIP panel with a gear (settings) in the header;
//  - per player (SteamID, weaponsmenu_players.json): ask to save every time, show the list every round, input mode;
//  - !swm: settings, "pick a set now", "save the last set", "delete the saved set";
//  - mouse / WASD (OnTick buttons) / number keys (PMM Core 1.2.05 NumberKeyPressed).
// The module itself is reached by reflection: VIPCore Features[WeaponsMenu].OnSelectItem.Target is its WeaponsMenu
// object with CreateMenu(player), GetPlayerSelection(player, out sel), RemovePlayerSelection(player).
public partial class Plugin
{
    private const int WmRows = 6;
    private const string WmPrefsFile = "weaponsmenu_players.json";

    public sealed class WmPrefs
    {
        public bool AskSave { get; set; } = true;
        public bool EveryRound { get; set; } = true;
        public string Mode { get; set; } = "";   // "" = WeaponsMenu.DefaultMode
    }

    private sealed class WmState
    {
        public bool Asked;              // the "save this set?" prompt was shown on this map
        public bool Hinted;             // chat hint about !swm was printed on this map
        public bool Manual;             // "Pick a set now" is running: show the list although EveryRound is off
        public bool Checking;           // number keys check of MenuManager is open
        public ChatMenuOption? Yes;     // "Yes" of the last save prompt: saves the set taken last
        public string LastPick = "";
        public string PendingName = "";
    }

    private readonly Dictionary<ulong, WmPrefs> _wmPrefs = new();
    private readonly Dictionary<int, WmState> _wm = new();
    private readonly Dictionary<string, string> _textKeys = new(StringComparer.Ordinal);
    private bool _digitsApi;
    private static readonly Regex RoundPrefix = new(@"^\s*\[[^\]]*\]\s*", RegexOptions.Compiled);

    private WeaponsMenuOptions Wmo => Config.WeaponsMenu;

    // ---------------- lifecycle ----------------

    private void WmLoad()
    {
        WmLoadPrefs();
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            foreach (var w in _wm.Values)
            {
                w.Asked = false;
                w.Hinted = false;
                w.Yes = null;
                w.LastPick = "";
                w.PendingName = "";
            }
        });
        RegisterListener<Listeners.OnClientDisconnect>(slot => _wm.Remove(slot));
        RegisterListener<Listeners.OnTick>(WmKeysTick);

        if (!Wmo.Enabled) return;
        foreach (var cmd in Wmo.Commands.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            AddCommand(cmd, "VIP WeaponsMenu settings (PMM_VIPCore)", OnWmCommand);
    }

    private WmState WS(int slot)
    {
        if (!_wm.TryGetValue(slot, out var w)) _wm[slot] = w = new WmState();
        return w;
    }

    // ---------------- module texts -> keys ----------------

    // GetTranslatedText result -> key: the module builds its menus with these titles.
    private void RememberTextKey(string? text, string key)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (_textKeys.Count > 512) _textKeys.Clear();
        _textKeys[text] = key;
    }

    private string? TextKey(string? text) => text != null && _textKeys.TryGetValue(text, out var k) ? k : null;

    // ---------------- which menu is it ----------------

    private WmKind WmClassify(IMenu src, string? owner)
    {
        if (!Wmo.Enabled) return WmKind.None;
        switch (TextKey(src.Title))
        {
            case "weaponsmenu.title": return WmKind.List;
            case "weaponsmenu.wanttosave": return WmKind.Save;
        }
        if (owner != null && owner.Equals(Wmo.Module, StringComparison.OrdinalIgnoreCase))
            return src.PostSelectAction == PostSelectAction.Close ? WmKind.Save : WmKind.List;
        return WmKind.None;
    }

    // false = the menu is not shown (player settings).
    private bool WmAllowShow(CCSPlayerController player, IMenu src, WmKind kind)
    {
        var ws = WS(player.Slot);
        var prefs = Prefs(player);
        switch (kind)
        {
            case WmKind.List:
                if (ws.Manual || prefs.EveryRound) return true;
                if (Config.Debug) Logger.LogInformation("[{Slot}] WeaponsMenu list skipped (EveryRound off)", player.Slot);
                WmHint(player, ws);
                return false;

            case WmKind.Save:
                // Kept for "Save set «X»" in the settings, also when the prompt is not shown.
                ws.Yes = src.MenuOptions.FirstOrDefault();
                ws.PendingName = RoundPrefix.Replace(ws.LastPick, "").Trim();
                if (!prefs.AskSave && ws.Asked)
                {
                    if (Config.Debug) Logger.LogInformation("[{Slot}] WeaponsMenu save prompt skipped (asked on this map)", player.Slot);
                    WmHint(player, ws);
                    return false;
                }
                ws.Asked = true;
                return true;
        }
        return true;
    }

    // The weapons list: remember which set was taken (name for "Save set «X»").
    private void WmWrapPicks(CCSPlayerController player, List<Entry> entries)
    {
        foreach (var e in entries)
        {
            var click = e.Click;
            string label = e.Label;
            e.Click = p =>
            {
                WS(p.Slot).LastPick = label;
                click(p);
            };
        }
    }

    private void WmHint(CCSPlayerController p, WmState ws)
    {
        if (ws.Hinted || !p.IsValid) return;
        ws.Hinted = true;
        WmChat(p, Fmt(T(p, "wm.hint", "{gold}[VIP]{default} Weapons menu and saving a set: {green}{0}"), WmCommandName()));
    }

    // ---------------- !swm ----------------

    private void OnWmCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid || caller.IsBot) return;
        if (!Active || !Wmo.Enabled || _api == null) return;
        if (!WmHasAccess(caller))
        {
            WmChat(caller, T(caller, "wm.no_access", "{red}[VIP]{default} The weapons menu is not available to you"));
            return;
        }
        WmOpenSettings(caller, null);
    }

    private void WmOpenSettings(CCSPlayerController p, Shown? parent)
    {
        if (!p.IsValid) return;
        var sh = new Shown
        {
            Source = new WmSettingsMenu(T(p, "wm.settings_title", "Weapons menu settings")) { ExitButton = true },
            Wm = WmKind.Settings,
            Parent = parent,
        };
        Render(p, sh);
    }

    private List<Entry> BuildWmSettings(CCSPlayerController player, Shown sh)
    {
        var ws = WS(player.Slot);
        var prefs = Prefs(player);
        bool saved = WmHasSaved(player);
        var list = new List<Entry>();

        var ask = new Entry { Kind = Kind.Toggle, Label = T(player, "wm.ask_each_time", "Ask every time whether to save the set?"), On = prefs.AskSave };
        ask.Click = p =>
        {
            var x = Prefs(p);
            x.AskSave = !x.AskSave;
            SetPrefs(p, x);
            ask.On = x.AskSave;
        };
        list.Add(ask);

        var every = new Entry { Kind = Kind.Toggle, Label = T(player, "wm.auto_menu", "Open the weapon set menu automatically"), On = prefs.EveryRound };
        every.Click = p =>
        {
            var x = Prefs(p);
            x.EveryRound = !x.EveryRound;
            SetPrefs(p, x);
            every.On = x.EveryRound;
        };
        list.Add(every);

        list.Add(new Entry
        {
            Kind = Kind.Action,
            Label = T(player, "wm.open_now", "Pick a set now"),
            // A saved set is given on spawn by the module itself; the list would only give it again.
            Disabled = saved,
            Desc = T(player, "wm.saved_auto", "A set is saved and given automatically. Delete it to pick another one"),
            Click = p =>
            {
                if (sh.Parent is { Wm: WmKind.List } back) Reopen(p, back);
                else WmOpenNow(p);
            },
        });

        list.Add(new Entry
        {
            Kind = Kind.Action,
            Label = ws.PendingName != ""
                ? Fmt(T(player, "wm.save_last", "Save set «{0}»"), ws.PendingName)
                : T(player, "wm.save", "Save the set"),
            Disabled = saved || ws.Yes == null,
            Desc = saved
                ? T(player, "wm.already_saved", "A set is already saved")
                : T(player, "wm.nothing_to_save", "Take a set from the weapons menu first"),
            Click = p =>
            {
                var yes = WS(p.Slot).Yes;
                if (yes == null) return;
                try { yes.OnSelect(p, yes); }
                catch (Exception e)
                {
                    if (!WarnApiMismatch(e)) Logger.LogError(e, "WeaponsMenu: saving the set failed");
                }
                Render(p, sh);
                Toast(p, T(p, "wm.saved", "Set saved: it is given automatically"), false);
            },
        });

        list.Add(new Entry
        {
            Kind = Kind.Action,
            Label = T(player, "wm.forget", "Delete the saved set"),
            Disabled = !saved,
            Desc = T(player, "wm.nothing_saved", "No saved set"),
            Click = p =>
            {
                WmForget(p);
                Render(p, sh);
                Toast(p, T(p, "wm.forgot", "Saved set deleted"), false);
            },
        });

        list.Add(new Entry
        {
            Kind = Kind.Action,
            Label = T(player, "wm.mode", "Menu control"),
            Value = WmModeName(player, WmModeOf(player)),
            Click = p => WmOpenModes(p, sh),
        });
        return list;
    }

    // "Pick a set now": the module's own CreateMenu (it checks alive / buy zone / buy time / round itself).
    private void WmOpenNow(CCSPlayerController p)
    {
        var target = WmFeature();
        var create = target?.GetType().GetMethod("CreateMenu", Any, null, new[] { typeof(CCSPlayerController) }, null);
        if (target == null || create == null)
        {
            WmSay(p, T(p, "wm.unavailable", "VIP_WeaponsMenu is not loaded"), true);
            return;
        }
        var st = State(p.Slot);
        var ws = WS(p.Slot);
        int before = st.ShowCount;
        ws.Manual = true;
        try { create.Invoke(target, new object[] { p }); }
        catch (Exception e)
        {
            if (!WarnApiMismatch(e)) Logger.LogError(e is TargetInvocationException { InnerException: { } inner } ? inner : e, "WeaponsMenu.CreateMenu failed");
        }
        finally { ws.Manual = false; }

        if (st.ShowCount == before && p.IsValid)
            WmSay(p, T(p, "wm.not_now", "You cannot take a set now (dead, out of the buy zone, buy time is over or no set yet)"), true);
    }

    // "Menu control" -> list of the three modes (as the mode list of PMM_GG1MapChooser): a pick sets it at once.
    private void WmOpenModes(CCSPlayerController p, Shown settings)
    {
        if (!p.IsValid) return;
        Render(p, new Shown
        {
            Source = new WmSettingsMenu(T(p, "wm.mode_title", "Menu control")) { ExitButton = true },
            Wm = WmKind.Mode,
            Parent = settings,
        });
    }

    private List<Entry> BuildWmModes(CCSPlayerController player, Shown sh)
    {
        var cur = WmModeOf(player);
        var list = new List<Entry>();
        foreach (var mode in new[] { WmInput.Mouse, WmInput.Wasd, WmInput.Digits })
        {
            var m = mode;
            var e = new Entry { Label = WmModeName(player, m), Kind = Kind.Toggle, On = m == cur };
            if (m == WmInput.Digits && !DigitsReady(player))
            {
                // Not a switch yet: the first pick runs the number keys check.
                e.Kind = Kind.Action;
                e.Disabled = !_digitsApi;
                e.Value = _digitsApi ? T(player, "wm.mode_need_check", "keys check") : "";
                e.Desc = T(player, "wm.mode_need_core", "Needs PMM Core 1.2.05 on the server");
            }
            e.Click = p => WmPickMode(p, sh, m);
            list.Add(e);
        }
        return list;
    }

    private void WmPickMode(CCSPlayerController p, Shown sh, WmInput mode)
    {
        var back = sh.Parent ?? sh;
        if (mode == WmInput.Digits && !DigitsReady(p))
        {
            if (!_digitsApi) return;
            // Number keys must be bound first: MenuManager's check panel replaces ours until it is done.
            var ws = WS(p.Slot);
            ws.Checking = true;
            CloseFor(p);
            WmChat(p, T(p, "wm.keys_check", "{gold}[VIP]{default} Press 1 … 0 one by one. The bind line is in the console"));
            try
            {
                OpenKeysCheck(_api!, p, (pl, ok) => Server.NextFrame(() => WmKeysCheckDone(pl, ok, back)));
            }
            catch (Exception e)
            {
                ws.Checking = false;
                Logger.LogWarning("OpenNumberKeysCheck failed: {Msg}", e.Message);
                Render(p, sh);
                Toast(p, T(p, "wm.keys_fail", "Number keys check failed, mode not changed"), true);
            }
            return;
        }

        WmSetMode(p, mode);
        Render(p, back);
        Toast(p, Fmt(T(p, "wm.mode_set", "Menu control: {0}"), WmModeName(p, mode)), false);
    }

    private void WmKeysCheckDone(CCSPlayerController p, bool ok, Shown sh)
    {
        if (p == null || !p.IsValid) return;
        WS(p.Slot).Checking = false;
        if (ok) WmSetMode(p, WmInput.Digits);
        Render(p, sh);
        if (ok) Toast(p, Fmt(T(p, "wm.mode_set", "Menu control: {0}"), WmModeName(p, WmInput.Digits)), false);
        else Toast(p, T(p, "wm.keys_fail", "Number keys check failed, mode not changed"), true);
    }

    // ---------------- module access (reflection) ----------------

    private object? WmCore()
    {
        if (_core != null) return _core;
        // vipcore:core is registered as PluginCapability<IVipCoreApi> (providers are per generic type),
        // so the capability is built for VIPCore's own IVipCoreApi type.
        try
        {
            var iapi = _vip?.GetType("VIPCore.VipCoreApi")?.GetInterfaces().FirstOrDefault(i => i.Name == "IVipCoreApi");
            if (iapi == null) return null;
            var cap = Activator.CreateInstance(typeof(PluginCapability<>).MakeGenericType(iapi), "vipcore:core");
            var api = cap?.GetType().GetMethod("Get")?.Invoke(cap, null);
            _core = Get(api, "_vipCore");
        }
        catch (Exception e)
        {
            if (Config.Debug) Logger.LogInformation("vipcore:core not available: {Msg}", e.Message);
        }
        return _core;
    }

    private object? WmFeature()
    {
        var f = (Get(WmCore(), "Features") as IDictionary)?[Wmo.Feature];
        return (Get(f, "OnSelectItem") as Delegate)?.Target;
    }

    private bool WmHasAccess(CCSPlayerController p)
    {
        var api = Get(WmCore(), "VipApi");
        if (api == null) return false;
        try
        {
            var isVip = api.GetType().GetMethod("IsClientVip", Any, null, new[] { typeof(CCSPlayerController) }, null);
            var has = api.GetType().GetMethod("PlayerHasFeature", Any, null, new[] { typeof(CCSPlayerController), typeof(string) }, null);
            if (isVip != null && isVip.Invoke(api, new object[] { p }) is false) return false;
            return has == null || has.Invoke(api, new object[] { p, Wmo.Feature }) is true;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "VIP access check failed");
            return false;
        }
    }

    private bool WmHasSaved(CCSPlayerController p)
    {
        var target = WmFeature();
        var m = target?.GetType().GetMethod("GetPlayerSelection", Any);
        if (m == null) return false;
        try
        {
            var args = new object?[] { p, null };
            return m.Invoke(target, args) is true && args[1] != null;
        }
        catch { return false; }
    }

    private void WmForget(CCSPlayerController p)
    {
        var target = WmFeature();
        var m = target?.GetType().GetMethod("RemovePlayerSelection", Any, null, new[] { typeof(CCSPlayerController) }, null);
        try { m?.Invoke(target, new object[] { p }); }
        catch (Exception e) { Logger.LogError(e, "WeaponsMenu.RemovePlayerSelection failed"); }
    }

    // ---------------- prefs ----------------

    private string WmPrefsPath => Path.Combine(ModuleDirectory, WmPrefsFile);

    private void WmLoadPrefs()
    {
        try
        {
            if (!File.Exists(WmPrefsPath)) return;
            var d = JsonSerializer.Deserialize<Dictionary<string, WmPrefs>>(File.ReadAllText(WmPrefsPath));
            if (d == null) return;
            foreach (var (k, v) in d)
                if (ulong.TryParse(k, out var id) && v != null) _wmPrefs[id] = v;
        }
        catch (Exception e)
        {
            Logger.LogWarning("{File} could not be read: {Msg}", WmPrefsPath, e.Message);
        }
    }

    private void WmSavePrefs()
    {
        try
        {
            var d = _wmPrefs.ToDictionary(x => x.Key.ToString(), x => x.Value);
            File.WriteAllText(WmPrefsPath, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            Logger.LogWarning("{File} could not be written: {Msg}", WmPrefsPath, e.Message);
        }
    }

    // Copy: a player without own settings gets the config defaults; stored only on the first change.
    private WmPrefs Prefs(CCSPlayerController p)
    {
        if (_wmPrefs.TryGetValue(p.SteamID, out var x)) return x;
        return new WmPrefs { AskSave = Wmo.DefaultAskSave, EveryRound = Wmo.DefaultEveryRound, Mode = "" };
    }

    private void SetPrefs(CCSPlayerController p, WmPrefs x)
    {
        _wmPrefs[p.SteamID] = x;
        WmSavePrefs();
    }

    private void WmSetMode(CCSPlayerController p, WmInput mode)
    {
        var x = Prefs(p);
        x.Mode = mode.ToString();
        SetPrefs(p, x);
    }

    // ---------------- input mode ----------------

    private WmInput WmModeOf(CCSPlayerController p)
    {
        string raw = Prefs(p).Mode;
        if (string.IsNullOrWhiteSpace(raw)) raw = Wmo.DefaultMode;
        var m = ParseWmMode(raw) ?? WmAuto(p);
        if (m == WmInput.Digits && !DigitsReady(p)) m = WmInput.Mouse;
        return m;
    }

    // Auto: the player's !menu choice.
    private WmInput WmAuto(CCSPlayerController p)
    {
        int sel;
        try { sel = (int)(_forceType ?? _api!.GetSelectedMenu(p)); }
        catch { return WmInput.Mouse; }
        return sel switch
        {
            (int)MenuType.PanoramaWasdMenu => WmInput.Wasd,
            (int)MenuType.CsgoMenu or 8 => WmInput.Digits,   // 8 = PanoramaDigitMenu (PMM Core 1.2.05)
            _ => WmInput.Mouse,
        };
    }

    private static WmInput? ParseWmMode(string raw)
    {
        string v = raw.Trim().Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
        return v switch
        {
            "mouse" or "click" or "panorama" or "panoramamenu" => WmInput.Mouse,
            "wasd" or "panoramawasd" or "panoramawasdmenu" => WmInput.Wasd,
            "digits" or "digit" or "numbers" or "keys" or "panoramadigit" or "panoramadigitmenu" => WmInput.Digits,
            _ => null,   // Auto
        };
    }

    private string WmModeName(CCSPlayerController p, WmInput m) => m switch
    {
        WmInput.Wasd => T(p, "wm.mode_wasd", "Panorama WASD"),
        WmInput.Digits => T(p, "wm.mode_digits", "Panorama digits"),
        _ => T(p, "wm.mode_mouse", "Panorama mouse"),
    };

    private string WmKeysHint(CCSPlayerController p, PaintView view, WmInput m)
    {
        bool set = view.Wm is WmKind.Settings or WmKind.Mode;
        return m switch
        {
            WmInput.Wasd => set
                ? T(p, "keys_wasd_set", "W/S — move · E — change · R — back")
                : T(p, "keys_wasd", "W/S — move · E — take · A/D — page · R — settings"),
            WmInput.Digits => set
                ? T(p, "keys_digits_set", "1–6 — change · 7 — back · 0 — close")
                : T(p, "keys_digits", "1–6 — take · 7 — settings · 8/9 — page · 0 — close"),
            _ => "",
        };
    }

    // ---------------- number keys (PMM Core 1.2.05) ----------------
    // Older cores do not have these members: the calls live in NoInlining helpers so the failure lands in our try/catch.

    private void WmHookDigits()
    {
        if (_api == null || _digitsApi) return;
        try
        {
            SubscribeDigits(_api, OnWmDigit);
            _digitsApi = true;
        }
        catch (Exception e)
        {
            _digitsApi = false;
            Logger.LogInformation("PMM Core without number keys API (needs 1.2.05): WeaponsMenu digits mode is off ({Msg})", e.Message);
        }
    }

    private void WmUnhookDigits()
    {
        if (_api == null || !_digitsApi) return;
        try { UnsubscribeDigits(_api, OnWmDigit); } catch { /* core already gone */ }
        _digitsApi = false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SubscribeDigits(IMenuApi api, Action<CCSPlayerController, int> h) => api.NumberKeyPressed += h;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnsubscribeDigits(IMenuApi api, Action<CCSPlayerController, int> h) => api.NumberKeyPressed -= h;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KeysChecked(IMenuApi api, CCSPlayerController p) => api.HasNumberKeysChecked(p);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OpenKeysCheck(IMenuApi api, CCSPlayerController p, Action<CCSPlayerController, bool> done) =>
        api.OpenNumberKeysCheck(p, done);

    private bool DigitsReady(CCSPlayerController p)
    {
        if (!_digitsApi || _api == null) return false;
        try { return KeysChecked(_api, p); }
        catch { return false; }
    }

    private void OnWmDigit(CCSPlayerController p, int digit)
    {
        if (p == null || !p.IsValid || p.Slot is < 0 or >= 65) return;
        var ps = _paint[p.Slot];
        if (!ps.Open || ps.View is not { Wm: not WmKind.None } || ps.Mode != WmInput.Digits) return;
        var pl = p;
        Server.NextFrame(() =>
        {
            if (pl.IsValid) WmKey(pl, digit switch
            {
                >= 1 and <= WmRows => WmAct.Row0 + (digit - 1),
                7 => WmAct.Second,
                8 => WmAct.Prev,
                9 => WmAct.Next,
                0 => WmAct.Close,
                _ => WmAct.None,
            });
        });
    }

    // ---------------- WASD ----------------

    private void WmKeysTick()
    {
        if (!Active) return;
        for (int slot = 0; slot < _paint.Length; slot++)
        {
            var ps = _paint[slot];
            if (!ps.Open || ps.View is not { Wm: not WmKind.None } || ps.Mode != WmInput.Wasd)
            {
                ps.Buttons = ulong.MaxValue;   // keys held while the panel opens do not count
                continue;
            }
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null || !p.IsValid || p.IsBot) continue;
            ulong now = (ulong)p.Buttons;
            ulong pressed = now & ~ps.Buttons;
            ps.Buttons = now;
            if (pressed == 0) continue;

            WmAct act =
                (pressed & (ulong)PlayerButtons.Forward) != 0 ? WmAct.Up :
                (pressed & (ulong)PlayerButtons.Back) != 0 ? WmAct.Down :
                (pressed & (ulong)PlayerButtons.Moveleft) != 0 ? WmAct.Prev :
                (pressed & (ulong)PlayerButtons.Moveright) != 0 ? WmAct.Next :
                (pressed & (ulong)PlayerButtons.Use) != 0 ? WmAct.Pick :
                (pressed & (ulong)PlayerButtons.Reload) != 0 ? WmAct.Second :
                WmAct.None;
            if (act == WmAct.None) continue;
            var pl = p;
            Server.NextFrame(() => { if (pl.IsValid) WmKey(pl, act); });
        }
    }

    private enum WmAct { None, Up, Down, Prev, Next, Pick, Second, Close, Row0 }

    private void WmKey(CCSPlayerController p, WmAct act)
    {
        if (act == WmAct.None || p.Slot is < 0 or >= 65) return;
        var ps = _paint[p.Slot];
        var view = ps.View;
        if (!ps.Open || view == null || view.Wm == WmKind.None || ps.Pick >= 0) return;
        try
        {
            int count = view.Entries.Count;
            int pages = Pages(count, WmRows);
            ps.Page = Math.Clamp(ps.Page, 0, pages - 1);
            int first = ps.Page * WmRows;
            int onPage = Math.Clamp(count - first, 0, WmRows);

            switch (act)
            {
                case WmAct.Up:
                case WmAct.Down:
                    if (onPage == 0) return;
                    ps.Cur = (Math.Clamp(ps.Cur, 0, onPage - 1) + (act == WmAct.Up ? onPage - 1 : 1)) % onPage;
                    Draw(p);
                    return;
                case WmAct.Prev:
                case WmAct.Next:
                {
                    int page = Math.Clamp(ps.Page + (act == WmAct.Next ? 1 : -1), 0, pages - 1);
                    if (page == ps.Page) return;
                    ps.Page = page;
                    ps.Pages[view.Title] = page;
                    ps.Cur = 0;
                    Draw(p);
                    return;
                }
                case WmAct.Pick:
                    if (onPage > 0) ClickEntry(p, ps, view, first + Math.Clamp(ps.Cur, 0, onPage - 1));
                    return;
                case WmAct.Second:
                    if (view.Wm is WmKind.List or WmKind.Save) WmOpenSettings(p, view.Shown);
                    else if (view.Back != null) view.Back(p);
                    else CloseFor(p);
                    return;
                case WmAct.Close:
                    CloseFor(p);
                    return;
                default:
                {
                    int row = act - WmAct.Row0;
                    if (row < 0 || row >= onPage) return;
                    ps.Cur = row;
                    ClickEntry(p, ps, view, first + row);
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "WeaponsMenu key {Act} failed", act);
        }
    }

    // ---------------- chat ----------------

    private string WmCommandName()
    {
        string c = Wmo.Commands.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "css_swm";
        return "!" + (c.StartsWith("css_", StringComparison.OrdinalIgnoreCase) ? c[4..] : c);
    }

    private static void WmChat(CCSPlayerController p, string text)
    {
        if (p.IsValid) p.PrintToChat(" " + CounterStrikeSharp.API.Core.Translations.StringExtensions.ReplaceColorTags(text));
    }

    // Status line of the open panel, chat otherwise.
    private void WmSay(CCSPlayerController p, string text, bool warn)
    {
        if (!Toast(p, text, warn)) WmChat(p, text);
    }
}
