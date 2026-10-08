using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace PMM_VIPCore;

public partial class Plugin : BasePlugin, IPluginConfig<BridgeConfig>
{
    public override string ModuleName => "PMM_VIPCore";
    public override string ModuleVersion => "0.1.1";
    public override string ModuleAuthor => "Rimmer";
    public override string ModuleDescription => "Shows VIPCore (thesamefabius) menus through PanoramaMenuManager (Harmony bridge)";

    private const string HarmonyId = "rimmer.pmm.vipcore";
    internal const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public BridgeConfig Config { get; set; } = new();
    public void OnConfigParsed(BridgeConfig config)
    {
        config.InterceptPlugins ??= new();
        config.InlineSelectFeatures ??= new();
        var paint = config.Paint ??= new PaintOptions();
        paint.Texts ??= new PaintTexts();
        paint.Theme ??= new ThemeOptions();
        paint.Theme.Palettes = new Dictionary<string, PaletteColors>(paint.Theme.Palettes ?? new(), StringComparer.OrdinalIgnoreCase);
        // JSON creates dictionaries without the case-insensitive comparer.
        paint.GroupPalette = new Dictionary<string, string>(paint.GroupPalette ?? new(), StringComparer.OrdinalIgnoreCase);
        paint.Icons = new Dictionary<string, string>(paint.Icons ?? new(), StringComparer.OrdinalIgnoreCase);
        paint.ValueFormat = new Dictionary<string, string>(paint.ValueFormat ?? new(), StringComparer.OrdinalIgnoreCase);
        if (paint.OpenDelay < 0.05f) paint.OpenDelay = 0.05f;
        if (paint.InputDelay < 0f) paint.InputDelay = 0f;
        Config = config;
    }

    private static readonly PluginCapability<IMenuApi?> MenuCapability = new("menu:nfcore");
    internal static Plugin? Instance;

    private IMenuApi? _api;
    private Assembly? _vip;
    private MethodInfo? _coreCreateMenu;   // VipCore.CreateMenu(CCSPlayerController), private
    private readonly List<string> _patched = new();
    private MenuType? _forceType;
    private bool _harmonyReady;
    private bool _cssHooks;

    internal bool Active => Config.Enabled && _api != null && _patched.Count > 0;

    public override void Load(bool hotReload)
    {
        Instance = this;
        _harmonyReady = HarmonyLoader.Ensure(ModuleDirectory, Logger);
        RegisterListener<Listeners.OnClientDisconnect>(slot => _slots.Remove(slot));
        _tr = new VipTranslations(ModuleDirectory, Logger);
        _tr.EnsureSection();
        WriteThemeCss();
        PaintLoad();
    }

    private VipTranslations? _tr;

