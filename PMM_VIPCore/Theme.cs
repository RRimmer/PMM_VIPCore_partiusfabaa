using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PMM_VIPCore;

// Paint.Theme -> pmm_vip_theme.css. The template (theme.template.css) is embedded in the dll;
// tools/gen_layout.py fills the same template with the defaults of Config.cs for the shipped file.
internal static class ThemeCss
{
    public const string FileName = "pmm_vip_theme.css";
    private const string PaletteMark = "/*@@palette@@*/\n";
    private static readonly Regex Hex = new("^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$", RegexOptions.Compiled);
    private static readonly Regex SafeName = new("^[a-z0-9_-]+$", RegexOptions.Compiled);

    public static bool IsName(string name) => SafeName.IsMatch(name.ToLowerInvariant());

    public static string Build(ThemeOptions theme, ILogger log)
    {
        string tpl;
        using (var stream = typeof(ThemeCss).Assembly.GetManifestResourceStream("theme.template.css"))
        {
            if (stream == null) throw new InvalidOperationException("theme.template.css is not embedded");
            using var reader = new StreamReader(stream);
            tpl = reader.ReadToEnd().Replace("\r\n", "\n");
        }
        int cut = tpl.IndexOf(PaletteMark, StringComparison.Ordinal);
        string head = cut >= 0 ? tpl[..cut] : tpl;
        string palette = cut >= 0 ? tpl[(cut + PaletteMark.Length)..] : "";

        var defaults = new ThemeOptions();
        foreach (var prop in typeof(ThemeOptions).GetProperties())
        {
            if (prop.PropertyType != typeof(string)) continue;
            string value = Color(prop.GetValue(theme) as string, prop.GetValue(defaults) as string ?? "#ffffff", prop.Name, log);
            head = head.Replace("{{" + prop.Name + ".rgb}}", value[..7]).Replace("{{" + prop.Name + "}}", value);
        }

        var sb = new StringBuilder(head);
        var fallback = new PaletteColors();
        foreach (var (rawName, c) in theme.Palettes)
        {
            string name = rawName.ToLowerInvariant();
            if (!SafeName.IsMatch(name) || c == null)
            {
                log.LogWarning("Theme: palette name '{Name}' skipped (only a-z 0-9 _ -)", rawName);
                continue;
            }
            sb.Append(palette
                .Replace("{{name}}", name)
                .Replace("{{a}}", Color(c.Accent, fallback.Accent, name + ".Accent", log)[..7])
                .Replace("{{l}}", Color(c.Light, fallback.Light, name + ".Light", log)[..7])
                .Replace("{{d}}", Color(c.Dark, fallback.Dark, name + ".Dark", log)[..7])
                .Replace("{{on}}", Color(c.OnAccent, fallback.OnAccent, name + ".OnAccent", log)));
        }
        return sb.ToString();
    }

    private static string Color(string? value, string fallback, string what, ILogger log)
    {
        value = value?.Trim();
        if (value != null && Hex.IsMatch(value)) return value.ToLowerInvariant();
        log.LogWarning("Theme: {What} = '{Value}' is not #rrggbb / #rrggbbaa, {Fallback} is used", what, value, fallback);
        return fallback;
    }
}
