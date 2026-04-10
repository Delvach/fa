using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MVR.FileManagementSecure;
using UnityEngine;
using uFileBrowser;

public partial class FASyncRuntime : MVRScript
{
    private const string PlayerPresetSchemaVersion = "frameangel_player_preset_v1";
    private const string PlayerPresetRootPath = "Custom\\PluginData\\FrameAngel\\Player\\presets";
    private const string PlayerPresetNoneChoice = "none";
    private const float PlayerPresetDeferredSeekTimeoutSeconds = 10f;
    private const float PlayerPresetPopupLabelWidth = 112f;
    private const float PlayerPresetPopupPanelHeight = 260f;
    private const string PlayerPresetSeekToSecondsActionId = "Player.SeekToSeconds";

    private sealed class PlayerPresetRecord
    {
        public string presetId = "";
        public string displayName = "";
        public bool favorite = false;
        public bool hasMediaPath = false;
        public string mediaPath = "";
        public bool hasTimeSeconds = false;
        public float timeSeconds = 0f;
        public bool hasHostScale = false;
        public float hostScale = 1f;
        public bool hasLoopMode = false;
        public string loopMode = "";
        public bool hasRandomEnabled = false;
        public bool randomEnabled = true;
        public bool playWhenLoaded = true;
    }

    private JSONStorableStringChooser playerPresetChooser;
    private JSONStorableStringChooser playerFavoritePresetChooser;
    private JSONStorableBool playerPresetLoadOnSelectToggle;
    private JSONStorableString playerPresetNameField;
    private JSONStorableBool playerPresetFavoriteToggle;
    private JSONStorableBool playerPresetStoreMediaToggle;
    private JSONStorableBool playerPresetStoreTimeToggle;
    private JSONStorableBool playerPresetStoreScaleToggle;
    private JSONStorableBool playerPresetStoreLoopToggle;
    private JSONStorableBool playerPresetStoreRandomToggle;
    private JSONStorableString playerPresetStatusField;
    private JSONStorableAction playerPresetSaveAction;
    private JSONStorableAction playerPresetLoadAction;
    private UIDynamicPopup playerFavoritePresetChooserPopup;
    private UIDynamicButton playerPresetBrowseButtonDynamic;
    private UIDynamicButton playerPresetNameButtonDynamic;
    private UIDynamicButton playerPresetSaveButtonDynamic;
    private UIDynamicButton playerPresetLoadButtonDynamic;
    private UIDynamicTextField playerPresetStatusDynamic;
    private string playerSelectedPresetId = "";
    private string playerSelectedFavoritePresetId = "";
    private bool playerPresetUiSyncGuard = false;
    private bool playerPresetSaveAfterNamePrompt = false;
    private Coroutine playerPresetDeferredSeekCoroutine;
    private readonly Dictionary<string, PlayerPresetRecord> playerPresetsById =
        new Dictionary<string, PlayerPresetRecord>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> playerPresetChoiceIds = new List<string>();
    private readonly List<string> playerPresetChoiceDisplays = new List<string>();
    private readonly List<string> playerFavoritePresetChoiceIds = new List<string>();
    private readonly List<string> playerFavoritePresetChoiceDisplays = new List<string>();

    private void BuildPlayerPresetStorables()
    {
        playerPresetStatusField = new JSONStorableString("FrameAngel Player Preset Status", "No presets saved yet");
        ConfigureTransientField(playerPresetStatusField, false);

        playerPresetLoadOnSelectToggle = new JSONStorableBool("Load Preset On Select", true);

        playerPresetChooser = new JSONStorableStringChooser(
            "Select Existing...",
            new List<string> { PlayerPresetNoneChoice },
            PlayerPresetNoneChoice,
            "Select Existing...");
        playerPresetChooser.displayChoices = new List<string> { "(none)" };
        playerPresetChooser.setCallbackFunction = delegate(string value)
        {
            HandlePlayerPresetSelectionChanged(value, false);
        };

        playerFavoritePresetChooser = new JSONStorableStringChooser(
            "Favorite Selection",
            new List<string> { PlayerPresetNoneChoice },
            PlayerPresetNoneChoice,
            "Favorite Selection");
        playerFavoritePresetChooser.displayChoices = new List<string> { "(none)" };
        playerFavoritePresetChooser.setCallbackFunction = delegate(string value)
        {
            HandlePlayerPresetSelectionChanged(value, true);
        };

        playerPresetNameField = new JSONStorableString(
            "Preset Name",
            "",
            delegate(string value)
            {
                SyncPlayerPresetActionButtons();
            });
        playerPresetNameField.enableOnChange = true;
        playerPresetFavoriteToggle = new JSONStorableBool("Favorite", false);
        playerPresetStoreMediaToggle = new JSONStorableBool("Store Media", true);
        playerPresetStoreTimeToggle = new JSONStorableBool("Store Video Time", true);
        playerPresetStoreScaleToggle = new JSONStorableBool("Store CUA Scale", true);
        playerPresetStoreLoopToggle = new JSONStorableBool("Store Loop Mode", true);
        playerPresetStoreRandomToggle = new JSONStorableBool("Store Shuffle", true);

        playerPresetSaveAction = new JSONStorableAction(
            "Player Preset Save",
            delegate
            {
                RunPlayerSavePreset();
            });
        playerPresetLoadAction = new JSONStorableAction(
            "Player Preset Load",
            delegate
            {
                RunPlayerLoadSelectedPreset();
            });

        RefreshPlayerPresetCatalog(false, "", true);
    }

    private void RegisterPlayerPresetStorables()
    {
        RegisterString(playerPresetStatusField);
        RegisterBool(playerPresetLoadOnSelectToggle);
        RegisterStringChooser(playerFavoritePresetChooser);
        RegisterStringChooser(playerPresetChooser);
        RegisterString(playerPresetNameField);
        RegisterBool(playerPresetFavoriteToggle);
        RegisterBool(playerPresetStoreMediaToggle);
        RegisterBool(playerPresetStoreTimeToggle);
        RegisterBool(playerPresetStoreScaleToggle);
        RegisterBool(playerPresetStoreLoopToggle);
        RegisterBool(playerPresetStoreRandomToggle);
        RegisterAction(playerPresetSaveAction);
        RegisterAction(playerPresetLoadAction);
    }