    // Paint.Theme -> plugins/PMM_VIPCore/pmm_vip_theme.css (goes into the addon by hand).
    private string WriteThemeCss()
    {
        string path = Path.Combine(ModuleDirectory, ThemeCss.FileName);
        try
        {
            string css = ThemeCss.Build(Config.Paint.Theme, Logger);
            if (!File.Exists(path) || File.ReadAllText(path) != css)
            {
                File.WriteAllText(path, css);
                Logger.LogInformation("{File} written. Copy it to panorama/styles/custom_game/ of the addon and rebuild the addon.", path);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Could not write {File}", path);
        }
        return path;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try { _api = MenuCapability.Get(); } catch { _api = null; }
        if (_api == null)
        {
            Logger.LogWarning("MenuManager (menu:nfcore) not found, VIPCore keeps its own menu");
            return;
        }
        _forceType = Config.ForceMenuType != "" && Enum.TryParse<MenuType>(Config.ForceMenuType, true, out var t) ? t : null;
        Patch();
        if (Active) RegisterPainter();
    }

    public override void Unload(bool hotReload)
    {
        UnregisterPainter();
        foreach (var p in Utilities.GetPlayers())
            if (p.IsValid && IsOpen(p)) Hide(p);
        if (_harmonyReady) HarmonyHost.UnpatchAll(HarmonyId);
        _patched.Clear();
        _slots.Clear();
        Instance = null;
    }

    // ---------------- Harmony ----------------

    private void Patch()
    {
        if (!_harmonyReady)
        {
            Logger.LogError("0Harmony is not loaded, bridge disabled");
            return;
        }
        HarmonyHost.UnpatchAll(HarmonyId);
        _patched.Clear();
        _slots.Clear();
        _cssHooks = false;
        _mainDepth = 0;

        _vip = AppDomain.CurrentDomain.GetAssemblies().LastOrDefault(a => a.GetName().Name == "VIPCore");
        if (_vip == null)
        {
            Logger.LogWarning("VIPCore assembly is not loaded, nothing to patch");
            return;
        }

        var tCore = _vip.GetType("VIPCore.VipCore");
        var tApi = _vip.GetType("VIPCore.VipCoreApi");
        if (tCore == null || tApi == null)
        {
            Logger.LogError("VIPCore classes not found (VIPCore.VipCore / VIPCore.VipCoreApi). Version changed?");
            return;
        }

        var p = typeof(Patches);
        _coreCreateMenu = tCore.GetMethod("CreateMenu", Any, null, new[] { typeof(CCSPlayerController) }, null);
        var apiCreateMenu = tApi.GetMethod("CreateMenu", Any, null, new[] { typeof(string) }, null);
        var apiPrint = tApi.GetMethod("PrintToChat", Any, null, new[] { typeof(CCSPlayerController), typeof(string) }, null);

        // VipApi.CreateMenu(title): !vip and every module that uses CreateMenu (Fov ...) get our menu object.
        TryPatch(apiCreateMenu, null, p.GetMethod(nameof(Patches.CreateMenuPostfix)), null);
        // VipCore.CreateMenu(player): marks "this is the !vip main menu" while it is being built.
        TryPatch(_coreCreateMenu, p.GetMethod(nameof(Patches.MainPrefix)), null, p.GetMethod(nameof(Patches.MainFinalizer)));
        // VipApi.PrintToChat: "Bhop: Enabled" after a toggle -> MenuManager toast.
        TryPatch(apiPrint, p.GetMethod(nameof(Patches.PrintPrefix)), null, null);

        if (!_patched.Contains("VipCoreApi.CreateMenu"))
        {
            Logger.LogError("VipCoreApi.CreateMenu was not patched, bridge disabled");
            HarmonyHost.UnpatchAll(HarmonyId);
            _patched.Clear();
            return;
        }

        // Plain CSS menus opened directly by VIP modules (VIP_Tag, VIP_WeaponsMenu ...).
        // Both Open() and the static MenuManager entry points are hooked, in case one of them is inlined.
        if (Config.InterceptPlugins.Count > 0)
        {
            var cssMm = typeof(ChatMenu).Assembly.GetType("CounterStrikeSharp.API.Modules.Menu.MenuManager");
            int before = _patched.Count;
            TryPatch(typeof(ChatMenu).GetMethod("Open", new[] { typeof(CCSPlayerController) }), p.GetMethod(nameof(Patches.ChatOpenPrefix)), null, null);
            TryPatch(typeof(CenterHtmlMenu).GetMethod("Open", new[] { typeof(CCSPlayerController) }), p.GetMethod(nameof(Patches.CenterOpenPrefix)), null, null);
            TryPatch(cssMm?.GetMethod("OpenChatMenu", BindingFlags.Public | BindingFlags.Static), p.GetMethod(nameof(Patches.OpenChatMenuPrefix)), null, null);
            TryPatch(cssMm?.GetMethod("OpenCenterHtmlMenu", BindingFlags.Public | BindingFlags.Static), p.GetMethod(nameof(Patches.OpenCenterHtmlMenuPrefix)), null, null);
            _cssHooks = _patched.Count > before;
        }

        Logger.LogInformation("Patched VIPCore {Ver}: {List}", _vip.GetName().Version, string.Join(", ", _patched));
    }

    private void TryPatch(MethodInfo? m, MethodInfo? prefix, MethodInfo? postfix, MethodInfo? finalizer)
    {
        if (m == null) return;
        try
        {
            HarmonyHost.Patch(HarmonyId, m, prefix, postfix, finalizer);
            _patched.Add($"{m.DeclaringType?.Name}.{m.Name}");
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Harmony failed on {Type}.{Method}", m.DeclaringType?.Name, m.Name);
        }
    }

    // ---------------- commands ----------------

    [ConsoleCommand("css_pmm_vip", "PMM_VIPCore status | repatch | css")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnStatus(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount > 1 && info.GetArg(1) == "css")
        {
            info.ReplyToCommand($"[PMM_VIP] written {WriteThemeCss()}");
            return;
        }
        if (info.ArgCount > 1 && info.GetArg(1) == "repatch")
        {
            try { _api = MenuCapability.Get(); } catch { _api = null; }
            if (_api != null) Patch();
            if (Active) RegisterPainter();
        }
        info.ReplyToCommand($"[PMM_VIP] active={Active} enabled={Config.Enabled} menuApi={_api != null} vipcore={_vip?.GetName().Version?.ToString() ?? "none"} core={(_core != null ? "seen" : "not yet")}");
        info.ReplyToCommand($"[PMM_VIP] patched: {(_patched.Count == 0 ? "-" : string.Join(", ", _patched))}");
        info.ReplyToCommand($"[PMM_VIP] css hooks={_cssHooks} intercept=[{string.Join(", ", Config.InterceptPlugins)}] inline=[{string.Join(", ", Config.InlineSelectFeatures)}]");
        info.ReplyToCommand($"[PMM_VIP] open menus: {_slots.Count(x => x.Value.Current != null)}");
        info.ReplyToCommand($"[PMM_VIP] translations: {_tr?.FilePath ?? "-"} ({_tr?.Languages ?? 0} languages)");
        info.ReplyToCommand($"[PMM_VIP] panorama: enabled={Config.Paint.Enabled} painter={_registry != null} layout={(_vipLayout is { IsValid: true } ? "ok" : "none")} open={_paint.Count(x => x.Open)}");
    }

    // ---------------- reflection ----------------

    internal static object? Get(object? o, string name)
    {
        if (o == null) return null;
        var t = o.GetType();
        var p = t.GetProperty(name, Any);
        if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(o);
        return t.GetField(name, Any)?.GetValue(o);
    }

    // First caller outside CSS / Harmony / this plugin / the BCL: that is the plugin that opened the menu.
    internal static string? CallerAssembly()
    {
        var own = typeof(Plugin).Assembly;
        var css = typeof(ChatMenu).Assembly;
        var trace = new StackTrace(1, false);
        for (int i = 0; i < trace.FrameCount; i++)
        {
            var asm = trace.GetFrame(i)?.GetMethod()?.DeclaringType?.Assembly;
            if (asm == null || asm.IsDynamic || asm == own || asm == css) continue;
            string? name = asm.GetName().Name;
            if (name == null || name == "0Harmony" || name.StartsWith("MonoMod", StringComparison.Ordinal) ||
                name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft", StringComparison.Ordinal))
                continue;
            return name;
        }
        return null;
    }
}

internal static class Patches
{
    // VipCoreApi.CreateMenu(string title) -> IMenu
    public static void CreateMenuPostfix(object __instance, string __0, ref IMenu __result)
    {
        try { Plugin.Instance?.OnCreateMenu(__instance, __0, ref __result); }
        catch (Exception e) { Plugin.Instance?.Logger.LogError(e, "CreateMenu bridge failed"); }
    }

    // VipCore.CreateMenu(CCSPlayerController)
    public static void MainPrefix(object __instance)
    {
        Plugin.Instance?.EnterMain(__instance);
    }

    public static Exception? MainFinalizer(Exception? __exception)
    {
        Plugin.Instance?.LeaveMain();
        return __exception;
    }

    // VipCoreApi.PrintToChat(player, message): false = swallowed (shown as a toast instead)
    public static bool PrintPrefix(CCSPlayerController __0, string __1)
    {
        try { return !(Plugin.Instance?.TryNotify(__0, __1) ?? false); }
        catch { return true; }
    }

    // CSS menus opened by VIP modules. false = MenuManager shows it instead.
    public static bool ChatOpenPrefix(ChatMenu __instance, CCSPlayerController __0)
        => !Safe(() => Plugin.Instance?.TryIntercept(__0, __instance, null) ?? false);

    public static bool CenterOpenPrefix(CenterHtmlMenu __instance, CCSPlayerController __0)
        => !Safe(() => Plugin.Instance?.TryIntercept(__0, __instance, Plugin.Get(__instance, "_plugin") as BasePlugin) ?? false);

    public static bool OpenChatMenuPrefix(CCSPlayerController __0, ChatMenu __1)
        => !Safe(() => Plugin.Instance?.TryIntercept(__0, __1, null) ?? false);

    public static bool OpenCenterHtmlMenuPrefix(BasePlugin __0, CCSPlayerController __1, CenterHtmlMenu __2)
        => !Safe(() => Plugin.Instance?.TryIntercept(__1, __2, __0) ?? false);

    private static bool Safe(Func<bool> f)
    {
        try { return f(); }
        catch (Exception e)
        {
            Plugin.Instance?.Logger.LogError(e, "menu intercept failed, CSS menu is shown");
            return false;
        }
    }
}

// 0Harmony must live in the default (non-collectible) load context: MonoMod builds a proxy
// in a non-collectible dynamic assembly that references 0Harmony by name, and CSS loads plugins
// into collectible contexts ("Resolving to a collectible assembly is not supported").
// So 0Harmony.dll ships in plugins/PMM_VIPCore/harmony/ (not next to the plugin dll,
// otherwise the plugin context would load its own collectible copy) and is loaded here.
// If PMM_GG1MapChooser already loaded it, the same copy is reused.
internal static class HarmonyLoader
{
    public static bool Ensure(string pluginDir, ILogger log)
    {
        try
        {
            var asm = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == "0Harmony");
            if (asm == null)
            {
                string path = Path.Combine(pluginDir, "harmony", "0Harmony.dll");
                if (!File.Exists(path))
                {
                    log.LogError("Not found: {Path}", path);
                    return false;
                }
                asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }

            var own = AssemblyLoadContext.GetLoadContext(typeof(HarmonyLoader).Assembly);
            if (own != null && own != AssemblyLoadContext.Default)
            {
                var found = asm;
                own.Resolving += (_, name) => name.Name == "0Harmony" ? found : null;
            }

            log.LogInformation("0Harmony {Ver} in default context: {Loc}", asm.GetName().Version, asm.Location);
            return true;
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed to load 0Harmony into the default context");
            return false;
        }
    }
}

// The only place that touches HarmonyLib types; it is JIT-compiled after HarmonyLoader.Ensure.
internal static class HarmonyHost
{
    private static readonly Dictionary<string, HarmonyLib.Harmony> Instances = new();

    private static HarmonyLib.Harmony Get(string id)
    {
        if (!Instances.TryGetValue(id, out var h)) Instances[id] = h = new HarmonyLib.Harmony(id);
        return h;
    }

    public static void Patch(string id, MethodBase original, MethodInfo? prefix, MethodInfo? postfix, MethodInfo? finalizer)
    {
        Get(id).Patch(original,
            prefix: prefix == null ? null : new HarmonyLib.HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyLib.HarmonyMethod(postfix),
            finalizer: finalizer == null ? null : new HarmonyLib.HarmonyMethod(finalizer));
    }

    public static void UnpatchAll(string id)
    {
        if (Instances.TryGetValue(id, out var h)) h.UnpatchAll(id);
    }
}
