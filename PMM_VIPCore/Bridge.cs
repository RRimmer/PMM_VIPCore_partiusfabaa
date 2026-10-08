using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace PMM_VIPCore;

// What VipApi.CreateMenu hands to VIPCore and its modules while the bridge is on.
// VIPCore fills it as usual (AddMenuOption, ExitButton ...); Open() goes to MenuManager.
internal sealed class VipMenu : BaseMenu
{
    public VipMenu(string title) : base(title) { }

    public override void Open(CCSPlayerController player)
    {
        if (Plugin.Instance is { Active: true } p)
        {
            p.Show(player, this);
            return;
        }

        // Bridge switched off after the menu was created: show the plain CSS chat menu.
        var m = new ChatMenu(Title) { ExitButton = ExitButton, PostSelectAction = PostSelectAction };
        foreach (var o in MenuOptions) m.AddMenuOption(o.Text, o.OnSelect, o.Disabled);
        m.Open(player);
    }
}

public partial class Plugin
{
    // Plain:  row of a sub menu.
    // Toggle: !vip toggle done by the bridge itself; MenuManager flips the switch in place.
    // Select: inline drop-down; MenuManager shows the new value in place.
    // Action: other !vip row (Respawn ...), VIPCore's own callback, menu stays as it is.
    // Legacy: VIPCore's own toggle callback (MainToggles off / layout failed); !vip is rebuilt afterwards.
    // MenuManager forgets the page on every Open (PanoramaHud.CloseMenu -> forgetPages), so the
    // bridge rebuilds !vip only when it has to.
    internal enum Kind { Plain, Toggle, Select, Action, Legacy }

    private sealed class Shown
    {
        public IMenu Source = null!;
        public bool IsMain;      // the !vip menu itself (rebuilt by VIPCore on every reopen)
        public Shown? Parent;    // menu whose row opened this one -> "Back"
    }

    private sealed class SlotState
    {
        public Shown? Current;
        public readonly Stack<Shown> Running = new();  // menus whose row callback is running right now
        public int ShowCount;
        public List<IMenu>? Capture;                    // collecting a sub menu for an inline drop-down
        public int ToggleDepth;
        public bool Rebuild;                            // in-place update not possible, reopen !vip
    }

    private object? _core;       // VIPCore.VipCore instance
    private int _mainDepth;      // > 0 while VipCore.CreateMenu(player) runs
    private int _inPmmOpen;      // > 0 while MenuManager opens our menu (it may open CSS menus itself)
    private readonly Dictionary<int, SlotState> _slots = new();

