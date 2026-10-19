using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>Owns equipment spawning and tap selection independently of fire placement.</summary>
[DisallowMultipleComponent]
public sealed class SIHExtinguisherSelection : MonoBehaviour
{
    [SerializeField] private SIHScenarioInteraction scenarioInteraction;
    public void ConfigureScenario(SIHScenarioInteraction value)=>scenarioInteraction=value;
    [SerializeField] private ARPlacementController placement;
    [SerializeField] private Camera arCamera;
    [SerializeField] private SIHExtinguisher extinguisherPrefab;
    [Tooltip("Offset from the tapped surface to the prefab root; zero for the current base-aligned model.")]
    [SerializeField] private float prefabBaseOffset = 0f;
    [SerializeField] private UnityEvent onExtinguisherSelected = new UnityEvent();
    [SerializeField] private UnityEvent onPinRemoved = new UnityEvent();

    public event Action<SIHExtinguisher> ExtinguisherSelected;
    public event Action SelectionCleared;
    public event Action<SIHExtinguisher> PinRemoved;
    public UnityEvent OnExtinguisherSelected => onExtinguisherSelected;
    public UnityEvent OnPinRemoved => onPinRemoved;
    public SIHSafetyPin SafetyPin { get; private set; }
    public SIHExtinguisher SpawnedExtinguisher { get; private set; }
    public bool IsSelected { get; private set; }
    public bool WaitingForGround => placement != null && placement.HasPlacedObject && SpawnedExtinguisher == null;
    public Pose EquipmentSurfacePose { get; private set; }
    private GameObject sourceFire;
    private bool waitingForTouchRelease;
    private int spawnFrame;
    private ARRaycastManager equipmentRaycasts;
    private ARPlaneManager equipmentPlanes;
    private readonly List<ARRaycastHit> groundHits = new List<ARRaycastHit>();


    public void Configure(ARPlacementController source, Camera camera, SIHExtinguisher prefab)
    {
        placement = source;
        arCamera = camera;
        extinguisherPrefab = prefab;
    }

    private void OnEnable()
    {
        if (placement == null || arCamera == null || extinguisherPrefab == null)
        {
            Debug.LogError("Run Tools > SIH > Setup Extinguisher Selection to assign equipment references.", this);
            enabled = false;
            return;
        }
        placement.Placed += BeginManualPlacement;
        placement.PlacementReset += ClearEquipment;
        equipmentRaycasts = placement.GetComponent<ARRaycastManager>();
        equipmentPlanes = placement.GetComponent<ARPlaneManager>();
        // Reconcile a reset/replacement that happened while this component was disabled.
        if (sourceFire != placement.SpawnedObject || !placement.HasPlacedObject)
            ClearEquipment();
        if (placement.HasPlacedObject && SpawnedExtinguisher == null)
            BeginManualPlacement(placement.PlacementSurfacePose);
    }

    private void OnDisable()
    {
        if (placement == null) return;
        placement.Placed -= BeginManualPlacement;
        placement.PlacementReset -= ClearEquipment;
    }

    private void OnDestroy()
    {
        if (SpawnedExtinguisher != null)
            Destroy(SpawnedExtinguisher.gameObject);
    }

    private void BeginManualPlacement(Pose surface)
    {
        // The fire-placement gesture must end before a second tap can place equipment.
        if (SpawnedExtinguisher != null) return;
        sourceFire = placement.SpawnedObject;
        waitingForTouchRelease = true;
        spawnFrame = Time.frameCount;
    }

