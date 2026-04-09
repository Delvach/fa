internal static class RuntimeMessageCatalog
{
    internal const string CodeIntegrityOk = "L000";
    internal const string CodeIntegrityMissing = "L001";
    internal const string CodeIntegrityMalformed = "L002";

    internal const string CodeCapturePresetApplied = "M101";
    internal const string CodeWarmStopped = "M110";
    internal const string CodeWarmNeedsMedia = "M111";
    internal const string CodeWarmProgress = "M112";
    internal const string CodeWarmComplete = "M113";
    internal const string CodeScaleParamMissing = "M120";
    internal const string CodeStickUnstable = "M130";
    internal const string CodeTiltFallback = "M140";
    internal const string CodeCalibrationAutoPass = "M150";
    internal const string CodeCalibrationNeedsGuided = "M151";
    internal const string CodeCalibrationGuidedPass = "M152";
    internal const string CodeMediaDialogDispatch = "M160";
    internal const string CodeMediaDialogQueued = "M161";
    internal const string CodeMediaDialogFallback = "M162";
    internal const string CodeMediaDialogFailed = "M163";
    internal const string CodeOverlayTrace = "M164";
    internal const string CodePresetSaved = "S040";
    internal const string CodePresetApplied = "S041";
    internal const string CodePresetCleared = "S042";
    internal const string CodeDemoPathBlocked = "D001";
    internal const string CodeDemoSequenceStep = "D100";
    internal const string CodeDemoSequenceDone = "D101";
    internal const string CodeDemoSequenceInvalid = "D102";
    internal const string CodeDemoSequenceStopped = "D103";
    internal const string CodeDemoNarrationCue = "D110";
    internal const string CodeVoiceSuccess = "V000";
    internal const string CodeVoiceBlockedNoMedia = "V010";
    internal const string CodeVoiceBlockedState = "V011";
    internal const string CodeVoiceBlockedRuntime = "V012";
    internal const string CodeVoiceUnknownCommand = "V013";
    internal const string CodeVoiceInvalidArgument = "V014";
    internal const string CodeVoiceFavoriteMissing = "V015";

    internal const string CodeSessionReady = "S001";
    internal const string CodeSessionSpawnPoseUnavailable = "S010";
    internal const string CodeSessionSpawnFailed = "S011";
    internal const string CodeSessionSpawnTimedOut = "S012";
    internal const string CodeSessionSpawnNoSelection = "S013";
    internal const string CodeSessionPluginPathMissing = "S020";
    internal const string CodeSessionPluginManagerMissing = "S021";
    internal const string CodeSessionPluginSlotFailed = "S022";
    internal const string CodeSessionPluginReloadFailed = "S023";
    internal const string CodeSessionBridgeMissing = "S024";
    internal const string CodeSessionLoaded = "S025";
    internal const string CodeSessionSpawnMode = "S030";
    internal const string CodeSessionNoPanels = "S031";
    internal const string CodeSessionActivated = "S032";

    internal static string Format(string code, string devText)
    {
        return BuildRuntimeInfo.FormatDiagnostic(code, devText);
    }
}
