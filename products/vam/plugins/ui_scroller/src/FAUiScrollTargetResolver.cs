using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal sealed class FAUiScrollTargetResolver
{
    private const float HoverLatchSeconds = 0.35f;

    internal sealed class ScrollTarget
    {
        internal GameObject activeUiObject;
        internal ScrollRect scrollRect;
        internal Scrollbar scrollbar;
        internal RectTransform hitRectTransform;
        internal string description;

        internal bool IsValid()
        {
            return activeUiObject != null
                && ((scrollRect != null && scrollRect.gameObject != null)
                    || (scrollbar != null && scrollbar.gameObject != null));
        }
    }

    private readonly HashSet<int> instrumentedRoots = new HashSet<int>();
    private SuperController.ActiveUI currentActiveUi = SuperController.ActiveUI.None;
    private float nextRescanAt = 0f;
    private ScrollTarget lastResolvedTarget;
    private float lastResolvedTargetAt = -1000f;

    internal ScrollTarget Resolve(bool force)
    {
        SuperController sc = SuperController.singleton;
        if (sc == null)
        {
            currentActiveUi = SuperController.ActiveUI.None;
            FAUiScrollHover.Current = null;
            instrumentedRoots.Clear();
            return null;
        }

        SuperController.ActiveUI activeUi = sc.activeUI;
        float now = Time.unscaledTime;
        if (force || activeUi != currentActiveUi || now >= nextRescanAt)
        {
            currentActiveUi = activeUi;
            InstrumentActiveUiRoots(sc, activeUi);
            nextRescanAt = now + 0.20f;
        }

        FAUiScrollHover currentHover = FAUiScrollHover.Current;
        ScrollTarget target = TryBuildTargetFromHover(currentHover, now);
        if (target != null)
        {
            lastResolvedTarget = target;
            lastResolvedTargetAt = now;
            return target;
        }

        if (lastResolvedTarget != null
            && lastResolvedTarget.IsValid()
            && (IsPointerInsideTarget(lastResolvedTarget) || (now - lastResolvedTargetAt) <= HoverLatchSeconds))
            return lastResolvedTarget;

        return null;
    }

    private void InstrumentActiveUiRoots(SuperController sc, SuperController.ActiveUI activeUi)
    {
        List<Transform> roots = new List<Transform>();
        AddRoots(roots, ResolveCandidateRoots(sc, activeUi));
        AddCanvasRoots(roots);

        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            if (root == null || root.gameObject == null)
                continue;

            int rootId = root.gameObject.GetInstanceID();
            if (instrumentedRoots.Contains(rootId))
                continue;

            AttachHoverComponents(root);
            instrumentedRoots.Add(rootId);
        }
    }

    private static void AddRoots(List<Transform> roots, Transform[] incoming)
    {
        if (roots == null || incoming == null)
            return;

        for (int i = 0; i < incoming.Length; i++)
        {
            Transform root = incoming[i];
            if (root == null || root.gameObject == null)
                continue;
            if (!roots.Contains(root))
                roots.Add(root);
        }
    }

    private static void AddCanvasRoots(List<Transform> roots)
    {
        if (roots == null)
            return;

        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null
                || canvas.gameObject == null
                || !canvas.isActiveAndEnabled
                || !canvas.gameObject.activeInHierarchy)
                continue;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                continue;

            Transform root = canvas.transform;
            if (!roots.Contains(root))
                roots.Add(root);
        }
    }

    private static Transform[] ResolveCandidateRoots(SuperController sc, SuperController.ActiveUI activeUi)
    {
        switch (activeUi)
        {
            case SuperController.ActiveUI.MainMenu:
            case SuperController.ActiveUI.MainMenuOnly:
                return BuildRootArray(sc.mainMenuUI, sc.worldUI, sc.topWorldUI);
            case SuperController.ActiveUI.SelectedOptions:
            case SuperController.ActiveUI.MultiButtonPanel:
            case SuperController.ActiveUI.Custom:
                return BuildRootArray(sc.sceneControlUI, sc.sceneControlUIAlt, sc.worldUI, sc.topWorldUI);
            case SuperController.ActiveUI.PackageManager:
                return BuildRootArray(sc.packageManagerUI, sc.worldUI, sc.topWorldUI);
            case SuperController.ActiveUI.PackageBuilder:
                return BuildRootArray(sc.packageBuilderUI, sc.worldUI, sc.topWorldUI);
            case SuperController.ActiveUI.OnlineBrowser:
                return BuildRootArray(sc.onlineBrowserUI, sc.worldUI, sc.topWorldUI);
            case SuperController.ActiveUI.EmbeddedScenePanel:
                return BuildRootArray(sc.embeddedSceneUI, sc.worldUI, sc.topWorldUI);
            default:
                return BuildRootArray(sc.sceneControlUI, sc.sceneControlUIAlt, sc.mainMenuUI, sc.worldUI, sc.topWorldUI);
        }
    }

    private static Transform[] BuildRootArray(params Transform[] roots)
    {
        return roots ?? new Transform[0];
    }

    private static ScrollTarget TryBuildTargetFromHover(FAUiScrollHover currentHover, float now)
    {
        if (currentHover == null)
            return null;

        bool hoverFresh = currentHover.isPointerOver
            || ((now - currentHover.lastPointerEventAt) <= HoverLatchSeconds);
        if (!hoverFresh)
            return null;

        ScrollTarget target = new ScrollTarget();
        target.activeUiObject = currentHover.activeUiObject != null ? currentHover.activeUiObject : currentHover.gameObject;
        target.scrollRect = currentHover.scrollRect;
        target.scrollbar = currentHover.scrollbar;
        target.hitRectTransform = currentHover.hitRectTransform;
        target.description = currentHover.description;
        return target.IsValid() ? target : null;
    }

    private static void AttachHoverComponents(Transform root)
    {
        if (root == null)
            return;

        ScrollRect[] scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scrollRect = scrollRects[i];
            if (!IsUsable(scrollRect) || HasUsableParentScrollRect(scrollRect.transform))
                continue;

            string description = root.gameObject.name + "/ScrollRect:" + scrollRect.gameObject.name;
            EnsureRaycastSurface(scrollRect.gameObject);
            BindHoverTarget(scrollRect.gameObject, root.gameObject, scrollRect, null, ResolveHitRect(scrollRect, null), description);

            if (scrollRect.viewport != null && scrollRect.viewport.gameObject != null)
            {
                EnsureRaycastSurface(scrollRect.viewport.gameObject);
                BindHoverTarget(scrollRect.viewport.gameObject, root.gameObject, scrollRect, null, ResolveHitRect(scrollRect, null), description);
            }

            Graphic[] graphics = scrollRect.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
            {
                Graphic graphic = graphics[g];
                if (graphic == null || graphic.gameObject == null)
                    continue;

                BindHoverTarget(graphic.gameObject, root.gameObject, scrollRect, null, ResolveHitRect(scrollRect, null), description);
            }
        }

        Scrollbar[] scrollbars = root.GetComponentsInChildren<Scrollbar>(true);
        for (int i = 0; i < scrollbars.Length; i++)
        {
            Scrollbar scrollbar = scrollbars[i];
            if (!IsUsable(scrollbar) || HasUsableParentScrollRect(scrollbar.transform))
                continue;

            if (scrollbar.GetComponent<ScrollRect>() != null)
                continue;

            string description = root.gameObject.name + "/Scrollbar:" + scrollbar.gameObject.name;
            EnsureRaycastSurface(scrollbar.gameObject);
            BindHoverTarget(scrollbar.gameObject, root.gameObject, null, scrollbar, ResolveHitRect(null, scrollbar), description);

            Graphic[] graphics = scrollbar.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
            {
                Graphic graphic = graphics[g];
                if (graphic == null || graphic.gameObject == null)
                    continue;

                BindHoverTarget(graphic.gameObject, root.gameObject, null, scrollbar, ResolveHitRect(null, scrollbar), description);
            }
        }
    }

    private static void BindHoverTarget(
        GameObject targetObject,
        GameObject activeUiObject,
        ScrollRect scrollRect,
        Scrollbar scrollbar,
        RectTransform hitRectTransform,
        string description)
    {
        if (targetObject == null)
            return;

        FAUiScrollHover hover = targetObject.GetComponent<FAUiScrollHover>();
        if (hover == null)
            hover = targetObject.AddComponent<FAUiScrollHover>();
        hover.activeUiObject = activeUiObject;
        hover.scrollRect = scrollRect;
        hover.scrollbar = scrollbar;
        hover.hitRectTransform = hitRectTransform;
        hover.description = description;
    }

    private static RectTransform ResolveHitRect(ScrollRect scrollRect, Scrollbar scrollbar)
    {
        if (scrollRect != null)
        {
            if (scrollRect.viewport != null)
                return scrollRect.viewport;

            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
                return scrollRectTransform;
        }

        if (scrollbar != null)
            return scrollbar.GetComponent<RectTransform>();

        return null;
    }

    private static bool IsPointerInsideTarget(ScrollTarget target)
    {
        if (target == null || target.hitRectTransform == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(target.hitRectTransform, Input.mousePosition, null);
    }

    private static void EnsureRaycastSurface(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        Graphic graphic = targetObject.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
            return;
        }

        Image image = targetObject.GetComponent<Image>();
        if (image == null)
            image = targetObject.AddComponent<Image>();

        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
    }

    private static bool IsUsable(ScrollRect scrollRect)
    {
        return scrollRect != null
            && scrollRect.gameObject != null
            && scrollRect.enabled
            && scrollRect.vertical;
    }

    private static bool HasUsableParentScrollRect(Transform start)
    {
        if (start == null)
            return false;

        Transform current = start.parent;
        while (current != null)
        {
            ScrollRect parent = current.GetComponent<ScrollRect>();
            if (IsUsable(parent))
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool IsUsable(Scrollbar scrollbar)
    {
        return scrollbar != null
            && scrollbar.gameObject != null
            && scrollbar.enabled;
    }
}