    private void BuildPlayerPresetUi()
    {
        playerPresetBrowseButtonDynamic = CreateButton("Select Existing...", false);
        playerPresetBrowseButtonDynamic.button.onClick.AddListener(
            delegate
            {
                OpenPlayerPresetSelectionDialog();
            });
        CreateSpacer(true);

        CreateToggle(playerPresetLoadOnSelectToggle, false);
        playerFavoritePresetChooserPopup = CreateFilterablePopup(playerFavoritePresetChooser, true);
        ConfigurePlayerPresetPopup(playerFavoritePresetChooserPopup);

        playerPresetNameButtonDynamic = CreateButton("Preset Name...", false);
        playerPresetNameButtonDynamic.button.onClick.AddListener(
            delegate
            {
                OpenPlayerPresetNameDialog(false);
            });
        CreateToggle(playerPresetFavoriteToggle, true);

        playerPresetSaveButtonDynamic = CreateButton("Create New Preset", false);
        playerPresetSaveButtonDynamic.button.onClick.AddListener(
            delegate
            {
                RunPlayerSavePresetFromUi();
            });
        playerPresetLoadButtonDynamic = CreateButton("Load", true);
        playerPresetLoadButtonDynamic.button.onClick.AddListener(
            delegate
            {
                RunPlayerLoadSelectedPreset();
            });
        playerPresetStatusDynamic = CreateTextField(playerPresetStatusField, false);
        CreateSpacer(true);

        CreateToggle(playerPresetStoreMediaToggle, false);
        CreateToggle(playerPresetStoreTimeToggle, true);
        CreateToggle(playerPresetStoreScaleToggle, false);
        CreateToggle(playerPresetStoreLoopToggle, true);
        CreateToggle(playerPresetStoreRandomToggle, false);
        CreateSpacer(true);

        SyncPlayerPresetActionButtons();
    }

    private void OnPlayerPresetDestroy()
    {
        if (playerPresetDeferredSeekCoroutine != null)
        {
            StopCoroutine(playerPresetDeferredSeekCoroutine);
            playerPresetDeferredSeekCoroutine = null;
        }
    }

    private void HandlePlayerPresetSelectionChanged(string value, bool fromFavoriteChooser)
    {
        if (playerPresetUiSyncGuard)
            return;

        string presetId = NormalizePlayerPresetChoiceId(value);
        SelectPlayerPresetId(presetId, true);

        if (playerPresetLoadOnSelectToggle != null
            && playerPresetLoadOnSelectToggle.val
            && !string.IsNullOrEmpty(presetId))
        {
            ApplyPlayerPresetById(presetId);
        }
    }

    private void OpenPlayerPresetSelectionDialog()
    {
        if (!TryGetPlayerPresetFileBrowser(out FileBrowser fileBrowser))
        {
            UpdatePlayerPresetStatusField("error");
            return;
        }

        ConfigurePlayerPresetFileBrowser(fileBrowser, "Select Existing...", false);
        fileBrowser.Show(HandlePlayerPresetSelectionDialogClosed);
    }

    private void HandlePlayerPresetSelectionDialogClosed(string value)
    {
        string presetPath = string.IsNullOrEmpty(value) ? "" : value.Trim();
        if (string.IsNullOrEmpty(presetPath))
            return;

        string presetId = GetPlayerPresetFileNameWithoutExtension(presetPath);
        if (string.IsNullOrEmpty(presetId))
        {
            UpdatePlayerPresetStatusField("error");
            return;
        }

        RefreshPlayerPresetCatalog(false, presetId, true);
        if (!playerPresetsById.ContainsKey(presetId))
        {
            UpdatePlayerPresetStatusField("error");
            return;
        }

        SelectPlayerPresetId(presetId, true);

        if (playerPresetLoadOnSelectToggle != null
            && playerPresetLoadOnSelectToggle.val)
        {
            ApplyPlayerPresetById(presetId);
        }
    }

    private void OpenPlayerPresetNameDialog(bool saveAfterNameSelection)
    {
        if (!TryGetPlayerPresetFileBrowser(out FileBrowser fileBrowser))
        {
            UpdatePlayerPresetStatusField("error");
            return;
        }

        playerPresetSaveAfterNamePrompt = saveAfterNameSelection;
        ConfigurePlayerPresetFileBrowser(fileBrowser, "Preset Name", true);
        fileBrowser.Show(HandlePlayerPresetNameDialogClosed);
        if (fileBrowser.fileEntryField != null)
        {
            fileBrowser.fileEntryField.text = ResolveSuggestedPlayerPresetName();
            fileBrowser.ActivateFileNameField();
        }
    }

    private void HandlePlayerPresetNameDialogClosed(string value)
    {
        string presetPath = string.IsNullOrEmpty(value) ? "" : value.Trim();
        bool saveAfterNameSelection = playerPresetSaveAfterNamePrompt;
        playerPresetSaveAfterNamePrompt = false;
        if (string.IsNullOrEmpty(presetPath))
            return;

        string presetName = GetPlayerPresetFileNameWithoutExtension(presetPath);
        if (string.IsNullOrEmpty(presetName))
        {
            UpdatePlayerPresetStatusField("error");
            return;
        }

        playerPresetNameField.val = presetName;
        if (saveAfterNameSelection)
            RunPlayerSavePreset();
    }

    private void RunPlayerSavePresetFromUi()
    {
        if (string.IsNullOrEmpty(ResolveSuggestedPlayerPresetName()))
        {
            OpenPlayerPresetNameDialog(true);
            return;
        }

        RunPlayerSavePreset();
    }

