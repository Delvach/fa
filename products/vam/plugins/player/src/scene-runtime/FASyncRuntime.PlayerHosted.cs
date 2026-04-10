using System;
using System.Collections;
using System.Collections.Generic;
using FrameAngel.Runtime.Shared;
using UnityEngine;

public partial class FASyncRuntime : MVRScript
{
    private const string HostedPlayerBindingMode = "hosted_scene_surface";
    private const string HostedPlayerScreenContractVersion = "hosted_player_surface_v1";
    private const string HostedPlayerScreenCoreContractVersion = "player_screen_core_surface_v1";
    private const string HostedPlayerInstanceIdPrefix = "hosted_player_host:";
    private const string HostedPlayerSlotId = "screen_surface";
    private const string HostedPlayerDisplayId = "main";
    private const string HostedPlayerScreenSurfaceNodeId = "screen_surface";
    private const string HostedPlayerDisconnectSurfaceNodeId = "disconnect_surface";
    private const string HostedPlayerScreenGlassNodeId = "screen_glass";
    private const string HostedPlayerScreenApertureNodeId = "screen_aperture";
    private const string HostedPlayerControlSurfaceNodeId = "control_surface";
    private const string HostedPlayerControlColliderNodeId = "control_collider";
    private const string HostedPlayerScreenBodyNodeId = "screen_body";
    private const string HostedPlayerBottomAnchorNodeId = "bottom_anchor";
    private const string HostedPlayerControlsAnchorNodeId = "controls_anchor";
    private const string HostedPlayerAuthoredPackageFallbackResourceId = "fa_cua_player_host_v1";
    private const string HostedPlayerAuthoredPackageRootNodeId = "fa_cua_player_host";
    private const string HostedPlayerScreenCoreRootNodeId = "fa_player_screen_core";
    private const float HostedPlayerAutoStartRetrySeconds = 2.0f;
    private const float HostedPlayerAutoStartRetryIntervalSeconds = 0.10f;
    private static readonly Vector3 HostedBootstrapScreenLocalPosition = new Vector3(0f, 0.32f, 0f);
    // Match the established rect Ghost screen proportions instead of the earlier oversized placeholder.
    private static readonly Vector3 HostedBootstrapScreenLocalScale = new Vector3(1.24f, 0.76f, 1f);
    private static readonly Vector3 HostedBootstrapControlLocalPosition = new Vector3(0f, -0.48f, 0f);
    private static readonly Vector3 HostedBootstrapControlLocalScale = new Vector3(1.25f, 0.22f, 1f);
    private Coroutine hostedPlayerAutoStartCoroutine;
    private bool hostedPlayerAutoStartPending = false;

    private sealed class HostedPlayerSurfaceContract
    {
        public Atom hostAtom;
        public GameObject screenSurfaceObject;
        public GameObject disconnectSurfaceObject;
        public GameObject screenBodyObject;
        public GameObject screenGlassObject;
        public GameObject screenApertureObject;
        public GameObject controlSurfaceObject;
        public GameObject controlColliderObject;
    }

    private string ResolveHostedPlayerScreenContractVersion(HostedPlayerSurfaceContract contract)
    {
        if (contract == null)
            return HostedPlayerScreenContractVersion;

        // The current rect player host is a composed screen-core shell that also carries an
        // authored control_surface. Presence of controls alone no longer means "older hosted
        // Meta panel"; the stable screen-core signal is the authored screen/disconnect pair.
        if (contract.screenSurfaceObject != null
            && contract.disconnectSurfaceObject != null)
        {
            return HostedPlayerScreenCoreContractVersion;
        }

        return HostedPlayerScreenContractVersion;
    }

    private bool TryResolveHostedPlayerSurfaceContract(out HostedPlayerSurfaceContract contract, out string errorMessage)
    {
        return TryResolveHostedPlayerSurfaceContract("", out contract, out errorMessage);
    }

    private bool ShouldAutoStartHostedPlayerOnContainingAtom()
    {
        Atom hostAtom;
        if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
            return false;

#if FRAMEANGEL_CUA_PLAYER
        // Direct CUA should bind to the versioned screen-core asset that is already on the
        // host atom. If that authored surface is not present yet, wait for it instead of
        // importing the fallback host package and stacking a second screen family onto the CUA.
        return HasHostedPlayerAuthoredScreenContract(hostAtom);
#else
        return ShouldBootstrapHostedPlayerScaffold(hostAtom);
#endif
    }

    private void QueueHostedPlayerAutoStart()
    {
        hostedPlayerAutoStartPending = true;
        if (hostedPlayerAutoStartCoroutine != null)
            StopCoroutine(hostedPlayerAutoStartCoroutine);

        hostedPlayerAutoStartCoroutine = StartCoroutine(RunHostedPlayerAutoStartCoroutine());
    }

    private IEnumerator RunHostedPlayerAutoStartCoroutine()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(HostedPlayerAutoStartRetryIntervalSeconds);

        float deadline = Time.unscaledTime + HostedPlayerAutoStartRetrySeconds;
        while (hostedPlayerAutoStartPending)
        {
            if (!ShouldAutoStartHostedPlayerOnContainingAtom())
            {
                if (Time.unscaledTime >= deadline)
                    break;

                yield return new WaitForSecondsRealtime(HostedPlayerAutoStartRetryIntervalSeconds);
                continue;
            }

            Atom hostAtom;
            if (!TryResolveHostedPlayerAtom(out hostAtom) || hostAtom == null)
            {
                if (Time.unscaledTime >= deadline)
                    break;

                yield return new WaitForSecondsRealtime(HostedPlayerAutoStartRetryIntervalSeconds);
                continue;
            }

            string errorMessage;
            if (!TryStartHostedPlayerAutoDemo(hostAtom, out errorMessage))
            {
                if (IsHostedPlayerPendingSurfaceContractError(errorMessage)
                    && Time.unscaledTime < deadline)
                {
                    yield return new WaitForSecondsRealtime(HostedPlayerAutoStartRetryIntervalSeconds);
                    continue;
                }

                hostedPlayerAutoStartPending = false;
                hostedPlayerAutoStartCoroutine = null;
                SetLastError(errorMessage);
                SetLastReceipt(BuildBrokerResult(false, "hosted player autostart failed", "{}"));
                yield break;
            }

            string atomUid = hostAtom.uid ?? "";
            string payload = BuildSelectedPlayerStateJson("{\"hostAtomUid\":\"" + EscapeJsonString(atomUid) + "\"}");
            hostedPlayerAutoStartPending = false;
            hostedPlayerAutoStartCoroutine = null;
            SetLastError("");
            SetLastReceipt(BuildBrokerResult(true, "hosted player ready", payload));
            yield break;
        }

        hostedPlayerAutoStartPending = false;
        hostedPlayerAutoStartCoroutine = null;
    }

    private bool TryResolveHostedPlayerSurfaceContract(string hostAtomUid, out HostedPlayerSurfaceContract contract, out string errorMessage)
    {
        contract = null;
        errorMessage = "";

        Atom hostAtom;
        if (!TryResolveHostedPlayerAtom(hostAtomUid, out hostAtom) || hostAtom == null)
        {
            errorMessage = "hosted player needs an attached non-Session atom";
            return false;
        }

        Transform hostRoot = ResolveHostedPlayerHostRootTransform(hostAtom);
        if (hostRoot == null)
        {
            errorMessage = "hosted player atom has no transform";
            return false;
        }

        GameObject screenSurfaceObject;
        GameObject disconnectSurfaceObject;
        GameObject screenBodyObject;
        GameObject controlSurfaceObject;
#if FRAMEANGEL_CUA_PLAYER
        screenSurfaceObject = FindHostedPlayerAttachedCuaNode(hostAtom, HostedPlayerScreenSurfaceNodeId);
        disconnectSurfaceObject = FindHostedPlayerAttachedCuaNode(hostAtom, HostedPlayerDisconnectSurfaceNodeId);
        screenBodyObject = FindHostedPlayerAttachedCuaNode(hostAtom, HostedPlayerScreenBodyNodeId);
        controlSurfaceObject = FindHostedPlayerAttachedCuaNode(hostAtom, HostedPlayerControlSurfaceNodeId);
        if (screenSurfaceObject != null)
            TryPruneHostedPlayerFallbackPackageRoot(hostAtom);
#else
        screenSurfaceObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerScreenSurfaceNodeId);
        disconnectSurfaceObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerDisconnectSurfaceNodeId);
        screenBodyObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerScreenBodyNodeId);
        controlSurfaceObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerControlSurfaceNodeId);
#endif
        if (screenSurfaceObject == null)
        {
#if FRAMEANGEL_CUA_PLAYER
            errorMessage = "hosted player screen_surface not found on attached CUA host";
            return false;
#else
            if (!TryEnsureHostedPlayerScaffold(hostAtom, out errorMessage))
                return false;

            screenSurfaceObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerScreenSurfaceNodeId);
            if (screenSurfaceObject == null)
            {
                errorMessage = "hosted player screen_surface node not found";
                return false;
            }
#endif
        }

        contract = new HostedPlayerSurfaceContract();
        contract.hostAtom = hostAtom;
        contract.screenSurfaceObject = screenSurfaceObject;
        contract.disconnectSurfaceObject = disconnectSurfaceObject;
        contract.screenBodyObject = screenBodyObject;
        contract.screenGlassObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerScreenGlassNodeId);
        contract.screenApertureObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerScreenApertureNodeId);
        contract.controlSurfaceObject = controlSurfaceObject;
        contract.controlColliderObject =
#if FRAMEANGEL_CUA_PLAYER
            FindHostedPlayerAttachedCuaNode(hostAtom, HostedPlayerControlColliderNodeId)
            ?? controlSurfaceObject;