    private static readonly Regex ColorTag = new(
        @"\{(default|white|darkred|green|lightyellow|lightblue|olive|lime|red|lightpurple|purple|grey|gray|yellow|gold|silver|blue|darkblue|bluegrey|magenta|lightred|orange)\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

    // ---------------- hooks ----------------

    internal void OnCreateMenu(object vipApi, string title, ref IMenu result)
    {
        if (!Active) return;
        _core ??= Get(vipApi, "_vipCore");
        result = new VipMenu(title);
    }

    internal void EnterMain(object core)
    {
        _core = core;
        _mainDepth++;
    }

    internal void LeaveMain()
    {
        if (_mainDepth > 0) _mainDepth--;
    }

    // A plain CSS menu is about to open. true = MenuManager shows it instead.
    internal bool TryIntercept(CCSPlayerController? player, IMenu menu, BasePlugin? plugin)
    {
        if (!Active || _inPmmOpen > 0 || menu is VipMenu) return false;
        if (player == null || !player.IsValid) return false;

        var st = State(player.Slot);
        if (st.Capture != null)
        {
            st.Capture.Add(menu);
            return true;
        }

        string? owner = plugin?.GetType().Assembly.GetName().Name ?? CallerAssembly();
        if (owner == null || !Matches(owner)) return false;

        if (Config.Debug) Logger.LogInformation("[{Slot}] intercept {Type} '{Title}' from {Owner}", player.Slot, menu.GetType().Name, menu.Title, owner);
        Show(player, menu);
        return true;
    }

    // VipApi.PrintToChat during a !vip toggle -> MenuManager toast. true = chat line swallowed.
    internal bool TryNotify(CCSPlayerController? player, string? message)
    {
        if (!Active || !Config.ToggleNotify || player == null || !player.IsValid || message == null) return false;
        if (!_slots.TryGetValue(player.Slot, out var st) || st.ToggleDepth == 0) return false;
        if (!IsPanorama(player)) return false;

        string text = Clean(message, true);
        int c = text.LastIndexOf(':');
        string title = c > 0 ? text[..c].Trim() : "VIP";
        string body = c > 0 ? text[(c + 1)..].Trim() : text;

        var notice = MenuNotice.Success;
        if (_core is BasePlugin core &&
            (body.Equals(Clean(core.Localizer["chat.Disabled"].Value, true), StringComparison.OrdinalIgnoreCase) ||
             body.Equals(_tr?.Get(VipTranslations.Culture(player), "pmm_vip.disabled") ?? Config.Paint.Texts.Disabled, StringComparison.OrdinalIgnoreCase)))
            notice = MenuNotice.Warning;

        // VIP panorama open: its own status line.
        if (Toast(player, c > 0 ? $"{title}: {body}" : body, notice != MenuNotice.Success)) return true;

        _api!.Notify(player, title, body, notice);
        return true;
    }

    // ---------------- showing ----------------

    internal void Show(CCSPlayerController player, IMenu src)
    {
        if (!player.IsValid) return;
        var st = State(player.Slot);
        if (st.Capture != null)
        {
            st.Capture.Add(src);
            return;
        }

        bool main = _mainDepth > 0;
        var sh = new Shown
        {
            Source = src,
            IsMain = main,
            Parent = main ? null : st.Running.Count > 0 ? st.Running.Peek() : null,
        };
        Render(player, sh);
    }

    // One row of a VIP menu, shared by the MenuManager menu and the VIP panorama (Paint.cs).
    internal sealed class Entry
    {
        public Kind Kind;
        public string Key = "";              // VIPCore feature key ("" for rows that are not features)
        public string Label = "";            // feature name / row text
        public string Desc = "";             // value from vip.json, "Enabled" ...
        public bool Disabled;
        public bool On;                      // toggle state
        public string Value = "";            // select: current choice
        public string[] Choices = Array.Empty<string>();
        public Action<CCSPlayerController> Click = _ => { };
        public Action<CCSPlayerController, int>? Pick;
    }

    private void Render(CCSPlayerController player, Shown sh)
    {
        var st = State(player.Slot);
        st.ShowCount++;
        st.Current = sh;

        var src = sh.Source;
        MenuType type = _forceType ?? _api!.GetSelectedMenu(player);
        bool strip = Config.StripColors && type != MenuType.ChatMenu;
        bool panorama = type is MenuType.PanoramaMenu or MenuType.PanoramaWasdMenu;

        var savedCulture = _culture;
        _culture = VipTranslations.Culture(player);
        try { RenderFor(player, sh, st, src, type, strip, panorama); }
        finally { _culture = savedCulture; }
    }

    private System.Globalization.CultureInfo _culture = System.Globalization.CultureInfo.InvariantCulture;

    // Translation file (MenuManager_Modules_Translation.json): whole text, then tokens such as tag.Disable.
    private string Tr(string text) => _tr?.Apply(_culture, text) ?? text;
    private string? TrKey(string key) => _tr?.Get(_culture, key);

    private void RenderFor(CCSPlayerController player, Shown sh, SlotState st, IMenu src, MenuType type, bool strip, bool panorama)
    {
        string title = Tr(CleanTitle(src.Title ?? "", strip));
        if (sh.IsMain && TrKey("pmm_vip.title") is { } mainTitle)
        {
            string group = Get((Get(_core, "Users") as IDictionary)?[player.SteamID], "group") as string ?? "";
            try { title = string.Format(mainTitle, group); } catch (FormatException) { title = mainTitle; }
        }
        Action<CCSPlayerController>? back = Config.BackButton && sh.Parent is { } parent ? p => Reopen(p, parent) : null;
        IMenu menu = _forceType is { } ft ? _api!.GetMenuForcetype(title, ft, back) : _api!.GetMenu(title, back);
        menu.ExitButton = Config.ExitButton || src.ExitButton;

        var options = src.MenuOptions.ToList();
        List<Entry>? entries = null;
        if (sh.IsMain)
        {
            try { entries = BuildMain(player, sh, options, strip, panorama); }
            catch (Exception e)
            {
                Logger.LogError(e, "!vip layout failed, plain rows are used");
                entries = null;
            }
        }
        entries ??= options.Select(o => PlainEntry(sh, o, strip, sh.IsMain ? Kind.Legacy : Kind.Plain)).ToList();

        foreach (var e in entries)
        {
            var en = e;
            switch (en.Kind)
            {
                case Kind.Toggle:
                    _api!.AddToggle(menu, en.Label, en.On, (p, _) => en.Click(p), en.Disabled);
                    break;
                case Kind.Select:
                    _api!.AddSelect(menu, en.Label, en.Value, en.Choices, (p, _, i) => en.Pick?.Invoke(p, i), en.Disabled);
                    break;
                default:
                    menu.AddMenuOption(en.Label, (p, _) => en.Click(p), en.Disabled);
                    break;
            }
        }

        // The VIP panorama (Paint.cs) draws this MenuManager menu when MenuManager offers it to the painters.
        _views.AddOrUpdate(menu, new PaintView
        {
            Shown = sh,
            Title = title,
            IsMain = sh.IsMain,
            Back = back,
            Entries = entries,
        });

        if (Config.Debug)
            Logger.LogInformation("[{Slot}] open '{Title}' rows={Rows} main={Main} back={Back} type={Type}",
                player.Slot, title, options.Count, sh.IsMain, back != null, type);

        _inPmmOpen++;
        try { menu.Open(player); }
        finally { _inPmmOpen--; }
    }

    private Entry PlainEntry(Shown sh, ChatMenuOption o, bool strip, Kind kind) => new()
    {
        Kind = kind,
        Label = Tr(Clean(o.Text ?? "", strip)),
        Disabled = o.Disabled,
        Click = p => Run(p, sh, () => o.OnSelect(p, o), kind),
    };

    // !vip rows: Toggle features -> toggles, listed Selectable features -> drop-down, the rest as is.
    private List<Entry>? BuildMain(CCSPlayerController player, Shown sh, List<ChatMenuOption> options,
        bool strip, bool panorama)
    {
        if (_core is not BasePlugin core) return null;
        if (Get(core, "Features") is not IDictionary features || Get(core, "Users") is not IDictionary users) return null;
        var user = users[player.SteamID];
        var states = Get(user, "FeatureState") as IDictionary;
        var values = GroupValues(core, Get(user, "group") as string);

        // VIPCore row text = Localizer[key] (+ " [state]" for toggles). Longest names first.
        var keys = new List<(string Key, string Raw)>();
        foreach (var k in features.Keys)
        {
            if (k is not string key) continue;
            string raw = core.Localizer[key].Value;
            if (!string.IsNullOrEmpty(raw)) keys.Add((key, raw));
        }
        keys.Sort((a, b) => b.Raw.Length.CompareTo(a.Raw.Length));

        var list = new List<Entry>();
        foreach (var o in options)
        {
            string text = o.Text ?? "";
            var hit = keys.FirstOrDefault(x => text == x.Raw || text.StartsWith(x.Raw + " [", StringComparison.Ordinal));
            if (hit.Key == null)
            {
                list.Add(PlainEntry(sh, o, strip, Kind.Action));
                continue;
            }

            object? feature = features[hit.Key];
            string ftype = Get(feature, "FeatureType")?.ToString() ?? "";
            object? state = states?[hit.Key];
            string label = TrKey(hit.Key) ?? Tr(Clean(hit.Raw, strip));
            string key = hit.Key;
            string desc = FormatValue(key, values?[key]);

            if (ftype == "Toggle")
            {
                if (Config.MainToggles)
                {
                    list.Add(new Entry
                    {
                        Kind = Kind.Toggle,
                        Key = key,
                        Label = label,
                        Desc = desc,
                        On = state?.ToString() == "Enabled",
                        Disabled = o.Disabled,
                        Click = p => Run(p, sh, () => ToggleFeature(p, key), Kind.Toggle),
                    });
                }
                else
                {
                    var e = PlainEntry(sh, o, strip, Kind.Legacy);
                    e.Key = key;
                    list.Add(e);
                }
                continue;
            }

            if (ftype == "Selectable" && panorama && _cssHooks && !o.Disabled && state != null && feature != null &&
                Config.InlineSelectFeatures.Any(f => f.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                var sub = CaptureSubMenu(player, feature, state);
                var subOpts = sub?.MenuOptions.ToList();
                if (subOpts is { Count: > 0 } && subOpts.Count <= Config.InlineSelectMax)
                {
                    var choices = subOpts.Select(x => Tr(Clean(x.Text ?? "", true))).ToArray();
                    int cur = subOpts.FindIndex(x => x.Disabled);
                    list.Add(new Entry
                    {
                        Kind = Kind.Select,
                        Key = key,
                        Label = label,
                        Desc = "",
                        Value = cur >= 0 ? choices[cur] : "",
                        Choices = choices,
                        Pick = (p, i) => PickInline(p, sh, key, feature, state, subOpts, i),
                    });
                    continue;
                }
            }

            var plain = PlainEntry(sh, o, strip, Kind.Action);
            plain.Key = key;
            plain.Label = label;
            plain.Desc = desc;
            list.Add(plain);
        }
        return list;
    }

    // vip.json values of the player's group: Config.Groups[group].Values
    private static IDictionary? GroupValues(object core, string? group)
    {
        if (group == null) return null;
        var groups = Get(Get(core, "Config"), "Groups") as IDictionary;
        if (groups == null || !groups.Contains(group)) return null;
        return Get(groups[group], "Values") as IDictionary;
    }

    // Scalar vip.json value -> "120 HP" (Paint.ValueFormat); lists / objects / bools -> "".
    private string FormatValue(string key, object? raw)
    {
        string? v = raw switch
        {
            null => null,
            System.Text.Json.JsonElement je => je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => je.GetRawText(),
                System.Text.Json.JsonValueKind.String => je.GetString(),
                _ => null,
            },
            string s => s,
            bool => null,
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(v) || v.Length > 24) return "";
        if (TrKey("pmm_vip.value." + key) is { } tf)
        {
            try { return string.Format(tf, v); } catch (FormatException) { return v; }
        }
        foreach (var (k, fmt) in Config.Paint.ValueFormat)
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                try { return string.Format(fmt, v); } catch (FormatException) { return v; }
            }
        return v;
    }

