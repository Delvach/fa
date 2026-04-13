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
    public string pluginTitle = "Joystick Scroller";
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
    public string devIdentity = "FrameAngel.JoystickScrollerDev.1.var";
}

internal static class FAUiScrollerCatalogStore
{
    internal static readonly FAUiScrollerCatalog Catalog = FAUiScrollerCatalogGenerated.Build();

    internal static FAUiScrollerUiStrings Ui
    {
        get { return Catalog != null && Catalog.ui != null ? Catalog.ui : new FAUiScrollerUiStrings(); }
    }

    internal static FAUiScrollerPackageIdentity Package
    {
        get { return Catalog != null && Catalog.package != null ? Catalog.package : new FAUiScrollerPackageIdentity(); }
    }
}
