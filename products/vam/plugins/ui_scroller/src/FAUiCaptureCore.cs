using UnityEngine;

internal sealed class FAUiCaptureCore
{
    private struct NavigationSnapshot
    {
        public bool disableNavigation;
        public bool disableInternalKeyBindings;
        public bool disableInternalNavigationKeyBindings;
        public bool hasDisableAllNavigationToggle;
        public bool disableAllNavigationToggle;
        public bool hasDisableGrabNavigationToggle;
        public bool disableGrabNavigationToggle;
        public bool alwaysEnablePointers;
        public bool hasRayLineLeft;
        public bool rayLineLeftEnabled;
        public bool hasRayLineRight;
        public bool rayLineRightEnabled;
    }

    private bool captureActive = false;
    private bool snapshotKnown = false;
    private NavigationSnapshot snapshot;

    internal bool IsActive
    {
        get { return captureActive; }
    }

    internal void SetCaptureEnabled(bool enabled)
    {
        SuperController sc = SuperController.singleton;
        if (sc == null)
        {
            captureActive = false;
            snapshotKnown = false;
            return;
        }

        if (enabled)
        {
            if (!snapshotKnown)
            {
                snapshot = ReadSnapshot(sc);
                snapshotKnown = true;
            }

            NavigationSnapshot desired = snapshot;
            desired.disableNavigation = true;
            desired.disableInternalKeyBindings = true;
            desired.disableInternalNavigationKeyBindings = true;
            if (desired.hasDisableAllNavigationToggle)
                desired.disableAllNavigationToggle = true;
            if (desired.hasDisableGrabNavigationToggle)
                desired.disableGrabNavigationToggle = true;
            WriteSnapshot(sc, desired);
            captureActive = true;
            return;
        }

        if (snapshotKnown)
            WriteSnapshot(sc, snapshot);

        captureActive = false;
        snapshotKnown = false;
    }

    private static NavigationSnapshot ReadSnapshot(SuperController sc)
    {
        NavigationSnapshot next = new NavigationSnapshot();
        next.disableNavigation = SafeReadBool(delegate { return sc.disableNavigation; });
        next.disableInternalKeyBindings = SafeReadBool(delegate { return sc.disableInternalKeyBindings; });
        next.disableInternalNavigationKeyBindings = SafeReadBool(delegate { return sc.disableInternalNavigationKeyBindings; });
        next.hasDisableAllNavigationToggle = sc.disableAllNavigationToggle != null;
        next.disableAllNavigationToggle = next.hasDisableAllNavigationToggle && sc.disableAllNavigationToggle.isOn;
        next.hasDisableGrabNavigationToggle = sc.disableGrabNavigationToggle != null;
        next.disableGrabNavigationToggle = next.hasDisableGrabNavigationToggle && sc.disableGrabNavigationToggle.isOn;
        next.alwaysEnablePointers = SafeReadBool(delegate { return sc.alwaysEnablePointers; });
        next.hasRayLineLeft = sc.rayLineLeft != null;
        next.rayLineLeftEnabled = next.hasRayLineLeft && SafeReadBool(delegate { return sc.rayLineLeft.enabled; });
        next.hasRayLineRight = sc.rayLineRight != null;
        next.rayLineRightEnabled = next.hasRayLineRight && SafeReadBool(delegate { return sc.rayLineRight.enabled; });
        return next;
    }

    private static void WriteSnapshot(SuperController sc, NavigationSnapshot next)
    {
        sc.disableNavigation = next.disableNavigation;
        sc.disableInternalKeyBindings = next.disableInternalKeyBindings;
        sc.disableInternalNavigationKeyBindings = next.disableInternalNavigationKeyBindings;
        sc.alwaysEnablePointers = next.alwaysEnablePointers;
        if (sc.disableAllNavigationToggle != null && next.hasDisableAllNavigationToggle)
            sc.disableAllNavigationToggle.isOn = next.disableAllNavigationToggle;
        if (sc.disableGrabNavigationToggle != null && next.hasDisableGrabNavigationToggle)
            sc.disableGrabNavigationToggle.isOn = next.disableGrabNavigationToggle;
        if (sc.rayLineLeft != null && next.hasRayLineLeft)
            sc.rayLineLeft.enabled = next.rayLineLeftEnabled;
        if (sc.rayLineRight != null && next.hasRayLineRight)
            sc.rayLineRight.enabled = next.rayLineRightEnabled;
    }

    private static bool SafeReadBool(System.Func<bool> getter)
    {
        try
        {
            return getter != null && getter();
        }
        catch
        {
            return false;
        }
    }
}
