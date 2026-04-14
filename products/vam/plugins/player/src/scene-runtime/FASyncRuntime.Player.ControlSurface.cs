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
    private bool TryResolveInnerPieceControlSurfaceInstance(
        string argsJson,
        out InnerPieceInstanceRecord instance,
        out FAInnerPieceControlSurfaceData controlSurface,
        out string errorMessage)
    {
        instance = null;
        controlSurface = null;
        errorMessage = "";

        string instanceId = ExtractJsonArgString(argsJson, "controlSurfaceInstanceId", "surfaceInstanceId", "instanceId");
        if (string.IsNullOrEmpty(instanceId))
        {
            errorMessage = "controlSurfaceInstanceId is required";
            return false;
        }

        if (!innerPieceInstances.TryGetValue(instanceId, out instance) || instance == null)
        {
            errorMessage = "control surface instance not found";
            return false;
        }

        controlSurface = instance.controlSurface;
        if (controlSurface == null)
        {
            errorMessage = "control surface contract not found";
            return false;
        }

        return true;
    }

    private bool TryResolvePlayerControlSurfaceBinding(
        string argsJson,
        out PlayerControlSurfaceBindingRecord binding,
        out InnerPieceInstanceRecord controlSurfaceInstance,
        out FAInnerPieceControlSurfaceData controlSurface,
        out string errorMessage)
    {
        binding = null;
        controlSurfaceInstance = null;
        controlSurface = null;
        errorMessage = "";

        if (!TryResolveInnerPieceControlSurfaceInstance(argsJson, out controlSurfaceInstance, out controlSurface, out errorMessage))
            return false;

        if (!playerControlSurfaceBindings.TryGetValue(controlSurfaceInstance.instanceId, out binding) || binding == null)
        {
            errorMessage = "player control surface binding not found";
            return false;
        }

        return true;
    }

    private bool TryResolveStandalonePlayerRecordForControlSurface(
        string argsJson,
        string targetDisplayId,
        out StandalonePlayerRecord record,
        out string errorMessage)
    {
        record = null;
        errorMessage = "";

        string playbackKey = ExtractJsonArgString(argsJson, "playbackKey", "id");
        if (!string.IsNullOrEmpty(playbackKey))
            return TryResolveStandalonePlayerRecord("{\"playbackKey\":\"" + EscapeJsonString(playbackKey) + "\"}", out record, out errorMessage);

        string targetInstanceId = ExtractJsonArgString(argsJson, "targetInstanceId", "playerInstanceId", "screenInstanceId");
        if (!string.IsNullOrEmpty(targetInstanceId))
        {
            StringBuilder selector = new StringBuilder(160);
            selector.Append('{');
            selector.Append("\"instanceId\":\"").Append(EscapeJsonString(targetInstanceId)).Append("\"");
            if (!string.IsNullOrEmpty(targetDisplayId))
                selector.Append(",\"displayId\":\"").Append(EscapeJsonString(targetDisplayId)).Append("\"");
            selector.Append('}');
            if (TryResolveStandalonePlayerRecord(selector.ToString(), out record, out errorMessage))
                return true;

            InnerPieceInstanceRecord targetInstance;
            InnerPieceScreenSlotRuntimeRecord targetSlot;
            string createErrorMessage;
            if (TryResolveOrCreateStandalonePlayerRecordForWrite(
                selector.ToString(),
                out record,
                out targetInstance,
                out targetSlot,
                out createErrorMessage))
            {
                errorMessage = "";
                return true;
            }

            errorMessage = string.IsNullOrEmpty(createErrorMessage) ? errorMessage : createErrorMessage;
            return false;
        }

        if (string.IsNullOrEmpty(targetDisplayId))
        {
            errorMessage = "targetDisplayId not resolved";
            return false;
        }

        List<StandalonePlayerRecord> matches = new List<StandalonePlayerRecord>();
        foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
        {
            StandalonePlayerRecord candidate = kvp.Value;
            if (candidate == null)
                continue;
            if (!AreEquivalentInnerPieceDisplayIds(candidate.displayId, targetDisplayId)
                && !AreEquivalentInnerPieceDisplayIds(candidate.slotId, targetDisplayId))
                continue;

            matches.Add(candidate);
        }

        if (matches.Count == 1)
        {
            record = matches[0];
            return true;
        }

        if (matches.Count > 1)
        {
            errorMessage = "player target ambiguous for displayId " + targetDisplayId + "; pass targetInstanceId or playbackKey";
            return false;
        }

        InnerPieceInstanceRecord liveTargetInstance;
        InnerPieceScreenSlotRuntimeRecord liveTargetSlot;
        if (!TryResolveStandalonePlayerTargetScreenSlotForControlSurface(
            targetDisplayId,
            out liveTargetInstance,
            out liveTargetSlot,
            out errorMessage))
        {
            return false;
        }

        string createArgsJson = "{"
            + "\"instanceId\":\"" + EscapeJsonString(liveTargetInstance.instanceId ?? "") + "\""
            + ",\"displayId\":\"" + EscapeJsonString(liveTargetSlot.displayId ?? "") + "\""
            + "}";

        InnerPieceInstanceRecord createdInstance;
        InnerPieceScreenSlotRuntimeRecord createdSlot;
        if (!TryResolveOrCreateStandalonePlayerRecordForWrite(
            createArgsJson,
            out record,
            out createdInstance,
            out createdSlot,
            out errorMessage))
        {
            return false;
        }

        errorMessage = "";
        return true;
    }

    private bool TryResolveStandalonePlayerTargetScreenSlotForControlSurface(
        string targetDisplayId,
        out InnerPieceInstanceRecord instance,
        out InnerPieceScreenSlotRuntimeRecord slot,
        out string errorMessage)
    {
        instance = null;
        slot = null;
        errorMessage = "";

        if (string.IsNullOrEmpty(targetDisplayId))
        {
            errorMessage = "targetDisplayId not resolved";
            return false;
        }

        foreach (KeyValuePair<string, InnerPieceInstanceRecord> kvp in innerPieceInstances)
        {
            InnerPieceInstanceRecord candidateInstance = kvp.Value;
            if (candidateInstance == null)
                continue;

            InnerPieceScreenSlotRuntimeRecord candidateSlot;
            if (!TryResolveInnerPieceScreenSlotRecord(candidateInstance, targetDisplayId, out candidateSlot)
                || candidateSlot == null)
            {
                continue;
            }

            if (instance != null
                && !string.Equals(instance.instanceId, candidateInstance.instanceId, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "player target ambiguous for displayId " + targetDisplayId + "; pass targetInstanceId or playbackKey";
                instance = null;
                slot = null;
                return false;
            }

            instance = candidateInstance;
            slot = candidateSlot;
        }

        if (instance == null || slot == null)
        {
            errorMessage = "player target screen not found for displayId " + targetDisplayId;
            return false;
        }

        return true;
    }

    private PlayerControlSurfaceBindingRecord BuildStandalonePlayerControlSurfaceBinding(
        InnerPieceInstanceRecord controlSurfaceInstance,
        FAInnerPieceControlSurfaceData controlSurface,
        StandalonePlayerRecord record,
        string targetDisplayId)
    {
        PlayerControlSurfaceBindingRecord binding = new PlayerControlSurfaceBindingRecord();
        binding.controlSurfaceInstanceId = controlSurfaceInstance != null ? (controlSurfaceInstance.instanceId ?? "") : "";
        binding.controlSurfaceResourceId = controlSurfaceInstance != null ? (controlSurfaceInstance.resourceId ?? "") : "";
        binding.controlSurfaceId = controlSurface != null ? (controlSurface.controlSurfaceId ?? "") : "";
        binding.controlFamilyId = controlSurface != null ? (controlSurface.controlFamilyId ?? "") : "";
        binding.controlThemeId = controlSurface != null ? (controlSurface.controlThemeId ?? "") : "";
        binding.controlThemeLabel = controlSurface != null ? (controlSurface.controlThemeLabel ?? "") : "";
        binding.controlThemeVariantId = controlSurface != null ? (controlSurface.controlThemeVariantId ?? "") : "";
        binding.toolkitCategory = controlSurface != null ? (controlSurface.toolkitCategory ?? "") : "";
        binding.sourcePrefabAssetPath = controlSurface != null ? (controlSurface.sourcePrefabAssetPath ?? "") : "";
        binding.targetDisplayId = targetDisplayId ?? "";
        binding.targetKind = "player";
        binding.playbackKey = record != null ? (record.playbackKey ?? "") : "";
        binding.targetInstanceId = record != null ? (record.instanceId ?? "") : "";
        binding.targetSlotId = record != null ? (record.slotId ?? "") : "";
        binding.boundAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        PlayerScreenBindingRecord screenBinding = record != null ? record.binding : null;
        if (screenBinding != null)
        {
            binding.matchedCurrentScreenBinding = AreEquivalentInnerPieceDisplayIds(screenBinding.displayId, targetDisplayId);
            if (binding.matchedCurrentScreenBinding)
            {
                binding.matchedScreenInstanceId = screenBinding.instanceId ?? "";
                binding.matchedScreenSlotId = screenBinding.slotId ?? "";
            }
        }

        return binding;
    }

    private bool HasStandalonePlayerRuntimeBinding(PlayerControlSurfaceBindingRecord binding)
    {
        return binding != null
            && (!string.IsNullOrEmpty(binding.playbackKey)
                || !string.IsNullOrEmpty(binding.targetInstanceId));
    }

    private string BuildPlayerControlSurfaceTargetStateJson(PlayerControlSurfaceBindingRecord binding)
    {
        if (binding == null)
            return "{}";

        if (HasStandalonePlayerRuntimeBinding(binding))
            return BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(binding.playbackKey ?? "") + "\"}");

        return BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(binding.atomUid ?? "") + "\"}");
    }

    private string ResolvePlayerControlSurfaceBindingTargetId(PlayerControlSurfaceBindingRecord binding)
    {
        if (binding == null)
            return "";

        if (HasStandalonePlayerRuntimeBinding(binding))
            return binding.playbackKey ?? "";

        return binding.atomUid ?? "";
    }

    private bool TryResolveBoundStandalonePlayerRecord(
        PlayerControlSurfaceBindingRecord binding,
        out StandalonePlayerRecord record,
        out string errorMessage)
    {
        record = null;
        errorMessage = "";
        if (binding == null)
        {
            errorMessage = "player control surface binding missing";
            return false;
        }

        if (!string.IsNullOrEmpty(binding.playbackKey)
            && standalonePlayerRecords.TryGetValue(binding.playbackKey, out record)
            && record != null)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(binding.targetInstanceId) && !string.IsNullOrEmpty(binding.targetDisplayId))
        {
            return TryResolveStandalonePlayerRecord(
                "{\"instanceId\":\"" + EscapeJsonString(binding.targetInstanceId)
                + "\",\"displayId\":\"" + EscapeJsonString(binding.targetDisplayId) + "\"}",
                out record,
                out errorMessage);
        }

        return TryResolveStandalonePlayerRecordForControlSurface(
            "{}",
            binding.targetDisplayId,
            out record,
            out errorMessage);
    }

    private bool TryTriggerStandalonePlayerControlSurfaceElement(
        PlayerControlSurfaceBindingRecord binding,
        FAInnerPieceControlElementData element,
        string argsJson,
        out string playerOperation,
        out string playerStateJson,
        out string playerReceiptJson,
        out string errorMessage)
    {
        playerOperation = "";
        playerStateJson = "{}";
        playerReceiptJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveBoundStandalonePlayerRecord(binding, out record, out errorMessage))
            return false;

        if (!TryEnsureStandalonePlayerRuntime(record, out errorMessage))
            return false;

        string actionId = ResolveStandalonePlayerControlSurfaceActionId(binding, element);
        if (string.Equals(actionId, "playlist_item_select", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryResolveMetaVideoPlayerPlaylistElementIndex(element, out int playlistIndex))
            {
                errorMessage = "player playlist element index missing";
                return false;
            }

            List<string> mediaPaths;
            bool usingConfiguredMedia;
            if (!TryResolveMetaProofRequestedMediaPaths(out mediaPaths, out usingConfiguredMedia, out errorMessage))
                return false;

            if (playlistIndex < 0 || playlistIndex >= mediaPaths.Count)
            {
                errorMessage = "player playlist item not available";
                return false;
            }

            string mediaPath = mediaPaths[playlistIndex] ?? "";
            if (string.IsNullOrEmpty(mediaPath))
            {
                errorMessage = "player playlist path missing";
                return false;
            }

            string loadArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(record.instanceId) + "\""
                + ",\"displayId\":\"" + EscapeJsonString(record.displayId) + "\""
                + ",\"mediaPath\":\"" + EscapeJsonString(mediaPath) + "\""
                + ",\"playlist\":" + BuildMetaProofSamplePlaylistJson(mediaPaths)
                + ",\"currentIndex\":" + playlistIndex.ToString(CultureInfo.InvariantCulture)
                + ",\"play\":true"
                + "}";
            if (!TryLoadPlayerPath(PlayerActionLoadPathId, loadArgsJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = usingConfiguredMedia ? "player_load_selected_media" : "player_load_playlist_item";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        if (string.Equals(actionId, "load_sample_media", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "load_demo_media", StringComparison.OrdinalIgnoreCase))
        {
            List<string> mediaPaths;
            bool usingConfiguredMedia;
            if (!TryResolveMetaProofRequestedMediaPaths(out mediaPaths, out usingConfiguredMedia, out errorMessage))
                return false;

            string mediaPath = mediaPaths.Count > 0 ? mediaPaths[0] : "";
            if (string.IsNullOrEmpty(mediaPath))
            {
                errorMessage = usingConfiguredMedia
                    ? "player configured media not found"
                    : "player sample media not found";
                return false;
            }

            string loadArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(record.instanceId) + "\""
                + ",\"displayId\":\"" + EscapeJsonString(record.displayId) + "\""
                + ",\"mediaPath\":\"" + EscapeJsonString(mediaPath) + "\""
                + (usingConfiguredMedia ? "" : ",\"playlist\":" + BuildMetaProofSamplePlaylistJson(mediaPaths))
                + ",\"play\":true"
                + "}";
            if (!TryLoadPlayerPath(PlayerActionLoadPathId, loadArgsJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = usingConfiguredMedia ? "player_load_selected_media" : "player_load_sample_media";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }
        if (string.Equals(actionId, "play_pause", StringComparison.OrdinalIgnoreCase))
        {
            bool playing = record.desiredPlaying;
            if (record.videoPlayer != null)
            {
                try
                {
                    playing = record.videoPlayer.isPlaying;
                }
                catch
                {
                }
            }

            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
            bool ok = playing
                ? TryPausePlayer(PlayerActionPauseId, selectorJson, out playerReceiptJson, out errorMessage)
                : TryPlayPlayer(PlayerActionPlayId, selectorJson, out playerReceiptJson, out errorMessage);
            if (!ok)
                return false;

            playerOperation = playing ? "player_pause" : "player_play";
            playerStateJson = BuildStandalonePlayerSelectedStateJson(selectorJson);
            return true;
        }

        if (string.Equals(actionId, "scrub_normalized", StringComparison.OrdinalIgnoreCase))
        {
            float normalizedValue;
            if (!TryExtractJsonFloatField(argsJson, "normalized", out normalizedValue)
                && !TryExtractJsonFloatField(argsJson, "value", out normalizedValue)
                && !TryExtractJsonFloatField(argsJson, "seekNormalized", out normalizedValue))
            {
                errorMessage = "normalized is required";
                return false;
            }

            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey)
                + "\",\"normalized\":" + FormatFloat(Mathf.Clamp01(normalizedValue)) + "}";
            if (!TrySeekPlayerNormalized(PlayerActionSeekNormalizedId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_seek_normalized";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        if (string.Equals(actionId, "skip_backward", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "skip_minus", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "scrub_minus", StringComparison.OrdinalIgnoreCase))
        {
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
            if (!TrySkipBackwardStandalonePlayer(PlayerActionSkipBackwardId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_skip_backward";
            playerStateJson = BuildStandalonePlayerSelectedStateJson(selectorJson);
            return true;
        }

        if (string.Equals(actionId, "skip_forward", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "skip_plus", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "scrub_plus", StringComparison.OrdinalIgnoreCase))
        {
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
            if (!TrySkipForwardStandalonePlayer(PlayerActionSkipForwardId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_skip_forward";
            playerStateJson = BuildStandalonePlayerSelectedStateJson(selectorJson);
            return true;
        }

        if (string.Equals(actionId, "previous", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "prev", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "playlist_previous", StringComparison.OrdinalIgnoreCase))
        {
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
            if (!TryPreviousStandalonePlayer(PlayerActionPreviousId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_previous";
            playerStateJson = BuildStandalonePlayerSelectedStateJson(selectorJson);
            return true;
        }

        if (string.Equals(actionId, "next", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "playlist_next", StringComparison.OrdinalIgnoreCase))
        {
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}";
            if (!TryNextStandalonePlayer(PlayerActionNextId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_next";
            playerStateJson = BuildStandalonePlayerSelectedStateJson(selectorJson);
            return true;
        }

        if (string.Equals(actionId, "volume_normalized", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "volume", StringComparison.OrdinalIgnoreCase))
        {
            float volumeValue;
            if (!TryExtractJsonFloatField(argsJson, "normalized", out volumeValue)
                && !TryExtractJsonFloatField(argsJson, "value", out volumeValue)
                && !TryExtractJsonFloatField(argsJson, "volume", out volumeValue)
                && !TryExtractJsonFloatField(argsJson, "volumeNormalized", out volumeValue))
            {
                errorMessage = "normalized or volume is required";
                return false;
            }

            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey)
                + "\",\"volume\":" + FormatFloat(Mathf.Clamp01(volumeValue)) + "}";
            if (!TrySetStandalonePlayerVolume(PlayerActionSetVolumeId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_set_volume";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        if (string.Equals(actionId, "mute_toggle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "toggle_mute", StringComparison.OrdinalIgnoreCase))
        {
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey)
                + "\",\"muted\":" + (record.muted ? "false" : "true") + "}";
            if (!TrySetStandalonePlayerMute(PlayerActionSetMuteId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_toggle_mute";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        if (string.Equals(actionId, "random_toggle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "toggle_random", StringComparison.OrdinalIgnoreCase))
        {
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey)
                + "\",\"random\":" + (record.randomEnabled ? "false" : "true") + "}";
            if (!TrySetStandalonePlayerRandom(PlayerActionSetRandomId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_toggle_random";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        if (string.Equals(actionId, "loop_toggle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "toggle_loop", StringComparison.OrdinalIgnoreCase))
        {
            string nextLoopMode = string.Equals(record.loopMode, PlayerLoopModeNone, StringComparison.OrdinalIgnoreCase)
                ? PlayerLoopModePlaylist
                : PlayerLoopModeNone;
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey)
                + "\",\"loopMode\":\"" + EscapeJsonString(nextLoopMode) + "\"}";
            if (!TrySetStandalonePlayerLoopMode(PlayerActionSetLoopModeId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_toggle_loop";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        if (string.Equals(actionId, "aspect_cycle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "toggle_aspect", StringComparison.OrdinalIgnoreCase))
        {
            string nextAspectMode = ResolveNextPlayerAspectModeCycle(record.aspectMode);
            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey)
                + "\",\"aspectMode\":\"" + EscapeJsonString(nextAspectMode) + "\"}";
            if (!TrySetStandalonePlayerAspectMode(PlayerActionSetAspectModeId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_cycle_aspect";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        if (string.Equals(actionId, "aspect_mode", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "set_aspect_mode", StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionId, "select_aspect_mode", StringComparison.OrdinalIgnoreCase))
        {
            string requestedAspectMode;
            if (!TryResolveControlSurfaceAspectModeArg(argsJson, record.aspectMode, out requestedAspectMode, out errorMessage))
                return false;

            string selectorJson = "{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey)
                + "\",\"aspectMode\":\"" + EscapeJsonString(requestedAspectMode) + "\"}";
            if (!TrySetStandalonePlayerAspectMode(PlayerActionSetAspectModeId, selectorJson, out playerReceiptJson, out errorMessage))
                return false;

            playerOperation = "player_set_aspect";
            playerStateJson = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
            return true;
        }

        errorMessage = "player control surface action not supported: " + actionId;
        return false;
    }

    private string ResolveStandalonePlayerControlSurfaceActionId(
        PlayerControlSurfaceBindingRecord binding,
        FAInnerPieceControlElementData element)
    {
        string actionId = NormalizeControlSurfaceActionId(element != null ? (element.actionId ?? "") : "");
        if (binding == null
            || !string.Equals(binding.controlFamilyId ?? "", "meta_ui_video_player", StringComparison.OrdinalIgnoreCase))
            return actionId;

        if (string.Equals(actionId, "playercontrols_controlbar_borderlessbutton_iconandlabel", StringComparison.OrdinalIgnoreCase))
            return "load_sample_media";
        if (string.Equals(actionId, "playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_1", StringComparison.OrdinalIgnoreCase))
            return "skip_backward";
        if (string.Equals(actionId, "playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_2", StringComparison.OrdinalIgnoreCase))
            return "skip_forward";
        if (string.Equals(actionId, "playercontrols_controlbar_borderlessbutton_iconandlabel_2", StringComparison.OrdinalIgnoreCase))
            return "previous";
        if (string.Equals(actionId, "playercontrols_controlbar_borderlessbutton_iconandlabel_3", StringComparison.OrdinalIgnoreCase))
            return "next";
        if (string.Equals(actionId, "playercontrols_control_sound_borderlessbutton_iconandlabel", StringComparison.OrdinalIgnoreCase))
            return "mute_toggle";
        if (TryResolveMetaVideoPlayerPlaylistElementIndex(element, out int playlistIndex) && playlistIndex >= 0)
            return "playlist_item_select";

        return actionId;
    }

    private bool TryResolveMetaVideoPlayerPlaylistElementIndex(FAInnerPieceControlElementData element, out int playlistIndex)
    {
        playlistIndex = -1;
        if (element == null)
            return false;

        const string prefix = "playercontrols_tilebuttons_scroll_view_viewport_content_texttilebutton_iconandlabel_regular";
        string elementId = element.elementId ?? "";
        string actionId = element.actionId ?? "";
        string identity = !string.IsNullOrEmpty(actionId) ? actionId : elementId;
        if (string.IsNullOrEmpty(identity))
            identity = elementId;

        if (!identity.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string suffix = identity.Length > prefix.Length ? identity.Substring(prefix.Length) : "";
        if (string.IsNullOrEmpty(suffix))
        {
            playlistIndex = 0;
            return true;
        }

        if (suffix.StartsWith("_", StringComparison.Ordinal)
            && int.TryParse(suffix.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex)
            && parsedIndex >= 0)
        {
            playlistIndex = parsedIndex;
            return true;
        }

        return false;
    }

    private string ResolvePlayerControlSurfaceTargetDisplayId(string argsJson, FAInnerPieceControlSurfaceData controlSurface)
    {
        string targetDisplayId = ExtractJsonArgString(argsJson, "targetDisplayId", "displayId", "slotId");
        if (!string.IsNullOrEmpty(targetDisplayId))
            return targetDisplayId;
        if (controlSurface == null)
            return "";
        if (!string.IsNullOrEmpty(controlSurface.defaultTargetDisplayId))
            return controlSurface.defaultTargetDisplayId;
        string[] targetDisplayIds = controlSurface.targetDisplayIds ?? new string[0];
        return targetDisplayIds.Length > 0 ? (targetDisplayIds[0] ?? "") : "";
    }

    private bool TryResolvePlayerControlSurfaceElement(
        FAInnerPieceControlSurfaceData controlSurface,
        string argsJson,
        out FAInnerPieceControlElementData element,
        out string errorMessage)
    {
        element = null;
        errorMessage = "";

        if (controlSurface == null)
        {
            errorMessage = "control surface contract missing";
            return false;
        }

        string elementSelector = ExtractJsonArgString(argsJson, "elementId");
        string actionSelector = NormalizeControlSurfaceActionId(ExtractJsonArgString(argsJson, "actionId", "controlActionId"));
        bool headerRequested = false;
        TryReadBoolArg(argsJson, out headerRequested, "header", "selectorHeader", "useHeader");

        int optionIndex = -1;
        float optionIndexValue;
        if (TryExtractJsonFloatField(argsJson, "optionIndex", out optionIndexValue)
            || TryExtractJsonFloatField(argsJson, "selectorIndex", out optionIndexValue)
            || TryExtractJsonFloatField(argsJson, "index", out optionIndexValue))
        {
            optionIndex = Mathf.Max(-1, Mathf.RoundToInt(optionIndexValue));
        }

        int optionNumber = -1;
        float optionNumberValue;
        if (TryExtractJsonFloatField(argsJson, "optionNumber", out optionNumberValue)
            || TryExtractJsonFloatField(argsJson, "selectorNumber", out optionNumberValue))
        {
            optionNumber = Mathf.Max(-1, Mathf.RoundToInt(optionNumberValue));
        }

        bool hasSelectorAddress = headerRequested || optionIndex >= 0 || optionNumber > 0;
        if (string.IsNullOrEmpty(elementSelector) && string.IsNullOrEmpty(actionSelector) && !hasSelectorAddress)
        {
            errorMessage = "elementId or actionId is required";
            return false;
        }

        FAInnerPieceControlElementData[] elements = controlSurface.elements ?? new FAInnerPieceControlElementData[0];
        if (hasSelectorAddress && IsSelectorToolkitSurface(controlSurface))
        {
            int currentOptionIndex = 0;
            for (int i = 0; i < elements.Length; i++)
            {
                FAInnerPieceControlElementData candidate = elements[i];
                if (candidate == null)
                    continue;

                if (headerRequested && IsSelectorHeaderElement(candidate))
                {
                    element = candidate;
                    return true;
                }

                if (!IsSelectorOptionElement(controlSurface, candidate))
                    continue;

                if (optionIndex >= 0 && currentOptionIndex == optionIndex)
                {
                    element = candidate;
                    return true;
                }

                if (optionNumber > 0 && currentOptionIndex + 1 == optionNumber)
                {
                    element = candidate;
                    return true;
                }

                currentOptionIndex++;
            }
        }

        for (int i = 0; i < elements.Length; i++)
        {
            FAInnerPieceControlElementData candidate = elements[i];
            if (candidate == null)
                continue;

            if (!string.IsNullOrEmpty(elementSelector)
                && string.Equals(candidate.elementId, elementSelector, StringComparison.OrdinalIgnoreCase))
            {
                element = candidate;
                return true;
            }

            if (!string.IsNullOrEmpty(actionSelector)
                && string.Equals(NormalizeControlSurfaceActionId(candidate.actionId), actionSelector, StringComparison.OrdinalIgnoreCase))
            {
                element = candidate;
                return true;
            }
        }

        errorMessage = "control surface element not found";
        return false;
    }

    private bool TryBuildPlayerControlSurfaceCommand(
        JSONStorable consumer,
        FAInnerPieceControlElementData element,
        string argsJson,
        out string commandJson,
        out string playerOperation,
        out string errorMessage)
    {
        commandJson = "{}";
        playerOperation = "";
        errorMessage = "";

        if (element == null)
        {
            errorMessage = "control surface element missing";
            return false;
        }

        string actionId = NormalizeControlSurfaceActionId(element.actionId);
        if (string.Equals(actionId, "play_pause", StringComparison.OrdinalIgnoreCase))
        {
            string stateJson;
            bool playing = false;
            if (TryReadPlayerStateJson(consumer, out stateJson))
                TryReadBoolArg(stateJson, out playing, "playing");

            playerOperation = playing ? "player_pause" : "player_play";
            commandJson = "{\"operation\":\"" + (playing ? "pause" : "play") + "\"}";
            return true;
        }

        if (string.Equals(actionId, "scrub_normalized", StringComparison.OrdinalIgnoreCase))
        {
            float normalized;
            if (!TryExtractJsonFloatField(argsJson, "normalized", out normalized)
                && !TryExtractJsonFloatField(argsJson, "value", out normalized)
                && !TryExtractJsonFloatField(argsJson, "scrubNormalized", out normalized))
            {
                errorMessage = "normalized value is required for scrub_normalized";
                return false;
            }

            playerOperation = "player_seek_normalized";
            commandJson = "{"
                + "\"operation\":\"seek_normalized\""
                + ",\"normalized\":" + FormatFloat(Mathf.Clamp01(normalized))
                + "}";
            return true;
        }

        errorMessage = "unsupported control surface actionId: " + actionId;
        return false;
    }

    private string NormalizeControlSurfaceActionId(string actionId)
    {
        actionId = string.IsNullOrEmpty(actionId) ? "" : actionId.Trim();
        if (string.IsNullOrEmpty(actionId))
            return "";

        if (string.Equals(actionId, "overlay_play_pause", StringComparison.OrdinalIgnoreCase))
            return "play_pause";
        if (string.Equals(actionId, "overlay_progress", StringComparison.OrdinalIgnoreCase))
            return "scrub_normalized";
        if (string.Equals(actionId, "overlay_volume", StringComparison.OrdinalIgnoreCase))
            return "volume_normalized";
        if (string.Equals(actionId, "overlay_skip_back", StringComparison.OrdinalIgnoreCase))
            return "skip_backward";
        if (string.Equals(actionId, "overlay_skip_forward", StringComparison.OrdinalIgnoreCase))
            return "skip_forward";
        if (string.Equals(actionId, "overlay_prev", StringComparison.OrdinalIgnoreCase))
            return "previous";
        if (string.Equals(actionId, "overlay_next", StringComparison.OrdinalIgnoreCase))
            return "next";
        if (string.Equals(actionId, "overlay_mute", StringComparison.OrdinalIgnoreCase))
            return "mute_toggle";
        if (string.Equals(actionId, "overlay_random", StringComparison.OrdinalIgnoreCase))
            return "random_toggle";
        if (string.Equals(actionId, "overlay_loop", StringComparison.OrdinalIgnoreCase))
            return "loop_toggle";
        if (string.Equals(actionId, "overlay_fix_aspect", StringComparison.OrdinalIgnoreCase))
            return "aspect_cycle";

        return actionId;
    }

    private bool TryResolveControlSurfaceAspectModeArg(
        string argsJson,
        string defaultAspectMode,
        out string aspectMode,
        out string errorMessage)
    {
        aspectMode = "";
        errorMessage = "";

        string requestedMode = ExtractJsonArgString(argsJson, "aspectMode", "displayMode", "screenMode", "value", "selectedValue", "optionId");
        if (string.IsNullOrEmpty(requestedMode))
        {
            errorMessage = "aspectMode or selector value is required";
            return false;
        }

        string resolvedMode = ResolveStandalonePlayerAspectMode("{\"aspectMode\":\"" + EscapeJsonString(requestedMode) + "\"}", defaultAspectMode);
        if (string.IsNullOrEmpty(resolvedMode))
        {
            errorMessage = "aspectMode not recognized";
            return false;
        }

        aspectMode = resolvedMode;
        return true;
    }

    private string BuildPlayerControlSurfaceReceiptPayload(
        string summary,
        PlayerControlSurfaceBindingRecord binding,
        FAInnerPieceControlElementData element,
        string playerStateJson,
        string playerReceiptJson)
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.Append('{');
        sb.Append("\"schemaVersion\":\"").Append(EscapeJsonString(PlayerControlSurfaceReceiptSchemaVersion)).Append("\",");
        sb.Append("\"summary\":\"").Append(EscapeJsonString(summary ?? "")).Append("\",");
        sb.Append("\"binding\":").Append(BuildPlayerControlSurfaceBindingJson(binding)).Append(',');
        if (element != null)
        {
            sb.Append("\"element\":{");
            sb.Append("\"elementId\":\"").Append(EscapeJsonString(element.elementId ?? "")).Append("\",");
            sb.Append("\"elementLabel\":\"").Append(EscapeJsonString(element.elementLabel ?? "")).Append("\",");
            sb.Append("\"actionId\":\"").Append(EscapeJsonString(element.actionId ?? "")).Append("\",");
            sb.Append("\"elementKind\":\"").Append(EscapeJsonString(element.elementKind ?? "")).Append("\",");
            sb.Append("\"valueKind\":\"").Append(EscapeJsonString(element.valueKind ?? "")).Append("\"");
            sb.Append("},");
        }
        sb.Append("\"playerState\":").Append(string.IsNullOrEmpty(playerStateJson) ? "{}" : playerStateJson).Append(',');
        sb.Append("\"playerReceipt\":").Append(string.IsNullOrEmpty(playerReceiptJson) ? "{}" : playerReceiptJson);
        sb.Append('}');
        return sb.ToString();
    }

    private string BuildPlayerControlSurfaceBindingJson(PlayerControlSurfaceBindingRecord binding)
    {
        if (binding == null)
            return "{}";

        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"schemaVersion\":\"").Append(EscapeJsonString(PlayerControlSurfaceBindingSchemaVersion)).Append("\",");
        sb.Append("\"controlSurfaceInstanceId\":\"").Append(EscapeJsonString(binding.controlSurfaceInstanceId ?? "")).Append("\",");
        sb.Append("\"controlSurfaceResourceId\":\"").Append(EscapeJsonString(binding.controlSurfaceResourceId ?? "")).Append("\",");
        sb.Append("\"controlSurfaceId\":\"").Append(EscapeJsonString(binding.controlSurfaceId ?? "")).Append("\",");
        sb.Append("\"controlFamilyId\":\"").Append(EscapeJsonString(binding.controlFamilyId ?? "")).Append("\",");
        sb.Append("\"controlThemeId\":\"").Append(EscapeJsonString(binding.controlThemeId ?? "")).Append("\",");
        sb.Append("\"controlThemeLabel\":\"").Append(EscapeJsonString(binding.controlThemeLabel ?? "")).Append("\",");
        sb.Append("\"controlThemeVariantId\":\"").Append(EscapeJsonString(binding.controlThemeVariantId ?? "")).Append("\",");
        sb.Append("\"toolkitCategory\":\"").Append(EscapeJsonString(binding.toolkitCategory ?? "")).Append("\",");
        sb.Append("\"sourcePrefabAssetPath\":\"").Append(EscapeJsonString(binding.sourcePrefabAssetPath ?? "")).Append("\",");
        sb.Append("\"targetDisplayId\":\"").Append(EscapeJsonString(binding.targetDisplayId ?? "")).Append("\",");
        sb.Append("\"targetKind\":\"").Append(EscapeJsonString(binding.targetKind ?? "")).Append("\",");
        sb.Append("\"atomUid\":\"").Append(EscapeJsonString(binding.atomUid ?? "")).Append("\",");
        sb.Append("\"playbackKey\":\"").Append(EscapeJsonString(binding.playbackKey ?? "")).Append("\",");
        sb.Append("\"targetInstanceId\":\"").Append(EscapeJsonString(binding.targetInstanceId ?? "")).Append("\",");
        sb.Append("\"targetSlotId\":\"").Append(EscapeJsonString(binding.targetSlotId ?? "")).Append("\",");
        sb.Append("\"boundAtUtc\":\"").Append(EscapeJsonString(binding.boundAtUtc ?? "")).Append("\",");
        sb.Append("\"matchedCurrentScreenBinding\":").Append(binding.matchedCurrentScreenBinding ? "true" : "false").Append(',');
        sb.Append("\"matchedScreenInstanceId\":\"").Append(EscapeJsonString(binding.matchedScreenInstanceId ?? "")).Append("\",");
        sb.Append("\"matchedScreenSlotId\":\"").Append(EscapeJsonString(binding.matchedScreenSlotId ?? "")).Append("\",");
        sb.Append("\"lastElementId\":\"").Append(EscapeJsonString(binding.lastElementId ?? "")).Append("\",");
        sb.Append("\"lastActionId\":\"").Append(EscapeJsonString(binding.lastActionId ?? "")).Append("\"");
        sb.Append('}');
        return sb.ToString();
    }

    private string EnsurePlayerOperation(string argsJson, string operation)
    {
        string trimmed = string.IsNullOrEmpty(argsJson) ? "{}" : argsJson.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "{}")
            return "{\"operation\":\"" + EscapeJsonString(operation) + "\"}";

        if (TryExtractJsonStringField(trimmed, "operation", out string existingOperation) && !string.IsNullOrEmpty(existingOperation))
            return trimmed;

        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
            return "{\"operation\":\"" + EscapeJsonString(operation) + "\"}";

        string body = trimmed.Substring(1, trimmed.Length - 2).Trim();
        if (string.IsNullOrEmpty(body))
            return "{\"operation\":\"" + EscapeJsonString(operation) + "\"}";

        return "{\"operation\":\"" + EscapeJsonString(operation) + "\"," + body + "}";
    }
}