    public bool TryPlaceExtinguisher(Vector2 screenPosition)
    {
        if (!isActiveAndEnabled || !WaitingForGround || waitingForTouchRelease ||
            SIHUIInput.Blocks(screenPosition) || equipmentRaycasts == null || equipmentPlanes == null ||
            ARSession.state != ARSessionState.SessionTracking) return false;
        if (!equipmentRaycasts.Raycast(screenPosition, groundHits, TrackableType.Planes)) return false;
        foreach (var hit in groundHits)
        {
            var plane = equipmentPlanes.GetPlane(hit.trackableId);
            if (plane == null || plane.trackingState != TrackingState.Tracking ||
                plane.alignment != PlaneAlignment.HorizontalUp ||
                Vector3.Dot(plane.transform.up, Vector3.up) < .985f) continue;
            EquipmentSurfacePose = hit.pose;
            SpawnedExtinguisher = Instantiate(extinguisherPrefab,
                hit.pose.position + hit.pose.rotation * Vector3.up * prefabBaseOffset, hit.pose.rotation);
            SafetyPin = SpawnedExtinguisher.GetComponent<SIHSafetyPin>();
            if (SafetyPin != null) SafetyPin.PinRemoved += HandlePinRemoved;
            sourceFire = placement.SpawnedObject;
            IsSelected = false;
            spawnFrame = Time.frameCount;
            waitingForTouchRelease = true;
            Physics.SyncTransforms();
            GetComponent<SIHAimSprayController>()?.SetRouteState(SIHAimSprayController.TrainingState.SelectEquipment);
            return true;
        }
        return false;
    }
    private void Update()
    {
        // Consume the entire placing gesture, including any second finger.
        if (waitingForTouchRelease)
        {
            if (Input.touchCount == 0 && Time.frameCount > spawnFrame)
                waitingForTouchRelease = false;
            return;
        }
        if (Input.touchCount == 0) return;
        Touch touch = Input.GetTouch(0); // Same legacy touch approach as ARPlacementController.
        if (touch.phase == TouchPhase.Began && !SIHUIInput.Blocks(touch.position))
        {
            if (WaitingForGround) TryPlaceExtinguisher(touch.position);
            else if (!IsSelected) TrySelect(touch.position);
            else TryRemovePin(touch.position);
        }
    }

    public bool TrySelect(Vector2 screenPosition)
    {
        if (SIHUIInput.Blocks(screenPosition) || !isActiveAndEnabled || IsSelected || waitingForTouchRelease ||
            SpawnedExtinguisher == null || !placement.HasPlacedObject ||
            ARSession.state != ARSessionState.SessionTracking)
            return false;
        // Query only the equipment collider so AR plane meshes cannot steal the hit.
        if (!SpawnedExtinguisher.HitTest(arCamera.ScreenPointToRay(screenPosition))) return false;
        IsSelected = true; // Set before callbacks to make reentrant/repeated taps harmless.
        GetComponent<SIHAimSprayController>()?.SetRouteState(SIHAimSprayController.TrainingState.PullPin);
        SpawnedExtinguisher.SetSelected(true);
        if (SafetyPin != null) SafetyPin.SetArmed(true);
        // Selection and pin removal must be two separate gestures, independent of Update order.
        waitingForTouchRelease = true;
        spawnFrame = Time.frameCount;
        Debug.Log("Extinguisher selected", this);
        onExtinguisherSelected.Invoke();
        ExtinguisherSelected?.Invoke(SpawnedExtinguisher);
        return true;
    }

    public bool TryRemovePin(Vector2 screenPosition)
    {
        if (SIHUIInput.Blocks(screenPosition) || !isActiveAndEnabled || !IsSelected || waitingForTouchRelease || SafetyPin == null ||
            ARSession.state != ARSessionState.SessionTracking) return false;
        return SafetyPin.TryBeginRemoval(arCamera.ScreenPointToRay(screenPosition));
    }

    private void HandlePinRemoved(SIHSafetyPin pin)
    {
        if (pin != SafetyPin || SpawnedExtinguisher == null) return;
        Debug.Log("Safety pin removed", this);
        onPinRemoved.Invoke();
        PinRemoved?.Invoke(SpawnedExtinguisher);
    }

    private void ClearEquipment()
    {
        if (SafetyPin != null) SafetyPin.PinRemoved -= HandlePinRemoved;
        SafetyPin = null;
        if (SpawnedExtinguisher != null)
        {
            SpawnedExtinguisher.gameObject.SetActive(false);
            Destroy(SpawnedExtinguisher.gameObject);
        }
        SpawnedExtinguisher = null;
        sourceFire = null;
        EquipmentSurfacePose = default;

        IsSelected = false;
        waitingForTouchRelease = true;
        SelectionCleared?.Invoke();
    }
}



