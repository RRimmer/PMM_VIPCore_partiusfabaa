using System.Collections;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace PMM_VIPCore;

// Which VIPCore the bridge talks to.
//  Legacy: thesamefabius VIPCore 1.x — class VIPCore.VipCore (Users, Features, Config.Groups, CreateMenu(player)).
//  V2:     partiusfabaa VIPCore 2.x — VIPCore.Plugin + services (PlayersManager, FeatureManager, Services.MenuManager),
//          features are VipFeature objects, the !vip menu is Services.MenuManager.OpenVipMenu(player).
internal enum CoreKind { None, Legacy, V2 }

public partial class Plugin
{
    private CoreKind _coreKind;
    private object? _core;        // Legacy: VIPCore.VipCore (BasePlugin). V2: VIPCore.VipCoreApi.
    private object? _mainHost;    // object whose !vip method runs: Legacy VipCore, V2 Services.MenuManager

    private string CoreName => _coreKind switch { CoreKind.Legacy => "1.x", CoreKind.V2 => "2.x", _ => "none" };

    // ---------------- discovery ----------------

    // VIPCore's own api object through its capability (vipcore:core is registered as PluginCapability<IVipCoreApi>,
    // providers are kept per generic type, so the capability is built for VIPCore's IVipCoreApi type).
    private object? CoreApiFromCapability()
    {
        try
        {
            var iapi = _vip?.GetType("VIPCore.VipCoreApi")?.GetInterfaces().FirstOrDefault(i => i.Name == "IVipCoreApi");
            if (iapi == null) return null;
            var cap = Activator.CreateInstance(typeof(PluginCapability<>).MakeGenericType(iapi), "vipcore:core");
            return cap?.GetType().GetMethod("Get")?.Invoke(cap, null);
        }
        catch (Exception e)
        {
            if (Config.Debug) Logger.LogInformation("vipcore:core not available: {Msg}", e.Message);
            return null;
        }
    }

    // _core is set by the hooks; before the first menu it is taken from the capability.
    private object? Core()
    {
        if (_core != null) return _core;
        var api = CoreApiFromCapability();
        if (api == null) return null;
        _core = _coreKind == CoreKind.V2 ? api : Get(api, "_vipCore");
        return _core;
    }

    internal void SeeApi(object vipApi)
    {
        if (_core != null) return;
        _core = _coreKind == CoreKind.V2 ? vipApi : Get(vipApi, "_vipCore");
    }

    // VIPCore plugin (Localizer, PrintToChat). Legacy: the core itself.
    private BasePlugin? CorePlugin()
    {
        var core = Core();
        return _coreKind == CoreKind.V2 ? Get(core, "_plugin") as BasePlugin : core as BasePlugin;
    }

    // IVipCoreApi object. Legacy: VipCore.VipApi.
    private object? CoreApi()
    {
        var core = Core();
        return _coreKind == CoreKind.V2 ? core : Get(core, "VipApi");
    }

    // Object with the !vip method (to reopen it).
    private object? MainHost()
    {
        if (_mainHost != null) return _mainHost;
        if (_coreKind == CoreKind.V2) _mainHost = Get(CorePlugin(), "_menuManager");
        else _mainHost = Core();
        return _mainHost;
    }

    // VIPCore 2: "MenuType" of vip_core.json (center / chat / console). The bridge takes over only the types in
    // CoreMenuTypes; with chat / console VIPCore keeps its own menus. VIPCore 1 has no such setting.
    private string CoreMenuType()
    {
        if (_coreKind != CoreKind.V2) return "";
        return (Get(Get(Get(Core(), "_coreConfig"), "Value"), "MenuType") as string ?? "center").Trim();
    }

