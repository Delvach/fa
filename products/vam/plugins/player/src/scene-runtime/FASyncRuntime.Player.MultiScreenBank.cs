using System;
using System.Collections.Generic;
using System.Text;
using FrameAngel.Runtime.Shared;
using UnityEngine;

public partial class FASyncRuntime : MVRScript
{
    private const string PlayerMirrorBankLayoutOff = "off";
    private const string PlayerMirrorBankLayoutGrid = "grid";
    private const string PlayerMirrorBankLayoutCircular = "circular";
    private const int PlayerMirrorBankMinCount = 1;
    private const int PlayerMirrorBankMaxCount = 8;
    private const float PlayerMirrorBankGapFactor = 0.04f;
    private const float PlayerMirrorBankMinGap = 0.0025f;
    private const float PlayerMirrorBankOverlayGap = 0.0015f;
    private const float PlayerMirrorBankDefaultCircularRadius = 4f;
    private const float PlayerMirrorBankMinCircularRadius = 0.5f;
    private const float PlayerMirrorBankMaxCircularRadius = 12f;
    private const float PlayerMirrorBankDefaultCircularMaxAngle = 180f;
    private const float PlayerMirrorBankMinCircularMaxAngle = 15f;
    private const float PlayerMirrorBankMaxCircularMaxAngle = 360f;

    private sealed class StandalonePlayerMirrorSurfaceRecord
    {
        public GameObject rootObject;
        public GameObject backingObject;
        public GameObject contentObject;
        public Material backingMaterial;
        public Material contentMaterial;
    }

    private sealed class StandalonePlayerMirrorBankState
    {
        public string playbackKey = "";
        public string layoutMode = PlayerMirrorBankLayoutOff;
        public int rows = PlayerMirrorBankMinCount;
        public int columns = PlayerMirrorBankMinCount;
        public float circularRadius = PlayerMirrorBankDefaultCircularRadius;
        public float circularMaxAngleDegrees = PlayerMirrorBankDefaultCircularMaxAngle;
        public string lastSignature = "";
        public bool dirty = true;
        public GameObject bankRoot;
        public readonly List<StandalonePlayerMirrorSurfaceRecord> surfaces =
            new List<StandalonePlayerMirrorSurfaceRecord>();
    }

    private readonly Dictionary<string, StandalonePlayerMirrorBankState> standalonePlayerMirrorBanks =
        new Dictionary<string, StandalonePlayerMirrorBankState>(StringComparer.OrdinalIgnoreCase);
    private bool playerMirrorBankUiSyncGuard = false;
    private JSONStorableFloat playerMirrorBankCircularRadiusField;
    private JSONStorableFloat playerMirrorBankCircularMaxAngleField;

    private void BuildPlayerMirrorBankStorables()
    {
        List<string> layoutChoices = new List<string>
        {
            PlayerMirrorBankLayoutOff,
            PlayerMirrorBankLayoutGrid,
            PlayerMirrorBankLayoutCircular
        };
        List<string> layoutDisplays = new List<string>
        {
            "MultiScreen Off",
            "MultiScreen Grid",
            "MultiScreen Circular"
        };

        playerMirrorBankLayoutChooser = new JSONStorableStringChooser(
            "Player MultiScreen Layout",
            layoutChoices,
            PlayerMirrorBankLayoutOff,
            "Player MultiScreen Layout");
        playerMirrorBankLayoutChooser.displayChoices = layoutDisplays;
        playerMirrorBankLayoutChooser.setCallbackFunction = delegate(string value)
        {
            if (playerMirrorBankUiSyncGuard)
                return;

            ApplyAttachedPlayerMirrorBankChoices(value, null, null);
        };

        List<string> countChoices = BuildPlayerMirrorBankCountChoices();
        playerMirrorBankRowsChooser = new JSONStorableStringChooser(
            "Player MultiScreen Rows",
            countChoices,
            PlayerMirrorBankMinCount.ToString(),
            "Player MultiScreen Rows");
        playerMirrorBankRowsChooser.displayChoices = new List<string>(countChoices);
        playerMirrorBankRowsChooser.setCallbackFunction = delegate(string value)
        {
            if (playerMirrorBankUiSyncGuard)
                return;

            ApplyAttachedPlayerMirrorBankChoices(null, value, null);
        };

        playerMirrorBankColumnsChooser = new JSONStorableStringChooser(
            "Player MultiScreen Columns",
            countChoices,
            PlayerMirrorBankMinCount.ToString(),
            "Player MultiScreen Columns");
        playerMirrorBankColumnsChooser.displayChoices = new List<string>(countChoices);
        playerMirrorBankColumnsChooser.setCallbackFunction = delegate(string value)
        {
            if (playerMirrorBankUiSyncGuard)
                return;

            ApplyAttachedPlayerMirrorBankChoices(null, null, value);
        };

        playerMirrorBankCircularRadiusField = new JSONStorableFloat(
            "Player MultiScreen Circular Radius",
            PlayerMirrorBankDefaultCircularRadius,
            delegate(float value)
            {
                if (playerMirrorBankUiSyncGuard)
                    return;

                ApplyAttachedPlayerMirrorBankCircularSettings(value, null);
            },
            PlayerMirrorBankMinCircularRadius,
            PlayerMirrorBankMaxCircularRadius,
            false);
        ConfigureTransientField(playerMirrorBankCircularRadiusField, false);

        playerMirrorBankCircularMaxAngleField = new JSONStorableFloat(
            "Player MultiScreen Circular Max Angle",
            PlayerMirrorBankDefaultCircularMaxAngle,
            delegate(float value)
            {
                if (playerMirrorBankUiSyncGuard)
                    return;

                ApplyAttachedPlayerMirrorBankCircularSettings(null, value);
            },
            PlayerMirrorBankMinCircularMaxAngle,
            PlayerMirrorBankMaxCircularMaxAngle,
            false);
        ConfigureTransientField(playerMirrorBankCircularMaxAngleField, false);
    }

