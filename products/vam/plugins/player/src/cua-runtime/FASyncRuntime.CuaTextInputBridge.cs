using System;

public partial class FASyncRuntime : MVRScript
{
    // CUA player stays core-only: text-entry surfaces can exist on authored controls
    // without pulling the Harp keyboard bridge and its broader session/input stack
    // into the distributable player DLL.
    private bool HasHarpTextInputBridge()
    {
        return false;
    }

    private bool TryGetHarpKeyboardBridgeState(
        out bool keyboardVisible,
        out string placementMode,
        out string preferredHand,
        out string focusedFieldId)
    {
        keyboardVisible = false;
        placementMode = "";
        preferredHand = "";
        focusedFieldId = "";
        return false;
    }

    private void NotifyHarpTextInputFocusChanged(
        string controlSurfaceInstanceId,
        string elementId,
        string operation)
    {
    }
}
