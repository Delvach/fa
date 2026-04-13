using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class FAUiScroller : MVRScript
{
    private const float ScrollInputThreshold = 0.12f;
    private const float CaptureReleaseHoldSeconds = 0.18f;

    private readonly FAUiCaptureCore captureCore = new FAUiCaptureCore();
    private readonly FAUiInputCore inputCore = new FAUiInputCore();
    private readonly FAUiScrollTargetResolver targetResolver = new FAUiScrollTargetResolver();
    private readonly FAUiScrollerUiStrings strings = FAUiScrollerCatalogStore.Ui;

    private JSONStorableString pluginTitleField;
    private JSONStorableBool enabledField;
    private JSONStorableBool captureNavigationField;
    private JSONStorableBool invertVerticalField;
    private JSONStorableFloat scrollSpeedField;
    private JSONStorableStringChooser stickPreferenceField;
    private JSONStorableString statusField;
    private JSONStorableAction rescanAction;
    private float lastScrollIntentAt = -1000f;
    private string lastTargetDescription = "";

    public override void Init()
    {
        try
        {
            BuildStorables();
            RegisterStorables();
            BuildUi();
            SetStatus(strings.statusIdle);
        }
        catch (System.Exception exc)
        {
            SuperController.LogError("FAUiScroller init failed: " + exc.Message);
        }
    }

    public void Update()
    {
        TickScroller();
    }

    public void OnDisable()
    {
        captureCore.SetCaptureEnabled(false);
        inputCore.ResetAxisLock();
        SetStatus(strings.statusDisabled);
    }

    public void OnDestroy()
    {
        captureCore.SetCaptureEnabled(false);
        inputCore.ResetAxisLock();
    }

    private void BuildStorables()
    {
        pluginTitleField = new JSONStorableString(strings.pluginTitleLabel, strings.pluginTitle);
        pluginTitleField.isStorable = false;
        pluginTitleField.isRestorable = false;
        enabledField = new JSONStorableBool(strings.enabledLabel, true);
        captureNavigationField = new JSONStorableBool(strings.captureNavigationLabel, true);
        invertVerticalField = new JSONStorableBool(strings.invertVerticalLabel, true);
        scrollSpeedField = new JSONStorableFloat(strings.scrollSpeedLabel, 900f, 120f, 3000f, false, true);
        stickPreferenceField = new JSONStorableStringChooser(
            strings.stickLabel,
            new List<string>(new string[] {
                strings.stickChoiceEither,
                strings.stickChoiceRight,
                strings.stickChoiceLeft,
                strings.stickChoiceStrongest
            }),
            strings.defaultStickChoice,
            strings.stickLabel);
        statusField = new JSONStorableString(strings.statusLabel, strings.statusIdle);
        statusField.isStorable = false;
        statusField.isRestorable = false;
        rescanAction = new JSONStorableAction(strings.rescanLabel, delegate
        {
            targetResolver.Resolve(true);
            SetStatus(strings.statusRescanned);
        });
    }

    private void RegisterStorables()
    {
        RegisterBool(enabledField);
        RegisterBool(captureNavigationField);
        RegisterBool(invertVerticalField);
        RegisterFloat(scrollSpeedField);
        RegisterStringChooser(stickPreferenceField);
        RegisterString(statusField);
        RegisterAction(rescanAction);
    }

    private void BuildUi()
    {
        CreateTextField(pluginTitleField, false);
        CreateToggle(enabledField);
        CreateToggle(captureNavigationField);
        CreateToggle(invertVerticalField);
        CreateSlider(scrollSpeedField, true);
        CreatePopup(stickPreferenceField);
        CreateTextField(statusField, false);
        CreateButton(rescanAction.name).button.onClick.AddListener(delegate
        {
            rescanAction.actionCallback();
        });
    }

    private void TickScroller()
    {
        if (enabledField == null || !enabledField.val)
        {
            captureCore.SetCaptureEnabled(false);
            inputCore.ResetAxisLock();
            SetStatus(strings.statusOff);
            return;
        }

        FAUiInputCore.StickPreference preference = ResolveStickPreference();
        FAUiInputCore.StickRead read = inputCore.ReadNavigation(preference);
        float vertical = ShapeScrollInput(read.navigation.y);

        bool forceResolve = Mathf.Abs(vertical) > 0f;
        FAUiScrollTargetResolver.ScrollTarget target = targetResolver.Resolve(forceResolve);
        if (target == null)
        {
            bool holdCapture = (Time.unscaledTime - lastScrollIntentAt) <= CaptureReleaseHoldSeconds;
            captureCore.SetCaptureEnabled(holdCapture && captureNavigationField != null && captureNavigationField.val);
            SetStatus(BuildStatus(read, holdCapture ? (strings.statusHoldingPrefix + lastTargetDescription) : strings.statusTargetMissing));
            return;
        }

        if (Mathf.Abs(vertical) <= 0f)
        {
            captureCore.SetCaptureEnabled(false);
            SetStatus(BuildStatus(read, target.description));
            return;
        }

        lastScrollIntentAt = Time.unscaledTime;
        lastTargetDescription = target.description ?? "";

        if (captureNavigationField != null && captureNavigationField.val)
            captureCore.SetCaptureEnabled(true);

        ApplyVerticalScroll(target, vertical, Time.unscaledDeltaTime);
        SetStatus(BuildStatus(read, target.description));
    }

    private FAUiInputCore.StickPreference ResolveStickPreference()
    {
        string current = stickPreferenceField != null ? stickPreferenceField.val : strings.defaultStickChoice;
        if (string.Equals(current, strings.stickChoiceEither, System.StringComparison.OrdinalIgnoreCase))
            return FAUiInputCore.StickPreference.Either;
        if (string.Equals(current, strings.stickChoiceLeft, System.StringComparison.OrdinalIgnoreCase))
            return FAUiInputCore.StickPreference.Left;
        if (string.Equals(current, strings.stickChoiceStrongest, System.StringComparison.OrdinalIgnoreCase))
            return FAUiInputCore.StickPreference.Strongest;
        return FAUiInputCore.StickPreference.Right;
    }

    private void ApplyVerticalScroll(FAUiScrollTargetResolver.ScrollTarget target, float vertical, float deltaTime)
    {
        if (target == null)
            return;

        float direction = invertVerticalField != null && invertVerticalField.val ? -vertical : vertical;
        float speed = scrollSpeedField != null ? scrollSpeedField.val : 900f;
        float delta = direction * speed * Mathf.Max(deltaTime, 0.0001f);

        if (target.scrollRect != null)
        {
            target.scrollRect.StopMovement();
            RectTransform content = target.scrollRect.content;
            RectTransform viewport = target.scrollRect.viewport != null
                ? target.scrollRect.viewport
                : target.scrollRect.GetComponent<RectTransform>();

            float hiddenHeight = 0f;
            if (content != null && viewport != null)
                hiddenHeight = Mathf.Max(0f, content.rect.height - viewport.rect.height);

            if (hiddenHeight > 0.01f)
            {
                float normalizedDelta = delta / hiddenHeight;
                target.scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                    target.scrollRect.verticalNormalizedPosition + normalizedDelta);
            }
            return;
        }

        if (target.scrollbar != null)
            target.scrollbar.value = Mathf.Clamp01(target.scrollbar.value + delta);
    }

    private static float ShapeScrollInput(float raw)
    {
        float abs = Mathf.Abs(raw);
        if (abs < ScrollInputThreshold)
            return 0f;

        float normalized = Mathf.Clamp01((abs - ScrollInputThreshold) / Mathf.Max(1f - ScrollInputThreshold, 0.0001f));
        float shaped = normalized * normalized;
        return Mathf.Sign(raw) * shaped;
    }

    private string BuildStatus(FAUiInputCore.StickRead read, string targetDescription)
    {
        StringBuilder sb = new StringBuilder(192);
        sb.Append("stick=").Append(string.IsNullOrEmpty(read.slot) ? strings.noneLabel : read.slot);
        sb.Append(" src=").Append(string.IsNullOrEmpty(read.source) ? strings.noneLabel : read.source);
        sb.Append(" lock=").Append(inputCore.CurrentAxisLock.ToString().ToLowerInvariant());
        sb.Append(" y=").Append(read.navigation.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        sb.Append(" capture=").Append(captureCore.IsActive ? "on" : "off");
        if (!string.IsNullOrEmpty(targetDescription))
            sb.Append(" target=").Append(targetDescription);
        return sb.ToString();
    }

    private void SetStatus(string value)
    {
        if (statusField != null)
            statusField.valNoCallback = string.IsNullOrEmpty(value) ? strings.statusIdle : value;
    }
}
