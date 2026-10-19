using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public static class SIHUIInput
{
    private static readonly List<RaycastResult> hits = new List<RaycastResult>();
    // Explicit raycast avoids depending on EventSystem's Update order.
    public static bool Blocks(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        foreach (var hit in hits) if (hit.module is UnityEngine.UI.GraphicRaycaster) return true;
        return false;
    }
}
