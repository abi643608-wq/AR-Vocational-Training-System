using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class SIHFireHazard : MonoBehaviour
{
    [SerializeField, Range(.5f, 3f)] private float fireSize = 1.5f;
    [Tooltip("Seconds of accurate held spray needed to extinguish. Independent of sweep completion; increase for slower practice passes.")]
    [SerializeField, Min(.1f)] private float requiredSpraySeconds = 5f;
    public float RequiredSpraySeconds => requiredSpraySeconds;
    [SerializeField, Min(.05f)] private float baseTargetRadius = .35f;
    [SerializeField] private Vector3 originalScale = Vector3.one * .3f;
    [SerializeField] private UnityEvent onExtinguished = new UnityEvent();
    public event Action<SIHFireHazard> Extinguished;
    public UnityEvent OnExtinguished => onExtinguished;
    public float Health { get; private set; } = 1;
    public bool IsExtinguished => Health <= 0;
    public float FireSize => fireSize;
    public float TargetRadius => baseTargetRadius * fireSize;
    public float EquipmentClearance => Mathf.Max(1.25f, .75f * fireSize + .25f);
    public Vector3 SurfacePosition { get; private set; }
    public Vector3 SurfaceNormal { get; private set; } = Vector3.up;
    public Vector3 TargetCenter => SurfacePosition + SurfaceNormal * (.08f * fireSize);
    private ParticleSystem[] systems;
    private float[] rates, sizes, volumes, intensities;
    private AudioSource[] audioSources;
    private Light[] lights;
    private float fade;
    private bool initialized;
    private ParticleSystemRenderer[] particleRenderers;
    private float[] emissionIntensities;
    private float[] particleLightIntensities;
    private MaterialPropertyBlock properties;

    private void Awake() { Capture(); ResetFire(); }
    private void Capture()
    {
        if (initialized) return;
        initialized = true;
        systems = GetComponentsInChildren<ParticleSystem>(true);
        rates = new float[systems.Length]; sizes = new float[systems.Length]; particleLightIntensities=new float[systems.Length];
        for (int i=0;i<systems.Length;i++) { rates[i]=systems[i].emission.rateOverTimeMultiplier; sizes[i]=systems[i].main.startSizeMultiplier; particleLightIntensities[i]=systems[i].lights.intensityMultiplier; }
        particleRenderers=GetComponentsInChildren<ParticleSystemRenderer>(true); emissionIntensities=new float[particleRenderers.Length]; properties=new MaterialPropertyBlock();
        for(int i=0;i<particleRenderers.Length;i++){var m=particleRenderers[i].sharedMaterial; emissionIntensities[i]=m!=null&&m.HasProperty("_EmissionIntensity")?m.GetFloat("_EmissionIntensity"):-1;}
        audioSources=GetComponentsInChildren<AudioSource>(true); volumes=new float[audioSources.Length];
        for(int i=0;i<volumes.Length;i++)volumes[i]=audioSources[i].volume;
        lights=GetComponentsInChildren<Light>(true); intensities=new float[lights.Length];
        for(int i=0;i<intensities.Length;i++)intensities[i]=lights[i].intensity;
    }
    public void Ground(Pose surface)
    {
        SurfacePosition=surface.position; SurfaceNormal=surface.rotation*Vector3.up;
        // Keep the logical root at the measured surface, independent of legacy placement clearance.
        transform.SetPositionAndRotation(surface.position, surface.rotation);
        transform.localScale=originalScale*fireSize;
    }
    public bool TryAim(Ray ray, out Vector3 point)
    {
        var plane=new Plane(SurfaceNormal,TargetCenter);
        if(!plane.Raycast(ray,out float distance) || distance > 8f) { point=ray.GetPoint(4); return false; }
        point=ray.GetPoint(distance);
        return (point-TargetCenter).sqrMagnitude <= TargetRadius*TargetRadius;
    }
    public void ApplySpray(float elapsedSeconds, bool accuratelyAimed)
    {
        if (!isActiveAndEnabled || IsExtinguished || !accuratelyAimed || elapsedSeconds<=0 || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)) return;
        Health=Mathf.Max(0,Health-elapsedSeconds/Mathf.Max(.1f,requiredSpraySeconds));
        if(Health<.00001f)Health=0;
        ApplyIntensity();
        if(IsExtinguished)
        {
            // Stop emitting, retain particles and the logical root for later training steps.
            foreach(var ps in systems) ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
            onExtinguished.Invoke(); Extinguished?.Invoke(this);
        }
    }
    private void Update()
    {
        if(!IsExtinguished)return;
        fade=Mathf.MoveTowards(fade,0,Time.deltaTime/.8f);
        for(int i=0;i<audioSources.Length;i++) { audioSources[i].volume=volumes[i]*fade; if(fade==0)audioSources[i].Stop(); }
        for(int i=0;i<lights.Length;i++)lights[i].intensity=intensities[i]*fade;
    }
    private void ApplyIntensity()
    {
        for(int i=0;i<systems.Length;i++)
        {
            var emission=systems[i].emission; emission.rateOverTimeMultiplier=rates[i]*Health;
            var particleLights=systems[i].lights; particleLights.intensityMultiplier=particleLightIntensities[i]*Health;
            var main=systems[i].main; main.startSizeMultiplier=sizes[i]*Mathf.Lerp(.35f,1,Health);
        }
        for(int i=0;i<audioSources.Length;i++)audioSources[i].volume=volumes[i]*Mathf.Lerp(.15f,1,Health);
        for(int i=0;i<lights.Length;i++)lights[i].intensity=intensities[i]*Health;
        SetVisualIntensity(Mathf.Lerp(.2f,1,Health));
        fade=.15f;
    }
    private void SetVisualIntensity(float factor)
    {
        for(int i=0;i<particleRenderers.Length;i++)
        {
            if(emissionIntensities[i]<0)continue;
            particleRenderers[i].GetPropertyBlock(properties);
            properties.SetFloat("_EmissionIntensity",emissionIntensities[i]*factor);
            particleRenderers[i].SetPropertyBlock(properties);
        }
    }
    public void ResetFire()
    {
        Capture(); Health=1; fade=1; SetVisualIntensity(1); transform.localScale=originalScale*fireSize;
        for(int i=0;i<systems.Length;i++)
        {
            var ps=systems[i]; ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.startSizeMultiplier=sizes[i];
            var emission=ps.emission; emission.rateOverTimeMultiplier=rates[i];
            var particleLights=ps.lights;particleLights.intensityMultiplier=particleLightIntensities[i];
            if(ps.gameObject.activeInHierarchy)ps.Play(false);
        }
        for(int i=0;i<audioSources.Length;i++){audioSources[i].volume=volumes[i];if(audioSources[i].clip!=null)audioSources[i].Play();}
        for(int i=0;i<lights.Length;i++)lights[i].intensity=intensities[i];
    }
}



