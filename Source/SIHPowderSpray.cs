using UnityEngine;

/// <summary>Swivels the existing nozzle at its hose joint and emits powder from its tip.</summary>
[DisallowMultipleComponent]
public sealed class SIHPowderSpray : MonoBehaviour
{
    [SerializeField] private Material powderMaterial;
    private Transform equipment;
    private Transform[] nozzleParts;
    private ParticleSystem powder;
    private static readonly Vector3 joint = new Vector3(.136f,.190f,.014f);
    private static readonly Vector3 originalTip = new Vector3(.1195f,.120f,.013f);
    public Vector3 Origin => powder!=null ? powder.transform.position : Vector3.zero;
    public Vector3 Direction => powder!=null ? powder.transform.forward : Vector3.forward;
    public void Configure(Material material) => powderMaterial=material;
    public void Bind(SIHExtinguisher extinguisher)
    {
        Clear(); equipment=extinguisher.transform;
        nozzleParts=new[]{equipment.Find("Discharge Nozzle"),equipment.Find("Nozzle Rim"),equipment.Find("Nozzle Aperture")};
        var go=new GameObject("SIH Powder Stream"); go.transform.SetParent(transform,false);
        powder=go.AddComponent<ParticleSystem>(); powder.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=powder.main;main.playOnAwake=false;main.loop=true;main.maxParticles=100;
        main.simulationSpace=ParticleSystemSimulationSpace.World; main.startSpeed=6;main.startLifetime=.4f;
        main.startSize=new ParticleSystem.MinMaxCurve(.06f,.11f); main.startColor=new Color(.92f,.90f,.82f,.65f);
        main.gravityModifier=.015f;
        var emission=powder.emission;emission.rateOverTime=150;
        var shape=powder.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=5;shape.radius=.009f;
        var size=powder.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.7f,1,2.3f));
        var color=powder.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=powderMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    public void Aim(Vector3 point)
    {
        if(equipment==null || powder==null)return;
        var localDirection=equipment.InverseTransformPoint(point)-joint;
        if(localDirection.sqrMagnitude<.001f)return;
        var rotation=Quaternion.FromToRotation((originalTip-joint).normalized,localDirection.normalized);
        foreach(var part in nozzleParts) if(part!=null){part.localRotation=rotation;part.localPosition=joint-rotation*joint;}
        Vector3 tip=equipment.TransformPoint(joint+rotation*(originalTip-joint));
        Vector3 direction=(point-tip).normalized;
        powder.transform.SetPositionAndRotation(tip,Quaternion.LookRotation(direction));
        var main=powder.main; main.startLifetime=Mathf.Clamp(Vector3.Distance(tip,point)/6f,.03f,1.2f);
    }
    public void SetSpraying(bool active)
    {
        if(powder==null)return;
        if(active){if(!powder.isPlaying)powder.Play();}
        else powder.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    public void Clear()
    {
        SetSpraying(false);
        if(nozzleParts!=null)foreach(var part in nozzleParts)if(part!=null){part.localPosition=Vector3.zero;part.localRotation=Quaternion.identity;}
        if(powder!=null)Destroy(powder.gameObject);
        powder=null;equipment=null;nozzleParts=null;
    }
    private void OnDisable()=>SetSpraying(false);
    private void OnDestroy()=>Clear();
}

