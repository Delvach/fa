using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FrameAngel.Runtime.Shared;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public partial class FASyncRuntime : MVRScript
{
    private bool TryGetStandalonePlayerState(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        if (HasStandalonePlayerSelector(argsJson))
        {
            StandalonePlayerRecord selectedRecord;
            if (!TryResolveStandalonePlayerRecord(argsJson, out selectedRecord, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }
        }

        string stateJson = BuildStandalonePlayerSelectedStateJson(argsJson);
        resultJson = BuildBrokerResult(true, "player_state ok", stateJson);
        EmitRuntimeEvent(
            "player_state",
            actionId,
            "ok",
            "",
            "player_state ok",
            ExtractJsonArgString(argsJson, "instanceId"),
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            ExtractJsonArgString(argsJson, "displayId", "slotId"),
            stateJson);
        return true;
    }

    private bool TryLoadStandalonePlayerPath(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        string mediaPath = ExtractJsonArgString(argsJson, "mediaPath", "path", "url");
        if (string.IsNullOrEmpty(mediaPath))
        {
            errorMessage = "mediaPath is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        StandalonePlayerRecord record;
        InnerPieceInstanceRecord instance;
        InnerPieceScreenSlotRuntimeRecord slot;
        if (HasStandalonePlayerSelector(argsJson))
        {
            if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage) || record == null)
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            if (IsHostedPlayerInstanceId(record.instanceId))
            {
                List<string> hostedMediaPaths;
                string hostedPlaybackKey = string.IsNullOrEmpty(record.playbackKey)
                    ? BuildStandalonePlayerPlaybackKey(record.instanceId, record.displayId)
                    : record.playbackKey;
                string hostAtomUid = ResolveHostedPlayerHostAtomUid(record);
                if (string.IsNullOrEmpty(hostAtomUid))
                {
                    errorMessage = "hosted player host atom uid not resolved";
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                record.instanceId = BuildHostedPlayerInstanceId(hostAtomUid);
                record.slotId = HostedPlayerSlotId;
                record.displayId = HostedPlayerDisplayId;
                record.playbackKey = hostedPlaybackKey;
                record.aspectMode = ResolveStandalonePlayerAspectMode(argsJson, record.aspectMode);
                record.randomEnabled = TryReadStandalonePlayerRandomEnabled(argsJson, record.randomEnabled);

                if (!TryResolvePlayerRuntimeMediaPaths(mediaPath, out hostedMediaPaths, out errorMessage)
                    || hostedMediaPaths == null
                    || hostedMediaPaths.Count <= 0)
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                mediaPath = ResolvePrimaryPlayerRuntimeMediaPath(mediaPath, hostedMediaPaths);
                record.desiredPlaying = ResolveStandalonePlayerLoadDesiredPlaying(argsJson, mediaPath, record.desiredPlaying);
                SetPendingPlayerSelection(mediaPath);
                List<string> previousHostedPlaylistPaths = new List<string>(record.playlistPaths);
                List<string> nextHostedPlaylistPaths = new List<string>(hostedMediaPaths.Count);
                for (int playlistIndex = 0; playlistIndex < hostedMediaPaths.Count; playlistIndex++)
                {
                    string hostedCandidate = hostedMediaPaths[playlistIndex];
                    if (!string.IsNullOrEmpty(hostedCandidate))
                        nextHostedPlaylistPaths.Add(hostedCandidate);
                }

                if (!AreStandalonePlayerPlaylistsEquivalent(previousHostedPlaylistPaths, nextHostedPlaylistPaths))
                    ClearStandalonePlayerRandomHistory(record);

                record.playlistPaths.Clear();
                for (int playlistIndex = 0; playlistIndex < nextHostedPlaylistPaths.Count; playlistIndex++)
                    record.playlistPaths.Add(nextHostedPlaylistPaths[playlistIndex]);

                int hostedCurrentIndex = FindStandalonePlayerPlaylistIndex(record.playlistPaths, mediaPath);
                record.currentIndex = hostedCurrentIndex >= 0 ? hostedCurrentIndex : 0;
                record.loopMode = ResolveStandalonePlayerLoopMode(argsJson, record.loopMode, record.playlistPaths.Count);
                if (record.randomEnabled)
                    EnsureStandalonePlayerRandomOrder(record, record.currentIndex, false);

                float hostedParsedVolume;
                if (TryExtractJsonFloatField(argsJson, "volume", out hostedParsedVolume))
                {
                    record.storedVolume = Mathf.Clamp01(hostedParsedVolume);
                    record.volume = record.muted ? 0f : record.storedVolume;
                }

                bool hostedParsedMuted;
                if (TryReadBoolArg(argsJson, out hostedParsedMuted, "muted", "mute"))
                {
                    record.muted = hostedParsedMuted;
                    record.volume = record.muted ? 0f : record.storedVolume;
                }

                if (!IsSupportedPlayerRuntimeMediaPath(mediaPath))
                {
                    errorMessage = "hosted player media type unsupported";
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                if (!TryLoadHostedStandalonePlayerRecordPath(record, hostAtomUid, record.playlistPaths, mediaPath, out errorMessage))
                {
                    resultJson = BuildBrokerResult(false, errorMessage, "{}");
                    return false;
                }

                string hostedPayload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(hostedPlaybackKey) + "\"}");
                resultJson = BuildBrokerResult(true, "player_load_path ok", hostedPayload);
                EmitRuntimeEvent(
                    "player_load_path",
                    actionId,
                    "ok",
                    "",
                    hostedPlaybackKey,
                    record.instanceId,
                    ExtractJsonArgString(argsJson, "correlationId"),
                    ExtractJsonArgString(argsJson, "messageId"),
                    record.displayId,
                    hostedPayload);
                return true;
            }

            if (!TryResolveInnerPieceScreenSlot(record.instanceId, record.slotId, out instance, out slot, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }
        }
        else
        {
            if (!TryResolveInnerPieceScreenSlot(
                ExtractJsonArgString(argsJson, "instanceId"),
                ExtractJsonArgString(argsJson, "slotId", "screenSlotId", "displayId"),
                out instance,
                out slot,
                out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }

            string directPlaybackKey = BuildStandalonePlayerPlaybackKey(instance.instanceId, slot.displayId);
            if (!standalonePlayerRecords.TryGetValue(directPlaybackKey, out record) || record == null)
            {
                record = new StandalonePlayerRecord();
                record.playbackKey = directPlaybackKey;
                standalonePlayerRecords[directPlaybackKey] = record;
            }
        }

        string playbackKey = string.IsNullOrEmpty(record.playbackKey)
            ? BuildStandalonePlayerPlaybackKey(instance.instanceId, slot.displayId)
            : record.playbackKey;
        record.instanceId = instance.instanceId;
        record.slotId = slot.slotId;
        record.displayId = slot.displayId;
        record.playbackKey = playbackKey;
        record.aspectMode = ResolveStandalonePlayerAspectMode(argsJson, instance.defaultAspectMode);
        record.randomEnabled = TryReadStandalonePlayerRandomEnabled(argsJson, record.randomEnabled);
        record.desiredPlaying = ResolveStandalonePlayerLoadDesiredPlaying(argsJson, mediaPath, record.desiredPlaying);
        ApplyStandalonePlayerPlaylistArgs(record, argsJson, mediaPath);
        record.loopMode = ResolveStandalonePlayerLoopMode(argsJson, record.loopMode, record.playlistPaths.Count);
        if (record.randomEnabled)
            EnsureStandalonePlayerRandomOrder(record, record.currentIndex, false);

        float parsedVolume;
        if (TryExtractJsonFloatField(argsJson, "volume", out parsedVolume))
        {
            record.storedVolume = Mathf.Clamp01(parsedVolume);
            record.volume = record.muted ? 0f : record.storedVolume;
        }

        bool parsedMuted;
        if (TryReadBoolArg(argsJson, out parsedMuted, "muted", "mute"))
        {
            record.muted = parsedMuted;
            record.volume = record.muted ? 0f : record.storedVolume;
        }

        if (!TryLoadStandalonePlayerRecordPath(record, instance, slot, mediaPath, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_load_path ok", payload);
        EmitRuntimeEvent(
            "player_load_path",
            actionId,
            "ok",
            "",
            playbackKey,
            instance.instanceId,
            ExtractJsonArgString(argsJson, "correlationId"),
            ExtractJsonArgString(argsJson, "messageId"),
            slot.displayId,
            payload);
        return true;
    }

    private bool TrySetStandalonePlayerPlaylist(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        InnerPieceInstanceRecord instance;
        InnerPieceScreenSlotRuntimeRecord slot;
        if (!TryResolveOrCreateStandalonePlayerRecordForWrite(argsJson, out record, out instance, out slot, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        ApplyStandalonePlayerPlaylistArgs(record, argsJson, "");
        if (record.playlistPaths.Count <= 0)
        {
            errorMessage = "playlist is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool loadCurrent;
        if (!TryReadBoolArg(argsJson, out loadCurrent, "loadCurrent", "autoLoad"))
        {
            loadCurrent = string.IsNullOrEmpty(record.mediaPath);
        }

        if (loadCurrent)
        {
            string currentPath = GetStandalonePlayerCurrentPlaylistPath(record);
            record.desiredPlaying = ResolveStandalonePlayerLoadDesiredPlaying(argsJson, currentPath, record.desiredPlaying);
            if (!string.IsNullOrEmpty(currentPath) && !TryLoadStandalonePlayerRecordPath(record, instance, slot, currentPath, out errorMessage))
            {
                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                return false;
            }
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_playlist ok", payload);
        EmitRuntimeEvent(
            "player_playlist",
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

    private bool TryPlayStandalonePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.desiredPlaying = !record.mediaIsStillImage;
        record.nextPlaybackStateApplyTime = Time.unscaledTime + StandalonePlayerPlaybackRetryIntervalSeconds;
        if (!record.mediaIsStillImage && record.prepared && record.videoPlayer != null)
        {
            try
            {
                if (record.abLoopEnabled
                    && HasValidStandalonePlayerAbLoopRange(record, out double startSeconds, out double endSeconds))
                {
                    if (TryReadStandalonePlayerTimeline(record, out double currentTimeSeconds, out _, out string timelineError))
                    {
                        if (currentTimeSeconds < (startSeconds - StandalonePlayerPlaybackMotionEpsilonSeconds)
                            || currentTimeSeconds >= (endSeconds - StandalonePlayerPlaybackMotionEpsilonSeconds))
                        {
                            if (!TrySeekStandalonePlayerRecordToSeconds(record, startSeconds, true, out errorMessage))
                            {
                                resultJson = BuildBrokerResult(false, errorMessage, "{}");
                                return false;
                            }
                        }
                        else if (!record.videoPlayer.isPlaying)
                        {
                            record.videoPlayer.Play();
                        }
                    }
                    else if (!string.IsNullOrEmpty(timelineError))
                    {
                        record.lastError = timelineError;
                    }
                    else if (!record.videoPlayer.isPlaying)
                    {
                        record.videoPlayer.Play();
                    }
                }
                else if (!record.videoPlayer.isPlaying)
                {
                    record.videoPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                record.lastError = "player play failed: " + ex.Message;
            }
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_play ok", payload);
        EmitRuntimeEvent(
            "player_play",
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

    private bool TryPauseStandalonePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.desiredPlaying = false;
        record.seekResumePending = false;
        record.seekResumeTargetSeconds = 0d;
        record.seekResumeRequestedAt = 0f;
        record.nextPlaybackStateApplyTime = Time.unscaledTime + StandalonePlayerPlaybackRetryIntervalSeconds;
        if (record.videoPlayer != null)
        {
            try
            {
                record.videoPlayer.Pause();
            }
            catch (Exception ex)
            {
                record.lastError = "player pause failed: " + ex.Message;
            }
        }

        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_pause ok", payload);
        EmitRuntimeEvent(
            "player_pause",
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

    private bool TrySeekStandalonePlayerToSeconds(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TrySeekStandalonePlayer(actionId, argsJson, false, out resultJson, out errorMessage);
    }

    private bool TrySeekStandalonePlayerNormalized(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TrySeekStandalonePlayer(actionId, argsJson, true, out resultJson, out errorMessage);
    }

    private bool TryNextStandalonePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TryAdvanceStandalonePlayer(actionId, argsJson, true, out resultJson, out errorMessage);
    }

    private bool TryPreviousStandalonePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TryAdvanceStandalonePlayer(actionId, argsJson, false, out resultJson, out errorMessage);
    }

    private bool TrySkipStandalonePlayerBySeconds(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TrySkipStandalonePlayer(actionId, argsJson, 0f, true, out resultJson, out errorMessage);
    }

    private bool TrySkipForwardStandalonePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TrySkipStandalonePlayer(actionId, argsJson, StandalonePlayerDefaultSkipSeconds, false, out resultJson, out errorMessage);
    }

    private bool TrySkipBackwardStandalonePlayer(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        return TrySkipStandalonePlayer(actionId, argsJson, -StandalonePlayerDefaultSkipSeconds, false, out resultJson, out errorMessage);
    }
}