    // Asks the module for its sub menu (OnSelectItem) without showing it.
    private IMenu? CaptureSubMenu(CCSPlayerController player, object feature, object state)
    {
        if (Get(feature, "OnSelectItem") is not Delegate onSelect) return null;
        var st = State(player.Slot);
        var saved = st.Capture;
        var list = new List<IMenu>();
        st.Capture = list;
        try
        {
            onSelect.DynamicInvoke(player, state);
        }
        catch (Exception e)
        {
            if (!WarnApiMismatch(e) && Config.Debug) Logger.LogWarning(e, "capture of sub menu failed");
            return null;
        }
        finally
        {
            st.Capture = saved;
        }
        return list.Count > 0 ? list[^1] : null;
    }

    private void PickInline(CCSPlayerController p, Shown sh, string key, object feature, object state,
        List<ChatMenuOption> subOpts, int index)
    {
        if (index < 0 || index >= subOpts.Count) return;
        var o = subOpts[index];
        // No Disabled check: the menu is not rebuilt, so "current value" flags of the captured sub menu are stale.
        Run(p, sh, () =>
        {
            if (FeatureAllowed(p, key, state, feature)) o.OnSelect(p, o);
            else State(p.Slot).Rebuild = true;   // refused: drop the value MenuManager already shows
        }, Kind.Select);
    }

