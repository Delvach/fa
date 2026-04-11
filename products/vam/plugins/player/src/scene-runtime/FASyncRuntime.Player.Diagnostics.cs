using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Video;

public partial class FASyncRuntime : MVRScript
{
    private const string PlayerDiagnosticsLogPrefix = "[FA.PlayerDiag]";

    // Keep the controls hidden so normal product UI stays clean, but leave them
    // storable/restorable so dedicated diagnostics scenes can preload them.
    private JSONStorableBool playerDiagnosticsEnabledField;
    private JSONStorableString playerDiagnosticsFilterField;
    private JSONStorableAction playerDiagnosticsDumpAction;
    private bool playerDiagnosticsEnabled = false;
    private string playerDiagnosticsFilter = "";

    private void BuildPlayerDiagnosticsStorables()
    {
        playerDiagnosticsEnabledField = new JSONStorableBool("FA Player Diagnostics Enabled", playerDiagnosticsEnabled);
        playerDiagnosticsEnabledField.setCallbackFunction = delegate(bool value)
        {
            playerDiagnosticsEnabled = value;
            LogStandalonePlayerDiagnostics(
                null,
                value ? "diagnostics_enabled" : "diagnostics_disabled",
                "filter=" + BuildStandalonePlayerDiagnosticsFilterSummary(),
                true);
        };
        playerDiagnosticsEnabledField.hidden = true;

        playerDiagnosticsFilterField = new JSONStorableString("FA Player Diagnostics Filter", playerDiagnosticsFilter);
        playerDiagnosticsFilterField.setCallbackFunction = delegate(string value)
        {
            playerDiagnosticsFilter = string.IsNullOrEmpty(value) ? "" : value.Trim();
            if (playerDiagnosticsEnabled)
            {
                LogStandalonePlayerDiagnostics(
                    null,
                    "diagnostics_filter",
                    "filter=" + BuildStandalonePlayerDiagnosticsFilterSummary(),
                    true);
            }
        };
        playerDiagnosticsFilterField.hidden = true;

        playerDiagnosticsDumpAction = new JSONStorableAction(
            "FA Player Diagnostics Dump",
            delegate
            {
                DumpStandalonePlayerDiagnostics();
            });
    }

    private void RegisterPlayerDiagnosticsStorables()
    {
        if (playerDiagnosticsEnabledField != null)
            RegisterBool(playerDiagnosticsEnabledField);
        if (playerDiagnosticsFilterField != null)
            RegisterString(playerDiagnosticsFilterField);
        if (playerDiagnosticsDumpAction != null)
            RegisterAction(playerDiagnosticsDumpAction);
    }

    private string BuildStandalonePlayerDiagnosticsFilterSummary()
    {
        return string.IsNullOrEmpty(playerDiagnosticsFilter) ? "(all)" : playerDiagnosticsFilter;
    }

    private void DumpStandalonePlayerDiagnostics()
    {
        int recordCount = standalonePlayerRecords != null ? standalonePlayerRecords.Count : 0;
        LogStandalonePlayerDiagnostics(
            null,
            "diagnostics_dump_begin",
            "recordCount=" + recordCount.ToString(CultureInfo.InvariantCulture)
                + " filter=" + BuildStandalonePlayerDiagnosticsFilterSummary(),
            true);

        if (standalonePlayerRecords != null)
        {
            foreach (KeyValuePair<string, StandalonePlayerRecord> kvp in standalonePlayerRecords)
            {
                if (kvp.Value == null)
                    continue;

                LogStandalonePlayerDiagnostics(
                    kvp.Value,
                    "diagnostics_dump",
                    BuildStandalonePlayerDiagnosticsSnapshot(kvp.Value),
                    true);
            }
        }

        LogStandalonePlayerDiagnostics(
            null,
            "diagnostics_dump_end",
            "recordCount=" + recordCount.ToString(CultureInfo.InvariantCulture),
            true);
    }

    private void LogStandalonePlayerDiagnostics(StandalonePlayerRecord record, string stage, string detail)
    {
        LogStandalonePlayerDiagnostics(record, stage, detail, false);
    }

    private void LogStandalonePlayerDiagnostics(StandalonePlayerRecord record, string stage, string detail, bool force)
    {
        if (!ShouldLogStandalonePlayerDiagnostics(record, stage, detail, force))
            return;

        StringBuilder sb = new StringBuilder(512);
        sb.Append(PlayerDiagnosticsLogPrefix);
        if (!string.IsNullOrEmpty(stage))
            sb.Append(' ').Append(stage);

        string identity = BuildStandalonePlayerDiagnosticsIdentity(record);
        if (!string.IsNullOrEmpty(identity))
            sb.Append(' ').Append(identity);

        if (!string.IsNullOrEmpty(detail))
            sb.Append(" :: ").Append(detail);

        try
        {
            SuperController.LogMessage(sb.ToString());
        }
        catch
        {
        }
    }

