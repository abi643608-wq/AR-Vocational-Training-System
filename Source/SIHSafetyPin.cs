using System;
using System.Collections;
using UnityEngine;

/// <summary>One-shot pin removal. Input and training progression remain with the equipment controller.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SIHExtinguisher))]
public sealed class SIHSafetyPin : MonoBehaviour
{
    [SerializeField] private Transform safetyPin;
    [SerializeField] private Transform pullRing;
    [SerializeField, Min(.1f)] private float pullDuration = .65f;
    [SerializeField, Min(.02f)] private float pullDistance = .12f;
    [SerializeField, Min(.01f)] private float tapRadius = .035f;
    public event Action<SIHSafetyPin> PinRemoved;
    public bool IsRemoving { get; private set; }
    public bool IsRemoved { get; private set; }
    public bool IsConfigured => safetyPin != null && pullRing != null;
    public Vector3 TargetPosition => ringRenderer != null ? ringRenderer.bounds.center : transform.position;
    private SIHExtinguisher owner;
    private Renderer ringRenderer;
    private Vector3 pinStart, ringStart;
    private MaterialPropertyBlock ringProperties;
    private bool captured;

    public void Configure(Transform pin, Transform ring)
    {
        safetyPin = pin; pullRing = ring;
        Capture();
    }

    private void Awake() => Capture();

    private void Capture()
    {
        owner = GetComponent<SIHExtinguisher>();
        if (!IsConfigured) return;
        pinStart = safetyPin.localPosition; ringStart = pullRing.localPosition;
        ringRenderer = pullRing.GetComponent<Renderer>();
        ringProperties = new MaterialPropertyBlock();
        captured = true;
    }

    public void SetArmed(bool armed)
    {
        if (ringRenderer == null || IsRemoved) return;
        // Instance-only tint: never change the shared metallic material.
        ringProperties.Clear();
        if (armed) ringProperties.SetColor("_BaseColor", new Color(.95f,.66f,.18f));
        ringRenderer.SetPropertyBlock(ringProperties);
    }

    public bool TryBeginRemoval(Ray ray)
    {
        if (!isActiveAndEnabled || !captured || owner == null || !owner.IsSelected ||
            IsRemoving || IsRemoved || !IsConfigured) return false;
        // Ring is on the front (-Z) of this design; don't select it through the vessel from behind.
        if (Vector3.Dot(ray.direction, transform.forward) <= .05f) return false;
        Vector3 direction = ray.direction.normalized;
        float distance = Vector3.Dot(TargetPosition - ray.origin, direction);
        if (distance < 0 || distance > 20f) return false;
        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z));
        if ((ray.origin + direction * distance - TargetPosition).sqrMagnitude > tapRadius*tapRadius*scale*scale) return false;
        IsRemoving = true;
        StartCoroutine(RemovePin());
        return true;
    }

    private IEnumerator RemovePin()
    {
        SetArmed(false);
        float elapsed = 0;
        while (elapsed < pullDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/pullDuration));
            Vector3 offset = Vector3.back * (pullDistance*t);
            safetyPin.localPosition = pinStart + offset;
            pullRing.localPosition = ringStart + offset;
            yield return null;
        }
        // Both parts leave together; hide after extraction rather than dropping a physics object.
        safetyPin.gameObject.SetActive(false);
        pullRing.gameObject.SetActive(false);
        IsRemoving = false;
        IsRemoved = true; // Latch before notifying listeners.
        PinRemoved?.Invoke(this);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (!IsRemoving || !captured || !IsConfigured) return;
        safetyPin.localPosition = pinStart; pullRing.localPosition = ringStart;
        IsRemoving = false;
        SetArmed(owner != null && owner.IsSelected);
    }
}