    // Same as VIPCore's toggle row, but with the current state (VIPCore's row keeps the state it was built with).
    private void ToggleFeature(CCSPlayerController p, string key)
    {
        if (_core is not BasePlugin core) return;
        var feature = (Get(core, "Features") as IDictionary)?[key];
        var states = Get((Get(core, "Users") as IDictionary)?[p.SteamID], "FeatureState") as IDictionary;
        var state = states?[key];
        if (feature == null || states == null || state == null) return;

        string now = state.ToString() ?? "";
        if (now != "Enabled" && now != "Disabled")
        {
            State(p.Slot).Rebuild = true;
            return;
        }
        if (!FeatureAllowed(p, key, state, feature))
        {
            State(p.Slot).Rebuild = true;
            return;
        }

        bool on = now == "Disabled";
        object next = Enum.Parse(state.GetType(), on ? "Enabled" : "Disabled");
        var culture = VipTranslations.Culture(p);
        string name = _tr?.Get(culture, key) ?? core.Localizer[key].Value;
        string word = _tr?.Get(culture, on ? "pmm_vip.enabled" : "pmm_vip.disabled")
                      ?? core.Localizer[on ? "chat.Enabled" : "chat.Disabled"].Value;
        VipPrint(p, $"{name}: {word}");
        states[key] = next;
        (Get(feature, "OnSelectItem") as Delegate)?.DynamicInvoke(p, next);

        // Only the panorama menus flip a toggle in place; chat / center / CS:GO menus need a fresh !vip.
        if (!IsPanorama(p)) State(p.Slot).Rebuild = true;
    }