    private void RefreshPlayerPresetCatalog(bool preserveSelection, string preferredPresetId, bool forceEditorSync)
    {
        string selectionBeforeRefresh = preserveSelection ? ResolveSelectedPlayerPresetId() : "";
        if (!string.IsNullOrEmpty(preferredPresetId))
            selectionBeforeRefresh = preferredPresetId;

        playerPresetsById.Clear();
        playerPresetChoiceIds.Clear();
        playerPresetChoiceDisplays.Clear();
        playerFavoritePresetChoiceIds.Clear();
        playerFavoritePresetChoiceDisplays.Clear();

        string rootPath = ResolvePlayerPresetRootPath();
        string[] presetPaths = FileManagerSecure.DirectoryExists(rootPath, false)
            ? FileManagerSecure.GetFiles(rootPath, "*.json")
            : new string[0];
        Array.Sort(presetPaths, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < presetPaths.Length; i++)
        {
            string presetPath = presetPaths[i];
            if (string.IsNullOrEmpty(presetPath))
                continue;

            if (!TryReadPlayerPresetFile(presetPath, out PlayerPresetRecord preset, out string errorMessage) || preset == null)
            {
                FrameAngelLog.Quiet("fa player preset skipped: " + errorMessage);
                continue;
            }

            playerPresetsById[preset.presetId] = preset;
        }

        List<string> sortedPresetIds = new List<string>(playerPresetsById.Keys);
        sortedPresetIds.Sort(StringComparer.OrdinalIgnoreCase);
        for (int presetIndex = 0; presetIndex < sortedPresetIds.Count; presetIndex++)
        {
            string presetId = sortedPresetIds[presetIndex];
            PlayerPresetRecord preset = playerPresetsById[presetId];
            playerPresetChoiceIds.Add(presetId);
            playerPresetChoiceDisplays.Add(string.IsNullOrEmpty(preset.displayName) ? presetId : preset.displayName);
            if (preset.favorite)
            {
                playerFavoritePresetChoiceIds.Add(presetId);
                playerFavoritePresetChoiceDisplays.Add(string.IsNullOrEmpty(preset.displayName) ? presetId : preset.displayName);
            }
        }

        if (playerPresetChoiceIds.Count <= 0)
        {
            playerPresetChoiceIds.Add(PlayerPresetNoneChoice);
            playerPresetChoiceDisplays.Add("(none)");
        }

        if (playerFavoritePresetChoiceIds.Count <= 0)
        {
            playerFavoritePresetChoiceIds.Add(PlayerPresetNoneChoice);
            playerFavoritePresetChoiceDisplays.Add("(none)");
        }

        playerPresetUiSyncGuard = true;
        try
        {
            if (playerPresetChooser != null)
            {
                playerPresetChooser.choices = new List<string>(playerPresetChoiceIds);
                playerPresetChooser.displayChoices = new List<string>(playerPresetChoiceDisplays);
            }
            if (playerFavoritePresetChooser != null)
            {
                playerFavoritePresetChooser.choices = new List<string>(playerFavoritePresetChoiceIds);
                playerFavoritePresetChooser.displayChoices = new List<string>(playerFavoritePresetChoiceDisplays);
            }
        }
        finally
        {
            playerPresetUiSyncGuard = false;
        }

        string resolvedSelection = NormalizePlayerPresetChoiceId(selectionBeforeRefresh);
        if (string.IsNullOrEmpty(resolvedSelection))
        {
            for (int i = 0; i < playerPresetChoiceIds.Count; i++)
            {
                if (!string.Equals(playerPresetChoiceIds[i], PlayerPresetNoneChoice, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedSelection = playerPresetChoiceIds[i];
                    break;
                }
            }
        }

        bool selectionChanged = !string.Equals(resolvedSelection, playerSelectedPresetId, StringComparison.OrdinalIgnoreCase);
        SelectPlayerPresetId(resolvedSelection, forceEditorSync || selectionChanged);
        UpdatePlayerPresetStatusField("");
    }

    private void SelectPlayerPresetId(string presetId, bool syncEditorFields)
    {
        string normalizedPresetId = NormalizePlayerPresetChoiceId(presetId);
        bool selectedIsFavorite = !string.IsNullOrEmpty(normalizedPresetId)
            && playerPresetsById.TryGetValue(normalizedPresetId, out PlayerPresetRecord selectedPreset)
            && selectedPreset != null
            && selectedPreset.favorite;

        playerSelectedPresetId = normalizedPresetId;
        playerSelectedFavoritePresetId = selectedIsFavorite ? normalizedPresetId : "";

        playerPresetUiSyncGuard = true;
        try
        {
            if (playerPresetChooser != null)
                playerPresetChooser.valNoCallback = string.IsNullOrEmpty(normalizedPresetId) ? PlayerPresetNoneChoice : normalizedPresetId;
            if (playerFavoritePresetChooser != null)
                playerFavoritePresetChooser.valNoCallback = selectedIsFavorite ? normalizedPresetId : PlayerPresetNoneChoice;
        }
        finally
        {
            playerPresetUiSyncGuard = false;
        }

        if (syncEditorFields)
            SyncPlayerPresetEditorFields(normalizedPresetId);

        UpdatePlayerPresetStatusField("");
    }

    private void SyncPlayerPresetEditorFields(string presetId)
    {
        if (playerPresetNameField == null || playerPresetFavoriteToggle == null)
            return;

        if (!string.IsNullOrEmpty(presetId)
            && playerPresetsById.TryGetValue(presetId, out PlayerPresetRecord preset)
            && preset != null)
        {
            playerPresetNameField.valNoCallback = string.IsNullOrEmpty(preset.displayName) ? presetId : preset.displayName;
            playerPresetFavoriteToggle.valNoCallback = preset.favorite;
            SyncPlayerPresetActionButtons();
            return;
        }

        playerPresetNameField.valNoCallback = "";
        playerPresetFavoriteToggle.valNoCallback = false;
        SyncPlayerPresetActionButtons();
    }

    private string ResolveSelectedPlayerPresetId()
    {
        if (!string.IsNullOrEmpty(playerSelectedPresetId)
            && playerPresetsById.ContainsKey(playerSelectedPresetId))
        {
            return playerSelectedPresetId;
        }

        if (!string.IsNullOrEmpty(playerSelectedFavoritePresetId)
            && playerPresetsById.ContainsKey(playerSelectedFavoritePresetId))
        {
            return playerSelectedFavoritePresetId;
        }

        return "";
    }

    private string NormalizePlayerPresetChoiceId(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        string normalized = value.Trim();
        if (string.IsNullOrEmpty(normalized)
            || string.Equals(normalized, PlayerPresetNoneChoice, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return playerPresetsById.ContainsKey(normalized) ? normalized : "";
    }

    private string ResolvePlayerPresetRootPath()
    {
        return NormalizeStandalonePlayerPath(PlayerPresetRootPath);
    }

    private string ResolvePlayerPresetPath(string presetId)
    {
        if (string.IsNullOrEmpty(presetId))
            return "";

        return CombineStandalonePlayerPath(ResolvePlayerPresetRootPath(), presetId + ".json");
    }

    private bool TryReadPlayerPresetFile(string presetPath, out PlayerPresetRecord preset, out string errorMessage)
    {
        preset = null;
        errorMessage = "";

        if (string.IsNullOrEmpty(presetPath) || !FileManagerSecure.FileExists(presetPath, false))
        {
            errorMessage = "preset file missing";
            return false;
        }

        string presetJson;
        try
        {
            presetJson = FileManagerSecure.ReadAllText(presetPath);
        }
        catch (Exception ex)
        {
            errorMessage = "preset read failed: " + ex.Message;
            return false;
        }

        string schemaVersion = ExtractJsonArgString(presetJson, "schemaVersion");
        if (!string.Equals(schemaVersion, PlayerPresetSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "preset schema unsupported: " + presetPath;
            return false;
        }

        string presetId = GetPlayerPresetFileNameWithoutExtension(presetPath);
        if (string.IsNullOrEmpty(presetId))
        {
            errorMessage = "preset id missing: " + presetPath;
            return false;
        }

        preset = new PlayerPresetRecord();
        preset.presetId = presetId;
        preset.displayName = ExtractJsonArgString(presetJson, "displayName", "presetName");
        if (string.IsNullOrEmpty(preset.displayName))
            preset.displayName = presetId;

        TryReadBoolArg(presetJson, out preset.favorite, "favorite");
        preset.mediaPath = ExtractJsonArgString(presetJson, "mediaPath");
        if (TryReadBoolArg(presetJson, out bool hasMediaPath, "hasMediaPath"))
            preset.hasMediaPath = hasMediaPath && !string.IsNullOrEmpty(preset.mediaPath);
        else
            preset.hasMediaPath = !string.IsNullOrEmpty(preset.mediaPath);

        if (TryReadBoolArg(presetJson, out bool hasTimeSeconds, "hasTimeSeconds")
            && hasTimeSeconds
            && TryExtractJsonFloatField(presetJson, "timeSeconds", out float timeSeconds))
        {
            preset.hasTimeSeconds = true;
            preset.timeSeconds = Mathf.Max(0f, timeSeconds);
        }

        if (TryReadBoolArg(presetJson, out bool hasHostScale, "hasHostScale")
            && hasHostScale
            && TryExtractJsonFloatField(presetJson, "hostScale", out float hostScale))
        {
            preset.hasHostScale = true;
            preset.hostScale = Mathf.Max(0.01f, hostScale);
        }

        preset.loopMode = NormalizeStandalonePlayerLoopMode(ExtractJsonArgString(presetJson, "loopMode"));
        if (TryReadBoolArg(presetJson, out bool hasLoopMode, "hasLoopMode"))
            preset.hasLoopMode = hasLoopMode && !string.IsNullOrEmpty(preset.loopMode);
        else
            preset.hasLoopMode = !string.IsNullOrEmpty(preset.loopMode);

        if (TryReadBoolArg(presetJson, out bool hasRandomEnabled, "hasRandomEnabled"))
        {
            preset.hasRandomEnabled = hasRandomEnabled;
            if (hasRandomEnabled)
                TryReadBoolArg(presetJson, out preset.randomEnabled, "randomEnabled", "random");
        }

        if (!TryReadBoolArg(presetJson, out preset.playWhenLoaded, "playWhenLoaded", "desiredPlaying", "play"))
            preset.playWhenLoaded = true;

        return true;
    }

    private string BuildPlayerPresetJson(PlayerPresetRecord preset)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        sb.Append("\"schemaVersion\":\"").Append(EscapeJsonString(PlayerPresetSchemaVersion)).Append("\",");
        sb.Append("\"presetId\":\"").Append(EscapeJsonString(preset.presetId ?? "")).Append("\",");
        sb.Append("\"displayName\":\"").Append(EscapeJsonString(preset.displayName ?? "")).Append("\",");
        sb.Append("\"favorite\":").Append(preset.favorite ? "true" : "false").Append(',');
        sb.Append("\"hasMediaPath\":").Append(preset.hasMediaPath ? "true" : "false").Append(',');
        sb.Append("\"mediaPath\":\"").Append(EscapeJsonString(preset.mediaPath ?? "")).Append("\",");
        sb.Append("\"hasTimeSeconds\":").Append(preset.hasTimeSeconds ? "true" : "false").Append(',');
        sb.Append("\"timeSeconds\":").Append(FormatFloat(Mathf.Max(0f, preset.timeSeconds))).Append(',');
        sb.Append("\"hasHostScale\":").Append(preset.hasHostScale ? "true" : "false").Append(',');
        sb.Append("\"hostScale\":").Append(FormatFloat(Mathf.Max(0.01f, preset.hostScale))).Append(',');
        sb.Append("\"hasLoopMode\":").Append(preset.hasLoopMode ? "true" : "false").Append(',');
        sb.Append("\"loopMode\":\"").Append(EscapeJsonString(preset.loopMode ?? "")).Append("\",");
        sb.Append("\"hasRandomEnabled\":").Append(preset.hasRandomEnabled ? "true" : "false").Append(',');
        sb.Append("\"randomEnabled\":").Append(preset.randomEnabled ? "true" : "false").Append(',');
        sb.Append("\"playWhenLoaded\":").Append(preset.playWhenLoaded ? "true" : "false").Append(',');
        sb.Append("\"savedAtUtc\":\"").Append(EscapeJsonString(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))).Append("\"");
        sb.Append('}');
        return sb.ToString();
    }

    private void RunPlayerSavePreset()
    {
        if (!TryBuildCurrentPlayerPreset(out PlayerPresetRecord preset, out string errorMessage) || preset == null)
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            UpdatePlayerPresetStatusField("error");
            return;
        }

        string presetPath = ResolvePlayerPresetPath(preset.presetId);
        if (string.IsNullOrEmpty(presetPath))
        {
            errorMessage = "preset path could not be resolved";
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            UpdatePlayerPresetStatusField("error");
            return;
        }

        string rootPath = ResolvePlayerPresetRootPath();
        try
        {
            if (!FileManagerSecure.DirectoryExists(rootPath, false))
                FileManagerSecure.CreateDirectory(rootPath);
            FileManagerSecure.WriteAllText(presetPath, BuildPlayerPresetJson(preset));
        }
        catch (Exception ex)
        {
            errorMessage = "preset save failed: " + ex.Message;
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            UpdatePlayerPresetStatusField("error");
            return;
        }

        SetLastError("");
        SetLastReceipt(BuildBrokerResult(
            true,
            "player_preset_saved",
            "{\"presetId\":\"" + EscapeJsonString(preset.presetId) + "\"}"));
        RefreshPlayerPresetCatalog(false, preset.presetId, true);
        UpdatePlayerPresetStatusField("saved");
    }

    private bool TryBuildCurrentPlayerPreset(out PlayerPresetRecord preset, out string errorMessage)
    {
        preset = null;
        errorMessage = "";

        string rawPresetName = playerPresetNameField != null ? playerPresetNameField.val : "";
        if (string.IsNullOrEmpty(rawPresetName) || string.IsNullOrEmpty(rawPresetName.Trim()))
        {
            string selectedPresetId = ResolveSelectedPlayerPresetId();
            if (!string.IsNullOrEmpty(selectedPresetId)
                && playerPresetsById.TryGetValue(selectedPresetId, out PlayerPresetRecord selectedPreset)
                && selectedPreset != null
                && !string.IsNullOrEmpty(selectedPreset.displayName))
            {
                rawPresetName = selectedPreset.displayName;
            }
        }

        rawPresetName = string.IsNullOrEmpty(rawPresetName) ? "" : rawPresetName.Trim();
        if (string.IsNullOrEmpty(rawPresetName))
        {
            errorMessage = "preset name is required";
            return false;
        }

        bool storeMedia = playerPresetStoreMediaToggle != null && playerPresetStoreMediaToggle.val;
        bool storeTime = playerPresetStoreTimeToggle != null && playerPresetStoreTimeToggle.val;
        bool storeScale = playerPresetStoreScaleToggle != null && playerPresetStoreScaleToggle.val;
        bool storeLoop = playerPresetStoreLoopToggle != null && playerPresetStoreLoopToggle.val;
        bool storeRandom = playerPresetStoreRandomToggle != null && playerPresetStoreRandomToggle.val;
        if (storeTime && !storeMedia)
        {
            errorMessage = "preset video time requires media selection";
            return false;
        }

        StandalonePlayerRecord record = null;
        Atom hostAtom = null;
        bool needsRecord = storeMedia || storeTime || storeLoop || storeRandom;
        if (needsRecord)
        {
            if (!TryResolveAttachedHostedStandalonePlayerRecord(out record, out hostAtom) || record == null)
            {
                errorMessage = "attached player record not resolved";
                return false;
            }
        }
        else if (storeScale)
        {
            if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
            {
                errorMessage = "attached player host atom not resolved";
                return false;
            }
        }

        preset = new PlayerPresetRecord();
        preset.displayName = rawPresetName;
        preset.presetId = SanitizeStandalonePlayerName(rawPresetName);
        preset.favorite = playerPresetFavoriteToggle != null && playerPresetFavoriteToggle.val;

        if (storeMedia)
        {
            string currentPath = ResolveCurrentStandalonePlayerMediaPath(record);
            if (string.IsNullOrEmpty(currentPath))
            {
                errorMessage = "current player media path is unavailable";
                return false;
            }

            preset.hasMediaPath = true;
            preset.mediaPath = currentPath;
            preset.playWhenLoaded = ResolveCurrentStandalonePlayerDesiredPlaying(record);
        }

        if (storeTime && record != null && !record.mediaIsStillImage)
        {
            float currentTimeSeconds = 0f;
            try
            {
                if (record.videoPlayer != null)
                    currentTimeSeconds = Mathf.Max(0f, (float)record.videoPlayer.time);
            }
            catch
            {
                currentTimeSeconds = 0f;
            }

            preset.hasTimeSeconds = true;
            preset.timeSeconds = currentTimeSeconds;
        }

        if (storeLoop && record != null)
        {
            preset.hasLoopMode = true;
            preset.loopMode = NormalizeStandalonePlayerLoopMode(record.loopMode);
        }

        if (storeRandom && record != null)
        {
            preset.hasRandomEnabled = true;
            preset.randomEnabled = record.randomEnabled;
        }

        if (storeScale)
        {
            if (!TryReadPlayerPresetHostScale(hostAtom, out float hostScale, out errorMessage))
                return false;

            preset.hasHostScale = true;
            preset.hostScale = hostScale;
        }

        if (!preset.hasMediaPath
            && !preset.hasHostScale
            && !preset.hasLoopMode
            && !preset.hasRandomEnabled)
        {
            errorMessage = "preset contains no restorable player state";
            preset = null;
            return false;
        }

        return true;
    }

    private void RunPlayerLoadSelectedPreset()
    {
        string presetId = ResolveSelectedPlayerPresetId();
        if (string.IsNullOrEmpty(presetId))
        {
            string errorMessage = "player preset is not selected";
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            UpdatePlayerPresetStatusField("error");
            return;
        }

        ApplyPlayerPresetById(presetId);
    }

    private void ApplyPlayerPresetById(string presetId)
    {
        if (string.IsNullOrEmpty(presetId)
            || !playerPresetsById.TryGetValue(presetId, out PlayerPresetRecord preset)
            || preset == null)
        {
            string errorMessage = "player preset not found";
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            UpdatePlayerPresetStatusField("error");
            return;
        }

        SelectPlayerPresetId(presetId, true);
        ApplyPlayerPreset(preset);
    }

    private void ApplyPlayerPreset(PlayerPresetRecord preset)
    {
        if (preset == null)
            return;

        if (playerPresetDeferredSeekCoroutine != null)
        {
            StopCoroutine(playerPresetDeferredSeekCoroutine);
            playerPresetDeferredSeekCoroutine = null;
        }

        string selectorJson = "";
        string playbackKey = "";
        if (preset.hasMediaPath || preset.hasLoopMode || preset.hasRandomEnabled)
        {
            if (!TryBuildAttachedPlayerSelectorJson(out selectorJson, out string errorMessage))
            {
                SetLastError(errorMessage);
                SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
                UpdatePlayerPresetStatusField("error");
                return;
            }

            playbackKey = ExtractJsonArgString(selectorJson, "playbackKey");
        }

        string resultJson = "{}";
        string lastError = "";
        if (preset.hasMediaPath)
        {
            if (!TryApplyPlayerPresetMedia(preset, selectorJson, out resultJson, out lastError))
            {
                SetLastError(lastError);
                SetLastReceipt(string.IsNullOrEmpty(resultJson) ? BuildBrokerResult(false, lastError, "{}") : resultJson);
                UpdatePlayerPresetStatusField("error");
                RefreshVisiblePlayerDebugFields();
                return;
            }
        }
        else
        {
            if (preset.hasLoopMode
                && !TryApplyAttachedPlayerPresetAction(
                    selectorJson,
                    PlayerActionSetLoopModeId,
                    "\"loopMode\":\"" + EscapeJsonString(preset.loopMode) + "\"",
                    out resultJson,
                    out lastError))
            {
                SetLastError(lastError);
                SetLastReceipt(string.IsNullOrEmpty(resultJson) ? BuildBrokerResult(false, lastError, "{}") : resultJson);
                UpdatePlayerPresetStatusField("error");
                RefreshVisiblePlayerDebugFields();
                return;
            }

            if (preset.hasRandomEnabled
                && !TryApplyAttachedPlayerPresetAction(
                    selectorJson,
                    PlayerActionSetRandomId,
                    "\"random\":" + (preset.randomEnabled ? "true" : "false"),
                    out resultJson,
                    out lastError))
            {
                SetLastError(lastError);
                SetLastReceipt(string.IsNullOrEmpty(resultJson) ? BuildBrokerResult(false, lastError, "{}") : resultJson);
                UpdatePlayerPresetStatusField("error");
                RefreshVisiblePlayerDebugFields();
                return;
            }
        }

        if (preset.hasHostScale)
        {
            Atom hostAtom;
            if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null || !TryApplyPlayerPresetHostScale(hostAtom, preset.hostScale, out lastError))
            {
                lastError = string.IsNullOrEmpty(lastError) ? "player preset host scale failed" : lastError;
                SetLastError(lastError);
                SetLastReceipt(BuildBrokerResult(false, lastError, "{}"));
                UpdatePlayerPresetStatusField("error");
                RefreshVisiblePlayerDebugFields();
                return;
            }
        }

        SetLastError("");
        SetLastReceipt(string.IsNullOrEmpty(resultJson)
            ? BuildBrokerResult(true, "player_preset_applied", "{\"presetId\":\"" + EscapeJsonString(preset.presetId) + "\"}")
            : resultJson);

        if (preset.hasTimeSeconds && !string.IsNullOrEmpty(playbackKey))
        {
            playerPresetDeferredSeekCoroutine = StartCoroutine(
                RunDeferredPlayerPresetSeekCoroutine(
                    preset.presetId,
                    playbackKey,
                    preset.timeSeconds,
                    preset.playWhenLoaded));
            UpdatePlayerPresetStatusField("seeking");
            RefreshVisiblePlayerDebugFields();
            return;
        }

        UpdatePlayerPresetStatusField("loaded");
        RefreshVisiblePlayerDebugFields();
    }