#else
            FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerControlColliderNodeId)
            ?? controlSurfaceObject;
#endif
        return true;
    }

    private bool TryEnsureHostedPlayerScaffold(Atom hostAtom, out string errorMessage)
    {
        errorMessage = "";
        if (hostAtom == null)
        {
            errorMessage = "hosted player host atom missing";
            return false;
        }

#if FRAMEANGEL_CUA_PLAYER
        errorMessage = "hosted player requires authored screen_surface on the attached host object";
        return false;
#else
        if (!ShouldBootstrapHostedPlayerScaffold(hostAtom))
        {
            errorMessage = "hosted player screen_surface node not found";
            return false;
        }

        Transform hostRoot = ResolveHostedPlayerHostRootTransform(hostAtom);
        if (hostRoot == null)
        {
            errorMessage = "hosted player atom has no transform";
            return false;
        }

        try
        {
            GameObject screenSurfaceObject = FindHostedPlayerNodeObject(hostRoot, HostedPlayerScreenSurfaceNodeId);
            if (screenSurfaceObject == null)
                screenSurfaceObject = CreateHostedBootstrapQuad(hostRoot, HostedPlayerScreenSurfaceNodeId, HostedBootstrapScreenLocalPosition, HostedBootstrapScreenLocalScale, false);

            GameObject controlSurfaceObject = FindHostedPlayerNodeObject(hostRoot, HostedPlayerControlSurfaceNodeId);
            if (controlSurfaceObject == null)
                controlSurfaceObject = CreateHostedBootstrapQuad(hostRoot, HostedPlayerControlSurfaceNodeId, HostedBootstrapControlLocalPosition, HostedBootstrapControlLocalScale, true);

            if (controlSurfaceObject != null)
            {
                GameObject colliderObject = FindHostedPlayerNodeObject(hostRoot, HostedPlayerControlColliderNodeId);
                if (colliderObject == null)
                {
                    controlSurfaceObject.name = HostedPlayerControlSurfaceNodeId;
                    GameObject colliderRoot = new GameObject(HostedPlayerControlColliderNodeId);
                    colliderRoot.transform.SetParent(controlSurfaceObject.transform, false);
                    colliderRoot.transform.localPosition = Vector3.zero;
                    colliderRoot.transform.localRotation = Quaternion.identity;
                    colliderRoot.transform.localScale = Vector3.one;
                    BoxCollider collider = colliderRoot.AddComponent<BoxCollider>();
                    collider.size = new Vector3(1f, 1f, 0.02f);
                }
            }

            return screenSurfaceObject != null;
        }
        catch (Exception ex)
        {
            errorMessage = "hosted player scaffold bootstrap failed: " + ex.Message;
            return false;
        }
#endif
    }

    private bool ShouldBootstrapHostedPlayerScaffold(Atom hostAtom)
    {
        if (hostAtom == null)
            return false;

        string type = hostAtom.type ?? "";
        return string.Equals(type, "Empty", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "CustomUnityAsset", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasHostedPlayerAuthoredScreenContract(Atom hostAtom)
    {
#if FRAMEANGEL_CUA_PLAYER
        return FindHostedPlayerAttachedCuaNode(hostAtom, HostedPlayerScreenSurfaceNodeId) != null;
#else
        return FindHostedPlayerNodeObjectOnHostAtom(hostAtom, HostedPlayerScreenSurfaceNodeId) != null;
#endif
    }

    private bool IsHostedPlayerPendingSurfaceContractError(string errorMessage)
    {
        string normalized = errorMessage ?? "";
        return string.Equals(normalized, "hosted player screen_surface not found on attached CUA host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "hosted player control_surface not found on attached CUA host", StringComparison.OrdinalIgnoreCase);
    }

    private GameObject FindHostedPlayerAttachedCuaNode(Atom hostAtom, string nodeId)
    {
        if (hostAtom == null || string.IsNullOrEmpty(nodeId))
            return null;

        GameObject nodeObject = FindHostedPlayerDirectCuaScreenCoreNode(hostAtom, nodeId);
        if (nodeObject != null)
            return nodeObject;

        nodeObject = FindHostedPlayerNodeObjectOnHostAtom(hostAtom, nodeId);
        if (nodeObject == null)
            return null;

        if (IsHostedPlayerNodeUnderAncestor(nodeObject.transform, HostedPlayerAuthoredPackageFallbackResourceId)
            || IsHostedPlayerNodeUnderAncestor(nodeObject.transform, HostedPlayerAuthoredPackageRootNodeId))
        {
            return null;
        }

        return nodeObject;
    }

    private GameObject FindHostedPlayerDirectCuaScreenCoreNode(Atom hostAtom, string nodeId)
    {
        if (hostAtom == null || string.IsNullOrEmpty(nodeId))
            return null;

        GameObject bestCandidate = null;
        int bestScore = int.MinValue;
        Transform[] roots =
        {
            ResolveHostedPlayerHostRootTransform(hostAtom),
            hostAtom.transform,
            hostAtom.mainController != null ? hostAtom.mainController.transform : null,
            hostAtom.mainController != null && hostAtom.mainController.transform != null
                ? hostAtom.mainController.transform.parent
                : null
        };

        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null)
                continue;

            GameObject screenCoreRoot = FindHostedPlayerNodeObject(root, HostedPlayerScreenCoreRootNodeId);
            if (screenCoreRoot == null)
            {
                int candidateScore;
                GameObject candidate = FindBestHostedPlayerDirectCuaNode(root, nodeId, out candidateScore);
                if (candidate != null && candidateScore > bestScore)
                {
                    bestCandidate = candidate;
                    bestScore = candidateScore;
                }

                continue;
            }

            GameObject nodeObject = FindHostedPlayerNodeObject(screenCoreRoot.transform, nodeId);
            if (nodeObject != null)
                return nodeObject;
        }

        return bestCandidate;
    }

    private GameObject FindBestHostedPlayerDirectCuaNode(Transform root, string nodeId, out int bestScore)
    {
        bestScore = int.MinValue;
        if (root == null || string.IsNullOrEmpty(nodeId))
            return null;

        GameObject bestCandidate = null;
        Queue<Transform> pending = new Queue<Transform>();
        pending.Enqueue(root);
        while (pending.Count > 0)
        {
            Transform current = pending.Dequeue();
            if (current == null)
                continue;

            if (string.Equals(current.name ?? "", nodeId, StringComparison.OrdinalIgnoreCase))
            {
                int candidateScore = ScoreHostedPlayerDirectCuaNodeCandidate(current);
                if (candidateScore > bestScore)
                {
                    bestScore = candidateScore;
                    bestCandidate = current.gameObject;
                }
            }

            for (int i = 0; i < current.childCount; i++)
                pending.Enqueue(current.GetChild(i));
        }

        return bestCandidate;
    }

    private int ScoreHostedPlayerDirectCuaNodeCandidate(Transform nodeTransform)
    {
        if (nodeTransform == null)
            return int.MinValue;

        int score = 0;
        Transform parent = nodeTransform.parent;
        if (parent != null)
        {
            if (string.Equals(parent.name ?? "", HostedPlayerScreenCoreRootNodeId, StringComparison.OrdinalIgnoreCase))
                score += 8;

            if (HasHostedPlayerChildNode(parent, HostedPlayerScreenBodyNodeId))
                score += 4;

            if (HasHostedPlayerChildNode(parent, HostedPlayerBottomAnchorNodeId))
                score += 2;

            if (HasHostedPlayerChildNode(parent, HostedPlayerControlsAnchorNodeId))
                score += 2;

            if (HasHostedPlayerChildNode(parent, HostedPlayerScreenSurfaceNodeId))
                score += 1;

            if (HasHostedPlayerChildNode(parent, HostedPlayerDisconnectSurfaceNodeId))
                score += 1;

            if (HasHostedPlayerChildNode(parent, HostedPlayerControlSurfaceNodeId))
                score -= 6;

            if (HasHostedPlayerChildNode(parent, HostedPlayerControlColliderNodeId))
                score -= 2;
        }

        if (IsHostedPlayerNodeUnderAncestor(nodeTransform, HostedPlayerAuthoredPackageFallbackResourceId))
            score -= 6;

        if (IsHostedPlayerNodeUnderAncestor(nodeTransform, HostedPlayerAuthoredPackageRootNodeId))
            score -= 4;

        return score;
    }

    private bool HasHostedPlayerChildNode(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return false;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name ?? "", childName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsHostedPlayerNodeUnderAncestor(Transform nodeTransform, string ancestorName)
    {
        if (nodeTransform == null || string.IsNullOrEmpty(ancestorName))
            return false;

        Transform current = nodeTransform.parent;
        while (current != null)
        {
            if (string.Equals(current.name ?? "", ancestorName, StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void TryPruneHostedPlayerFallbackPackageRoot(Atom hostAtom)
    {
        if (hostAtom == null)
            return;

        Transform[] roots =
        {
            ResolveHostedPlayerHostRootTransform(hostAtom),
            hostAtom.transform,
            hostAtom.mainController != null ? hostAtom.mainController.transform : null,
            hostAtom.mainController != null && hostAtom.mainController.transform != null
                ? hostAtom.mainController.transform.parent
                : null
        };

        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i];
            if (root == null)
                continue;

            string[] packageRootNames =
            {
                HostedPlayerAuthoredPackageFallbackResourceId,
                HostedPlayerAuthoredPackageRootNodeId
            };

            for (int nameIndex = 0; nameIndex < packageRootNames.Length; nameIndex++)
            {
                GameObject packageRoot = FindHostedPlayerNodeObject(root, packageRootNames[nameIndex]);
                if (packageRoot == null)
                    continue;

                try
                {
                    UnityEngine.Object.Destroy(packageRoot);
                }
                catch
                {
                }
            }
        }
    }

    private GameObject FindHostedPlayerNodeObjectOnHostAtom(Atom hostAtom, string nodeId)
    {
        if (hostAtom == null || string.IsNullOrEmpty(nodeId))
            return null;

        Transform primaryRoot = ResolveHostedPlayerHostRootTransform(hostAtom);
        GameObject nodeObject = FindHostedPlayerNodeObject(primaryRoot, nodeId);
        if (nodeObject != null)
            return nodeObject;

        Transform atomRoot = hostAtom.transform;
        if (atomRoot != null && !ReferenceEquals(atomRoot, primaryRoot))
        {
            nodeObject = FindHostedPlayerNodeObject(atomRoot, nodeId);
            if (nodeObject != null)
                return nodeObject;
        }

        FreeControllerV3 mainController = hostAtom.mainController;
        if (mainController != null && mainController.transform != null)
        {
            Transform controllerParent = mainController.transform.parent;
            if (controllerParent != null
                && !ReferenceEquals(controllerParent, primaryRoot)
                && !ReferenceEquals(controllerParent, atomRoot))
            {
                nodeObject = FindHostedPlayerNodeObject(controllerParent, nodeId);
                if (nodeObject != null)
                    return nodeObject;
            }
        }

        return null;
    }

    private GameObject CreateHostedBootstrapQuad(
        Transform hostRoot,
        string nodeId,
        Vector3 localPosition,
        Vector3 localScale,
        bool keepCollider)
    {
        if (hostRoot == null || string.IsNullOrEmpty(nodeId))
            return null;

        GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        nodeObject.name = nodeId;
        nodeObject.layer = hostRoot.gameObject.layer;
        nodeObject.transform.SetParent(hostRoot, false);
        nodeObject.transform.localPosition = localPosition;
        nodeObject.transform.localRotation = Quaternion.identity;
        nodeObject.transform.localScale = localScale;

        if (!keepCollider)
        {
            Collider nodeCollider = nodeObject.GetComponent<Collider>();
            if (nodeCollider != null)
                UnityEngine.Object.Destroy(nodeCollider);
        }

        Renderer renderer = nodeObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = true;

        return nodeObject;
    }

    private bool TryResolveHostedPlayerAtom(out Atom hostAtom)
    {
        return TryResolveHostedPlayerAtom("", out hostAtom);
    }

    private bool TryResolveHostedPlayerAtom(string hostAtomUid, out Atom hostAtom)
    {
        hostAtom = null;

        if (!string.IsNullOrEmpty(hostAtomUid))
        {
            if (!TryFindSceneAtomByUid(hostAtomUid, out hostAtom) || hostAtom == null)
                return false;
        }
        else
        {
            hostAtom = containingAtom;
        }

        if (hostAtom == null)
            return false;

        string uid = hostAtom.uid ?? "";
        if (string.Equals(uid, "Session", StringComparison.OrdinalIgnoreCase))
        {
            hostAtom = null;
            return false;
        }

        return true;
    }

    private Transform ResolveHostedPlayerHostRootTransform(Atom hostAtom)
    {
        if (hostAtom == null)
            return null;

        // For Empty/CUA atoms, VaM interaction and operator dragging flows through the main
        // controller. Root the hosted player on that same transform so the product surface
        // actually moves with the host the operator manipulates.
        if (hostAtom.mainController != null && hostAtom.mainController.transform != null)
            return hostAtom.mainController.transform;

        return hostAtom.transform;
    }

    private Transform ResolveHostedPlayerAnchorTransform(Atom hostAtom)
    {
        return ResolveHostedPlayerHostRootTransform(hostAtom);
    }

    private bool IsHostedPlayerInstanceId(string instanceId)
    {
        return !string.IsNullOrEmpty(instanceId)
            && instanceId.StartsWith(HostedPlayerInstanceIdPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsHostedPlayerBindingRecord(PlayerScreenBindingRecord binding)
    {
        return binding != null
            && (string.Equals(binding.screenBindingMode ?? "", HostedPlayerBindingMode, StringComparison.OrdinalIgnoreCase)
                || IsHostedPlayerInstanceId(binding.instanceId));
    }

    private string ResolvePlayerScreenBindingMode(PlayerScreenBindingRecord binding)
    {
        if (binding == null)
            return "session_scene_surface";

        if (!string.IsNullOrEmpty(binding.screenBindingMode))
            return binding.screenBindingMode;

        return IsHostedPlayerBindingRecord(binding)
            ? HostedPlayerBindingMode
            : "session_scene_surface";
    }

    private string BuildHostedPlayerInstanceId(string hostAtomUid)
    {
        return HostedPlayerInstanceIdPrefix + (hostAtomUid ?? "").Trim();
    }

    private string BuildHostedPlayerPlaybackKey(string hostAtomUid)
    {
        return BuildStandalonePlayerPlaybackKey(BuildHostedPlayerInstanceId(hostAtomUid), HostedPlayerDisplayId);
    }

    private string ResolveHostedPlayerHostAtomUid(StandalonePlayerRecord record)
    {
        if (record == null)
            return "";

        if (record.binding != null && !string.IsNullOrEmpty(record.binding.atomUid))
            return record.binding.atomUid;

        string instanceId = record.instanceId ?? "";
        if (!IsHostedPlayerInstanceId(instanceId))
            return "";

        return instanceId.Substring(HostedPlayerInstanceIdPrefix.Length);
    }

    private bool TryResolveOrCreateHostedStandalonePlayerRecordForWrite(
        string hostAtomUid,
        string aspectMode,
        out StandalonePlayerRecord record,
        out HostedPlayerSurfaceContract contract,
        out string errorMessage)
    {
        record = null;
        contract = null;
        errorMessage = "";

        if (string.IsNullOrEmpty(hostAtomUid))
        {
            errorMessage = "hosted player host atom uid not resolved";
            return false;
        }

        if (!TryResolveHostedPlayerSurfaceContract(hostAtomUid, out contract, out errorMessage) || contract == null)
            return false;

        string playbackKey = BuildHostedPlayerPlaybackKey(hostAtomUid);
        if (!standalonePlayerRecords.TryGetValue(playbackKey, out record) || record == null)
        {
            record = new StandalonePlayerRecord();
            record.playbackKey = playbackKey;
            standalonePlayerRecords[playbackKey] = record;
        }

        record.instanceId = BuildHostedPlayerInstanceId(hostAtomUid);
        record.slotId = HostedPlayerSlotId;
        record.displayId = HostedPlayerDisplayId;
        record.aspectMode = string.IsNullOrEmpty(aspectMode) ? GhostScreenAspectModeFit : aspectMode;
        record.loopMode = NormalizeStandalonePlayerLoopMode(record.loopMode);
        return true;
    }

    private bool TryStartHostedPlayerAutoDemo(Atom hostAtom, out string errorMessage)
    {
        errorMessage = "";
        if (hostAtom == null)
        {
            errorMessage = "hosted player host atom missing";
            return false;
        }

#if FRAMEANGEL_CUA_PLAYER
        if (!HasHostedPlayerAuthoredScreenContract(hostAtom))
        {
            errorMessage = "hosted player screen_surface not found on attached CUA host";
            return false;
        }
#else
        if (!TryEnsureHostedPlayerScaffold(hostAtom, out errorMessage))
            return false;
#endif

        string hostAtomUid = string.IsNullOrEmpty(hostAtom.uid) ? "" : hostAtom.uid.Trim();
        if (string.IsNullOrEmpty(hostAtomUid))
        {
            errorMessage = "hosted player host atom uid missing";
            return false;
        }

        StandalonePlayerRecord record;
        HostedPlayerSurfaceContract contract;
        if (!TryResolveOrCreateHostedStandalonePlayerRecordForWrite(hostAtomUid, GhostScreenAspectModeFit, out record, out contract, out errorMessage)
            || record == null)
        {
            return false;
        }

        record.desiredPlaying = true;
        record.muted = false;
        record.storedVolume = Mathf.Clamp01(record.storedVolume <= 0f ? 1f : record.storedVolume);
        record.volume = Mathf.Clamp01(record.storedVolume);
        record.aspectMode = GhostScreenAspectModeFit;

#if FRAMEANGEL_CUA_PLAYER
        List<string> selectedMediaPaths;
        string selectedMediaPath = string.IsNullOrEmpty(playerMediaPath) ? "" : playerMediaPath.Trim();
        string selectedMediaError = "";
        if (!string.IsNullOrEmpty(selectedMediaPath))
        {
            if (!TryResolvePlayerRuntimeMediaPaths(selectedMediaPath, out selectedMediaPaths, out selectedMediaError)
                || selectedMediaPaths == null
                || selectedMediaPaths.Count <= 0)
            {
                ClearStandalonePlayerRecordMediaState(record);
                errorMessage = string.IsNullOrEmpty(selectedMediaError)
                    ? "hosted player media path unavailable"
                    : selectedMediaError;
                return false;
            }

            selectedMediaPath = ResolvePrimaryPlayerRuntimeMediaPath(selectedMediaPath, selectedMediaPaths);
            SetPendingPlayerSelection(selectedMediaPath);
            record.desiredPlaying = true;
            if (!TryLoadHostedStandalonePlayerRecordPath(record, hostAtomUid, selectedMediaPaths, selectedMediaPath, out errorMessage))
                return false;
        }
        else
        {
            ClearStandalonePlayerRecordMediaState(record);
            if (!TryEnsureStandalonePlayerRuntime(record, out errorMessage))
                return false;

            record.needsScreenRefresh = true;
        }
#else
        List<string> mediaPaths;
        bool usingConfiguredMedia;
        string mediaError;
        if (TryResolveMetaProofRequestedMediaPaths(out mediaPaths, out usingConfiguredMedia, out mediaError)
            && mediaPaths != null
            && mediaPaths.Count > 0
            && !string.IsNullOrEmpty(mediaPaths[0]))
        {
            if (!TryLoadHostedStandalonePlayerRecordPath(record, hostAtomUid, mediaPaths, mediaPaths[0], out errorMessage))
                return false;
        }
        else
        {
            if (!TryEnsureStandalonePlayerRuntime(record, out errorMessage))
                return false;

            record.needsScreenRefresh = false;
        }
#endif

        record.nextPlaybackStateApplyTime = 0f;
        if (!TryEnsureHostedPlayerControlSurfaceBound(hostAtomUid, record.instanceId, out errorMessage))
            return false;

        return true;
    }

    private void ClearStandalonePlayerRecordMediaState(StandalonePlayerRecord record)
    {
        if (record == null)
            return;

        record.playlistPaths.Clear();
        record.currentIndex = -1;
        record.mediaPath = "";
        record.resolvedMediaPath = "";
        record.desiredPlaying = false;
        record.prepared = false;
        record.preparePending = false;
        record.prepareStartedAt = 0f;
        record.textureWidth = 0;
        record.textureHeight = 0;
        record.lastError = "";
        record.mediaIsStillImage = false;

        try
        {
            if (record.videoPlayer != null)
            {
                record.videoPlayer.Stop();
                record.videoPlayer.targetTexture = null;
                record.videoPlayer.url = "";
            }
        }
        catch
        {
        }

        if (record.renderTexture != null)
        {
            try
            {
                UnityEngine.Object.Destroy(record.renderTexture);
            }
            catch
            {
            }

            record.renderTexture = null;
        }

        DestroyStandalonePlayerImageTexture(record);
    }

    private bool TryEnsureHostedPlayerControlSurfaceBound(
        string hostAtomUid,
        string targetInstanceId,
        out string errorMessage)
    {
        errorMessage = "";
        if (string.IsNullOrEmpty(hostAtomUid))
        {
            errorMessage = "hosted player host atom uid missing";
            return false;
        }

        if (string.IsNullOrEmpty(targetInstanceId))
        {
            errorMessage = "hosted player target instance id missing";
            return false;
        }

        HostedPlayerSurfaceContract hostedContract;
        if (!TryResolveHostedPlayerSurfaceContract(hostAtomUid, out hostedContract, out errorMessage) || hostedContract == null)
            return false;

        string playbackKey = BuildHostedPlayerPlaybackKey(hostAtomUid);
        if (string.IsNullOrEmpty(playbackKey)
            || !standalonePlayerRecords.TryGetValue(playbackKey, out StandalonePlayerRecord record)
            || record == null)
        {
            errorMessage = "hosted player runtime record not found";
            return false;
        }

        InnerPieceInstanceRecord authoredControlInstance;
        FAInnerPieceControlSurfaceData authoredControlSurface;
        if (!TryEnsureHostedPlayerAuthoredControlSurfaceInstance(
                hostAtomUid,
                record,
                hostedContract,
                out authoredControlInstance,
                out authoredControlSurface,
                out errorMessage))
        {
            return false;
        }

        PlayerControlSurfaceBindingRecord binding = BuildStandalonePlayerControlSurfaceBinding(
            authoredControlInstance,
            authoredControlSurface,
            record,
            HostedPlayerDisplayId);
        binding.atomUid = hostAtomUid;
        binding.hostedPanelPoseCaptured = true;
        binding.hostedFollowCleared = true;
        binding.hostedFollowBound = true;
        playerControlSurfaceBindings[binding.controlSurfaceInstanceId] = binding;

        ApplyHostedPlayerPlaceholderVisualState(hostedContract);
        return true;
    }

    private bool TryEnsureHostedPlayerAuthoredControlSurfaceInstance(
        string hostAtomUid,
        StandalonePlayerRecord record,
        HostedPlayerSurfaceContract contract,
        out InnerPieceInstanceRecord instance,
        out FAInnerPieceControlSurfaceData controlSurface,
        out string errorMessage)
    {
        instance = null;
        controlSurface = null;
        errorMessage = "";

        if (record == null || string.IsNullOrEmpty(record.instanceId))
        {
            errorMessage = "hosted player runtime record missing";
            return false;
        }

        if (contract == null || contract.hostAtom == null)
        {
            errorMessage = "hosted player surface contract missing";
            return false;
        }

        if (contract.controlSurfaceObject == null)
        {
            errorMessage = "hosted player control_surface not found on attached CUA host";
            return false;
        }

        if (!TryResolveHostedPlayerAuthoredControlSurface(contract, out controlSurface, out errorMessage) || controlSurface == null)
            return false;

        if (!innerPieceInstances.TryGetValue(record.instanceId, out instance) || instance == null)
        {
            instance = new InnerPieceInstanceRecord();
            instance.instanceId = record.instanceId;
            innerPieceInstances[record.instanceId] = instance;
        }

        instance.resourceId = HostedPlayerAuthoredPackageFallbackResourceId;
        instance.screenContractVersion = ResolveHostedPlayerScreenContractVersion(contract);
        instance.defaultAspectMode = GhostScreenAspectModeFit;
        instance.shellId = HostedPlayerAuthoredPackageRootNodeId;
        instance.deviceClass = "monitor";
        instance.orientationSupport = "landscape";
        instance.inputStyle = "fixed";
        instance.nodeObjects.Clear();
        instance.screenSlots.Clear();
        if (contract.screenSurfaceObject != null)
            instance.nodeObjects[HostedPlayerScreenSurfaceNodeId] = contract.screenSurfaceObject;
        if (contract.disconnectSurfaceObject != null)
            instance.nodeObjects[HostedPlayerDisconnectSurfaceNodeId] = contract.disconnectSurfaceObject;
        if (contract.screenBodyObject != null)
            instance.nodeObjects[HostedPlayerScreenBodyNodeId] = contract.screenBodyObject;
        if (contract.screenGlassObject != null)
            instance.nodeObjects[HostedPlayerScreenGlassNodeId] = contract.screenGlassObject;
        if (contract.screenApertureObject != null)
            instance.nodeObjects[HostedPlayerScreenApertureNodeId] = contract.screenApertureObject;
        if (contract.controlSurfaceObject != null)
            instance.nodeObjects[HostedPlayerControlSurfaceNodeId] = contract.controlSurfaceObject;
        if (contract.controlColliderObject != null)
            instance.nodeObjects[HostedPlayerControlColliderNodeId] = contract.controlColliderObject;

        InnerPieceScreenSlotRuntimeRecord slot;
        if (!instance.screenSlots.TryGetValue(HostedPlayerSlotId, out slot) || slot == null)
        {
            slot = new InnerPieceScreenSlotRuntimeRecord();
            instance.screenSlots[HostedPlayerSlotId] = slot;
        }

        slot.slotId = HostedPlayerSlotId;
        slot.displayId = HostedPlayerDisplayId;
        slot.surfaceTargetId = "player:screen";
        slot.disconnectStateId = "";
        slot.screenSurfaceNodeId = HostedPlayerScreenSurfaceNodeId;
        slot.disconnectSurfaceNodeId = HostedPlayerDisconnectSurfaceNodeId;
        slot.screenGlassNodeId = HostedPlayerScreenGlassNodeId;
        slot.screenApertureNodeId = HostedPlayerScreenApertureNodeId;
        slot.screenSurfaceObject = contract.screenSurfaceObject;
        slot.disconnectSurfaceObject = contract.disconnectSurfaceObject;
        slot.screenGlassObject = contract.screenGlassObject;
        slot.screenApertureObject = contract.screenApertureObject;
        slot.disconnectSurfaceVisible = false;
        slot.forceOperatorFacingFrontFace =
            !string.Equals(instance.screenContractVersion ?? "", HostedPlayerScreenCoreContractVersion, StringComparison.OrdinalIgnoreCase);

        instance.controlSurface = CloneInnerPieceControlSurface(controlSurface);
        EnsureLocalControlSurfaceState(instance, instance.controlSurface);
        AttachInnerPieceControlSurfacePointerRuntime(instance);
        return true;
    }

    private bool TryResolveHostedPlayerAuthoredControlSurface(
        HostedPlayerSurfaceContract contract,
        out FAInnerPieceControlSurfaceData controlSurface,
        out string errorMessage)
    {
        controlSurface = null;
        errorMessage = "";

        if (ShouldUseHostedPlayerScreenCoreControlSurfaceContract(contract))
        {
            controlSurface = BuildHostedPlayerScreenCoreControlSurfaceContract();
            return true;
        }

        FAInnerPieceStoredResource storedResource = FAInnerPieceStorage.LoadResource(HostedPlayerAuthoredPackageFallbackResourceId);
        if (storedResource != null && storedResource.controlSurface != null)
        {
            controlSurface = CloneInnerPieceControlSurface(storedResource.controlSurface);
            return true;
        }

        // Keep direct CUA self-contained. When the archived hosted resource is absent, fall back
        // to the authored control contract in code instead of importing package JSON at runtime.
        controlSurface = BuildHostedPlayerAuthoredControlSurfaceContract();
        return true;
    }

    private bool ShouldUseHostedPlayerScreenCoreControlSurfaceContract(HostedPlayerSurfaceContract contract)
    {
        if (contract == null || contract.hostAtom == null)
            return false;

        return FindHostedPlayerDirectCuaScreenCoreNode(contract.hostAtom, HostedPlayerScreenCoreRootNodeId) != null;
    }

    private FAInnerPieceControlSurfaceData BuildHostedPlayerScreenCoreControlSurfaceContract()
    {
        FAInnerPieceControlSurfaceData controlSurface = new FAInnerPieceControlSurfaceData();
        controlSurface.controlSurfaceId = "fa_player_screen_core_controls_v1";
        controlSurface.controlSurfaceLabel = "FA Player Screen Core Controls";
        controlSurface.controlFamilyId = "meta_ui_video_player";
        controlSurface.layoutSource = "screen_core_authored_controls_v1";
        controlSurface.targetDisplayIds = new[] { "player_main" };
        controlSurface.defaultTargetDisplayId = "player_main";
        controlSurface.surfaceNodeId = HostedPlayerControlSurfaceNodeId;
        controlSurface.colliderNodeId = HostedPlayerControlSurfaceNodeId;
        controlSurface.surfaceWidthMeters = 0.460800022f;
        controlSurface.surfaceHeightMeters = 0.309600025f;
        controlSurface.elements = new[]
        {
            BuildHostedPlayerAuthoredControlElement(
                "control_scrub_normalized",
                "Scrub",
                "scrub_normalized",
                "control_scrub_normalized",
                "slider",
                "normalized_float",
                0.16f,
                0.68f,
                0.78f,
                0.06f),
            BuildHostedPlayerAuthoredControlElement(
                "control_volume_normalized",
                "Volume",
                "volume_normalized",
                "control_volume_normalized",
                "slider",
                "normalized_float",
                0.05f,
                0.24f,
                0.05f,
                0.48f),
            BuildHostedPlayerAuthoredControlElement(
                "control_mute_toggle",
                "Mute",
                "mute_toggle",
                "control_mute_toggle",
                "toggle",
                "bool",
                0.03f,
                0.08f,
                0.09f,
                0.10f),
            BuildHostedPlayerAuthoredControlElement(
                "control_skip_backward",
                "Skip Backward",
                "skip_backward",
                "control_skip_backward",
                "button",
                "bool",
                0.20f,
                0.20f,
                0.09f,
                0.14f),
            BuildHostedPlayerAuthoredControlElement(
                "control_previous",
                "Previous",
                "previous",
                "control_previous",
                "button",
                "bool",
                0.32f,
                0.20f,
                0.09f,
                0.14f),
            BuildHostedPlayerAuthoredControlElement(
                "control_play_pause",
                "Play Pause",
                "play_pause",
                "control_play_pause",
                "button",
                "bool",
                0.44f,
                0.18f,
                0.12f,
                0.18f),
            BuildHostedPlayerAuthoredControlElement(
                "control_next",
                "Next",
                "next",
                "control_next",
                "button",
                "bool",
                0.59f,
                0.20f,
                0.09f,
                0.14f),
            BuildHostedPlayerAuthoredControlElement(
                "control_skip_forward",
                "Skip Forward",
                "skip_forward",
                "control_skip_forward",
                "button",
                "bool",
                0.71f,
                0.20f,
                0.09f,
                0.14f)
        };

        return controlSurface;
    }

    private FAInnerPieceControlSurfaceData BuildHostedPlayerAuthoredControlSurfaceContract()
    {
        FAInnerPieceControlSurfaceData controlSurface = new FAInnerPieceControlSurfaceData();
        controlSurface.controlSurfaceId = "fa_cua_player_host_v1_controls";
        controlSurface.controlSurfaceLabel = "FA CUA Player Host Controls";
        controlSurface.controlFamilyId = "meta_ui_video_player";
        controlSurface.layoutSource = "authored_host_contract_v1";
        controlSurface.targetDisplayIds = new[] { "player_main" };
        controlSurface.defaultTargetDisplayId = "player_main";
        controlSurface.surfaceNodeId = HostedPlayerControlSurfaceNodeId;
        controlSurface.colliderNodeId = HostedPlayerControlColliderNodeId;
        controlSurface.surfaceWidthMeters = 0.460800022f;
        controlSurface.surfaceHeightMeters = 0.309600025f;
        controlSurface.elements = new[]
        {
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_controlbar_borderlessbutton_iconandlabel",
                "BorderlessButton_IconAndLabel",
                "playercontrols_controlbar_borderlessbutton_iconandlabel",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_controlbar_borderlessbutton_iconandlabel",
                "toggle",
                "bool",
                0.0117188394f,
                0.929578364f,
                0.05468726f,
                0.0581395365f),
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_controlbar_borderlessbutton_iconandlabel_2",
                "BorderlessButton_IconAndLabel (2)",
                "playercontrols_controlbar_borderlessbutton_iconandlabel_2",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_controlbar_borderlessbutton_iconandlabel_2",
                "toggle",
                "bool",
                0.8789065f,
                0.929578364f,
                0.05468726f,
                0.0581395365f),
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_controlbar_borderlessbutton_iconandlabel_3",
                "BorderlessButton_IconAndLabel (3)",
                "playercontrols_controlbar_borderlessbutton_iconandlabel_3",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_controlbar_borderlessbutton_iconandlabel_3",
                "toggle",
                "bool",
                0.93359375f,
                0.929578364f,
                0.05468726f,
                0.0581395365f),
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_1",
                "BorderlessButton_IconAndLabel (1)",
                "playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_1",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_1",
                "toggle",
                "bool",
                0.42578125f,
                0.3103923f,
                0.0390627757f,
                0.0581391677f),
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_2",
                "BorderlessButton_IconAndLabel (2)",
                "playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_2",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_control_quickcontrols_borderlessbutton_iconandlabel_2",
                "toggle",
                "bool",
                0.5351558f,
                0.3103923f,
                0.0390627757f,
                0.0581391677f),
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_control_quickcontrols_secondarybutton_iconandlabel",
                "SecondaryButton_IconAndLabel",
                "play_pause",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_control_quickcontrols_secondarybutton_iconandlabel",
                "button",
                "bool",
                0.4804685f,
                0.3103923f,
                0.0390627757f,
                0.0581391677f),
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_control_sound_volumeslider_smallslider_labelsandicons_1_smallslider",
                "SmallSlider",
                "volume_normalized",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_control_sound_volumeslider_smallslider_labelsandicons_1_smallslider",
                "slider",
                "normalized_float",
                0.031249702f,
                0.4002178f,
                0.0117191374f,
                0.2906978f),
            BuildHostedPlayerAuthoredControlElement(
                "playercontrols_smallslider_labelsandicons_smallslider",
                "SmallSlider",
                "scrub_normalized",
                "control__toolkitexport_meta_patterns_contentuiexample_videoplayer_e7cfc411_contentuiexample_videoplayer_canvasroot_controls_playercontrols_smallslider_labelsandicons_smallslider",
                "slider",
                "normalized_float",
                0.02343744f,
                0.2696944f,
                0.95312494f,
                0.0174416825f)
        };

        return controlSurface;
    }

    private FAInnerPieceControlElementData BuildHostedPlayerAuthoredControlElement(
        string elementId,
        string elementLabel,
        string actionId,
        string nodeId,
        string elementKind,
        string valueKind,
        float x,
        float y,
        float width,
        float height)
    {
        FAInnerPieceControlElementData element = new FAInnerPieceControlElementData();
        element.elementId = elementId;
        element.elementLabel = elementLabel;
        element.actionId = actionId;
        element.nodeId = nodeId;
        element.colliderNodeId = nodeId;
        element.elementKind = elementKind;
        element.valueKind = valueKind;
        element.normalizedRect = new FAInnerPieceNormalizedRectData();
        element.normalizedRect.x = x;
        element.normalizedRect.y = y;
        element.normalizedRect.width = width;
        element.normalizedRect.height = height;
        element.readOnly = false;
        return element;
    }

    private bool TryLayoutHostedMetaProofControlSurface(
        string controlSurfaceInstanceId,
        string targetInstanceId,
        out string errorMessage,
        float? gapMetersOverride = null,
        float? forwardOffsetMetersOverride = null)
    {
        errorMessage = "";
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
        {
            errorMessage = "hosted player control surface instance id missing";
            return false;
        }

        string hostAtomUid = ResolveHostedPlayerHostAtomUidFromTargetInstanceId(targetInstanceId);
        if (string.IsNullOrEmpty(hostAtomUid))
        {
            errorMessage = "hosted player host atom uid not resolved";
            return false;
        }

        HostedPlayerSurfaceContract hostedContract;
        if (!TryResolveHostedPlayerSurfaceContract(hostAtomUid, out hostedContract, out errorMessage) || hostedContract == null)
            return false;

        InnerPieceInstanceRecord controlInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        if (!TryResolveMetaProofControlSurfaceInstance(controlSurfaceInstanceId, out controlInstance, out controlSurface) || controlInstance == null)
        {
            errorMessage = "hosted player control surface instance not found";
            return false;
        }

        SyncObjectRecord controlRootRecord;
        if (!syncObjects.TryGetValue(controlInstance.rootObjectId ?? "", out controlRootRecord)
            || controlRootRecord == null
            || controlRootRecord.gameObject == null)
        {
            errorMessage = "hosted player control surface root not found";
            return false;
        }

        GameObject targetSurfaceObject = hostedContract.screenSurfaceObject ?? hostedContract.controlSurfaceObject;
        if (targetSurfaceObject == null)
        {
            errorMessage = "hosted player control surface target not found";
            return false;
        }

        FAInnerPiecePlaneData hostPlane;
        if (!TryBuildInnerPiecePlaneData(targetSurfaceObject, out hostPlane))
        {
            errorMessage = "hosted player control surface plane not resolved";
            return false;
        }

        bool hasAuthoredPanelSize =
            controlSurface != null
            && controlSurface.surfaceWidthMeters > 0f
            && controlSurface.surfaceHeightMeters > 0f;
        float panelWidthMeters = hasAuthoredPanelSize
            ? controlSurface.surfaceWidthMeters
            : 0.60f;
        float panelHeightMeters = hasAuthoredPanelSize
            ? controlSurface.surfaceHeightMeters
            : 0.24f;
        float panelDepthMeters = 0.01f;

        string surfaceNodeId = controlSurface != null ? (controlSurface.surfaceNodeId ?? "") : "";
        GameObject panelSurfaceObject = ResolveInnerPieceNodeObject(controlInstance, surfaceNodeId);
        FAInnerPiecePlaneData panelPlane;
        if (TryBuildInnerPiecePlaneData(panelSurfaceObject, out panelPlane))
        {
            panelDepthMeters = Mathf.Max(0.001f, panelPlane.depthMeters);
            if (!hasAuthoredPanelSize)
            {
                panelWidthMeters = Mathf.Max(0.001f, panelPlane.widthMeters);
                panelHeightMeters = Mathf.Max(0.001f, panelPlane.heightMeters);
            }
        }

        Transform controlRootTransform = controlRootRecord.gameObject.transform;
        if (controlRootTransform == null)
        {
            errorMessage = "hosted player control surface root transform not found";
            return false;
        }

        PlayerControlSurfaceBindingRecord bindingRecord = null;
        playerControlSurfaceBindings.TryGetValue(controlSurfaceInstanceId, out bindingRecord);

        Vector3 panelLocalPosition = Vector3.zero;
        Quaternion panelLocalRotation = Quaternion.identity;
        bool needCapturePanelPose = bindingRecord == null || !bindingRecord.hostedPanelPoseCaptured;
        if (!needCapturePanelPose)
        {
            panelLocalPosition = bindingRecord.hostedPanelLocalPosition;
            panelLocalRotation = bindingRecord.hostedPanelLocalRotation;
        }
        else if (panelSurfaceObject != null)
        {
            panelLocalPosition = controlRootTransform.InverseTransformPoint(panelSurfaceObject.transform.position);
            panelLocalRotation = Quaternion.Inverse(controlRootTransform.rotation) * panelSurfaceObject.transform.rotation;
            if (bindingRecord != null)
            {
                bindingRecord.hostedPanelLocalPosition = panelLocalPosition;
                bindingRecord.hostedPanelLocalRotation = panelLocalRotation;
                bindingRecord.hostedPanelPoseCaptured = true;
            }
        }

        float gapMeters = gapMetersOverride.HasValue
            ? Mathf.Max(0f, gapMetersOverride.Value)
            : 0f;
        float forwardOffsetMeters = forwardOffsetMetersOverride.HasValue
            ? Mathf.Max(0f, forwardOffsetMetersOverride.Value)
            : 0.0025f;

        float targetWidthMeters = Mathf.Max(0.001f, hostPlane.widthMeters);
        float targetHeightMeters = Mathf.Max(0.001f, hostPlane.heightMeters);
        float widthScale = targetWidthMeters / Mathf.Max(0.001f, panelWidthMeters);
        float heightScale = targetHeightMeters / Mathf.Max(0.001f, panelHeightMeters);
        float uniformScale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.01f, 50f);

        Vector3 desiredPanelForward = -hostPlane.forward;
        Vector3 desiredPanelCenter =
            hostPlane.center
            + (desiredPanelForward * Mathf.Max(
                forwardOffsetMeters,
                ((hostPlane.depthMeters + panelDepthMeters) * 0.5f) + gapMeters));
        Quaternion desiredPanelRotation = Quaternion.LookRotation(desiredPanelForward, hostPlane.up);
        Quaternion worldRotation = desiredPanelRotation * Quaternion.Inverse(panelLocalRotation);
        Vector3 worldPosition = desiredPanelCenter - (worldRotation * (panelLocalPosition * uniformScale));

        bool anchorMatchesHost =
            !string.IsNullOrEmpty(controlInstance.anchorAtomUid)
            && string.Equals(controlInstance.anchorAtomUid, hostAtomUid, StringComparison.OrdinalIgnoreCase);
        bool shouldBindHostedFollow = !anchorMatchesHost || bindingRecord == null || !bindingRecord.hostedFollowBound;
        bool needsFollowReset =
            !string.IsNullOrEmpty(controlInstance.anchorAtomUid)
            && !anchorMatchesHost;
        if (needsFollowReset)
        {
            string clearFollowArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\""
                + ",\"clear\":true"
                + "}";
            if (!TryExecuteMetaProofAction(HostInstanceSetFollowActionId, clearFollowArgsJson, out _))
            {
                errorMessage = string.IsNullOrEmpty(syncMetaProofStatus)
                    ? "hosted player control panel follow reset failed"
                    : syncMetaProofStatus;
                return false;
            }
        }

        if (shouldBindHostedFollow)
        {
            string transformArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\""
                + ",\"position\":{\"x\":" + FormatFloat(worldPosition.x)
                + ",\"y\":" + FormatFloat(worldPosition.y)
                + ",\"z\":" + FormatFloat(worldPosition.z) + "}"
                + ",\"rotX\":" + FormatFloat(worldRotation.x)
                + ",\"rotY\":" + FormatFloat(worldRotation.y)
                + ",\"rotZ\":" + FormatFloat(worldRotation.z)
                + ",\"rotW\":" + FormatFloat(worldRotation.w)
                + ",\"scaleX\":" + FormatFloat(uniformScale)
                + ",\"scaleY\":" + FormatFloat(uniformScale)
                + ",\"scaleZ\":" + FormatFloat(uniformScale)
                + "}";
            if (!TryExecuteMetaProofAction(HostInstanceTransformActionId, transformArgsJson, out _))
            {
                errorMessage = string.IsNullOrEmpty(syncMetaProofStatus)
                    ? "hosted player control panel transform failed"
                    : syncMetaProofStatus;
                return false;
            }

            Transform hostAnchorTransform = ResolveHostedPlayerAnchorTransform(hostedContract.hostAtom);
            if (hostAnchorTransform == null)
            {
                errorMessage = "hosted player host transform not found";
                return false;
            }

            Vector3 localPositionOffset = Quaternion.Inverse(hostAnchorTransform.rotation) * (worldPosition - hostAnchorTransform.position);
            Quaternion localRotationOffset = Quaternion.Inverse(hostAnchorTransform.rotation) * worldRotation;
            Vector3 localRotationEuler = localRotationOffset.eulerAngles;

            string followArgsJson = "{"
                + "\"instanceId\":\"" + EscapeJsonString(controlSurfaceInstanceId) + "\""
                + ",\"anchorAtomUid\":\"" + EscapeJsonString(hostAtomUid) + "\""
                + ",\"followPosition\":true"
                + ",\"followRotation\":true"
                + ",\"localPositionOffset\":{\"x\":" + FormatFloat(localPositionOffset.x)
                + ",\"y\":" + FormatFloat(localPositionOffset.y)
                + ",\"z\":" + FormatFloat(localPositionOffset.z) + "}"
                + ",\"localRotationEuler\":{\"x\":" + FormatFloat(localRotationEuler.x)
                + ",\"y\":" + FormatFloat(localRotationEuler.y)
                + ",\"z\":" + FormatFloat(localRotationEuler.z) + "}"
                + "}";
            if (!TryExecuteMetaProofAction(HostInstanceSetFollowActionId, followArgsJson, out _))
            {
                errorMessage = string.IsNullOrEmpty(syncMetaProofStatus)
                    ? "hosted player control panel follow bind failed"
                    : syncMetaProofStatus;
                return false;
            }

            if (bindingRecord != null)
            {
                bindingRecord.hostedFollowBound = true;
                bindingRecord.hostedFollowCleared = true;
            }
        }

        ApplyHostedPlayerPlaceholderVisualState(hostedContract);
        return true;
    }

    private bool TryLoadHostedStandalonePlayerRecordPath(
        StandalonePlayerRecord record,
        string hostAtomUid,
        List<string> requestedPaths,
        string selectedMediaPath,
        out string errorMessage)
    {
        errorMessage = "";
        if (record == null)
        {
            errorMessage = "hosted player record missing";
            return false;
        }

        if (string.IsNullOrEmpty(hostAtomUid))
        {
            errorMessage = "hosted player host atom uid missing";
            return false;
        }

        string mediaPath = string.IsNullOrEmpty(selectedMediaPath)
            ? (requestedPaths != null && requestedPaths.Count > 0 ? requestedPaths[0] : "")
            : selectedMediaPath.Trim();
        if (string.IsNullOrEmpty(mediaPath))
        {
            errorMessage = "mediaPath is required";
            return false;
        }

        string resolvedMediaPath = ResolveStandalonePlayerAbsolutePath(mediaPath);
        if (string.IsNullOrEmpty(resolvedMediaPath))
        {
            errorMessage = "mediaPath could not be resolved";
            return false;
        }

        record.instanceId = BuildHostedPlayerInstanceId(hostAtomUid);
        record.slotId = HostedPlayerSlotId;
        record.displayId = HostedPlayerDisplayId;
        record.mediaPath = mediaPath;
        record.resolvedMediaPath = resolvedMediaPath;
        record.lastError = "";
        record.prepared = false;
        record.preparePending = false;
        record.prepareStartedAt = 0f;
        record.textureWidth = 0;
        record.textureHeight = 0;
        record.needsScreenRefresh = false;
        record.mediaIsStillImage = false;
        record.hasObservedPlaybackTime = false;
        record.lastObservedPlaybackTimeSeconds = 0d;
        record.lastPlaybackMotionObservedAt = 0f;
        record.naturalEndHandled = false;

        DestroyStandalonePlayerImageTexture(record);

        List<string> existingPlaylistPaths = new List<string>(record.playlistPaths);
        List<string> resolvedPlaylistPaths = new List<string>();
        bool canReuseExistingPlaylist =
            record.playlistPaths != null
            && record.playlistPaths.Count > 1
            && FindStandalonePlayerPlaylistIndex(record.playlistPaths, mediaPath) >= 0;

        if (requestedPaths != null)
        {
            for (int i = 0; i < requestedPaths.Count; i++)
            {
                string candidate = requestedPaths[i];
                if (!string.IsNullOrEmpty(candidate)
                    && FrameAngelPlayerMediaParity.IsSupportedMediaPath(candidate))
                {
                    resolvedPlaylistPaths.Add(candidate);
                }
            }
        }

        if (resolvedPlaylistPaths.Count <= 1 && canReuseExistingPlaylist)
        {
            resolvedPlaylistPaths.Clear();
            for (int i = 0; i < record.playlistPaths.Count; i++)
            {
                string existingPath = record.playlistPaths[i];
                if (!string.IsNullOrEmpty(existingPath)
                    && FrameAngelPlayerMediaParity.IsSupportedMediaPath(existingPath))
                {
                    resolvedPlaylistPaths.Add(existingPath);
                }
            }
        }

        record.playlistPaths.Clear();
        for (int i = 0; i < resolvedPlaylistPaths.Count; i++)
            record.playlistPaths.Add(resolvedPlaylistPaths[i]);

        if (!DoStandalonePlayerPlaylistsContainSameEntries(existingPlaylistPaths, record.playlistPaths))
            ClearStandalonePlayerRandomHistory(record);

        if (record.playlistPaths.Count <= 0)
            record.playlistPaths.Add(mediaPath);

        int selectedIndex = FindStandalonePlayerPlaylistIndex(record.playlistPaths, mediaPath);
        record.currentIndex = selectedIndex >= 0 ? selectedIndex : 0;

        if (!TryEnsureStandalonePlayerRuntime(record, out errorMessage))
            return false;

        record.needsScreenRefresh = true;
        ApplyStandalonePlayerLoopMode(record);
        ApplyStandalonePlayerAudioState(record);

        bool loadStillImage = FrameAngelPlayerMediaParity.IsSupportedImagePath(mediaPath);
        if (loadStillImage && !TryLoadStandalonePlayerImageTexture(record, resolvedMediaPath, out errorMessage))
            return false;

        // If the host surface is already present when load is invoked, bind immediately to the
        // authored screen instead of waiting for a later refresh tick to discover it.
        HostedPlayerSurfaceContract eagerContract;
        string eagerContractError;
        bool preserveExistingHostedBindingUntilPrepared =
            !loadStillImage
            && record.binding != null
            && record.binding.runtimeMediaSurfaceObject != null;

        if (!preserveExistingHostedBindingUntilPrepared
            && (record.renderTexture != null || record.imageTexture != null))
        {
            if (TryResolveHostedPlayerSurfaceContract(hostAtomUid, out eagerContract, out eagerContractError)
                && eagerContract != null)
            {
                string eagerTargetId = record.binding != null && !string.IsNullOrEmpty(record.binding.surfaceTargetId)
                    ? record.binding.surfaceTargetId
                    : "player:screen";
                string eagerRefreshError;
                if (TryRefreshHostedStandalonePlayerScreenBinding(hostAtomUid, eagerTargetId, record, eagerContract, out eagerRefreshError))
                {
                    record.lastError = "";
                    // Keep one more refresh armed so prepared video dimensions can replace the
                    // placeholder 16x16 bind with the real media size as soon as it arrives.
                    record.needsScreenRefresh = true;
                }
                else if (!string.IsNullOrEmpty(eagerRefreshError))
                {
                    record.lastError = eagerRefreshError;
                    record.needsScreenRefresh = true;
                }
            }
            else if (!string.IsNullOrEmpty(eagerContractError))
            {
                record.lastError = eagerContractError;
            }
        }

        if (loadStillImage)
        {
            if (record.binding == null && string.IsNullOrEmpty(record.lastError))
                record.lastError = "screen owner unresolved";
            record.needsScreenRefresh = true;
            return true;
        }

        try
        {
            if (record.videoPlayer != null)
                record.videoPlayer.Stop();
        }
        catch
        {
        }

        try
        {
            if (record.videoPlayer != null)
            {
                record.videoPlayer.url = resolvedMediaPath;
                record.videoPlayer.Prepare();
                record.preparePending = true;
                record.prepareStartedAt = Time.unscaledTime;
                if (record.binding == null && string.IsNullOrEmpty(record.lastError))
                {
                    // Starting Prepare() only proves the runtime accepted the source; the hosted
                    // screen contract is still unresolved until a real binding lands.
                    record.lastError = "screen owner unresolved";
                    record.needsScreenRefresh = true;
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = "player prepare failed: " + ex.Message;
            record.lastError = errorMessage;
            return false;
        }

        errorMessage = "player runtime unavailable";
        return false;
    }

    private bool TryRefreshHostedStandalonePlayerScreenBinding(
        string hostAtomUid,
        string screenSurfaceTargetId,
        StandalonePlayerRecord record,
        HostedPlayerSurfaceContract contract,
        out string errorMessage)
    {
        errorMessage = "";
        if (record == null)
        {
            errorMessage = "hosted player record missing";
            return false;
        }

        if (contract == null || contract.hostAtom == null || contract.screenSurfaceObject == null)
        {
            errorMessage = "hosted player surface contract missing";
            return false;
        }

        Texture sourceTexture;
        Vector2 sourceScale;
        Vector2 sourceOffset;
        string sourceName;
        if (!TryResolveStandalonePlayerSourceTexture(record, out sourceTexture, out sourceScale, out sourceOffset, out sourceName)
            || sourceTexture == null)
        {
            errorMessage = "player source texture unavailable";
            return false;
        }

        if (record.binding != null)
        {
            TryRestoreDisconnectSurface(record.binding);
            record.binding = null;
        }

        string hostedContractVersion = ResolveHostedPlayerScreenContractVersion(contract);

        InnerPieceInstanceRecord hostedInstance = new InnerPieceInstanceRecord();
        hostedInstance.instanceId = record.instanceId;
        hostedInstance.resourceId = "";
        hostedInstance.screenContractVersion = hostedContractVersion;
        hostedInstance.defaultAspectMode = GhostScreenAspectModeFit;

        InnerPieceScreenSlotRuntimeRecord hostedSlot = new InnerPieceScreenSlotRuntimeRecord();
        hostedSlot.slotId = HostedPlayerSlotId;
        hostedSlot.displayId = HostedPlayerDisplayId;
        hostedSlot.surfaceTargetId = string.IsNullOrEmpty(screenSurfaceTargetId) ? "player:screen" : screenSurfaceTargetId;
        hostedSlot.disconnectStateId = "";
        hostedSlot.screenSurfaceNodeId = HostedPlayerScreenSurfaceNodeId;
        hostedSlot.disconnectSurfaceNodeId = HostedPlayerDisconnectSurfaceNodeId;
        hostedSlot.screenGlassNodeId = HostedPlayerScreenGlassNodeId;
        hostedSlot.screenApertureNodeId = HostedPlayerScreenApertureNodeId;
        hostedSlot.screenSurfaceObject = contract.screenSurfaceObject;
        hostedSlot.screenGlassObject = contract.screenGlassObject;
        hostedSlot.screenApertureObject = contract.screenApertureObject;
        hostedSlot.disconnectSurfaceObject = contract.disconnectSurfaceObject;
        hostedSlot.disconnectSurfaceVisible = false;
        hostedSlot.forceOperatorFacingFrontFace =
            !string.Equals(hostedContractVersion ?? "", HostedPlayerScreenCoreContractVersion, StringComparison.OrdinalIgnoreCase);

#if !FRAMEANGEL_CUA_PLAYER
        TryConfigureHostedMetaVideoPlayerMediaTarget(hostAtomUid, hostedSlot);
#endif

        Renderer[] attachedRenderers;
        Material[][] originalMaterials;
        Material[][] appliedMaterials;
        string debugJson;
        if (!TryAttachStandalonePlayerScreenMaterial(
            record,
            hostedInstance,
            hostedSlot,
            sourceTexture,
            sourceScale,
            sourceOffset,
            sourceName,
            out attachedRenderers,
            out originalMaterials,
            out appliedMaterials,
            out debugJson,
            out errorMessage))
        {
            return false;
        }

        PlayerScreenBindingRecord nextBinding = new PlayerScreenBindingRecord();
        nextBinding.atomUid = hostAtomUid ?? "";
        nextBinding.instanceId = record.instanceId;
        nextBinding.slotId = hostedSlot.slotId;
        nextBinding.displayId = hostedSlot.displayId;
        nextBinding.screenContractVersion = hostedInstance.screenContractVersion ?? HostedPlayerScreenContractVersion;
        nextBinding.disconnectStateId = "";
        nextBinding.surfaceTargetId = hostedSlot.surfaceTargetId;
        nextBinding.embeddedHostAtomUid = hostAtomUid ?? "";
        nextBinding.aspectMode = record.aspectMode;
        nextBinding.screenBindingMode = HostedPlayerBindingMode;
        nextBinding.debugJson = string.IsNullOrEmpty(debugJson) ? "{}" : debugJson;
        nextBinding.screenSurfaceRenderers = attachedRenderers ?? new Renderer[0];
        nextBinding.originalSurfaceMaterials = originalMaterials ?? new Material[0][];
        nextBinding.appliedSurfaceMaterials = appliedMaterials ?? new Material[0][];
        nextBinding.runtimeMediaSurfaceObject = hostedSlot.runtimeMediaSurfaceObject;
        nextBinding.runtimeMediaSurfaceRenderer = hostedSlot.runtimeMediaSurfaceRenderer;

        string visibilityError;
        if (!TryApplyBoundPlayerSurfaceVisibility(hostedInstance, hostedSlot, nextBinding, out visibilityError))
        {
            errorMessage = visibilityError;
            return false;
        }

        ApplyHostedScreenCoreBackdropPresentation(contract, nextBinding);
        record.binding = nextBinding;
        playerScreenBindings[hostAtomUid] = nextBinding;
        record.needsScreenRefresh = false;
        ApplyHostedPlayerPlaceholderVisualState(contract);
        return true;
    }

    private void TryConfigureHostedMetaVideoPlayerMediaTarget(
        string hostAtomUid,
        InnerPieceScreenSlotRuntimeRecord hostedSlot)
    {
        if (hostedSlot == null || string.IsNullOrEmpty(hostAtomUid))
            return;

        MetaPlayerRuntimeSurfaceConfig activeSurfaceConfig;
        if (!TryResolveMetaPlayerRuntimeActiveSurfaceConfig(out activeSurfaceConfig) || activeSurfaceConfig == null)
            return;

        if (!string.Equals(activeSurfaceConfig.controlFamilyId ?? "", "meta_ui_video_player", StringComparison.OrdinalIgnoreCase))
            return;

        string controlSurfaceInstanceId = BuildMetaProofHostedControlSurfaceInstanceId(hostAtomUid);
        if (string.IsNullOrEmpty(controlSurfaceInstanceId))
            return;

        InnerPieceInstanceRecord controlInstance;
        FAInnerPieceControlSurfaceData controlSurface;
        if (!TryResolveMetaProofControlSurfaceInstance(controlSurfaceInstanceId, out controlInstance, out controlSurface)
            || controlInstance == null
            || controlSurface == null)
        {
            return;
        }

        string controlsNodeId = controlSurface.surfaceNodeId ?? "";
        GameObject controlsSurfaceObject = ResolveInnerPieceNodeObject(controlInstance, controlsNodeId);
        if (controlsSurfaceObject == null)
            return;

        hostedSlot.mediaTargetObject = controlsSurfaceObject;
        hostedSlot.mediaTargetUsesNormalizedRect = false;
        hostedSlot.mediaTargetNormalizedRect = new Rect(0f, 0f, 1f, 1f);
        hostedSlot.screenSurfaceNodeId = string.IsNullOrEmpty(activeSurfaceConfig.videoSurfaceNodeId)
            ? controlsNodeId
            : activeSurfaceConfig.videoSurfaceNodeId;

        if (activeSurfaceConfig.hasVideoRect)
        {
            // For the hosted product path, the stable target is the authored panel surface plus
            // the exported video rect. Targeting the raw video node directly made the movie peel
            // off from the surrounding Meta panel and bypass the established fit/crop flow.
            hostedSlot.mediaTargetUsesNormalizedRect = true;
            hostedSlot.mediaTargetNormalizedRect = new Rect(
                activeSurfaceConfig.videoRectX,
                activeSurfaceConfig.videoRectY,
                activeSurfaceConfig.videoRectWidth,
                activeSurfaceConfig.videoRectHeight);
        }
        else
        {
            GameObject videoSurfaceObject = ResolveInnerPieceNodeObject(controlInstance, hostedSlot.screenSurfaceNodeId);
            if (videoSurfaceObject != null)
            {
                hostedSlot.mediaTargetObject = videoSurfaceObject;
                hostedSlot.mediaTargetUsesNormalizedRect = false;
                hostedSlot.mediaTargetNormalizedRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        // The embedded Meta player region already faces the same side as the visible panel.
        // Only the older placeholder screen slab needs the emergency Y-180 front-face flip.
        hostedSlot.forceOperatorFacingFrontFace = false;
    }

    private string ResolveHostedPlayerHostAtomUidFromTargetInstanceId(string targetInstanceId)
    {
        if (!IsHostedPlayerInstanceId(targetInstanceId))
            return "";

        return targetInstanceId.Substring(HostedPlayerInstanceIdPrefix.Length);
    }

    private void ApplyHostedPlayerPlaceholderVisualState(HostedPlayerSurfaceContract contract)
    {
        if (contract == null || contract.hostAtom == null)
            return;

        if (!ShouldBootstrapHostedPlayerScaffold(contract.hostAtom))
            return;

#if FRAMEANGEL_CUA_PLAYER
        // The standalone CUA product path needs a visible host-owned screen basis even
        // before the final control surface is promoted. Hiding both quads makes the attach
        // path look dead when the host scaffold is actually present and bound.
        SetHostedPlayerNodeRendererEnabled(contract.screenSurfaceObject, true);
        SetHostedPlayerNodeRendererEnabled(contract.disconnectSurfaceObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.screenBodyObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.screenGlassObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.controlSurfaceObject, contract.controlSurfaceObject != null);
#else
        SetHostedPlayerNodeRendererEnabled(contract.screenSurfaceObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.disconnectSurfaceObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.screenBodyObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.screenGlassObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.controlSurfaceObject, false);
#endif
    }

    private void ApplyHostedScreenCoreBackdropPresentation(
        HostedPlayerSurfaceContract contract,
        PlayerScreenBindingRecord binding)
    {
        if (contract == null
            || binding == null
            || binding.runtimeMediaSurfaceObject == null
            || !string.Equals(binding.screenContractVersion ?? "", HostedPlayerScreenCoreContractVersion, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(ExtractJsonArgString(binding.debugJson, "attachMode"), "runtime_overlay_quad", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!binding.backdropTransformCaptured)
        {
            List<Renderer> capturedRenderers = new List<Renderer>();
            List<bool> capturedStates = new List<bool>();

            CaptureHostedBackdropRendererStates(contract.disconnectSurfaceObject, capturedRenderers, capturedStates);
            CaptureHostedBackdropRendererStates(contract.screenBodyObject, capturedRenderers, capturedStates);

            binding.backdropTransform = null;
            binding.backdropTransformCaptured = true;
            binding.backdropRenderers = capturedRenderers.ToArray();
            binding.backdropRendererStates = capturedStates.ToArray();
        }

        CaptureAndHideHostedScreenSurface(contract, binding);

        // The runtime overlay now owns its own rear backing quad. Leave the authored
        // disconnect/body slabs out of the live presentation path so repeated next/rebind
        // events cannot drag a stale helper slab back into view behind the media plane.
        SetHostedPlayerNodeRendererEnabled(contract.disconnectSurfaceObject, false);
        SetHostedPlayerNodeRendererEnabled(contract.screenBodyObject, false);
    }

    private void CaptureAndHideHostedScreenSurface(
        HostedPlayerSurfaceContract contract,
        PlayerScreenBindingRecord binding)
    {
        if (contract == null
            || binding == null
            || contract.screenSurfaceObject == null
            || (binding.hiddenShellRenderers != null && binding.hiddenShellRenderers.Length > 0))
        {
            return;
        }

        Renderer[] renderers = contract.screenSurfaceObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length <= 0)
            return;

        Transform runtimeSurfaceTransform = binding.runtimeMediaSurfaceObject != null
            ? binding.runtimeMediaSurfaceObject.transform
            : null;

        List<Renderer> capturedRenderers = new List<Renderer>(renderers.Length);
        List<bool> capturedStates = new List<bool>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (runtimeSurfaceTransform != null
                && renderer.transform != null
                && (renderer.transform == runtimeSurfaceTransform
                    || renderer.transform.IsChildOf(runtimeSurfaceTransform)))
            {
                continue;
            }

            capturedRenderers.Add(renderer);
            capturedStates.Add(renderer.enabled);
            renderer.enabled = false;
        }

        binding.hiddenShellRenderers = capturedRenderers.ToArray();
        binding.hiddenShellRendererStates = capturedStates.ToArray();
    }

    private void CaptureHostedBackdropRendererStates(
        GameObject nodeObject,
        List<Renderer> capturedRenderers,
        List<bool> capturedStates)
    {
        if (nodeObject == null || capturedRenderers == null || capturedStates == null)
            return;

        Renderer[] renderers = nodeObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length <= 0)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            capturedRenderers.Add(renderer);
            capturedStates.Add(renderer.enabled);
        }
    }

    private void SetHostedPlayerNodeRendererEnabled(GameObject nodeObject, bool enabled)
    {
        if (nodeObject == null)
            return;

        Renderer[] renderers = nodeObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    private GameObject FindHostedPlayerNodeObject(Transform root, string nodeId)
    {
        if (root == null || string.IsNullOrEmpty(nodeId))
            return null;

        Queue<Transform> pending = new Queue<Transform>();
        pending.Enqueue(root);
        while (pending.Count > 0)
        {
            Transform current = pending.Dequeue();
            if (current == null)
                continue;

            if (string.Equals(current.name ?? "", nodeId, StringComparison.OrdinalIgnoreCase))
                return current.gameObject;

            for (int i = 0; i < current.childCount; i++)
                pending.Enqueue(current.GetChild(i));
        }

        return null;
    }

    private bool TryBuildHostedPlayerScreenPlane(out FAInnerPiecePlaneData plane, out string errorMessage)
    {
        plane = new FAInnerPiecePlaneData();
        errorMessage = "";

        HostedPlayerSurfaceContract contract;
        if (!TryResolveHostedPlayerSurfaceContract(out contract, out errorMessage) || contract == null)
            return false;

        if (!TryBuildInnerPiecePlaneData(contract.screenSurfaceObject, out plane))
        {
            errorMessage = "hosted player screen plane could not be resolved";
            return false;
        }

        return true;
    }

    private Renderer[] ResolveHostedPlayerScreenRenderers()
    {
        HostedPlayerSurfaceContract contract;
        string ignoredError;
        if (!TryResolveHostedPlayerSurfaceContract(out contract, out ignoredError) || contract == null || contract.screenSurfaceObject == null)
            return new Renderer[0];

        return contract.screenSurfaceObject.GetComponentsInChildren<Renderer>(true);
    }
}