    // VipApi.PrintToChat -> goes through PrintPrefix, so during a toggle it becomes a toast.
    private void VipPrint(CCSPlayerController p, string message)
    {
        var vipApi = Get(_core, "VipApi");
        vipApi?.GetType().GetMethod("PrintToChat", Any, null, new[] { typeof(CCSPlayerController), typeof(string) }, null)
            ?.Invoke(vipApi, new object[] { p, message });
    }

    // VIPCore asks OnPlayerUseFeature subscribers before a feature is used; keep that for inline picks.
    private bool FeatureAllowed(CCSPlayerController p, string key, object state, object feature)
    {
        var vipApi = Get(_core, "VipApi");
        var m = vipApi?.GetType().GetMethod("PlayerUseFeature", Any);
        if (m == null) return true;
        var r = m.Invoke(vipApi, new object?[] { p, key, state, Get(feature, "FeatureType") });
        return !(r is HookResult h && (h == HookResult.Handled || h == HookResult.Stop));
    }

    // A row was picked in MenuManager: run the original callback, then decide what stays on screen.
    private void Run(CCSPlayerController p, Shown sh, Action callback, Kind kind)
    {
        if (p == null || !p.IsValid) return;
        var st = State(p.Slot);
        int before = st.ShowCount;
        bool toast = kind is Kind.Toggle or Kind.Select;
        st.Rebuild = false;
        st.Running.Push(sh);
        if (toast) st.ToggleDepth++;
        try
        {
            callback();
        }
        catch (Exception e)
        {
            if (!WarnApiMismatch(e)) Logger.LogError(e, "VIP menu callback failed");
        }
        finally
        {
            st.Running.Pop();
            if (toast) st.ToggleDepth--;
        }

        // The callback opened another menu (sub menu, or VIPCore reopened !vip itself).
        if (st.ShowCount != before || !p.IsValid) return;

        if (sh.IsMain)
        {
            // Toggle / Select are already updated in place by MenuManager.
            if (st.Rebuild || kind == Kind.Legacy) ReopenMain(p);
            st.Rebuild = false;
            return;
        }

        if (sh.Source.PostSelectAction == PostSelectAction.Close)
        {
            CloseFor(p);
            return;
        }
        if (Config.ReturnToParentAfterSelect && sh.Parent != null) Reopen(p, sh.Parent);
    }

