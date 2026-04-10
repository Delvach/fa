using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class GhostMetaControlSurfaceProofMetadata : MonoBehaviour
{
    [Serializable]
    public sealed class ControlElementBinding
    {
        public string elementId;
        public string elementLabel;
        public string actionId;
        public string elementKind;
        public string valueKind;
        public RectTransform rectTransform;
        public bool readOnly;
    }

    [Header("Surface Identity")]
    public string controlSurfaceId = "meta_video_player_proof";
    public string controlSurfaceLabel = "Meta Video Player Proof";
    public string controlFamilyId = "meta_ui_video_player";
    public string layoutSource = "canvas_export_v1";
    public string defaultTargetDisplayId = "player_main";
    public string[] targetDisplayIds = { "player_main" };

    [Header("Surface Nodes")]
    public string surfaceNodeId = "video_surface_root";
    public string colliderNodeId = "video_surface_collider";
    public RectTransform surfaceRoot;
    public GhostMetaVideoPlayerProof proofBinder;

    [Header("Elements")]
    public List<ControlElementBinding> elements = new List<ControlElementBinding>();

    public void ConfigureForVideoPlayerProof(GhostMetaVideoPlayerProof proof)
    {
        proofBinder = proof != null ? proof : GetComponent<GhostMetaVideoPlayerProof>();
        EnsureDefaults();
        if (proofBinder == null)
        {
            return;
        }

        RectTransform preferredSurfaceRoot = ResolvePreferredSurfaceRoot(proofBinder);
        if (preferredSurfaceRoot != null)
        {
            surfaceRoot = preferredSurfaceRoot;
        }

        UpsertElement(
            "play_pause_button",
            "Play/Pause",
            "play_pause",
            "button",
            "bool",
            ResolveRectTransform(proofBinder.playPauseToggle),
            readOnly: false);

        UpsertElement(
            "scrub_slider",
            "Scrub",
            "scrub_normalized",
            "slider",
            "normalized_float",
            ResolveRectTransform(proofBinder.timeSlider),
            readOnly: false);

        UpsertElement(
            "volume_slider",
            "Volume",
            "volume_normalized",
            "slider",
            "normalized_float",
            FindRelativeRectTransform("CanvasRoot/Controls/PlayerControls/Control/Sound/VolumeSlider"),
            readOnly: false);

        UpsertElement(
            "time_current_label",
            "Current Time",
            "time_current_label",
            "label",
            "none",
            ResolveRectTransform(proofBinder.leftLabel),
            readOnly: true);

        UpsertElement(
            "time_remaining_label",
            "Remaining Time",
            "time_remaining_label",
            "label",
            "none",
            ResolveRectTransform(proofBinder.rightLabel),
            readOnly: true);
    }

    [ContextMenu("Log Export Surface Summary")]
    public void LogExportSurfaceSummary()
    {
        Debug.Log(BuildSummary());
    }

    public string BuildSummary()
    {
        EnsureDefaults();

        var builder = new StringBuilder();
        builder.Append("GhostMetaControlSurfaceProofMetadata: ");
        builder.Append(controlSurfaceId);
        builder.Append(" -> ");
        builder.Append(defaultTargetDisplayId);
        builder.Append(" [");
        builder.Append(layoutSource);
        builder.Append(']');

        if (elements.Count == 0)
        {
            builder.Append(" elements=0");
            return builder.ToString();
        }

        builder.Append(" elements=");
        builder.Append(elements.Count);
        for (int i = 0; i < elements.Count; i++)
        {
            ControlElementBinding element = elements[i];
            builder.Append(" | ");
            builder.Append(element.elementId);
            builder.Append(':');
            builder.Append(element.actionId);
            builder.Append('@');
            builder.Append(element.rectTransform != null ? element.rectTransform.name : "missing");
        }

        return builder.ToString();
    }

    private void OnValidate()
    {
        EnsureDefaults();
        RepairBindingsFromProofBinder();
    }

    private void OnEnable()
    {
        EnsureDefaults();
        RepairBindingsFromProofBinder();
    }

    private void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(controlSurfaceId))
        {
            controlSurfaceId = "meta_video_player_proof";
        }

        if (string.IsNullOrWhiteSpace(controlSurfaceLabel))
        {
            controlSurfaceLabel = "Meta Video Player Proof";
        }

        if (string.IsNullOrWhiteSpace(controlFamilyId))
        {
            controlFamilyId = "meta_ui_video_player";
        }

        if (string.IsNullOrWhiteSpace(layoutSource))
        {
            layoutSource = "canvas_export_v1";
        }

        if (targetDisplayIds == null || targetDisplayIds.Length == 0)
        {
            targetDisplayIds = new[] { "player_main" };
        }

        if (string.IsNullOrWhiteSpace(defaultTargetDisplayId))
        {
            defaultTargetDisplayId = targetDisplayIds[0];
        }
    }

    private void RepairBindingsFromProofBinder()
    {
        GhostMetaVideoPlayerProof binder = proofBinder != null ? proofBinder : GetComponent<GhostMetaVideoPlayerProof>();
        if (binder == null)
        {
            return;
        }

        bool changed = false;

        if (proofBinder != binder)
        {
            proofBinder = binder;
            changed = true;
        }

        RectTransform preferredSurfaceRoot = ResolvePreferredSurfaceRoot(binder);
        if ((surfaceRoot == null || surfaceRoot == binder.demoVideoContent) && preferredSurfaceRoot != null)
        {
            surfaceRoot = preferredSurfaceRoot;
            changed = true;
        }

        if (surfaceRoot == null)
        {
            RectTransform resolvedSurfaceRoot = FindRelativeRectTransform("CanvasRoot/Controls");
            if (resolvedSurfaceRoot != null)
            {
                surfaceRoot = resolvedSurfaceRoot;
                changed = true;
            }
        }

        RectTransform playPauseRect = ResolveRectTransform(binder.playPauseToggle) ??
            FindRelativeRectTransform("CanvasRoot/Controls/PlayerControls/ControlBar/BorderlessButton_IconAndLabel");
        RectTransform scrubRect = ResolveRectTransform(binder.timeSlider) ??
            FindRelativeRectTransform("CanvasRoot/Controls/PlayerControls/SmallSlider_LabelsAndIcons/SmallSlider");
        RectTransform volumeRect = FindRelativeRectTransform("CanvasRoot/Controls/PlayerControls/Control/Sound/VolumeSlider");
        RectTransform currentTimeRect = ResolveRectTransform(binder.leftLabel) ??
            FindRelativeRectTransform("CanvasRoot/Controls/PlayerControls/SmallSlider_LabelsAndIcons/LabelsText/TextLeft");
        RectTransform remainingTimeRect = ResolveRectTransform(binder.rightLabel) ??
            FindRelativeRectTransform("CanvasRoot/Controls/PlayerControls/SmallSlider_LabelsAndIcons/LabelsText/TextRight");

        changed |= UpsertElement(
            "play_pause_button",
            "Play/Pause",
            "play_pause",
            "button",
            "bool",
            playPauseRect,
            readOnly: false);

        changed |= UpsertElement(
            "scrub_slider",
            "Scrub",
            "scrub_normalized",
            "slider",
            "normalized_float",
            scrubRect,
            readOnly: false);

        changed |= UpsertElement(
            "volume_slider",
            "Volume",
            "volume_normalized",
            "slider",
            "normalized_float",
            volumeRect,
            readOnly: false);

        changed |= UpsertElement(
            "time_current_label",
            "Current Time",
            "time_current_label",
            "label",
            "none",
            currentTimeRect,
            readOnly: true);

        changed |= UpsertElement(
            "time_remaining_label",
            "Remaining Time",
            "time_remaining_label",
            "label",
            "none",
            remainingTimeRect,
            readOnly: true);

#if UNITY_EDITOR
        if (changed && !Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif
    }

    private bool UpsertElement(
        string elementId,
        string elementLabel,
        string actionId,
        string elementKind,
        string valueKind,
        RectTransform rectTransform,
        bool readOnly)
    {
        bool changed = false;
        ControlElementBinding element = elements.Find(
            existing => string.Equals(existing.elementId, elementId, StringComparison.Ordinal));
        if (element == null)
        {
            element = new ControlElementBinding();
            elements.Add(element);
            changed = true;
        }

        if (!string.Equals(element.elementId, elementId, StringComparison.Ordinal))
        {
            element.elementId = elementId;
            changed = true;
        }

        if (!string.Equals(element.elementLabel, elementLabel, StringComparison.Ordinal))
        {
            element.elementLabel = elementLabel;
            changed = true;
        }

        if (!string.Equals(element.actionId, actionId, StringComparison.Ordinal))
        {
            element.actionId = actionId;
            changed = true;
        }

        if (!string.Equals(element.elementKind, elementKind, StringComparison.Ordinal))
        {
            element.elementKind = elementKind;
            changed = true;
        }

        if (!string.Equals(element.valueKind, valueKind, StringComparison.Ordinal))
        {
            element.valueKind = valueKind;
            changed = true;
        }

        if (element.rectTransform != rectTransform)
        {
            element.rectTransform = rectTransform;
            changed = true;
        }

        if (element.readOnly != readOnly)
        {
            element.readOnly = readOnly;
            changed = true;
        }

        return changed;
    }

    private RectTransform ResolvePreferredSurfaceRoot(GhostMetaVideoPlayerProof binder)
    {
        RectTransform controlsRoot = FindRelativeRectTransform("CanvasRoot/Controls");
        if (controlsRoot != null)
        {
            return controlsRoot;
        }

        if (binder != null && binder.demoVideoContent != null)
        {
            return binder.demoVideoContent;
        }

        return FindRelativeRectTransform("CanvasRoot/Controls/DemoVideoContent");
    }

    private static RectTransform ResolveRectTransform(Component component)
    {
        if (component == null)
        {
            return null;
        }

        return component.transform as RectTransform;
    }

    private RectTransform FindRelativeRectTransform(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        Transform current = transform;
        string[] parts = relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (current == null)
            {
                return null;
            }

            current = current.Find(parts[i]);
        }

        return current as RectTransform;
    }
}