    private void RegisterPlayerMirrorBankStorables()
    {
        if (playerMirrorBankLayoutChooser != null)
            RegisterStringChooser(playerMirrorBankLayoutChooser);
        if (playerMirrorBankRowsChooser != null)
            RegisterStringChooser(playerMirrorBankRowsChooser);
        if (playerMirrorBankColumnsChooser != null)
            RegisterStringChooser(playerMirrorBankColumnsChooser);
        if (playerMirrorBankCircularRadiusField != null)
            RegisterFloat(playerMirrorBankCircularRadiusField);
        if (playerMirrorBankCircularMaxAngleField != null)
            RegisterFloat(playerMirrorBankCircularMaxAngleField);
    }

    private void BuildPlayerMirrorBankUi()
    {
        if (playerMirrorBankLayoutChooser == null
            || playerMirrorBankRowsChooser == null
            || playerMirrorBankColumnsChooser == null)
        {
            return;
        }

        CreateSpacer(true);
        CreateFilterablePopup(playerMirrorBankLayoutChooser, false);
        CreateFilterablePopup(playerMirrorBankRowsChooser, true);
        CreateFilterablePopup(playerMirrorBankColumnsChooser, false);
        CreateSlider(playerMirrorBankCircularRadiusField, true);
        CreateSlider(playerMirrorBankCircularMaxAngleField, false);
    }

    private List<string> BuildPlayerMirrorBankCountChoices()
    {
        List<string> values = new List<string>(PlayerMirrorBankMaxCount);
        for (int i = PlayerMirrorBankMinCount; i <= PlayerMirrorBankMaxCount; i++)
            values.Add(i.ToString());
        return values;
    }

    private void RefreshAttachedPlayerMirrorBankChooserFields()
    {
        if (playerMirrorBankLayoutChooser == null
            || playerMirrorBankRowsChooser == null
            || playerMirrorBankColumnsChooser == null)
        {
            return;
        }

        StandalonePlayerRecord record;
        Atom hostAtom;
        if (!TryResolveAttachedHostedStandalonePlayerRecord(out record, out hostAtom) || record == null)
        {
            RefreshAttachedPlayerMirrorBankChooserFields(null);
            return;
        }

        RefreshAttachedPlayerMirrorBankChooserFields(record);
    }

