using UnityEngine;

public partial class FASyncRuntime
{
    private void RunAttachedPlayerDirectAction(string actionId, string extraArgsBody, string successStatus)
    {
        ClearQueuedAttachedPlayerSeekNormalizedAction();
        string selectorJson;
        string errorMessage;
        if (!TryBuildAttachedPlayerSelectorJson(out selectorJson, out errorMessage))
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            RefreshVisiblePlayerDebugFields();
            return;
        }

        string argsJson = BuildAttachedPlayerActionArgsJson(selectorJson, extraArgsBody);
        string resultJson;
        if (!TryExecuteAction(actionId, argsJson, out resultJson, out errorMessage))
        {
            SetLastError(errorMessage);
            SetLastReceipt(resultJson);
            RefreshVisiblePlayerDebugFields();
            return;
        }

        SetLastError("");
        SetLastReceipt(resultJson);
        if (playerRuntimeStateField != null)
            playerRuntimeStateField.valNoCallback = string.IsNullOrEmpty(successStatus) ? "state=ok" : successStatus;
        UpdateAttachedPlayerAspectModeField(ExtractJsonArgString(resultJson, "aspectMode"));
        RefreshVisiblePlayerDebugFields();
    }

    private void QueueAttachedPlayerSeekNormalizedAction(float normalized, string successStatus)
    {
        queuedAttachedPlayerSeekNormalized = true;
        queuedAttachedPlayerSeekNormalizedValue = Mathf.Clamp01(normalized);
        queuedAttachedPlayerSeekNormalizedApplyAt = Time.unscaledTime + StandalonePlayerScrubCommitDebounceSeconds;
        queuedAttachedPlayerSeekNormalizedStatus = string.IsNullOrEmpty(successStatus) ? "Player scrub set" : successStatus;
        if (playerRuntimeStateField != null)
            playerRuntimeStateField.valNoCallback = "Player scrub pending";
    }

    private void TickQueuedAttachedPlayerSeekNormalizedAction()
    {
        if (!queuedAttachedPlayerSeekNormalized || Time.unscaledTime < queuedAttachedPlayerSeekNormalizedApplyAt)
            return;

        float normalized = queuedAttachedPlayerSeekNormalizedValue;
        string successStatus = queuedAttachedPlayerSeekNormalizedStatus;
        ClearQueuedAttachedPlayerSeekNormalizedAction();
        RunAttachedPlayerSeekNormalizedAction(normalized, successStatus);
    }

    private void ClearQueuedAttachedPlayerSeekNormalizedAction()
    {
        queuedAttachedPlayerSeekNormalized = false;
        queuedAttachedPlayerSeekNormalizedValue = 0f;
        queuedAttachedPlayerSeekNormalizedApplyAt = 0f;
        queuedAttachedPlayerSeekNormalizedStatus = "";
    }

    private void RunAttachedPlayerSeekNormalizedAction(float normalized, string successStatus)
    {
        RunAttachedPlayerDirectAction(
            PlayerActionSeekNormalizedId,
            "\"normalized\":" + FormatFloat(Mathf.Clamp01(normalized)),
            successStatus);
    }

    private void RunAttachedPlayerSetVolumeAction(float normalized, string successStatus)
    {
        RunAttachedPlayerDirectAction(
            PlayerActionSetVolumeId,
            "\"volume\":" + FormatFloat(Mathf.Clamp01(normalized)),
            successStatus);
    }

    private void RunAttachedPlayerSetLoopModeAction(string loopMode, string successStatus)
    {
        RunAttachedPlayerDirectAction(
            PlayerActionSetLoopModeId,
            "\"loopMode\":\"" + EscapeJsonString(loopMode) + "\"",
            successStatus);
    }

    private void RunAttachedPlayerSetRandomAction(bool enabled, string successStatus)
    {
        RunAttachedPlayerDirectAction(
            PlayerActionSetRandomId,
            "\"random\":" + (enabled ? "true" : "false"),
            successStatus);
    }

    private void RunAttachedPlayerPlayPauseAction()
    {
        StandalonePlayerRecord record;
        Atom hostAtom;
        bool shouldPause = false;
        if (TryResolveAttachedHostedStandalonePlayerRecord(out record, out hostAtom) && record != null)
        {
            try
            {
                shouldPause = (record.videoPlayer != null && record.videoPlayer.isPlaying) || record.desiredPlaying;
            }
            catch
            {
                shouldPause = record.desiredPlaying;
            }
        }

        RunAttachedPlayerDirectAction(
            shouldPause ? PlayerActionPauseId : PlayerActionPlayId,
            "",
            shouldPause ? "Player paused" : "Player playing");
    }

    private void RunAttachedPlayerResizeAction(float multiplier, string successStatus)
    {
        if (multiplier <= 0f)
        {
            SetLastError("player resize multiplier invalid");
            SetLastReceipt(BuildBrokerResult(false, "player resize multiplier invalid", "{}"));
            return;
        }

        StandalonePlayerRecord record;
        Atom hostAtom;
        if (!TryResolveAttachedHostedStandalonePlayerRecord(out record, out hostAtom) || record == null)
        {
            SetLastError("attached player record not resolved");
            SetLastReceipt(BuildBrokerResult(false, "attached player record not resolved", "{}"));
            RefreshVisiblePlayerDebugFields();
            return;
        }

        FAInnerPiecePlaneData plane;
        string errorMessage;
        if (!TryResolveInnerPieceScreenPlane(record.instanceId, record.slotId, out plane, out errorMessage))
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            RefreshVisiblePlayerDebugFields();
            return;
        }

        float targetWidthMeters = Mathf.Max(0.05f, plane.widthMeters * multiplier);
        float targetHeightMeters = Mathf.Max(0.05f, plane.heightMeters * multiplier);
        RunAttachedPlayerDirectAction(
            PlayerActionSetDisplaySizeId,
            "\"displayWidthMeters\":" + FormatFloat(targetWidthMeters)
                + ",\"displayHeightMeters\":" + FormatFloat(targetHeightMeters)
                + ",\"resizeBehavior\":\"smooth\""
                + ",\"resizeAnchor\":\"bottom_anchor\"",
            successStatus);
    }
}
