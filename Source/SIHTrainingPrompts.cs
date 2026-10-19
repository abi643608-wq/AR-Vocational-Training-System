using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[DisallowMultipleComponent]
public sealed class SIHTrainingPrompts : MonoBehaviour
{
    [SerializeField] private ARPlacementController placement;
    [SerializeField] private ARPlaneManager planes;
    [SerializeField] private SIHExtinguisherSelection equipment;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private SIHAimSprayController aimSpray;
    public void ConfigureAim(SIHAimSprayController value) => aimSpray = value;
    private string lastPrompt;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    public void Configure(ARPlacementController source, ARPlaneManager manager,
        SIHExtinguisherSelection selection, TMP_Text text, RectTransform safe)
    {
        placement = source; planes = manager; equipment = selection; label = text; safeArea = safe;
    }

    private void Update()
    {
        if (label == null || placement == null || equipment == null || planes == null) return;
        string prompt;
        if (aimSpray != null && aimSpray.Prompt != null) prompt = aimSpray.Prompt;
        else if (equipment.SafetyPin != null && equipment.SafetyPin.IsRemoved) prompt = "Aim at the base of the fire.";
        else if (equipment.SafetyPin != null && equipment.SafetyPin.IsRemoving) prompt = "Removing safety pin…";
        else if (equipment.IsSelected) prompt = "Extinguisher selected — tap the highlighted pull ring";
        else if (ARSession.state != ARSessionState.SessionTracking)
            prompt = "Move your phone slowly to scan a surface";
        else if (equipment.WaitingForGround) prompt = "Tap the floor to place the extinguisher.";
        else if (placement.HasPlacedObject) prompt = "Tap the extinguisher to select it";
        else
        {
            bool found = false;
            foreach (ARPlane plane in planes.trackables)
                if (plane.trackingState == TrackingState.Tracking) { found = true; break; }
            prompt = found ? "Tap a detected surface to place the fire" :
                "Move your phone slowly to scan a surface";
        }
        if (prompt != lastPrompt) { label.text = prompt; lastPrompt = prompt; }
        Rect area = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (safeArea != null && size.x > 0 && size.y > 0 &&
            (area != lastSafeArea || size != lastScreenSize))
        {
            safeArea.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            safeArea.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            lastSafeArea = area;
            lastScreenSize = size;
        }
    }
}


