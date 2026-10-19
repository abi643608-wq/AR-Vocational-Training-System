using UnityEngine;

/// <summary>Physical approach/dwell guard. Has no knowledge of taps, scoring, or navigation.</summary>
[DisallowMultipleComponent]
public sealed class SIHRouteArrival : MonoBehaviour
{
    [SerializeField, Min(.2f)] private float arrivalRadius=.6f;
    [SerializeField, Min(.2f)] private float dwellSeconds=.75f;
    [SerializeField, Min(.2f)] private float stableTrackingSeconds=1.5f;
    [SerializeField, Min(.05f)] private float leaveMargin=.15f;
    [SerializeField, Min(.1f)] private float maximumStep=.65f;
    [SerializeField, Min(.1f)] private float maximumSpeed=3f;
    [SerializeField, Min(.05f)] private float maximumSampleGap=.3f;
    public float Radius=>arrivalRadius;
    public float OuterRadius=>arrivalRadius+leaveMargin;
    public static float HorizontalDistance(Vector3 camera,Vector3 target)=>Vector3.ProjectOnPlane(camera-target,Vector3.up).magnitude;
    public bool Stable=>stable>=stableTrackingSeconds;
    public bool Armed { get; private set; }
    public float DwellProgress=>Mathf.Clamp01(dwell/Mathf.Max(.2f,dwellSeconds));
    public string Status { get; private set; }="Tracking paused";
    private Vector3 previous;
    private bool hasPrevious,latched;
    private float stable,dwell;
    public void ResetTarget(){Pause();latched=false;}
    public void Pause(){stable=dwell=0;Armed=false;hasPrevious=false;Status="Tracking paused";}
    public bool Observe(Vector3 cameraPosition,Vector3 target,Vector3 normal,bool reliable,float elapsed)
    {
        if(latched)return false;
        if(!reliable||!float.IsFinite(elapsed)||elapsed<=0||elapsed>maximumSampleGap||!Finite(cameraPosition)||!Finite(target)){Pause();return false;}
        var position=Vector3.ProjectOnPlane(cameraPosition,Vector3.up);
        if(!hasPrevious){previous=position;hasPrevious=true;Status="Waiting for stable tracking";return false;}
        float step=Vector3.Distance(position,previous);previous=position;
        if(step>maximumStep || step/elapsed>maximumSpeed){Pause();Status="Tracking jump — waiting for stable tracking";return false;}
        stable+=elapsed;
        if(!Stable){Status=$"Waiting for stable tracking ({stable:F1}/{stableTrackingSeconds:F1} s)";return false;}
        float distance=HorizontalDistance(cameraPosition,target);
        if(distance>OuterRadius){Armed=false;dwell=0;Status=$"Approach marker — {distance:F2} m (enter {arrivalRadius:F2} m)";return false;}
        if(!Armed&&distance>arrivalRadius){Status=$"Approach marker — {distance:F2} m (enter {arrivalRadius:F2} m)";return false;}
        // Enter at the inner radius; retain the dwell through small boundary jitter.
        Armed=true;
        dwell+=elapsed;Status=$"Hold position — {Mathf.Min(dwell,dwellSeconds):F2}/{dwellSeconds:F2} s";
        if(dwell<dwellSeconds)return false;
        latched=true;Status="Reached";return true;
    }
    private static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
}


