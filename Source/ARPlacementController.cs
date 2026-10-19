using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacementController : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;

    [Header("Object To Place")]
    [SerializeField] private GameObject placementPrefab;
    [Tooltip("Metres above the hit plane. Use 0 for the original Cube test.")]
    [SerializeField, Min(0f)] private float placementHeightOffset = 0f;

    [Header("Diagnostics")]
    [SerializeField] private bool logPlacementEvents = true;

    private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private readonly List<Renderer> planeRenderers = new List<Renderer>();
    private GameObject spawnedObject;
    private bool hasPlacedObject;

    public bool HasPlacedObject => hasPlacedObject;
    public GameObject SpawnedObject => spawnedObject;
    // The unmodified hit pose is the surface, before the fire-only height offset.
    public Pose PlacementSurfacePose { get; private set; }
    public event System.Action<Pose> Placed;
    public event System.Action PlacementReset;
    [SerializeField] private SIHTrainingRoute trainingRoute;
    public void ConfigureRoute(SIHTrainingRoute value)=>trainingRoute=value;
    private int resetFrame = -1;

    private void Awake()
    {
        // Both managers already live on XR Origin in the working scene.
        if (raycastManager == null)
            raycastManager = GetComponent<ARRaycastManager>();
        if (planeManager == null)
            planeManager = GetComponent<ARPlaneManager>();

        if (raycastManager == null || planeManager == null || placementPrefab == null)
        {
            Debug.LogError("Assign Raycast Manager, Plane Manager and Placement Prefab " +
                           "on ARPlacementController.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (planeManager == null)
            return;

        planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        if (hasPlacedObject)
            SetPlaneVisualsHidden(true);
    }

    private void OnDisable()
    {
        if (planeManager == null)
            return;

        planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        if (hasPlacedObject)
            SetPlaneVisualsHidden(false);
    }

    private void Update()
    {
        // Later training taps must never move the hazard or create a second one.
        // Keep this flag even if another component destroys/extinguishes the fire.
        if ((trainingRoute != null && !trainingRoute.CanPlaceFire) || hasPlacedObject || Time.frameCount == resetFrame || Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began || SIHUIInput.Blocks(touch.position))
            return;

        if (!raycastManager.Raycast(touch.position, hits, TrackableType.Planes))
        {
            if (logPlacementEvents)
                Debug.Log("AR TAP BUT NO PLANE HIT: scan a surface and try again.", this);
            return;
        }

        // Keep the broad Planes raycast; validate tracked horizontal ground separately.
        foreach (var hit in hits)
        {
            var plane = planeManager.GetPlane(hit.trackableId);
            if (plane == null || plane.trackingState != TrackingState.Tracking ||
                plane.alignment != PlaneAlignment.HorizontalUp ||
                Vector3.Dot(plane.transform.up, Vector3.up) < .985f) continue;
            SpawnAtSurface(hit.pose);
            return;
        }
    }

    private void SpawnAtSurface(Pose surface)
    {
        PlacementSurfacePose = surface;
        Vector3 position = surface.position + surface.rotation * Vector3.up * placementHeightOffset;
        spawnedObject = Instantiate(placementPrefab, position, surface.rotation);
        hasPlacedObject = true;
        SetPlaneVisualsHidden(true);
        Placed?.Invoke(surface);
        if (logPlacementEvents) Debug.Log("AR OBJECT SPAWNED; plane visuals hidden, tracking still active.", this);
    }

    // Reuse only the already measured pose in the same validated AR session.
    public bool RestartPlacedObject()
    {
        if (!Application.isPlaying || !hasPlacedObject) return false;
        Pose surface = PlacementSurfacePose;
        ResetPlacement();
        SpawnAtSurface(surface);
        return true;
    }
    private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> changes)
    {
        if (!hasPlacedObject)
            return;

        foreach (ARPlane plane in changes.added)
            SetRenderersHidden(plane.gameObject, true);
        foreach (ARPlane plane in changes.updated)
            SetRenderersHidden(plane.gameObject, true);
    }

    private void SetPlaneVisualsHidden(bool hidden)
    {
        foreach (ARPlane plane in planeManager.trackables)
            SetRenderersHidden(plane.gameObject, hidden);

        // This project uses a scene object as the plane template. Hide that
        // object's visuals too, but never modify a prefab asset or deactivate it.
        GameObject template = planeManager.planePrefab;
        if (template != null && template.scene.IsValid())
            SetRenderersHidden(template, hidden);
    }

    private void SetRenderersHidden(GameObject planeObject, bool hidden)
    {
        planeRenderers.Clear();
        planeObject.GetComponentsInChildren<Renderer>(true, planeRenderers);
        foreach (Renderer renderer in planeRenderers)
        {
            if (renderer is MeshRenderer || renderer is LineRenderer)
            {
                // ARPlaneMeshVisualizer can toggle renderer.enabled as tracking
                // changes. This independent rendering flag keeps it hidden.
                renderer.forceRenderingOff = hidden;
            }
        }
    }

    // Can be connected to a future Reset button or called by TrainingManager.
    [ContextMenu("Reset Placement")]
    public void ResetPlacement()
    {
        if (!Application.isPlaying)
            return;

        if (spawnedObject != null)
            Destroy(spawnedObject);
        spawnedObject = null;
        hasPlacedObject = false;
        PlacementSurfacePose = default;
        resetFrame = Time.frameCount;
        PlacementReset?.Invoke();

        if (planeManager != null)
            SetPlaneVisualsHidden(false);
    }
}




