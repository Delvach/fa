using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public partial class FASyncRuntime : MVRScript
{
    private const string StandalonePlayerResizeAnchorBottom = "bottom_anchor";
    private const string StandalonePlayerResizeAnchorControls = "controls_anchor";
    private const string StandalonePlayerResizeAnchorScreen = "screen_surface";
    private const string StandalonePlayerResizeAnchorDisconnect = "disconnect_surface";

    // V1 resize keeps the shell and screen coherent by scaling the authored root uniformly.
    // That gives us smooth deterministic resize behavior now without inventing a second
    // transform authority for the display plane.
    private bool TrySetStandalonePlayerDisplaySize(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        InnerPieceInstanceRecord instance;
        if (!innerPieceInstances.TryGetValue(record.instanceId ?? "", out instance) || instance == null)
        {
            errorMessage = "player instance not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        SyncObjectRecord rootRecord;
        if (!syncObjects.TryGetValue(instance.rootObjectId ?? "", out rootRecord) || rootRecord == null)
        {
            errorMessage = "player root object not found";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        FAInnerPiecePlaneData plane;
        if (!TryResolveInnerPieceScreenPlane(record.instanceId, record.slotId, out plane, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        float targetWidthMeters = 0f;
        float targetHeightMeters = 0f;
        bool hasWidth =
            TryExtractJsonFloatField(argsJson, "displayWidthMeters", out targetWidthMeters)
            || TryExtractJsonFloatField(argsJson, "targetWidthMeters", out targetWidthMeters)
            || TryExtractJsonFloatField(argsJson, "widthMeters", out targetWidthMeters);
        bool hasHeight =
            TryExtractJsonFloatField(argsJson, "displayHeightMeters", out targetHeightMeters)
            || TryExtractJsonFloatField(argsJson, "targetHeightMeters", out targetHeightMeters)
            || TryExtractJsonFloatField(argsJson, "heightMeters", out targetHeightMeters);

        if (!hasWidth && !hasHeight)
        {
            errorMessage = "displayWidthMeters or displayHeightMeters is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        if ((hasWidth && targetWidthMeters <= 0f) || (hasHeight && targetHeightMeters <= 0f))
        {
            errorMessage = "display size targets must be positive";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        float widthScale = hasWidth
            ? (targetWidthMeters / Mathf.Max(0.001f, plane.widthMeters))
            : 0f;
        float heightScale = hasHeight
            ? (targetHeightMeters / Mathf.Max(0.001f, plane.heightMeters))
            : 0f;

        if (hasWidth && hasHeight && Mathf.Abs(widthScale - heightScale) > 0.05f)
        {
            errorMessage = "requested width/height would require non-uniform shell scaling";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        float multiplier = hasWidth ? widthScale : heightScale;
        if (hasWidth && hasHeight)
            multiplier = (widthScale + heightScale) * 0.5f;

        if (multiplier <= 0.0001f)
        {
            errorMessage = "display resize multiplier invalid";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        Vector3 targetScale = rootRecord.scale * multiplier;
        targetScale = new Vector3(
            Mathf.Max(MinPrimitiveScale, targetScale.x),
            Mathf.Max(MinPrimitiveScale, targetScale.y),
            Mathf.Max(MinPrimitiveScale, targetScale.z));

        string resizeBehavior = ExtractJsonArgString(argsJson, "resizeBehavior", "behavior");
        if (string.IsNullOrEmpty(resizeBehavior))
            resizeBehavior = "smooth";
        record.resizeBehavior = string.Equals(resizeBehavior, "instant", StringComparison.OrdinalIgnoreCase)
            ? "instant"
            : "smooth";

        string resizeAnchor = ExtractJsonArgString(argsJson, "resizeAnchor");
        record.resizeAnchor = string.IsNullOrEmpty(resizeAnchor)
            ? StandalonePlayerResizeAnchorBottom
            : resizeAnchor.Trim();
        record.targetDisplayWidthMeters = hasWidth ? targetWidthMeters : (plane.widthMeters * multiplier);
        record.targetDisplayHeightMeters = hasHeight ? targetHeightMeters : (plane.heightMeters * multiplier);

        float durationSeconds = 0f;
        if (!TryExtractJsonFloatField(argsJson, "resizeSeconds", out durationSeconds)
            && !TryExtractJsonFloatField(argsJson, "durationSeconds", out durationSeconds))
        {
            durationSeconds = 0.25f;
        }
        durationSeconds = Mathf.Max(0f, durationSeconds);

        StopStandalonePlayerResize(record);
        Vector3 startPosition = rootRecord.position;
        Vector3 anchorWorldPosition;
        Vector3 anchorLocalPoint;
        bool keepAnchorFixed = TryResolveStandalonePlayerResizeAnchor(
            record,
            rootRecord,
            out anchorWorldPosition,
            out anchorLocalPoint);
        if (record.resizeBehavior == "smooth" && durationSeconds > 0.0001f)
        {
            record.resizeInFlight = true;
            record.resizeProgressNormalized = 0f;
            record.resizeStartedAt = Time.unscaledTime;
            record.resizeDurationSeconds = durationSeconds;
            record.resizeCoroutine = StartCoroutine(
                RunStandalonePlayerResize(
                    record,
                    rootRecord,
                    startPosition,
                    targetScale,
                    keepAnchorFixed,
                    anchorWorldPosition,
                    anchorLocalPoint,
                    durationSeconds,
                    actionId,
                    ExtractJsonArgString(argsJson, "correlationId"),
                    ExtractJsonArgString(argsJson, "messageId")));
        }
        else
        {
            ApplyStandalonePlayerResizeStep(
                record,
                rootRecord,
                startPosition,
                targetScale,
                keepAnchorFixed,
                anchorWorldPosition,
                anchorLocalPoint);
            record.resizeInFlight = false;
            record.resizeProgressNormalized = 1f;
            record.resizeStartedAt = 0f;
            record.resizeDurationSeconds = 0f;
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_display_size ok", payload);
        EmitRuntimeEvent(
            "player_display_size",
            actionId,
            "ok",
            "",
            record.playbackKey,
            record.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            record.displayId,
            payload);
        return true;
    }

    private IEnumerator RunStandalonePlayerResize(
        StandalonePlayerRecord record,
        SyncObjectRecord rootRecord,
        Vector3 startPosition,
        Vector3 targetScale,
        bool keepAnchorFixed,
        Vector3 anchorWorldPosition,
        Vector3 anchorLocalPoint,
        float durationSeconds,
        string actionId,
        string correlationId,
        string messageId)
    {
        if (record == null || rootRecord == null)
            yield break;

        Vector3 startScale = rootRecord.scale;
        float startedAt = Time.unscaledTime;
        record.resizeInFlight = true;
        record.resizeProgressNormalized = 0f;
        record.resizeStartedAt = startedAt;
        record.resizeDurationSeconds = durationSeconds;

        while (record != null && rootRecord != null && rootRecord.gameObject != null)
        {
            float elapsed = Time.unscaledTime - startedAt;
            float t = durationSeconds <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / durationSeconds);
            ApplyStandalonePlayerResizeStep(
                record,
                rootRecord,
                startPosition,
                Vector3.Lerp(startScale, targetScale, t),
                keepAnchorFixed,
                anchorWorldPosition,
                anchorLocalPoint);
            record.resizeProgressNormalized = t;
            if (t >= 1f)
                break;
            yield return null;
        }

        if (record != null)
        {
            record.resizeCoroutine = null;
            record.resizeInFlight = false;
            record.resizeProgressNormalized = 1f;
            record.resizeStartedAt = 0f;
            record.resizeDurationSeconds = 0f;

            string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            EmitRuntimeEvent(
                "player_resize_finished",
                actionId,
                "ok",
                "",
                record.playbackKey,
                record.instanceId,
                correlationId,
                messageId,
                record.displayId,
                payload);
        }
    }

    private void StopStandalonePlayerResize(StandalonePlayerRecord record)
    {
        if (record == null || record.resizeCoroutine == null)
            return;

        StopCoroutine(record.resizeCoroutine);
        record.resizeCoroutine = null;
        record.resizeInFlight = false;
        record.resizeProgressNormalized = 1f;
        record.resizeStartedAt = 0f;
        record.resizeDurationSeconds = 0f;
    }

    private bool TryResolveStandalonePlayerResizeAnchor(
        StandalonePlayerRecord record,
        SyncObjectRecord rootRecord,
        out Vector3 anchorWorldPosition,
        out Vector3 anchorLocalPoint)
    {
        anchorWorldPosition = Vector3.zero;
        anchorLocalPoint = Vector3.zero;

        if (record == null || rootRecord == null || rootRecord.gameObject == null)
            return false;

        string anchorNodeId = ResolveStandalonePlayerResizeAnchorNodeId(record.resizeAnchor);
        if (string.IsNullOrEmpty(anchorNodeId))
            return false;

        GameObject anchorObject = FindHostedPlayerNodeObject(rootRecord.gameObject.transform, anchorNodeId);
        if (anchorObject == null || anchorObject.transform == null)
            return false;

        Transform rootTransform = rootRecord.gameObject.transform;
        anchorWorldPosition = anchorObject.transform.position;
        anchorLocalPoint = rootTransform.InverseTransformPoint(anchorWorldPosition);
        return true;
    }

    private string ResolveStandalonePlayerResizeAnchorNodeId(string resizeAnchor)
    {
        string normalized = string.IsNullOrEmpty(resizeAnchor)
            ? StandalonePlayerResizeAnchorBottom
            : resizeAnchor.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "bottom_anchor":
            case "bottom":
            case "bottom_center":
            case "bottom-center":
            case "center_bottom":
            case "base":
                return StandalonePlayerResizeAnchorBottom;
            case "top_anchor":
            case "top":
            case "controls_anchor":
            case "controls":
                return StandalonePlayerResizeAnchorControls;
            case "screen_surface":
            case "screen":
                return StandalonePlayerResizeAnchorScreen;
            case "disconnect_surface":
            case "disconnect":
                return StandalonePlayerResizeAnchorDisconnect;
            default:
                return resizeAnchor == null ? "" : resizeAnchor.Trim();
        }
    }

    private void ApplyStandalonePlayerResizeStep(
        StandalonePlayerRecord record,
        SyncObjectRecord rootRecord,
        Vector3 startPosition,
        Vector3 targetScale,
        bool keepAnchorFixed,
        Vector3 anchorWorldPosition,
        Vector3 anchorLocalPoint)
    {
        if (rootRecord == null || rootRecord.gameObject == null)
            return;

        rootRecord.position = startPosition;
        rootRecord.scale = targetScale;
        ApplyRecordVisuals(rootRecord);

        if (keepAnchorFixed)
        {
            Vector3 currentAnchorWorld = rootRecord.gameObject.transform.TransformPoint(anchorLocalPoint);
            rootRecord.position = startPosition + (anchorWorldPosition - currentAnchorWorld);
            ApplyRecordVisuals(rootRecord);
        }

        if (record != null)
            record.needsScreenRefresh = true;
    }
}