    private bool TryApplyPlayerPresetMedia(PlayerPresetRecord preset, string selectorJson, out string resultJson, out string errorMessage)
    {
        resultJson = "{}";
        errorMessage = "";

        List<string> mediaPaths;
        if (!TryResolvePlayerRuntimeMediaPaths(preset.mediaPath, out mediaPaths, out errorMessage))
        {
            resultJson = BuildBrokerResult(false, errorMessage, "{}");
            return false;
        }

        string selectedMediaPath = ResolvePrimaryPlayerRuntimeMediaPath(preset.mediaPath, mediaPaths);
        SetPendingPlayerSelection(selectedMediaPath);
        SetPendingPlayerStateSummary("state=load_requested source=preset");

        bool playImmediately = preset.playWhenLoaded && !preset.hasTimeSeconds;
        string extraArgsBody = "\"mediaPath\":\"" + EscapeJsonString(selectedMediaPath) + "\""
            + ",\"playlist\":" + BuildMetaProofSamplePlaylistJson(mediaPaths)
            + ",\"play\":" + (playImmediately ? "true" : "false");
        if (preset.hasLoopMode)
            extraArgsBody += ",\"loopMode\":\"" + EscapeJsonString(preset.loopMode) + "\"";
        if (preset.hasRandomEnabled)
            extraArgsBody += ",\"random\":" + (preset.randomEnabled ? "true" : "false");

        if (!TryApplyAttachedPlayerPresetAction(selectorJson, PlayerActionLoadPathId, extraArgsBody, out resultJson, out errorMessage))
            return false;

        SetPendingPlayerStateSummary("state=load_requested source=preset");
        return true;
    }

