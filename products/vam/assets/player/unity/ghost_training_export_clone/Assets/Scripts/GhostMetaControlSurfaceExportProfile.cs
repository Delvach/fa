using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class GhostMetaControlSurfaceExportProfile : MonoBehaviour
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

    [Serializable]
    public sealed class ActionMappingRule
    {
        public string pathContains;
        public string labelContains;
        public string elementKind;
        public string actionId;
        public string elementKindOverride;
        public string valueKindOverride;
    }

    [Header("Export")]
    public string exportDisplayName = "";
    public string exportTagsCsv = "ghost,meta_ui,toolkit";
    public string sourcePrefabAssetPath = "";
    public bool autoRefreshOnValidate;
    public bool includeReadOnlyLabels;
    [Tooltip("Multiplier applied to canvas-space units before export. Leave 0 to auto-guess.")]
    public float surfaceUnitsToMetersMultiplier;

    [Header("Surface Identity")]
    public string controlSurfaceId = "meta_ui_surface";
    public string controlSurfaceLabel = "Meta UI Surface";
    public string controlFamilyId = "meta_ui_toolkit";
    public string controlThemeId = "";
    public string controlThemeLabel = "";
    public string controlThemeVariantId = "";
    public string controlThemeAssetPath = "";
    public string controlThemeAssetGuid = "";
    public string toolkitCategory = "";
    public string layoutSource = "canvas_export_v1";
    public string defaultTargetDisplayId = "player_main";
    public string[] targetDisplayIds = { "player_main" };

    [Header("Surface Nodes")]
    public string surfaceNodeId = "";
    public string colliderNodeId = "";
    public RectTransform surfaceRoot;

    [Header("Elements")]
    public List<ControlElementBinding> elements = new List<ControlElementBinding>();
    public List<ActionMappingRule> actionMappings = new List<ActionMappingRule>();

    [ContextMenu("Auto Configure Export Elements")]
    public void AutoConfigureElements()
    {
        EnsureDefaults();
        RectTransform resolvedSurfaceRoot = ResolveSurfaceRoot();
        if (resolvedSurfaceRoot == null)
        {
            return;
        }

        surfaceRoot = resolvedSurfaceRoot;
        if (surfaceUnitsToMetersMultiplier <= 0f)
        {
            surfaceUnitsToMetersMultiplier = GuessSurfaceUnitsToMetersMultiplier(resolvedSurfaceRoot);
        }

        List<ControlElementBinding> rebuilt = new List<ControlElementBinding>();
        HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int sequence = 0;

        AppendScrollViews(rebuilt, usedIds, ref sequence);
        AppendInputFields(rebuilt, usedIds, ref sequence);
        AppendSliders(rebuilt, usedIds, ref sequence);
        AppendToggles(rebuilt, usedIds, ref sequence);
        AppendButtons(rebuilt, usedIds, ref sequence);

        if (includeReadOnlyLabels)
        {
            AppendLabels(rebuilt, usedIds, ref sequence);
        }

        if (rebuilt.Count == 0 && surfaceRoot != null)
        {
            AppendElement(rebuilt, usedIds, ref sequence, surfaceRoot, "Surface", "surface", "none", true);
        }

        elements = rebuilt
            .OrderBy(binding => binding.elementId ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif
    }

    public void Configure(
        string surfaceId,
        string surfaceLabel,
        string familyId,
        RectTransform root,
        string displayName,
        string tagsCsv,
        string targetDisplayId,
        string[] supportedTargetDisplayIds)
    {
        controlSurfaceId = string.IsNullOrWhiteSpace(surfaceId) ? controlSurfaceId : surfaceId.Trim();
        controlSurfaceLabel = string.IsNullOrWhiteSpace(surfaceLabel) ? controlSurfaceLabel : surfaceLabel.Trim();
        controlFamilyId = string.IsNullOrWhiteSpace(familyId) ? controlFamilyId : familyId.Trim();
        surfaceRoot = root != null ? root : surfaceRoot;
        exportDisplayName = string.IsNullOrWhiteSpace(displayName) ? exportDisplayName : displayName.Trim();
        exportTagsCsv = string.IsNullOrWhiteSpace(tagsCsv) ? exportTagsCsv : tagsCsv.Trim();
        defaultTargetDisplayId = string.IsNullOrWhiteSpace(targetDisplayId) ? defaultTargetDisplayId : targetDisplayId.Trim();
        targetDisplayIds = supportedTargetDisplayIds != null && supportedTargetDisplayIds.Length > 0
            ? supportedTargetDisplayIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : targetDisplayIds;
        EnsureDefaults();
    }

    public void SetActionMappings(IEnumerable<ActionMappingRule> mappings)
    {
        actionMappings = mappings != null
            ? mappings.Where(rule => rule != null).Select(CloneActionMappingRule).ToList()
            : new List<ActionMappingRule>();
    }

    private void OnEnable()
    {
        EnsureDefaults();
    }

    private void OnValidate()
    {
        EnsureDefaults();
        if (autoRefreshOnValidate)
        {
            AutoConfigureElements();
        }
    }

    private void EnsureDefaults()
    {
        if (string.IsNullOrWhiteSpace(controlSurfaceId))
        {
            controlSurfaceId = "meta_ui_surface";
        }

        if (string.IsNullOrWhiteSpace(controlSurfaceLabel))
        {
            controlSurfaceLabel = "Meta UI Surface";
        }

        if (string.IsNullOrWhiteSpace(controlFamilyId))
        {
            controlFamilyId = "meta_ui_toolkit";
        }

        if (controlThemeId == null)
        {
            controlThemeId = "";
        }

        if (controlThemeLabel == null)
        {
            controlThemeLabel = "";
        }

        if (controlThemeVariantId == null)
        {
            controlThemeVariantId = "";
        }

        if (controlThemeAssetPath == null)
        {
            controlThemeAssetPath = "";
        }

        if (controlThemeAssetGuid == null)
        {
            controlThemeAssetGuid = "";
        }

        if (toolkitCategory == null)
        {
            toolkitCategory = "";
        }

        if (string.IsNullOrWhiteSpace(layoutSource))
        {
            layoutSource = "canvas_export_v1";
        }

        if (string.IsNullOrWhiteSpace(defaultTargetDisplayId))
        {
            defaultTargetDisplayId = "player_main";
        }

        if (targetDisplayIds == null || targetDisplayIds.Length == 0)
        {
            targetDisplayIds = new[] { defaultTargetDisplayId };
        }

        if (string.IsNullOrWhiteSpace(exportDisplayName))
        {
            exportDisplayName = controlSurfaceLabel;
        }

        if (string.IsNullOrWhiteSpace(exportTagsCsv))
        {
            exportTagsCsv = "ghost,meta_ui,toolkit";
        }

        if (surfaceUnitsToMetersMultiplier < 0f)
        {
            surfaceUnitsToMetersMultiplier = 0f;
        }
    }

    private RectTransform ResolveSurfaceRoot()
    {
        if (surfaceRoot != null)
        {
            return surfaceRoot;
        }

        RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
        RectTransform best = null;
        float bestArea = 0f;
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform candidate = rectTransforms[i];
            if (candidate == null)
            {
                continue;
            }

            float area = Mathf.Abs(candidate.rect.width * candidate.rect.height);
            if (area <= bestArea)
            {
                continue;
            }

            best = candidate;
            bestArea = area;
        }

        return best;
    }

    private void AppendButtons(List<ControlElementBinding> output, HashSet<string> usedIds, ref int sequence)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            AppendElement(output, usedIds, ref sequence, button.transform as RectTransform, button.name, "button", "none", false);
        }
    }

    private void AppendToggles(List<ControlElementBinding> output, HashSet<string> usedIds, ref int sequence)
    {
        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle toggle = toggles[i];
            if (toggle == null)
            {
                continue;
            }

            AppendElement(output, usedIds, ref sequence, toggle.transform as RectTransform, toggle.name, "toggle", "bool", false);
        }
    }

    private void AppendSliders(List<ControlElementBinding> output, HashSet<string> usedIds, ref int sequence)
    {
        Slider[] sliders = GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            Slider slider = sliders[i];
            if (slider == null)
            {
                continue;
            }

            AppendElement(output, usedIds, ref sequence, slider.transform as RectTransform, slider.name, "slider", "normalized_float", false);
        }
    }

    private void AppendInputFields(List<ControlElementBinding> output, HashSet<string> usedIds, ref int sequence)
    {
        TMP_InputField[] tmpInputs = GetComponentsInChildren<TMP_InputField>(true);
        for (int i = 0; i < tmpInputs.Length; i++)
        {
            TMP_InputField input = tmpInputs[i];
            if (input == null)
            {
                continue;
            }

            AppendElement(output, usedIds, ref sequence, input.transform as RectTransform, input.name, "text_input", "string", false);
        }

        InputField[] inputs = GetComponentsInChildren<InputField>(true);
        for (int i = 0; i < inputs.Length; i++)
        {
            InputField input = inputs[i];
            if (input == null)
            {
                continue;
            }

            AppendElement(output, usedIds, ref sequence, input.transform as RectTransform, input.name, "text_input", "string", false);
        }
    }

    private void AppendScrollViews(List<ControlElementBinding> output, HashSet<string> usedIds, ref int sequence)
    {
        ScrollRect[] scrollRects = GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scrollRect = scrollRects[i];
            if (scrollRect == null)
            {
                continue;
            }

            AppendElement(output, usedIds, ref sequence, scrollRect.transform as RectTransform, scrollRect.name, "scroll_view", "normalized_vector2", false);
        }
    }

    private void AppendLabels(List<ControlElementBinding> output, HashSet<string> usedIds, ref int sequence)
    {
        TextMeshProUGUI[] tmpTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TextMeshProUGUI text = tmpTexts[i];
            if (text == null || string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            AppendElement(output, usedIds, ref sequence, text.transform as RectTransform, text.name, "label", "none", true);
        }
    }

    private void AppendElement(
        List<ControlElementBinding> output,
        HashSet<string> usedIds,
        ref int sequence,
        RectTransform rectTransform,
        string label,
        string elementKind,
        string valueKind,
        bool readOnly)
    {
        if (surfaceRoot == null || rectTransform == null)
        {
            return;
        }

        string relativePath = BuildRelativePath(surfaceRoot, rectTransform);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            relativePath = label;
        }

        string baseId = SanitizeId(relativePath);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = elementKind + "_" + sequence.ToString();
        }

        string elementId = BuildUniqueElementId(usedIds, baseId);
        string resolvedElementKind = elementKind;
        string resolvedValueKind = valueKind;
        string actionId = ResolveActionId(relativePath, label, elementKind, ref resolvedElementKind, ref resolvedValueKind, elementId);
        usedIds.Add(elementId);
        sequence++;

        output.Add(new ControlElementBinding
        {
            elementId = elementId,
            elementLabel = string.IsNullOrWhiteSpace(label) ? elementId : label.Trim(),
            actionId = actionId,
            elementKind = resolvedElementKind,
            valueKind = resolvedValueKind,
            rectTransform = rectTransform,
            readOnly = readOnly
        });
    }

    private string ResolveActionId(
        string relativePath,
        string label,
        string elementKind,
        ref string resolvedElementKind,
        ref string resolvedValueKind,
        string fallbackActionId)
    {
        if (actionMappings == null || actionMappings.Count <= 0)
        {
            return fallbackActionId;
        }

        for (int i = 0; i < actionMappings.Count; i++)
        {
            ActionMappingRule rule = actionMappings[i];
            if (!MatchesRule(rule, relativePath, label, elementKind))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(rule.elementKindOverride))
            {
                resolvedElementKind = rule.elementKindOverride.Trim();
            }

            if (!string.IsNullOrWhiteSpace(rule.valueKindOverride))
            {
                resolvedValueKind = rule.valueKindOverride.Trim();
            }

            if (!string.IsNullOrWhiteSpace(rule.actionId))
            {
                return rule.actionId.Trim();
            }
        }

        return fallbackActionId;
    }

    private static bool MatchesRule(ActionMappingRule rule, string relativePath, string label, string elementKind)
    {
        if (rule == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.pathContains) &&
            (string.IsNullOrWhiteSpace(relativePath) || relativePath.IndexOf(rule.pathContains, StringComparison.OrdinalIgnoreCase) < 0))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.labelContains) &&
            (string.IsNullOrWhiteSpace(label) || label.IndexOf(rule.labelContains, StringComparison.OrdinalIgnoreCase) < 0))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.elementKind) &&
            !string.Equals(rule.elementKind.Trim(), elementKind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static ActionMappingRule CloneActionMappingRule(ActionMappingRule source)
    {
        if (source == null)
        {
            return null;
        }

        return new ActionMappingRule
        {
            pathContains = source.pathContains ?? "",
            labelContains = source.labelContains ?? "",
            elementKind = source.elementKind ?? "",
            actionId = source.actionId ?? "",
            elementKindOverride = source.elementKindOverride ?? "",
            valueKindOverride = source.valueKindOverride ?? ""
        };
    }

    private static float GuessSurfaceUnitsToMetersMultiplier(RectTransform root)
    {
        if (root == null)
        {
            return 1f;
        }

        Canvas canvas = root.GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            return 1f;
        }

        return 0.001f;
    }

    private static string BuildUniqueElementId(HashSet<string> usedIds, string baseId)
    {
        string candidate = baseId;
        int suffix = 2;
        while (usedIds.Contains(candidate))
        {
            candidate = baseId + "_" + suffix.ToString();
            suffix++;
        }

        return candidate;
    }

    private static string BuildRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null)
        {
            return "";
        }

        if (root == target)
        {
            return target.name;
        }

        List<string> parts = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        if (current != root)
        {
            return target.name;
        }

        parts.Reverse();
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('/');
            }

            builder.Append(parts[i]);
        }

        return builder.ToString();
    }

    private static string SanitizeId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "";
        }

        StringBuilder builder = new StringBuilder(input.Length);
        bool previousUnderscore = false;
        for (int i = 0; i < input.Length; i++)
        {
            char current = char.ToLowerInvariant(input[i]);
            if ((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9'))
            {
                builder.Append(current);
                previousUnderscore = false;
                continue;
            }

            if (previousUnderscore)
            {
                continue;
            }

            builder.Append('_');
            previousUnderscore = true;
        }

        string sanitized = builder.ToString().Trim('_');
        return sanitized;
    }
}
