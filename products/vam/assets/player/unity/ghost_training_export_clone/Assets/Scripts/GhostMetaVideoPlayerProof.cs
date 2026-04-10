using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GhostMetaVideoPlayerProof : MonoBehaviour
{
    [Header("Meta UI Set Hook Points")]
    public RoundedBoxVideoController legacyController;
    public Slider timeSlider;
    public Toggle playPauseToggle;
    public Image playPauseImage;
    public Sprite playIcon;
    public Sprite pauseIcon;
    public TextMeshProUGUI leftLabel;
    public TextMeshProUGUI rightLabel;
    public RectTransform demoVideoContent;
    public Image backgroundImage;

    [Header("Video Source")]
    public VideoClip initialVideoClip;
    public string initialVideoUrl = "";
    public bool playOnStart = true;
    public bool loop = true;

    [Header("Surface")]
    public int renderWidth = 1280;
    public int renderHeight = 720;
    public Color fallbackBackground = Color.black;

    private VideoPlayer _videoPlayer;
    private RawImage _videoSurface;
    private RenderTexture _renderTexture;
    private bool _prepared;
    private bool _suppressSliderEvents;

    private void Awake()
    {
        AutoWireFromLegacy();
        BuildVideoSurface();
        BuildVideoPlayer();
        HookUi();
        HideLegacyDemoContent();
    }

    private void Start()
    {
        PrepareVideo();
    }

    private void OnDestroy()
    {
        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            _videoPlayer.loopPointReached -= HandleLoopPointReached;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }
    }

    private void Update()
    {
        if (_videoPlayer == null)
        {
            return;
        }

        UpdateTimelineUi();
        UpdatePlayPauseIcon();
    }

    public void TogglePlayPause(bool isOn)
    {
        if (_videoPlayer == null || !_prepared)
        {
            UpdatePlayPauseIcon();
            return;
        }

        if (isOn)
        {
            _videoPlayer.Play();
        }
        else
        {
            _videoPlayer.Pause();
        }

        UpdatePlayPauseIcon();
    }

    public void ScrubNormalized(float normalizedValue)
    {
        if (_suppressSliderEvents)
        {
            return;
        }

        if (_videoPlayer == null || !_prepared)
        {
            return;
        }

        double length = GetVideoLengthSeconds();
        if (length <= 0.01d)
        {
            return;
        }

        _videoPlayer.time = Math.Max(0d, Math.Min(length, normalizedValue * length));
        UpdateTimelineUi();
    }

    private void AutoWireFromLegacy()
    {
        if (legacyController == null)
        {
            legacyController = GetComponent<RoundedBoxVideoController>();
        }

        if (legacyController != null)
        {
            if (timeSlider == null)
            {
                timeSlider = legacyController.timeSlider;
            }

            if (playPauseImage == null)
            {
                playPauseImage = legacyController.playPauseImg;
            }

            if (playIcon == null)
            {
                playIcon = legacyController.playIcon;
            }

            if (pauseIcon == null)
            {
                pauseIcon = legacyController.pauseIcon;
            }

            if (leftLabel == null)
            {
                leftLabel = legacyController.leftLabel;
            }

            if (rightLabel == null)
            {
                rightLabel = legacyController.rightLabel;
            }

            if (backgroundImage == null)
            {
                backgroundImage = legacyController.backgroundImage;
            }
        }

        if (demoVideoContent == null)
        {
            Transform content = FindChildRecursive(transform, "DemoVideoContent");
            if (content != null)
            {
                demoVideoContent = content as RectTransform;
            }
        }

        if (playPauseToggle == null && playPauseImage != null)
        {
            playPauseToggle = playPauseImage.GetComponentInParent<Toggle>(true);
        }
    }

    private void BuildVideoSurface()
    {
        if (demoVideoContent == null)
        {
            return;
        }

        Transform existing = demoVideoContent.Find("GhostVideoSurface");
        if (existing != null)
        {
            _videoSurface = existing.GetComponent<RawImage>();
        }

        if (_videoSurface == null)
        {
            GameObject surfaceObject = new GameObject("GhostVideoSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            surfaceObject.transform.SetParent(demoVideoContent, false);
            RectTransform rectTransform = surfaceObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _videoSurface = surfaceObject.GetComponent<RawImage>();
        }

        _videoSurface.color = Color.white;
        _videoSurface.raycastTarget = false;
    }

    private void BuildVideoPlayer()
    {
        if (_videoPlayer == null)
        {
            _videoPlayer = GetComponent<VideoPlayer>();
        }

        if (_videoPlayer == null)
        {
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(renderWidth, renderHeight, 0, RenderTextureFormat.ARGB32);
            _renderTexture.name = "GhostMetaVideoPlayerProofRT";
            _renderTexture.Create();
        }

        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = loop;
        _videoPlayer.waitForFirstFrame = true;
        _videoPlayer.skipOnDrop = true;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _renderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _videoPlayer.prepareCompleted -= HandlePrepareCompleted;
        _videoPlayer.prepareCompleted += HandlePrepareCompleted;
        _videoPlayer.loopPointReached -= HandleLoopPointReached;
        _videoPlayer.loopPointReached += HandleLoopPointReached;

        if (initialVideoClip != null)
        {
            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip = initialVideoClip;
        }
        else if (!string.IsNullOrWhiteSpace(initialVideoUrl))
        {
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = initialVideoUrl;
        }

        if (_videoSurface != null)
        {
            _videoSurface.texture = _renderTexture;
        }
    }

    private void HookUi()
    {
        if (timeSlider != null)
        {
            timeSlider.onValueChanged.RemoveListener(ScrubNormalized);
            timeSlider.onValueChanged.AddListener(ScrubNormalized);
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 1f;
        }

        if (playPauseToggle != null)
        {
            playPauseToggle.onValueChanged.RemoveListener(TogglePlayPause);
            playPauseToggle.onValueChanged.AddListener(TogglePlayPause);
        }
    }

    private void HideLegacyDemoContent()
    {
        if (demoVideoContent == null)
        {
            return;
        }

        for (int i = 0; i < demoVideoContent.childCount; i++)
        {
            Transform child = demoVideoContent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (_videoSurface != null && child == _videoSurface.transform)
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }

        if (backgroundImage != null)
        {
            backgroundImage.enabled = false;
        }
    }

    private void PrepareVideo()
    {
        _prepared = false;

        if (_videoPlayer == null)
        {
            return;
        }

        bool hasClip = _videoPlayer.source == VideoSource.VideoClip && _videoPlayer.clip != null;
        bool hasUrl = _videoPlayer.source == VideoSource.Url && !string.IsNullOrWhiteSpace(_videoPlayer.url);
        if (!hasClip && !hasUrl)
        {
            return;
        }

        _videoPlayer.Prepare();
        UpdateTimelineUi();
        UpdatePlayPauseIcon();
    }

    private void HandlePrepareCompleted(VideoPlayer source)
    {
        _prepared = true;
        UpdateTimelineUi();

        if (playPauseToggle != null)
        {
            playPauseToggle.SetIsOnWithoutNotify(playOnStart);
        }

        if (playOnStart)
        {
            source.Play();
        }
        else
        {
            source.Pause();
        }

        UpdatePlayPauseIcon();
    }

    private void HandleLoopPointReached(VideoPlayer source)
    {
        if (loop)
        {
            return;
        }

        if (playPauseToggle != null)
        {
            playPauseToggle.SetIsOnWithoutNotify(false);
        }

        UpdatePlayPauseIcon();
    }

    private void UpdateTimelineUi()
    {
        if (_videoPlayer == null)
        {
            return;
        }

        double length = GetVideoLengthSeconds();
        double currentTime = _prepared ? Math.Max(0d, _videoPlayer.time) : 0d;
        double remainingTime = Math.Max(0d, length - currentTime);

        if (timeSlider != null && length > 0.01d)
        {
            _suppressSliderEvents = true;
            timeSlider.SetValueWithoutNotify((float)(currentTime / length));
            _suppressSliderEvents = false;
        }

        if (leftLabel != null)
        {
            leftLabel.SetText(FormatTime(currentTime));
        }

        if (rightLabel != null)
        {
            rightLabel.SetText(FormatTime(remainingTime));
        }
    }

    private void UpdatePlayPauseIcon()
    {
        if (playPauseImage == null)
        {
            return;
        }

        bool isPlaying = _videoPlayer != null && _videoPlayer.isPlaying;
        if (isPlaying && pauseIcon != null)
        {
            playPauseImage.sprite = pauseIcon;
        }
        else if (!isPlaying && playIcon != null)
        {
            playPauseImage.sprite = playIcon;
        }
    }

    private double GetVideoLengthSeconds()
    {
        if (_videoPlayer == null)
        {
            return 0d;
        }

        if (_videoPlayer.length > 0.01d)
        {
            return _videoPlayer.length;
        }

        if (_videoPlayer.frameCount > 0 && _videoPlayer.frameRate > 0.01f)
        {
            return _videoPlayer.frameCount / _videoPlayer.frameRate;
        }

        return 0d;
    }

    private string FormatTime(double seconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        return string.Format("{0}:{1:00}", (int)span.TotalMinutes, span.Seconds);
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