    private bool TryApplyAttachedPlayerPresetAction(
        string selectorJson,
        string actionId,
        string extraArgsBody,
        out string resultJson,
        out string errorMessage)
    {
        string argsJson = BuildAttachedPlayerActionArgsJson(selectorJson, extraArgsBody);
        return TryExecuteAction(actionId, argsJson, out resultJson, out errorMessage);
    }

    private IEnumerator RunDeferredPlayerPresetSeekCoroutine(
        string presetId,
        string playbackKey,
        float timeSeconds,
        bool playWhenLoaded)
    {
        float deadline = Time.unscaledTime + PlayerPresetDeferredSeekTimeoutSeconds;
        while (Time.unscaledTime < deadline)
        {
            if (standalonePlayerRecords.TryGetValue(playbackKey, out StandalonePlayerRecord record)
                && record != null
                && record.prepared)
            {
                string seekArgsJson = "{"
                    + "\"playbackKey\":\"" + EscapeJsonString(playbackKey) + "\""
                    + ",\"seconds\":" + FormatFloat(Mathf.Max(0f, timeSeconds))
                    + "}";
                if (!TryExecuteAction(PlayerPresetSeekToSecondsActionId, seekArgsJson, out string seekResultJson, out string seekErrorMessage))
                {
                    SetLastError(seekErrorMessage);
                    SetLastReceipt(seekResultJson);
                    UpdatePlayerPresetStatusField("error");
                    playerPresetDeferredSeekCoroutine = null;
                    RefreshVisiblePlayerDebugFields();
                    yield break;
                }

                SetLastReceipt(seekResultJson);
                if (playWhenLoaded)
                {
                    string playArgsJson = "{\"playbackKey\":\"" + EscapeJsonString(playbackKey) + "\"}";
                    if (!TryExecuteAction(PlayerActionPlayId, playArgsJson, out string playResultJson, out string playErrorMessage))
                    {
                        SetLastError(playErrorMessage);
                        SetLastReceipt(playResultJson);
                        UpdatePlayerPresetStatusField("error");
                        playerPresetDeferredSeekCoroutine = null;
                        RefreshVisiblePlayerDebugFields();
                        yield break;
                    }

                    SetLastReceipt(playResultJson);
                }

                SetLastError("");
                UpdatePlayerPresetStatusField("loaded");
                playerPresetDeferredSeekCoroutine = null;
                RefreshVisiblePlayerDebugFields();
                yield break;
            }

            yield return null;
        }

        string timeoutError = "player preset seek timed out: " + presetId;
        SetLastError(timeoutError);
        SetLastReceipt(BuildBrokerResult(false, timeoutError, "{}"));
        UpdatePlayerPresetStatusField("error");
        playerPresetDeferredSeekCoroutine = null;
        RefreshVisiblePlayerDebugFields();
    }