    private bool ShouldLogStandalonePlayerDiagnostics(StandalonePlayerRecord record, string stage, string detail, bool force)
    {
        if (force)
            return true;

        if (!playerDiagnosticsEnabled)
            return false;

        string filter = string.IsNullOrEmpty(playerDiagnosticsFilter) ? "" : playerDiagnosticsFilter.Trim();
        if (string.IsNullOrEmpty(filter))
            return true;

        if (ContainsStandalonePlayerDiagnosticsTerm(stage, filter)
            || ContainsStandalonePlayerDiagnosticsTerm(detail, filter))
        {
            return true;
        }

        if (record == null)
            return false;

        return ContainsStandalonePlayerDiagnosticsTerm(record.playbackKey, filter)
            || ContainsStandalonePlayerDiagnosticsTerm(record.displayId, filter)
            || ContainsStandalonePlayerDiagnosticsTerm(record.instanceId, filter)
            || ContainsStandalonePlayerDiagnosticsTerm(record.slotId, filter)
            || ContainsStandalonePlayerDiagnosticsTerm(record.mediaPath, filter)
            || ContainsStandalonePlayerDiagnosticsTerm(record.resolvedMediaPath, filter)
            || ContainsStandalonePlayerDiagnosticsTerm(TryGetPathLeafName(record.mediaPath), filter)
            || ContainsStandalonePlayerDiagnosticsTerm(TryGetPathLeafName(record.resolvedMediaPath), filter);
    }

    private static bool ContainsStandalonePlayerDiagnosticsTerm(string value, string filter)
    {
        return !string.IsNullOrEmpty(value)
            && !string.IsNullOrEmpty(filter)
            && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string BuildStandalonePlayerDiagnosticsIdentity(StandalonePlayerRecord record)
    {
        if (record == null)
            return "";

        StringBuilder sb = new StringBuilder(192);
        sb.Append("key=").Append(string.IsNullOrEmpty(record.playbackKey) ? "(none)" : record.playbackKey);
        if (!string.IsNullOrEmpty(record.displayId))
            sb.Append(" display=").Append(record.displayId);
        if (!string.IsNullOrEmpty(record.instanceId))
            sb.Append(" instance=").Append(record.instanceId);
        if (!string.IsNullOrEmpty(record.slotId))
            sb.Append(" slot=").Append(record.slotId);
        sb.Append(" hosted=").Append(IsHostedPlayerInstanceId(record.instanceId) ? "true" : "false");

        string mediaLeaf = TryGetPathLeafName(!string.IsNullOrEmpty(record.mediaPath) ? record.mediaPath : record.resolvedMediaPath);
        if (!string.IsNullOrEmpty(mediaLeaf))
            sb.Append(" media=").Append(mediaLeaf);

        return sb.ToString();
    }

    private string BuildStandalonePlayerDiagnosticsSnapshot(StandalonePlayerRecord record)
    {
        if (record == null)
            return "record=(null)";

        StringBuilder sb = new StringBuilder(320);
        sb.Append("prepared=").Append(record.prepared ? "true" : "false");
        sb.Append(" preparePending=").Append(record.preparePending ? "true" : "false");
        sb.Append(" desiredPlaying=").Append(record.desiredPlaying ? "true" : "false");
        sb.Append(" kind=").Append(record.mediaIsStillImage ? "image" : "video");
        sb.Append(" loop=").Append(NormalizeStandalonePlayerLoopMode(record.loopMode));
        sb.Append(" random=").Append(record.randomEnabled ? "on" : "off");
        sb.Append(" playlistCount=")
            .Append(record.playlistPaths != null ? record.playlistPaths.Count.ToString(CultureInfo.InvariantCulture) : "0");
        sb.Append(" rt=")
            .Append(record.textureWidth.ToString(CultureInfo.InvariantCulture))
            .Append('x')
            .Append(record.textureHeight.ToString(CultureInfo.InvariantCulture));
        AppendStandalonePlayerDiagnosticsTimeline(record, sb);
        sb.Append(" audio={").Append(BuildStandalonePlayerDiagnosticsAudioSummary(record)).Append('}');

        if (!string.IsNullOrEmpty(record.lastError))
            sb.Append(" lastError=").Append(record.lastError);

        return sb.ToString();
    }

    private void AppendStandalonePlayerDiagnosticsTimeline(StandalonePlayerRecord record, StringBuilder sb)
    {
        if (record == null || sb == null)
            return;

        if (record.mediaIsStillImage)
        {
            sb.Append(" time=still");
            return;
        }

        double currentTimeSeconds;
        double durationSeconds;
        string errorMessage;
        if (!TryReadStandalonePlayerTimeline(record, out currentTimeSeconds, out durationSeconds, out errorMessage))
        {
            sb.Append(" time=unavailable");
            return;
        }

        sb.Append(" time=")
            .Append(currentTimeSeconds.ToString("0.###", CultureInfo.InvariantCulture))
            .Append('/')
            .Append(durationSeconds.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private string BuildStandalonePlayerDiagnosticsAudioSummary(StandalonePlayerRecord record)
    {
        if (record == null)
            return "record=(null)";

        StringBuilder sb = new StringBuilder(160);
        sb.Append("mute=").Append(record.muted ? "true" : "false");
        sb.Append(" stored=").Append(record.storedVolume.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(" gain=").Append(MapStandalonePlayerNormalizedVolumeToAudioGain(record.storedVolume).ToString("0.###", CultureInfo.InvariantCulture));

        if (record.audioSource != null)
        {
            sb.Append(" spatial=").Append(record.audioSource.spatialBlend.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(" sourceMute=").Append(record.audioSource.mute ? "true" : "false");
            sb.Append(" sourceVol=").Append(record.audioSource.volume.ToString("0.###", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(" source=missing");
        }

        if (record.videoPlayer != null)
        {
            sb.Append(" output=").Append(record.videoPlayer.audioOutputMode.ToString());
            sb.Append(" controlledTracks=")
                .Append(record.videoPlayer.controlledAudioTrackCount.ToString(CultureInfo.InvariantCulture));
            try
            {
                sb.Append(" channels=")
                    .Append(record.videoPlayer.GetAudioChannelCount(0).ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
            }
        }
        else
        {
            sb.Append(" video=missing");
        }

        return sb.ToString();
    }
}