    private void RefreshAttachedPlayerMirrorBankChooserFields(StandalonePlayerRecord record)
    {
        if (playerMirrorBankLayoutChooser == null
            || playerMirrorBankRowsChooser == null
            || playerMirrorBankColumnsChooser == null)
        {
            return;
        }

        StandalonePlayerMirrorBankState state = null;
        TryGetStandalonePlayerMirrorBankState(record, out state);

        string layoutMode = NormalizeStandalonePlayerMirrorBankLayoutMode(
            state != null ? state.layoutMode : PlayerMirrorBankLayoutOff);
        string rows = ResolveStandalonePlayerMirrorBankCountChoice(
            state != null ? state.rows : PlayerMirrorBankMinCount);
        string columns = ResolveStandalonePlayerMirrorBankCountChoice(
            state != null ? state.columns : PlayerMirrorBankMinCount);
        float circularRadius = state != null
            ? ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius)
            : PlayerMirrorBankDefaultCircularRadius;
        float circularMaxAngle = state != null
            ? ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees)
            : PlayerMirrorBankDefaultCircularMaxAngle;

        playerMirrorBankUiSyncGuard = true;
        try
        {
            playerMirrorBankLayoutChooser.valNoCallback = layoutMode;
            playerMirrorBankRowsChooser.valNoCallback = rows;
            playerMirrorBankColumnsChooser.valNoCallback = columns;
            if (playerMirrorBankCircularRadiusField != null)
                playerMirrorBankCircularRadiusField.valNoCallback = circularRadius;
            if (playerMirrorBankCircularMaxAngleField != null)
                playerMirrorBankCircularMaxAngleField.valNoCallback = circularMaxAngle;
        }
        finally
        {
            playerMirrorBankUiSyncGuard = false;
        }
    }

    private void ApplyAttachedPlayerMirrorBankChoices(string requestedLayoutMode, string requestedRows, string requestedColumns)
    {
        StandalonePlayerRecord record;
        string errorMessage;
        if (!TryResolveAttachedPlayerMirrorBankRecordForWrite(out record, out errorMessage) || record == null)
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            RefreshAttachedPlayerMirrorBankChooserFields();
            RefreshVisiblePlayerDebugFields();
            return;
        }

        StandalonePlayerMirrorBankState state = GetOrCreateStandalonePlayerMirrorBankState(record);
        state.layoutMode = string.IsNullOrEmpty(requestedLayoutMode)
            ? NormalizeStandalonePlayerMirrorBankLayoutMode(state.layoutMode)
            : NormalizeStandalonePlayerMirrorBankLayoutMode(requestedLayoutMode);
        state.rows = string.IsNullOrEmpty(requestedRows)
            ? ClampStandalonePlayerMirrorBankCount(state.rows)
            : ParseStandalonePlayerMirrorBankCount(requestedRows);
        state.columns = string.IsNullOrEmpty(requestedColumns)
            ? ClampStandalonePlayerMirrorBankCount(state.columns)
            : ParseStandalonePlayerMirrorBankCount(requestedColumns);
        state.dirty = true;

        if (string.Equals(state.layoutMode, PlayerMirrorBankLayoutOff, StringComparison.OrdinalIgnoreCase))
            DestroyStandalonePlayerMirrorBankSurfaces(state, true);

        RefreshAttachedPlayerMirrorBankChooserFields(record);
        RefreshVisiblePlayerDebugFields();
    }

    private void ApplyAttachedPlayerMirrorBankCircularSettings(float? requestedRadius, float? requestedMaxAngle)
    {
        StandalonePlayerRecord record;
        string errorMessage;
        if (!TryResolveAttachedPlayerMirrorBankRecordForWrite(out record, out errorMessage) || record == null)
        {
            SetLastError(errorMessage);
            SetLastReceipt(BuildBrokerResult(false, errorMessage, "{}"));
            RefreshAttachedPlayerMirrorBankChooserFields();
            RefreshVisiblePlayerDebugFields();
            return;
        }

        StandalonePlayerMirrorBankState state = GetOrCreateStandalonePlayerMirrorBankState(record);
        state.circularRadius = requestedRadius.HasValue
            ? ClampStandalonePlayerMirrorBankCircularRadius(requestedRadius.Value)
            : ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius);
        state.circularMaxAngleDegrees = requestedMaxAngle.HasValue
            ? ClampStandalonePlayerMirrorBankCircularMaxAngle(requestedMaxAngle.Value)
            : ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees);
        state.dirty = true;

        RefreshAttachedPlayerMirrorBankChooserFields(record);
        RefreshVisiblePlayerDebugFields();
    }

    private bool TryResolveAttachedPlayerMirrorBankRecordForWrite(out StandalonePlayerRecord record, out string errorMessage)
    {
        record = null;
        errorMessage = "";

        Atom hostAtom;
        if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
        {
            errorMessage = "attached player host atom not resolved";
            return false;
        }

        string hostAtomUid = string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim();
        if (string.IsNullOrEmpty(hostAtomUid))
        {
            errorMessage = "attached player host atom uid missing";
            return false;
        }

        HostedPlayerSurfaceContract ignoredContract;
        return TryResolveOrCreateHostedStandalonePlayerRecordForWrite(
            hostAtomUid,
            ResolveAttachedPlayerCurrentAspectMode(),
            out record,
            out ignoredContract,
            out errorMessage);
    }

    private StandalonePlayerMirrorBankState GetOrCreateStandalonePlayerMirrorBankState(StandalonePlayerRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.playbackKey))
            return null;

        StandalonePlayerMirrorBankState state;
        if (!standalonePlayerMirrorBanks.TryGetValue(record.playbackKey, out state) || state == null)
        {
            state = new StandalonePlayerMirrorBankState();
            state.playbackKey = record.playbackKey;
            standalonePlayerMirrorBanks[record.playbackKey] = state;
        }

        state.layoutMode = NormalizeStandalonePlayerMirrorBankLayoutMode(state.layoutMode);
        state.rows = ClampStandalonePlayerMirrorBankCount(state.rows);
        state.columns = ClampStandalonePlayerMirrorBankCount(state.columns);
        state.circularRadius = ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius);
        state.circularMaxAngleDegrees = ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees);
        return state;
    }

    private bool TryGetStandalonePlayerMirrorBankState(StandalonePlayerRecord record, out StandalonePlayerMirrorBankState state)
    {
        state = null;
        if (record == null || string.IsNullOrEmpty(record.playbackKey))
            return false;

        if (!standalonePlayerMirrorBanks.TryGetValue(record.playbackKey, out state) || state == null)
            return false;

        state.layoutMode = NormalizeStandalonePlayerMirrorBankLayoutMode(state.layoutMode);
        state.rows = ClampStandalonePlayerMirrorBankCount(state.rows);
        state.columns = ClampStandalonePlayerMirrorBankCount(state.columns);
        state.circularRadius = ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius);
        state.circularMaxAngleDegrees = ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees);
        return true;
    }

    private string NormalizeStandalonePlayerMirrorBankLayoutMode(string layoutMode)
    {
        string normalized = string.IsNullOrEmpty(layoutMode)
            ? PlayerMirrorBankLayoutOff
            : layoutMode.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case PlayerMirrorBankLayoutGrid:
            case PlayerMirrorBankLayoutCircular:
                return normalized;
            default:
                return PlayerMirrorBankLayoutOff;
        }
    }

    private int ParseStandalonePlayerMirrorBankCount(string rawValue)
    {
        int parsedValue;
        if (!int.TryParse(string.IsNullOrEmpty(rawValue) ? "" : rawValue.Trim(), out parsedValue))
            parsedValue = PlayerMirrorBankMinCount;
        return ClampStandalonePlayerMirrorBankCount(parsedValue);
    }

    private int ClampStandalonePlayerMirrorBankCount(int value)
    {
        return Mathf.Clamp(value, PlayerMirrorBankMinCount, PlayerMirrorBankMaxCount);
    }

    private string ResolveStandalonePlayerMirrorBankCountChoice(int value)
    {
        return ClampStandalonePlayerMirrorBankCount(value).ToString();
    }

    private float ClampStandalonePlayerMirrorBankCircularRadius(float value)
    {
        return Mathf.Clamp(value, PlayerMirrorBankMinCircularRadius, PlayerMirrorBankMaxCircularRadius);
    }

    private float ClampStandalonePlayerMirrorBankCircularMaxAngle(float value)
    {
        return Mathf.Clamp(value, PlayerMirrorBankMinCircularMaxAngle, PlayerMirrorBankMaxCircularMaxAngle);
    }

    private string DescribeStandalonePlayerMirrorBank(StandalonePlayerRecord record)
    {
        StandalonePlayerMirrorBankState state;
        if (!TryGetStandalonePlayerMirrorBankState(record, out state) || state == null)
            return "bank=off";

        string layoutMode = NormalizeStandalonePlayerMirrorBankLayoutMode(state.layoutMode);
        if (string.Equals(layoutMode, PlayerMirrorBankLayoutOff, StringComparison.OrdinalIgnoreCase))
            return "bank=off";

        int screenCount = ClampStandalonePlayerMirrorBankCount(state.rows) * ClampStandalonePlayerMirrorBankCount(state.columns);
        string summary = "bank=" + layoutMode + " " + state.columns + "x" + state.rows + " screens=" + screenCount;
        if (string.Equals(layoutMode, PlayerMirrorBankLayoutCircular, StringComparison.OrdinalIgnoreCase))
        {
            summary += " r="
                + ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius).ToString("0.##")
                + " angle="
                + ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees).ToString("0.#");
        }

        return summary;
    }

    private void TickStandalonePlayerMirrorBank(StandalonePlayerRecord record)
    {
        if (record == null)
            return;

        string ignoredError;
        TryRefreshStandalonePlayerMirrorBank(record, false, out ignoredError);
    }

    private void DestroyStandalonePlayerMirrorBank(StandalonePlayerRecord record)
    {
        StandalonePlayerMirrorBankState state;
        if (!TryGetStandalonePlayerMirrorBankState(record, out state) || state == null)
            return;

        DestroyStandalonePlayerMirrorBankSurfaces(state, true);
        standalonePlayerMirrorBanks.Remove(record.playbackKey ?? "");
    }

    private void DestroyStandalonePlayerMirrorBankSurfaces(StandalonePlayerMirrorBankState state, bool destroyRoot)
    {
        if (state == null)
            return;

        for (int i = 0; i < state.surfaces.Count; i++)
        {
            StandalonePlayerMirrorSurfaceRecord surface = state.surfaces[i];
            if (surface == null)
                continue;

            if (surface.rootObject != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(surface.rootObject);
                }
                catch
                {
                }
            }

            if (surface.backingMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(surface.backingMaterial);
                }
                catch
                {
                }
            }

            if (surface.contentMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(surface.contentMaterial);
                }
                catch
                {
                }
            }
        }

        state.surfaces.Clear();
        state.lastSignature = "";
        if (destroyRoot && state.bankRoot != null)
        {
            try
            {
                UnityEngine.Object.Destroy(state.bankRoot);
            }
            catch
            {
            }

            state.bankRoot = null;
        }
    }

    private bool TryRefreshStandalonePlayerMirrorBank(StandalonePlayerRecord record, bool forceRebuild, out string errorMessage)
    {
        errorMessage = "";
        if (record == null)
        {
            errorMessage = "player record missing";
            return false;
        }

        StandalonePlayerMirrorBankState state;
        if (!TryGetStandalonePlayerMirrorBankState(record, out state) || state == null)
            return true;

        state.layoutMode = NormalizeStandalonePlayerMirrorBankLayoutMode(state.layoutMode);
        if (string.Equals(state.layoutMode, PlayerMirrorBankLayoutOff, StringComparison.OrdinalIgnoreCase))
        {
            DestroyStandalonePlayerMirrorBankSurfaces(state, true);
            return true;
        }

        GameObject surfaceObject;
        Material basisMaterial;
        Vector3 surfaceLocalCenter;
        Vector3 surfaceLocalSize;
        if (!TryResolveStandalonePlayerMirrorBankAnchor(
                record,
                out surfaceObject,
                out basisMaterial,
                out surfaceLocalCenter,
                out surfaceLocalSize,
                out errorMessage))
        {
            DestroyStandalonePlayerMirrorBankSurfaces(state, true);
            return false;
        }

        Texture sourceTexture;
        Vector2 sourceScale;
        Vector2 sourceOffset;
        string sourceName;
        if (!TryResolveStandalonePlayerSourceTexture(record, out sourceTexture, out sourceScale, out sourceOffset, out sourceName)
            || sourceTexture == null)
        {
            DestroyStandalonePlayerMirrorBankSurfaces(state, true);
            errorMessage = "player mirror bank source texture unavailable";
            return false;
        }

        string signature = BuildStandalonePlayerMirrorBankSignature(
            state,
            record,
            sourceTexture,
            sourceScale,
            sourceOffset,
            surfaceObject,
            surfaceLocalSize);
        if (!forceRebuild
            && !state.dirty
            && string.Equals(state.lastSignature, signature, StringComparison.Ordinal)
            && state.bankRoot != null)
        {
            return true;
        }

        DestroyStandalonePlayerMirrorBankSurfaces(state, true);

        Transform anchorParent = surfaceObject.transform.parent != null
            ? surfaceObject.transform.parent
            : surfaceObject.transform;
        GameObject bankRoot = new GameObject("FAPlayerMirrorBank_" + (string.IsNullOrEmpty(record.displayId) ? "main" : record.displayId));
        bankRoot.layer = surfaceObject.layer;
        bankRoot.transform.SetParent(anchorParent, false);
        bankRoot.transform.position = surfaceObject.transform.TransformPoint(surfaceLocalCenter);
        bankRoot.transform.rotation = surfaceObject.transform.rotation;
        bankRoot.transform.localScale = Vector3.one;
        state.bankRoot = bankRoot;

        float slabWidth = Mathf.Max(0.001f, surfaceLocalSize.x);
        float slabHeight = Mathf.Max(0.001f, surfaceLocalSize.y);
        float horizontalGap = Mathf.Max(PlayerMirrorBankMinGap, slabWidth * PlayerMirrorBankGapFactor);
        float verticalGap = Mathf.Max(PlayerMirrorBankMinGap, slabHeight * PlayerMirrorBankGapFactor);
        float horizontalStride = slabWidth + horizontalGap;
        float verticalStride = slabHeight + verticalGap;

        float contentAspect = 0f;
        TryResolveProjectedContentAspect(basisMaterial, sourceTexture, out contentAspect);
        if (contentAspect <= 0.001f)
            contentAspect = Mathf.Max(0.001f, slabWidth / Mathf.Max(0.001f, slabHeight));

        float overlayWidth;
        float overlayHeight;
        FrameAngelPlayerMediaParity.ComputePresentedSize(
            slabWidth,
            slabHeight,
            contentAspect,
            record.aspectMode,
            out overlayWidth,
            out overlayHeight);
        overlayWidth = Mathf.Max(0.001f, overlayWidth);
        overlayHeight = Mathf.Max(0.001f, overlayHeight);

        int rows = ClampStandalonePlayerMirrorBankCount(state.rows);
        int columns = ClampStandalonePlayerMirrorBankCount(state.columns);
        float circularRadiusMeters = slabWidth * ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius);
        float circularMaxAngle = ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees);
        for (int columnIndex = 0; columnIndex < columns; columnIndex++)
        {
            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                Vector3 localPosition;
                Quaternion localRotation;
                ResolveStandalonePlayerMirrorBankScreenPose(
                    state.layoutMode,
                    columnIndex,
                    columns,
                    rowIndex,
                    rows,
                    horizontalStride,
                    verticalStride,
                    circularRadiusMeters,
                    circularMaxAngle,
                    out localPosition,
                    out localRotation);

                int logicalOffset = ResolveStandalonePlayerMirrorBankLogicalOffset(columnIndex);
                StandalonePlayerMirrorSurfaceRecord createdSurface;
                if (!TryCreateStandalonePlayerMirrorSurface(
                        bankRoot.transform,
                        surfaceObject.layer,
                        basisMaterial,
                        sourceTexture,
                        sourceScale,
                        sourceOffset,
                        sourceName,
                        slabWidth,
                        slabHeight,
                        overlayWidth,
                        overlayHeight,
                        localPosition,
                        localRotation,
                        logicalOffset,
                        rowIndex,
                        out createdSurface,
                        out errorMessage))
                {
                    DestroyStandalonePlayerMirrorBankSurfaces(state, true);
                    return false;
                }

                state.surfaces.Add(createdSurface);
            }
        }

        state.lastSignature = signature;
        state.dirty = false;
        return true;
    }

    private string BuildStandalonePlayerMirrorBankSignature(
        StandalonePlayerMirrorBankState state,
        StandalonePlayerRecord record,
        Texture sourceTexture,
        Vector2 sourceScale,
        Vector2 sourceOffset,
        GameObject surfaceObject,
        Vector3 surfaceLocalSize)
    {
        return NormalizeStandalonePlayerMirrorBankLayoutMode(state.layoutMode)
            + "|r=" + ClampStandalonePlayerMirrorBankCount(state.rows)
            + "|c=" + ClampStandalonePlayerMirrorBankCount(state.columns)
            + "|tex=" + (sourceTexture != null ? sourceTexture.GetInstanceID().ToString() : "none")
            + "|scale=" + FormatFloat(sourceScale.x) + "," + FormatFloat(sourceScale.y)
            + "|offset=" + FormatFloat(sourceOffset.x) + "," + FormatFloat(sourceOffset.y)
            + "|aspect=" + (record != null ? (record.aspectMode ?? "") : "")
            + "|radius=" + FormatFloat(state != null ? ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius) : PlayerMirrorBankDefaultCircularRadius)
            + "|angle=" + FormatFloat(state != null ? ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees) : PlayerMirrorBankDefaultCircularMaxAngle)
            + "|surface=" + (surfaceObject != null ? surfaceObject.GetInstanceID().ToString() : "none")
            + "|size=" + FormatFloat(surfaceLocalSize.x) + "," + FormatFloat(surfaceLocalSize.y) + "," + FormatFloat(surfaceLocalSize.z);
    }

    private bool TryResolveStandalonePlayerMirrorBankAnchor(
        StandalonePlayerRecord record,
        out GameObject surfaceObject,
        out Material basisMaterial,
        out Vector3 surfaceLocalCenter,
        out Vector3 surfaceLocalSize,
        out string errorMessage)
    {
        surfaceObject = null;
        basisMaterial = null;
        surfaceLocalCenter = Vector3.zero;
        surfaceLocalSize = Vector3.zero;
        errorMessage = "";

        if (record == null)
        {
            errorMessage = "player record missing";
            return false;
        }

        if (IsHostedPlayerInstanceId(record.instanceId))
        {
            string hostAtomUid = ResolveHostedPlayerHostAtomUid(record);
            HostedPlayerSurfaceContract contract;
            if (!TryResolveHostedPlayerSurfaceContract(hostAtomUid, out contract, out errorMessage) || contract == null)
                return false;

            surfaceObject = contract.screenSurfaceObject != null
                ? contract.screenSurfaceObject
                : contract.disconnectSurfaceObject;
        }
        else
        {
            InnerPieceInstanceRecord instance;
            InnerPieceScreenSlotRuntimeRecord slot;
            if (!TryResolveInnerPieceScreenSlot(record.instanceId, record.slotId, out instance, out slot, out errorMessage))
                return false;

            surfaceObject = slot.screenSurfaceObject != null
                ? slot.screenSurfaceObject
                : ResolvePlayerMediaTargetObject(instance, slot);
        }

        if (surfaceObject == null)
        {
            errorMessage = "player mirror bank surface not found";
            return false;
        }

        if (!TryBuildInnerPieceSurfaceLocalBounds(surfaceObject, out surfaceLocalCenter, out surfaceLocalSize))
        {
            errorMessage = "player mirror bank bounds unavailable";
            return false;
        }

        basisMaterial = ResolveStandalonePlayerMirrorBankBasisMaterial(surfaceObject);
        return true;
    }

    private Material ResolveStandalonePlayerMirrorBankBasisMaterial(GameObject surfaceObject)
    {
        if (surfaceObject == null)
            return null;

        Renderer[] renderers = surfaceObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null)
            return null;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null)
                continue;

            for (int j = 0; j < materials.Length; j++)
            {
                if (materials[j] != null)
                    return materials[j];
            }
        }

        return null;
    }

    private void ResolveStandalonePlayerMirrorBankScreenPose(
        string layoutMode,
        int columnIndex,
        int columnCount,
        int rowIndex,
        int rowCount,
        float horizontalStride,
        float verticalStride,
        float circularRadiusMeters,
        float circularMaxAngleDegrees,
        out Vector3 localPosition,
        out Quaternion localRotation)
    {
        float y = ResolveStandalonePlayerMirrorBankRowOffset(rowIndex, rowCount, verticalStride);
        if (string.Equals(layoutMode, PlayerMirrorBankLayoutCircular, StringComparison.OrdinalIgnoreCase))
        {
            int logicalOffset = ResolveStandalonePlayerMirrorBankLogicalOffset(columnIndex);
            float angleStepDegrees = ClampStandalonePlayerMirrorBankCircularMaxAngle(circularMaxAngleDegrees) / Mathf.Max(1f, columnCount + 1f);
            float angleDegrees = logicalOffset * angleStepDegrees;
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            float radius = Mathf.Max(0.001f, circularRadiusMeters);
            // The circular bank is arranged around a focal point in front of the main screen
            // instead of around the screen itself. That keeps "semi-circle in front of me"
            // behavior deterministic while still letting the main screen anchor the bank.
            Vector3 focusPoint = new Vector3(0f, 0f, radius);
            localPosition = new Vector3(
                Mathf.Sin(angleRadians) * radius,
                y,
                radius - (Mathf.Cos(angleRadians) * radius));
            Vector3 lookDirection = focusPoint - localPosition;
            localRotation = lookDirection.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : Quaternion.identity;
            return;
        }

        int gridTrack = ResolveStandalonePlayerMirrorBankLogicalOffset(columnIndex);
        localPosition = new Vector3(gridTrack * horizontalStride, y, 0f);
        localRotation = Quaternion.identity;
    }

    private float ResolveStandalonePlayerMirrorBankRowOffset(int rowIndex, int rowCount, float verticalStride)
    {
        return ((rowCount - 1) * 0.5f - rowIndex) * verticalStride;
    }

    private int ResolveStandalonePlayerMirrorBankLogicalOffset(int columnIndex)
    {
        int step = (columnIndex / 2) + 1;
        return (columnIndex % 2) == 0 ? step : -step;
    }

    private bool TryCreateStandalonePlayerMirrorSurface(
        Transform bankRoot,
        int layer,
        Material basisMaterial,
        Texture sourceTexture,
        Vector2 sourceScale,
        Vector2 sourceOffset,
        string sourceName,
        float slabWidth,
        float slabHeight,
        float overlayWidth,
        float overlayHeight,
        Vector3 localPosition,
        Quaternion localRotation,
        int logicalOffset,
        int rowIndex,
        out StandalonePlayerMirrorSurfaceRecord createdSurface,
        out string errorMessage)
    {
        createdSurface = null;
        errorMessage = "";
        if (bankRoot == null || sourceTexture == null)
        {
            errorMessage = "player mirror bank create arguments invalid";
            return false;
        }

        Material contentMaterial;
        if (!TryCreateResolvedVideoTextureMaterialFromTexture(
                basisMaterial,
                sourceTexture,
                sourceScale,
                sourceOffset,
                true,
                false,
                out contentMaterial)
            || contentMaterial == null)
        {
            errorMessage = "player mirror bank content material not created";
            return false;
        }

        if (ShouldFlipProjectedOverlayVertically(sourceTexture, sourceName, false))
            FlipProjectedScreenTextureVertically(contentMaterial);
        TryForceProjectedScreenFrontFaceOnly(contentMaterial);

        Material backingMaterial;
        if (!TryCreateStandalonePlayerMirrorBackingMaterial(basisMaterial, out backingMaterial) || backingMaterial == null)
        {
            try
            {
                UnityEngine.Object.Destroy(contentMaterial);
            }
            catch
            {
            }

            errorMessage = "player mirror bank backing material not created";
            return false;
        }

        GameObject rootObject = null;
        try
        {
            rootObject = new GameObject("FAPlayerMirrorScreen_" + logicalOffset + "_" + rowIndex);
            rootObject.layer = layer;
            rootObject.transform.SetParent(bankRoot, false);
            rootObject.transform.localPosition = localPosition;
            rootObject.transform.localRotation = localRotation;
            rootObject.transform.localScale = Vector3.one;

            GameObject backingObject = CreateStandalonePlayerMirrorQuad(
                rootObject.transform,
                layer,
                "Backing",
                Vector3.zero,
                Quaternion.identity,
                new Vector3(slabWidth, slabHeight, 1f),
                backingMaterial);
            GameObject contentObject = CreateStandalonePlayerMirrorQuad(
                rootObject.transform,
                layer,
                "Content",
                new Vector3(0f, 0f, PlayerMirrorBankOverlayGap),
                Quaternion.identity,
                new Vector3(overlayWidth, overlayHeight, 1f),
                contentMaterial);

            createdSurface = new StandalonePlayerMirrorSurfaceRecord();
            createdSurface.rootObject = rootObject;
            createdSurface.backingObject = backingObject;
            createdSurface.contentObject = contentObject;
            createdSurface.backingMaterial = backingMaterial;
            createdSurface.contentMaterial = contentMaterial;
            return true;
        }
        catch (Exception ex)
        {
            if (rootObject != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(rootObject);
                }
                catch
                {
                }
            }

            try
            {
                UnityEngine.Object.Destroy(backingMaterial);
            }
            catch
            {
            }

            try
            {
                UnityEngine.Object.Destroy(contentMaterial);
            }
            catch
            {
            }

            errorMessage = "player mirror bank create surface failed: " + ex.Message;
            return false;
        }
    }

    private GameObject CreateStandalonePlayerMirrorQuad(
        Transform parent,
        int layer,
        string name,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.layer = layer;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = localRotation;
        quad.transform.localScale = localScale;

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.Destroy(collider);

        Renderer renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterials = new[] { material };
            renderer.enabled = true;
            renderer.receiveShadows = false;
#pragma warning disable CS0618
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#pragma warning restore CS0618
            renderer.allowOcclusionWhenDynamic = false;
        }

        return quad;
    }

    private bool TryCreateStandalonePlayerMirrorBackingMaterial(Material basisMaterial, out Material backingMaterial)
    {
        if (TryCreateRuntimeBackingMaterial(basisMaterial, out backingMaterial) && backingMaterial != null)
            return true;

        backingMaterial = null;
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return false;

        try
        {
            backingMaterial = new Material(shader);
            if (backingMaterial.HasProperty("_Color"))
                backingMaterial.SetColor("_Color", Color.black);
            try
            {
                backingMaterial.renderQueue = 4490;
            }
            catch
            {
            }

            return true;
        }
        catch
        {
            if (backingMaterial != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(backingMaterial);
                }
                catch
                {
                }
            }

            backingMaterial = null;
            return false;
        }
    }

    private void AppendStandalonePlayerMirrorBankJson(StringBuilder sb, StandalonePlayerRecord record)
    {
        if (sb == null)
            return;

        StandalonePlayerMirrorBankState state;
        if (!TryGetStandalonePlayerMirrorBankState(record, out state) || state == null)
        {
            sb.Append("{\"layoutMode\":\"").Append(PlayerMirrorBankLayoutOff).Append("\",\"rows\":1,\"columns\":1,\"screenCount\":0,\"active\":false}");
            return;
        }

        string layoutMode = NormalizeStandalonePlayerMirrorBankLayoutMode(state.layoutMode);
        int rows = ClampStandalonePlayerMirrorBankCount(state.rows);
        int columns = ClampStandalonePlayerMirrorBankCount(state.columns);
        float circularRadius = ClampStandalonePlayerMirrorBankCircularRadius(state.circularRadius);
        float circularMaxAngle = ClampStandalonePlayerMirrorBankCircularMaxAngle(state.circularMaxAngleDegrees);
        int screenCount = string.Equals(layoutMode, PlayerMirrorBankLayoutOff, StringComparison.OrdinalIgnoreCase)
            ? 0
            : (rows * columns);
        sb.Append('{');
        sb.Append("\"layoutMode\":\"").Append(EscapeJsonString(layoutMode)).Append("\",");
        sb.Append("\"rows\":").Append(rows).Append(',');
        sb.Append("\"columns\":").Append(columns).Append(',');
        sb.Append("\"circularRadius\":").Append(FormatFloat(circularRadius)).Append(',');
        sb.Append("\"circularMaxAngleDegrees\":").Append(FormatFloat(circularMaxAngle)).Append(',');
        sb.Append("\"screenCount\":").Append(screenCount).Append(',');
        sb.Append("\"active\":").Append(screenCount > 0 ? "true" : "false");
        sb.Append('}');
    }
}