    private bool TryReadPlayerPresetHostScale(Atom hostAtom, out float hostScale, out string errorMessage)
    {
        hostScale = 1f;
        errorMessage = "";

        JSONStorableFloat scaleParam;
        if (!TryResolvePlayerPresetHostScaleParam(hostAtom, out scaleParam) || scaleParam == null)
        {
            errorMessage = "player preset host scale parameter not resolved";
            return false;
        }

        hostScale = Mathf.Max(0.01f, scaleParam.val);
        return true;
    }

    private bool TryApplyPlayerPresetHostScale(Atom hostAtom, float hostScale, out string errorMessage)
    {
        errorMessage = "";

        JSONStorableFloat scaleParam;
        if (!TryResolvePlayerPresetHostScaleParam(hostAtom, out scaleParam) || scaleParam == null)
        {
            errorMessage = "player preset host scale parameter not resolved";
            return false;
        }

        scaleParam.val = Mathf.Clamp(hostScale, 0.01f, 100f);
        return true;
    }

    private bool TryResolvePlayerPresetHostScaleParam(Atom hostAtom, out JSONStorableFloat scaleParam)
    {
        scaleParam = null;
        if (hostAtom == null)
            return false;

        JSONStorable control = hostAtom.GetStorableByID("Control");
        if (control == null)
            control = hostAtom.GetStorableByID("control");
        if (control == null)
            return false;

        scaleParam = control.GetFloatJSONParam("Scale");
        return scaleParam != null;
    }