    private void Reopen(CCSPlayerController p, Shown target)
    {
        if (!p.IsValid) return;
        if (target.IsMain) ReopenMain(p);
        else Render(p, target);
    }

    private void ReopenMain(CCSPlayerController p)
    {
        if (_core == null || _coreCreateMenu == null || !p.IsValid) return;
        try
        {
            _coreCreateMenu.Invoke(_core, new object[] { p });
        }
        catch (TargetInvocationException e)
        {
            Logger.LogError(e.InnerException ?? e, "VipCore.CreateMenu failed");
        }
    }

    private void CloseFor(CCSPlayerController p)
    {
        var st = State(p.Slot);
        st.Current = null;
        if (_api!.HasOpenedMenu(p)) _api.CloseMenu(p);
    }

    // ---------------- helpers ----------------

    private readonly HashSet<string> _apiWarned = new();

    // A module built against another VipCoreApi (e.g. the old PMM fork: OnMenuSelect) fails with
    // MissingMethodException / TypeLoadException. Say which plugin it is, once.
    private bool WarnApiMismatch(Exception e)
    {
        while (e is TargetInvocationException { InnerException: { } inner }) e = inner;
        if (e is not (MissingMethodException or MissingFieldException or TypeLoadException)) return false;

        string module = new System.Diagnostics.StackTrace(e, false).GetFrames()
            .Select(f => f.GetMethod()?.DeclaringType?.Assembly.GetName().Name)
            .FirstOrDefault(n => n != null && n.StartsWith("VIP_", StringComparison.OrdinalIgnoreCase)) ?? "a VIP module";
        if (_apiWarned.Add(module + e.Message))
            Logger.LogError("{Module} was built for a different VipCoreApi ({Error}). Install the original {Module} build " +
                            "(the PMM fork modules VIP_Fov / VIP_Tag do not work with the original VIPCore).", module, e.Message, module);
        return true;
    }

    private SlotState State(int slot)
    {
        if (!_slots.TryGetValue(slot, out var st)) _slots[slot] = st = new SlotState();
        return st;
    }

    private bool IsPanorama(CCSPlayerController player)
    {
        var type = _forceType ?? _api!.GetSelectedMenu(player);
        return type is MenuType.PanoramaMenu or MenuType.PanoramaWasdMenu;
    }

    private bool Matches(string owner)
    {
        foreach (var pattern in Config.InterceptPlugins)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;
            if (pattern.EndsWith('*'))
            {
                if (owner.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (owner.Equals(pattern, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // Chat colour bytes (\x01..\x10) and leftover {color} tags mean nothing in a panorama menu.
    private static string Clean(string s, bool strip)
    {
        if (!strip || string.IsNullOrEmpty(s)) return s ?? "";
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
            if (ch >= ' ' || ch == '\n') sb.Append(ch);
        string r = ColorTag.Replace(sb.ToString(), "");
        return Spaces.Replace(r, " ").Trim();
    }

    // "[VIP Menu - GOLD]" -> "VIP Menu - GOLD"
    private static string CleanTitle(string s, bool strip)
    {
        string r = Clean(s, strip);
        if (strip && r.Length > 2 && r[0] == '[' && r[^1] == ']') r = r[1..^1].Trim();
        return r;
    }
}
