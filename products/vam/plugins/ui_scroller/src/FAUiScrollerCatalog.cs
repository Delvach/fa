using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

[System.Serializable]
internal sealed class FAUiScrollerCatalog
{
    public FAUiScrollerUiStrings ui = new FAUiScrollerUiStrings();
    public FAUiScrollerPackageIdentity package = new FAUiScrollerPackageIdentity();
}

[System.Serializable]
internal sealed class FAUiScrollerUiStrings
{
    public string pluginTitleLabel = "Plugin Title";
    public string pluginTitle = "Joystick Scroll";
    public string enabledLabel = "Enabled";
    public string captureNavigationLabel = "Capture Navigation";
    public string invertVerticalLabel = "Invert Vertical";
    public string scrollSpeedLabel = "Scroll Speed";
    public string stickLabel = "Stick";
    public string stickChoiceEither = "Either";
    public string stickChoiceRight = "Right";
    public string stickChoiceLeft = "Left";
    public string stickChoiceStrongest = "Strongest";
    public string defaultStickChoice = "Either";
    public string statusLabel = "Status";
    public string rescanLabel = "Rescan UI";
    public string statusIdle = "idle";
    public string statusOff = "off";
    public string statusDisabled = "disabled";
    public string statusRescanned = "rescanned";
    public string statusTargetMissing = "target_missing";
    public string statusHoldingPrefix = "holding:";
    public string noneLabel = "none";
}

[System.Serializable]
internal sealed class FAUiScrollerPackageIdentity
{
    public string releaseIdentity = "FrameAngel.JoystickScroller.1.var";
    public string devIdentity = "FrameAngel.JoystickScrollerDev.var";
}

internal static class FAUiScrollerCatalogStore
{
    private const string ResourceName = "frameangel.ui_scroller.catalog.json";
    private static readonly FAUiScrollerCatalog Catalog = LoadCatalog();

    internal static FAUiScrollerUiStrings Ui
    {
        get { return Catalog != null && Catalog.ui != null ? Catalog.ui : new FAUiScrollerUiStrings(); }
    }

    internal static FAUiScrollerPackageIdentity Package
    {
        get { return Catalog != null && Catalog.package != null ? Catalog.package : new FAUiScrollerPackageIdentity(); }
    }

    private static FAUiScrollerCatalog LoadCatalog()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    return new FAUiScrollerCatalog();

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    FAUiScrollerCatalog catalog = new FAUiScrollerCatalog();

                    FAUiScrollerUiStrings ui = catalog.ui;
                    ui.pluginTitleLabel = ReadString(json, "pluginTitleLabel", ui.pluginTitleLabel);
                    ui.pluginTitle = ReadString(json, "pluginTitle", ui.pluginTitle);
                    ui.enabledLabel = ReadString(json, "enabledLabel", ui.enabledLabel);
                    ui.captureNavigationLabel = ReadString(json, "captureNavigationLabel", ui.captureNavigationLabel);
                    ui.invertVerticalLabel = ReadString(json, "invertVerticalLabel", ui.invertVerticalLabel);
                    ui.scrollSpeedLabel = ReadString(json, "scrollSpeedLabel", ui.scrollSpeedLabel);
                    ui.stickLabel = ReadString(json, "stickLabel", ui.stickLabel);
                    ui.stickChoiceEither = ReadString(json, "stickChoiceEither", ui.stickChoiceEither);
                    ui.stickChoiceRight = ReadString(json, "stickChoiceRight", ui.stickChoiceRight);
                    ui.stickChoiceLeft = ReadString(json, "stickChoiceLeft", ui.stickChoiceLeft);
                    ui.stickChoiceStrongest = ReadString(json, "stickChoiceStrongest", ui.stickChoiceStrongest);
                    ui.defaultStickChoice = ReadString(json, "defaultStickChoice", ui.defaultStickChoice);
                    ui.statusLabel = ReadString(json, "statusLabel", ui.statusLabel);
                    ui.rescanLabel = ReadString(json, "rescanLabel", ui.rescanLabel);
                    ui.statusIdle = ReadString(json, "statusIdle", ui.statusIdle);
                    ui.statusOff = ReadString(json, "statusOff", ui.statusOff);
                    ui.statusDisabled = ReadString(json, "statusDisabled", ui.statusDisabled);
                    ui.statusRescanned = ReadString(json, "statusRescanned", ui.statusRescanned);
                    ui.statusTargetMissing = ReadString(json, "statusTargetMissing", ui.statusTargetMissing);
                    ui.statusHoldingPrefix = ReadString(json, "statusHoldingPrefix", ui.statusHoldingPrefix);
                    ui.noneLabel = ReadString(json, "noneLabel", ui.noneLabel);

                    FAUiScrollerPackageIdentity packageIdentity = catalog.package;
                    packageIdentity.releaseIdentity = ReadString(json, "releaseIdentity", packageIdentity.releaseIdentity);
                    packageIdentity.devIdentity = ReadString(json, "devIdentity", packageIdentity.devIdentity);

                    return catalog;
                }
            }
        }
        catch
        {
            return new FAUiScrollerCatalog();
        }
    }

    private static string ReadString(string json, string fieldName, string fallback)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            return fallback;

        Match match = Regex.Match(
            json,
            "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"",
            RegexOptions.Singleline);
        if (!match.Success)
            return fallback;

        return UnescapeJsonString(match.Groups[1].Value);
    }

    private static string UnescapeJsonString(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        StringBuilder sb = new StringBuilder(raw.Length);
        bool escaping = false;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (!escaping)
            {
                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }

                sb.Append(c);
                continue;
            }

            escaping = false;
            switch (c)
            {
                case '\\':
                case '"':
                case '/':
                    sb.Append(c);
                    break;
                case 'b':
                    sb.Append('\b');
                    break;
                case 'f':
                    sb.Append('\f');
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                case 'u':
                    if (i + 4 < raw.Length)
                    {
                        string hex = raw.Substring(i + 1, 4);
                        ushort code;
                        if (ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                    }
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        if (escaping)
            sb.Append('\\');

        return sb.ToString();
    }
}