    private string ResolveCurrentStandalonePlayerMediaPath(StandalonePlayerRecord record)
    {
        if (record == null)
            return string.IsNullOrEmpty(playerMediaPath) ? "" : playerMediaPath.Trim();

        string currentPath = GetStandalonePlayerCurrentPlaylistPath(record);
        if (!string.IsNullOrEmpty(currentPath))
            return currentPath;
        if (!string.IsNullOrEmpty(record.mediaPath))
            return record.mediaPath;
        if (!string.IsNullOrEmpty(record.resolvedMediaPath))
            return record.resolvedMediaPath;

        return string.IsNullOrEmpty(playerMediaPath) ? "" : playerMediaPath.Trim();
    }

    private bool ResolveCurrentStandalonePlayerDesiredPlaying(StandalonePlayerRecord record)
    {
        if (record == null)
            return true;
        if (record.mediaIsStillImage)
            return true;

        try
        {
            if (record.videoPlayer != null && record.videoPlayer.isPlaying)
                return true;
        }
        catch
        {
        }

        return record.desiredPlaying;
    }

    private void UpdatePlayerPresetStatusField(string state)
    {
        if (playerPresetStatusField == null)
            return;

        string selectedPresetId = ResolveSelectedPlayerPresetId();
        string selectedPresetName = "";
        if (!string.IsNullOrEmpty(selectedPresetId)
            && playerPresetsById.TryGetValue(selectedPresetId, out PlayerPresetRecord selectedPreset)
            && selectedPreset != null)
        {
            selectedPresetName = string.IsNullOrEmpty(selectedPreset.displayName) ? selectedPresetId : selectedPreset.displayName;
        }

        string message;
        switch ((state ?? "").Trim().ToLowerInvariant())
        {
            case "saved":
                message = string.IsNullOrEmpty(selectedPresetName)
                    ? "Preset saved"
                    : "Saved preset " + selectedPresetName;
                break;
            case "loaded":
                message = string.IsNullOrEmpty(selectedPresetName)
                    ? "Preset loaded"
                    : "Loaded preset " + selectedPresetName;
                break;
            case "seeking":
                message = string.IsNullOrEmpty(selectedPresetName)
                    ? "Loading preset..."
                    : "Loading preset " + selectedPresetName + "...";
                break;
            case "error":
                message = string.IsNullOrEmpty(selectedPresetName)
                    ? "Preset action failed"
                    : "Preset action failed for " + selectedPresetName;
                break;
            default:
                if (!string.IsNullOrEmpty(selectedPresetName))
                    message = "Selected preset " + selectedPresetName;
                else if (playerPresetsById.Count <= 0)
                    message = "No presets saved yet";
                else
                    message = "Choose or create a preset";
                break;
        }

        playerPresetStatusField.valNoCallback = message;
        SyncPlayerPresetActionButtons();
    }

