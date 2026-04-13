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
    private bool TrySetStandalonePlayerVolume(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        float volume;
        if (!TryExtractJsonFloatField(argsJson, "volume", out volume))
        {
            errorMessage = "volume is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.storedVolume = Mathf.Clamp01(volume);
        record.volume = record.muted ? 0f : record.storedVolume;
        ApplyStandalonePlayerAudioState(record);
        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_volume ok", payload);
        EmitRuntimeEvent(
            "player_volume",
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

    private bool TrySetStandalonePlayerMute(string actionId, string argsJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        StandalonePlayerRecord record;
        if (!TryResolveStandalonePlayerRecord(argsJson, out record, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        bool muted;
        if (!TryReadBoolArg(argsJson, out muted, "muted", "mute"))
        {
            errorMessage = "muted is required";
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        record.muted = muted;
        record.volume = record.muted ? 0f : record.storedVolume;
        ApplyStandalonePlayerAudioState(record);
        string payload = BuildStandalonePlayerSelectedStateJson("{\"playbackKey\":\"" + EscapeJsonString(record.playbackKey) + "\"}");
        resultJson = BuildBrokerResult(true, "player_mute ok", payload);
        EmitRuntimeEvent(
            "player_mute",
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
}