    internal bool CoreTakeover
    {
        get
        {
            if (_coreKind != CoreKind.V2) return true;
            string type = CoreMenuType();
            return Config.CoreMenuTypes.Any(t => t.Equals(type, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---------------- players ----------------

    // Legacy: Users[SteamID] (User). V2: PlayersManager.TryGetPlayer -> VipPlayer, only while VIP.
    private object? CoreUser(CCSPlayerController p)
    {
        var core = Core();
        if (core == null || !p.IsValid) return null;
        if (_coreKind == CoreKind.Legacy)
            return (Get(core, "Users") as IDictionary)?[p.SteamID];

        var players = Get(core, "_players");
        var m = players?.GetType().GetMethod("TryGetPlayer", Any);
        if (m == null) return null;
        var args = new object?[] { p, null };
        try
        {
            if (m.Invoke(players, args) is not true || args[1] == null) return null;
        }
        catch { return null; }
        return Get(args[1], "IsVip") is true ? args[1] : null;
    }

    private string UserGroup(CCSPlayerController p)
    {
        var user = CoreUser(p);
        return (_coreKind == CoreKind.V2 ? Get(user, "Group") : Get(user, "group")) as string ?? "";
    }

    // Unix seconds, 0 = forever.
    private long UserExpires(CCSPlayerController p)
    {
        var user = CoreUser(p);
        object? raw = _coreKind == CoreKind.V2 ? Get(Get(user, "Data"), "Expires") : Get(user, "expires");
        return raw switch { int i => i, long l => l, _ => 0 };
    }

    // feature key -> FeatureState
    private IDictionary? UserStates(CCSPlayerController p)
    {
        var user = CoreUser(p);
        return Get(user, _coreKind == CoreKind.V2 ? "FeatureStates" : "FeatureState") as IDictionary;
    }

    private string ReadState(CCSPlayerController p, string key) => UserStates(p)?[key]?.ToString() ?? "";

    // vip.json value of the feature for the player's group (V2: with $inherit already resolved).
    private object? UserValue(CCSPlayerController p, string key)
    {
        if (_coreKind == CoreKind.V2)
        {
            var features = Get(CoreUser(p), "Features") as IDictionary;
            return features != null && features.Contains(key) ? features[key] : null;
        }
        var core = Core();
        string? group = Get(CoreUser(p), "group") as string;
        if (core == null || group == null) return null;
        var groups = Get(Get(core, "Config"), "Groups") as IDictionary;
        if (groups == null || !groups.Contains(group)) return null;
        var values = Get(groups[group], "Values") as IDictionary;
        return values != null && values.Contains(key) ? values[key] : null;
    }

    private bool CoreHasFeature(CCSPlayerController p, string feature)
    {
        var api = CoreApi();
        if (api == null) return false;
        try
        {
            var t = api.GetType();
            var isVip = t.GetMethod("IsPlayerVip", Any, null, new[] { typeof(CCSPlayerController) }, null)
                        ?? t.GetMethod("IsClientVip", Any, null, new[] { typeof(CCSPlayerController) }, null);
            var has = t.GetMethod("PlayerHasFeature", Any, null, new[] { typeof(CCSPlayerController), typeof(string) }, null);
            if (isVip != null && isVip.Invoke(api, new object[] { p }) is false) return false;
            return has == null || has.Invoke(api, new object[] { p, feature }) is true;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "VIP access check failed");
            return false;
        }
    }

    // ---------------- features ----------------

    // (key, feature object, "Toggle" / "Selectable" / "Hide")
    private List<(string Key, object Feature, string Type)> CoreFeatures()
    {
        var list = new List<(string, object, string)>();
        var core = Core();
        if (_coreKind == CoreKind.V2)
        {
            var fm = Get(core, "_featureManager");
            if (fm?.GetType().GetMethod("GetFeatures", Any, null, Type.EmptyTypes, null)?.Invoke(fm, null) is IEnumerable all)
                foreach (var f in all)
                    if (f != null && Get(f, "Name") is string name)
                        list.Add((name, f, Get(f, "Type")?.ToString() ?? ""));
            return list;
        }
        if (Get(core, "Features") is IDictionary features)
            foreach (DictionaryEntry e in features)
                if (e.Key is string key && e.Value != null)
                    list.Add((key, e.Value, Get(e.Value, "FeatureType")?.ToString() ?? ""));
        return list;
    }

    private object? CoreFeature(string key) => CoreFeatures().FirstOrDefault(f => f.Key == key).Feature;

    // The module object behind a feature (VIP_WeaponsMenu's WeaponsMenu). Legacy: OnSelectItem.Target.
    private object? FeatureObject(string key)
    {
        var f = CoreFeature(key);
        return _coreKind == CoreKind.V2 ? f : (Get(f, "OnSelectItem") as Delegate)?.Target;
    }

    // VIPCore text of a key in the player's language (feature names, chat.Enabled ...).
    private string CoreText(CCSPlayerController? p, string key)
    {
        var plugin = CorePlugin();
        if (plugin == null) return "";
        try
        {
            if (_coreKind == CoreKind.V2 && p != null && p.IsValid)
            {
                var m = plugin.GetType().GetMethod("Translate", Any, null,
                    new[] { typeof(CCSPlayerController), typeof(string), typeof(object[]) }, null);
                if (m?.Invoke(plugin, new object[] { p, key, Array.Empty<object>() }) is string s) return s;
            }
            return plugin.Localizer[key].Value;
        }
        catch { return ""; }
    }

    // Feature's own OnSelectItem (opens its sub menu / applies a toggle). Legacy: (player, state). V2: (player, feature).
    private void InvokeSelect(CCSPlayerController p, object feature, object? state)
    {
        if (_coreKind == CoreKind.V2)
        {
            feature.GetType().GetMethod("OnSelectItem", Any)?.Invoke(feature, new[] { p, feature });
            return;
        }
        (Get(feature, "OnSelectItem") as Delegate)?.DynamicInvoke(p, state);
    }

    // VIPCore asks OnPlayerUseFeature subscribers before a feature is used.
    private bool FeatureAllowed(CCSPlayerController p, string key, object state, object feature)
    {
        var api = CoreApi();
        if (api == null) return true;
        if (_coreKind == CoreKind.V2)
        {
            var invoke = api.GetType().GetMethod("InvokeOnPlayerUseFeature", Any);
            var argsType = invoke?.GetParameters().FirstOrDefault()?.ParameterType;
            if (invoke == null || argsType == null) return true;
            var args = Activator.CreateInstance(argsType);
            argsType.GetProperty("Controller")?.SetValue(args, p);
            argsType.GetProperty("Feature")?.SetValue(args, feature);
            argsType.GetProperty("State")?.SetValue(args, state);
            invoke.Invoke(api, new[] { args });
            return argsType.GetProperty("Allow")?.GetValue(args) is not false;
        }
        var m = api.GetType().GetMethod("PlayerUseFeature", Any);
        if (m == null) return true;
        var r = m.Invoke(api, new object?[] { p, key, state, Get(feature, "FeatureType") });
        return !(r is HookResult h && (h == HookResult.Handled || h == HookResult.Stop));
    }

    // Toggle row of !vip with the current state.
    // V2: VIPCore's own Services.MenuManager.OnFeatureSelected (use check, state, cookie, chat line, OnSelectItem).
    private void ToggleFeature(CCSPlayerController p, string key)
    {
        if (_coreKind == CoreKind.V2)
        {
            var host = MainHost();
            var feature = CoreFeature(key);
            var players = Get(Core(), "_players");
            var get = players?.GetType().GetMethod("TryGetPlayer", Any);
            var select = host?.GetType().GetMethod("OnFeatureSelected", Any);
            if (feature == null || get == null || select == null)
            {
                State(p.Slot).Rebuild = true;
                return;
            }
            var args = new object?[] { p, null };
            if (get.Invoke(players, args) is not true || args[1] == null) return;
            select.Invoke(host, new[] { p, args[1], feature });
            if (!IsPanorama(p)) State(p.Slot).Rebuild = true;
            return;
        }

        var core = CorePlugin();
        var legacyFeature = CoreFeature(key);
        var states = UserStates(p);
        var state = states?[key];
        if (core == null || legacyFeature == null || states == null || state == null) return;

        string now = state.ToString() ?? "";
        if (now != "Enabled" && now != "Disabled")
        {
            State(p.Slot).Rebuild = true;
            return;
        }
        if (!FeatureAllowed(p, key, state, legacyFeature))
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
        InvokeSelect(p, legacyFeature, next);

        // Only the panorama menus flip a toggle in place; chat / center / CS:GO menus need a fresh !vip.
        if (!IsPanorama(p)) State(p.Slot).Rebuild = true;
    }

    // VipApi.PrintToChat -> goes through PrintPrefix, so during a toggle it becomes a toast.
    private void VipPrint(CCSPlayerController p, string message)
    {
        var api = CoreApi();
        api?.GetType().GetMethod("PrintToChat", Any, null, new[] { typeof(CCSPlayerController), typeof(string) }, null)
            ?.Invoke(api, new object[] { p, message });
    }

    private void ReopenMain(CCSPlayerController p)
    {
        var host = MainHost();
        if (host == null || _coreMainMenu == null || !p.IsValid) return;
        try
        {
            _coreMainMenu.Invoke(host, new object[] { p });
        }
        catch (TargetInvocationException e)
        {
            Logger.LogError(e.InnerException ?? e, "VIPCore !vip menu failed");
        }
    }
}