    private void ConfigurePlayerPresetPopup(UIDynamicPopup dynamicPopup)
    {
        if (dynamicPopup == null)
            return;

        dynamicPopup.labelWidth = PlayerPresetPopupLabelWidth;
        dynamicPopup.popupPanelHeight = PlayerPresetPopupPanelHeight;
    }

    private bool TryGetPlayerPresetFileBrowser(out FileBrowser fileBrowser)
    {
        fileBrowser = null;
        if (SuperController.singleton == null || SuperController.singleton.fileBrowserUI == null)
            return false;

        fileBrowser = SuperController.singleton.fileBrowserUI;
        return fileBrowser != null;
    }

    private void ConfigurePlayerPresetFileBrowser(FileBrowser fileBrowser, string title, bool allowTextEntry)
    {
        if (fileBrowser == null)
            return;

        string rootPath = ResolvePlayerPresetRootPath();
        if (!FileManagerSecure.DirectoryExists(rootPath, false))
            FileManagerSecure.CreateDirectory(rootPath);

        fileBrowser.ClearCurrentPath();
        fileBrowser.SetShortCuts(null);
        fileBrowser.SetTitle(title);
        fileBrowser.fileFormat = "json";
        fileBrowser.defaultPath = rootPath;
        fileBrowser.keepOpen = false;
        fileBrowser.hideExtension = true;
        fileBrowser.showDirs = false;
        fileBrowser.SetTextEntry(allowTextEntry);
    }

    private string ResolveSuggestedPlayerPresetName()
    {
        string rawPresetName = playerPresetNameField != null ? playerPresetNameField.val : "";
        rawPresetName = string.IsNullOrEmpty(rawPresetName) ? "" : rawPresetName.Trim();
        if (!string.IsNullOrEmpty(rawPresetName))
            return rawPresetName;

        string selectedPresetId = ResolveSelectedPlayerPresetId();
        if (!string.IsNullOrEmpty(selectedPresetId)
            && playerPresetsById.TryGetValue(selectedPresetId, out PlayerPresetRecord selectedPreset)
            && selectedPreset != null
            && !string.IsNullOrEmpty(selectedPreset.displayName))
        {
            return selectedPreset.displayName.Trim();
        }

        return "";
    }

    private void SyncPlayerPresetActionButtons()
    {
        string rawPresetName = ResolveSuggestedPlayerPresetName();

        string selectedPresetId = ResolveSelectedPlayerPresetId();
        string targetPresetId = "";
        if (!string.IsNullOrEmpty(rawPresetName))
            targetPresetId = SanitizeStandalonePlayerName(rawPresetName);
        else if (!string.IsNullOrEmpty(selectedPresetId))
            targetPresetId = selectedPresetId;

        bool hasTargetPresetId = !string.IsNullOrEmpty(targetPresetId);
        bool willOverwrite = hasTargetPresetId && playerPresetsById.ContainsKey(targetPresetId);
        if (playerPresetSaveButtonDynamic != null)
        {
            playerPresetSaveButtonDynamic.label = willOverwrite ? "Overwrite Preset" : "Create New Preset";
            playerPresetSaveButtonDynamic.buttonColor = willOverwrite ? Color.red : Color.green;
            if (playerPresetSaveButtonDynamic.button != null)
                playerPresetSaveButtonDynamic.button.interactable = true;
        }

        bool canLoad = !string.IsNullOrEmpty(selectedPresetId);
        if (playerPresetLoadButtonDynamic != null)
        {
            playerPresetLoadButtonDynamic.label = "Load";
            playerPresetLoadButtonDynamic.buttonColor = canLoad ? Color.white : Color.gray;
            if (playerPresetLoadButtonDynamic.button != null)
                playerPresetLoadButtonDynamic.button.interactable = canLoad;
        }

        if (playerPresetNameButtonDynamic != null)
        {
            playerPresetNameButtonDynamic.label = string.IsNullOrEmpty(rawPresetName)
                ? "Preset Name..."
                : rawPresetName;
            playerPresetNameButtonDynamic.buttonColor = Color.white;
        }

        if (playerPresetStatusDynamic != null)
            playerPresetStatusDynamic.backgroundColor = new Color(1f, 1f, 1f, 0.075f);
    }

    private static string GetPlayerPresetFileNameWithoutExtension(string path)
    {
        string value = string.IsNullOrEmpty(path) ? "" : path.Trim();
        if (string.IsNullOrEmpty(value))
            return "";

        int slash = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        string leaf = slash < 0 ? value : value.Substring(slash + 1);
        if (string.IsNullOrEmpty(leaf))
            return "";

        int dot = leaf.LastIndexOf('.');
        if (dot <= 0)
            return leaf;

        return leaf.Substring(0, dot);
    }
}
