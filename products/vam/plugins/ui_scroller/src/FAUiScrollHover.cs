using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FAUiScrollHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    internal static FAUiScrollHover Current;

    internal GameObject activeUiObject;
    internal ScrollRect scrollRect;
    internal Scrollbar scrollbar;
    internal RectTransform hitRectTransform;
    internal string description;
    internal bool isPointerOver;
    internal float lastPointerEventAt = -1000f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        lastPointerEventAt = Time.unscaledTime;
        Current = this;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        lastPointerEventAt = Time.unscaledTime;
    }

    public void OnDisable()
    {
        isPointerOver = false;
        if (ReferenceEquals(Current, this))
            Current = null;
    }

    public void OnDestroy()
    {
        isPointerOver = false;
        if (ReferenceEquals(Current, this))
            Current = null;
    }
}
