using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameAngel.Runtime.Shared
{
    internal static class FrameAngelPlayerMediaParity
    {
        public const string AspectModeFit = "fit";
        public const string AspectModeCrop = "crop";
        public const string AspectModeFullWidth = "full_width";
        public const string AspectModeStretch = "stretch";

        private static readonly string[] SupportedMediaExtensions =
        {
            ".mp4",
            ".m4v",
            ".mov",
            ".webm",
            ".avi",
            ".mpg",
            ".mpeg",
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".tga",
            ".gif"
        };

        private static readonly string[] SupportedVideoExtensions =
        {
            ".mp4",
            ".m4v",
            ".mov",
            ".webm",
            ".avi",
            ".mpg",
            ".mpeg"
        };

        private static readonly string[] SupportedImageExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp",
            ".tga",
            ".gif"
        };

        public static string NormalizeAspectMode(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
                return fallback ?? string.Empty;

            string normalized = value.Trim();
            if (EqualsIgnoreCase(normalized, "fit")
                || EqualsIgnoreCase(normalized, "fit_black")
                || EqualsIgnoreCase(normalized, "letterbox")
                || EqualsIgnoreCase(normalized, "fullscreen")
                || EqualsIgnoreCase(normalized, "contain")
                || EqualsIgnoreCase(normalized, "bars"))
                return AspectModeFit;

            if (EqualsIgnoreCase(normalized, "full_width")
                || EqualsIgnoreCase(normalized, "fullwidth")
                || EqualsIgnoreCase(normalized, "width_locked")
                || EqualsIgnoreCase(normalized, "widthlock")
                || EqualsIgnoreCase(normalized, "width_locked_fit"))
                return AspectModeFullWidth;

            if (EqualsIgnoreCase(normalized, "stretch")
                || EqualsIgnoreCase(normalized, "cover")
                || EqualsIgnoreCase(normalized, "fill_stretch"))
                return AspectModeStretch;

            if (EqualsIgnoreCase(normalized, "crop"))
                return AspectModeCrop;

            return fallback ?? string.Empty;
        }

        public static bool IsFitBlackAspectMode(string aspectMode)
        {
            return string.Equals(aspectMode, AspectModeFit, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWidthLockedAspectMode(string aspectMode)
        {
            return string.Equals(aspectMode, AspectModeFullWidth, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCropFillAspectMode(string aspectMode)
        {
            return string.IsNullOrEmpty(aspectMode)
                || string.Equals(aspectMode, AspectModeCrop, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldUseOverlayPresentation(bool explicitOverlayBinding, string aspectMode)
        {
            return explicitOverlayBinding
                || IsFitBlackAspectMode(aspectMode)
                || IsWidthLockedAspectMode(aspectMode);
        }

        public static bool ShouldApplyBackdropToTarget(string aspectMode, bool usingDisconnectSurfaceTarget, bool isScreenCoreSurface)
        {
            if (isScreenCoreSurface)
                return false;

            return (IsFitBlackAspectMode(aspectMode) || IsWidthLockedAspectMode(aspectMode))
                && usingDisconnectSurfaceTarget;
        }

        public static bool CanSeekWithoutKnownDuration(float normalizedTarget)
        {
            return normalizedTarget <= 0.0001f;
        }

        public static string DescribeAspectMode(string aspectMode)
        {
            string normalized = NormalizeAspectMode(aspectMode, string.Empty);
            if (IsFitBlackAspectMode(normalized))
                return "fit/contain";

            if (IsWidthLockedAspectMode(normalized))
                return "full_width/width_locked";

            if (IsCropFillAspectMode(normalized))
                return "crop/fill";

            if (string.Equals(normalized, AspectModeStretch, StringComparison.OrdinalIgnoreCase))
                return "stretch/fill";

            return string.IsNullOrEmpty(normalized) ? "unknown" : normalized;
        }

        public static bool DoFitAndFullWidthConverge(float contentAspect, float surfaceAspect)
        {
            return contentAspect > 0.001f
                && surfaceAspect > 0.001f
                && contentAspect >= surfaceAspect;
        }

        public static string DescribeAspectOutcome(string aspectMode, float contentAspect, float surfaceAspect)
        {
            string normalized = NormalizeAspectMode(aspectMode, string.Empty);
            if (IsFitBlackAspectMode(normalized))
            {
                return DoFitAndFullWidthConverge(contentAspect, surfaceAspect)
                    ? "fit_matches_full_width"
                    : "fit_letterbox";
            }

            if (IsWidthLockedAspectMode(normalized))
            {
                return DoFitAndFullWidthConverge(contentAspect, surfaceAspect)
                    ? "full_width_matches_fit"
                    : "full_width_height_reduced";
            }

            if (IsCropFillAspectMode(normalized))
                return "crop_fill";

            if (string.Equals(normalized, AspectModeStretch, StringComparison.OrdinalIgnoreCase))
                return "stretch_fill";

            return "unknown";
        }

        public static void ComputePresentedSize(
            float surfaceWidth,
            float surfaceHeight,
            float contentAspect,
            string aspectMode,
            out float presentedWidth,
            out float presentedHeight)
        {
            presentedWidth = Mathf.Max(0.001f, surfaceWidth);
            presentedHeight = Mathf.Max(0.001f, surfaceHeight);

            if (contentAspect <= 0.001f)
                return;

            float surfaceAspect = presentedWidth / Mathf.Max(0.001f, presentedHeight);
            if (surfaceAspect <= 0.001f)
                return;

            if (IsWidthLockedAspectMode(aspectMode))
            {
                presentedHeight = Mathf.Max(0.001f, presentedWidth / contentAspect);
                return;
            }

            if (contentAspect >= surfaceAspect)
            {
                presentedHeight = Mathf.Max(0.001f, presentedHeight * (surfaceAspect / contentAspect));
            }
            else
            {
                presentedWidth = Mathf.Max(0.001f, presentedWidth * (contentAspect / surfaceAspect));
            }
        }

        public static void ComputeCropScaleOffset(
            float contentAspect,
            float surfaceAspect,
            out Vector2 textureScale,
            out Vector2 textureOffset)
        {
            textureScale = Vector2.one;
            textureOffset = Vector2.zero;

            if (contentAspect <= 0.001f || surfaceAspect <= 0.001f)
                return;

            if (contentAspect >= surfaceAspect)
            {
                float scaleX = surfaceAspect / contentAspect;
                textureScale = new Vector2(scaleX, 1f);
                textureOffset = new Vector2((1f - scaleX) * 0.5f, 0f);
            }
            else
            {
                float scaleY = contentAspect / surfaceAspect;
                textureScale = new Vector2(1f, scaleY);
                textureOffset = new Vector2(0f, (1f - scaleY) * 0.5f);
            }
        }

        public static Quaternion ResolveOverlayLocalRotation(bool isAuthoredFrontScreen)
        {
            // Direct-CUA authored front screens are already operator-facing. The legacy
            // Y-180 fit correction belongs on older fallback seams, not on the shared
            // screen-surface overlay path used by the current player runtime.
            return Quaternion.identity;
        }

        public static bool IsSupportedMediaPath(string path)
        {
            string extension = TryGetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return false;

            for (int i = 0; i < SupportedMediaExtensions.Length; i++)
            {
                if (string.Equals(extension, SupportedMediaExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsSupportedVideoPath(string path)
        {
            string extension = TryGetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return false;

            for (int i = 0; i < SupportedVideoExtensions.Length; i++)
            {
                if (string.Equals(extension, SupportedVideoExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool IsSupportedImagePath(string path)
        {
            string extension = TryGetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return false;

            for (int i = 0; i < SupportedImageExtensions.Length; i++)
            {
                if (string.Equals(extension, SupportedImageExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static List<string> BuildSiblingPlaylist(string selectedMediaPath, IEnumerable<string> siblingEntries)
        {
            List<string> results = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string normalizedSelected = string.IsNullOrEmpty(selectedMediaPath) ? string.Empty : selectedMediaPath.Trim();
            bool hasSelected = normalizedSelected.Length > 0 && IsSupportedMediaPath(normalizedSelected);

            if (hasSelected && seen.Add(normalizedSelected))
                results.Add(normalizedSelected);

            List<string> discoveredEntries = new List<string>();

            if (siblingEntries != null)
            {
                foreach (string entry in siblingEntries)
                {
                    if (string.IsNullOrEmpty(entry) || entry.Trim().Length == 0 || !IsSupportedMediaPath(entry))
                        continue;

                    string normalized = entry.Trim();
                    if (!seen.Add(normalized))
                        continue;

                    discoveredEntries.Add(normalized);
                }
            }

            discoveredEntries.Sort(StringComparer.OrdinalIgnoreCase);
            results.AddRange(discoveredEntries);
            return results;
        }

        private static string TryGetExtension(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Trim().Length == 0)
                return string.Empty;

            try
            {
                string normalized = path.Trim();
                int lastSlash = normalized.LastIndexOfAny(new[] { '\\', '/' });
                int lastDot = normalized.LastIndexOf('.');
                if (lastDot < 0)
                    return string.Empty;

                if (lastSlash >= 0 && lastDot <= lastSlash)
                    return string.Empty;

                return normalized.Substring(lastDot);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool EqualsIgnoreCase(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
