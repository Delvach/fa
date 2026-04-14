using System;

internal static class BuildRuntimeInfo
{
    internal const string BuildChannel = "frameangel";
#if FRAMEANGEL_CUA_PLAYER
    internal static readonly bool IsDistributionBuild = true;
#else
    internal static readonly bool IsDistributionBuild = false;
#endif

    internal const string IntegrityMarker = "FRAMEANGEL:20260408:R1";
    // Reflection is intentionally avoided because VaM guardrails block it.
    // Stamped by scripts/Build-Plugins.ps1 from VERSION.
    internal static readonly string BuildVersion = "0.6.26";

    internal static bool TryValidateIntegrity(out string code)
    {
        if (string.IsNullOrEmpty(IntegrityMarker))
        {
            code = RuntimeMessageCatalog.CodeIntegrityMissing;
            return false;
        }

        if (!IntegrityMarker.StartsWith("FRAMEANGEL:", StringComparison.Ordinal))
        {
            code = RuntimeMessageCatalog.CodeIntegrityMalformed;
            return false;
        }

        code = RuntimeMessageCatalog.CodeIntegrityOk;
        return true;
    }

    internal static string FormatDiagnostic(string code, string devText)
    {
#if VAM_DEV_TEXT
        if (!string.IsNullOrEmpty(devText))
            return devText;
#endif
        return string.IsNullOrEmpty(code) ? "M000" : code;
    }
}

internal enum FrameAngelLogLevel
{
    None = 0,
    Quiet = 1,
    Debug = 2
}

internal static class FrameAngelLog
{
#if VAM_LOG_LEVEL_DEBUG
    internal const FrameAngelLogLevel DefaultLevel = FrameAngelLogLevel.Debug;
    internal const string DefaultLevelName = "debug";
#elif VAM_LOG_LEVEL_QUIET
    internal const FrameAngelLogLevel DefaultLevel = FrameAngelLogLevel.Quiet;
    internal const string DefaultLevelName = "quiet";
#else
    internal const FrameAngelLogLevel DefaultLevel = FrameAngelLogLevel.None;
    internal const string DefaultLevelName = "none";
#endif

    internal static bool IsQuietEnabled
    {
        get { return DefaultLevel >= FrameAngelLogLevel.Quiet; }
    }

    internal static bool IsDebugEnabled
    {
        get { return DefaultLevel >= FrameAngelLogLevel.Debug; }
    }

    internal static void Quiet(string message)
    {
        if (!IsQuietEnabled || string.IsNullOrEmpty(message))
            return;

        try
        {
            SuperController.LogMessage(message);
        }
        catch { }
    }

    internal static void Debug(string message)
    {
        if (!IsDebugEnabled || string.IsNullOrEmpty(message))
            return;

        try
        {
            SuperController.LogMessage(message);
        }
        catch { }
    }

    internal static void Error(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

#if FRAMEANGEL_CUA_PLAYER
        return;
#else
        try
        {
            SuperController.LogError(message);
        }
        catch
        {
            try
            {
                SuperController.LogMessage(message);
            }
            catch { }
        }
#endif
    }
}
