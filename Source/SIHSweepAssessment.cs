using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Prototype coverage criterion, not an official training standard. Never modifies fire health.</summary>
[DisallowMultipleComponent]
public sealed class SIHSweepAssessment : MonoBehaviour
{
    [SerializeField, Min(1)] private int requiredAlternatingPasses = 2;
    [Tooltip("Minimum travel as a fraction of the full fire-base diameter.")]
    [SerializeField, Range(.1f,1f)] private float minimumTravel = .55f;
    [Tooltip("Movement back from the furthest point required to confirm a reversal, in base diameters.")]
    [SerializeField, Range(.01f,.3f)] private float reversalThreshold = .08f;
    [SerializeField, Range(0,.2f)] private float smoothingSeconds = .06f;
    [SerializeField, Min(0)] private float minimumPassSeconds = .45f;
    [Tooltip("Long sample gaps discard the partial pass; they are not trainee errors.")]
    [SerializeField, Min(.05f)] private float maximumSampleGap = .25f;
    [Tooltip("Reject discontinuous hit jumps, measured in base diameters per sample.")]
    [SerializeField, Range(.05f,1f)] private float maximumSampleJump = .25f;
    [SerializeField] private UnityEvent onSweepCompleted = new UnityEvent();
    public event Action SweepCompleted;
    public UnityEvent OnSweepCompleted => onSweepCompleted;
    public int CompletedPasses { get; private set; }
    public bool IsComplete { get; private set; }
    public int RequiredPasses => Mathf.Max(1,requiredAlternatingPasses);
    public int CurrentDirection => sampling ? direction : 0;
    public float SmoothedPosition => filtered;
    public string SampleStatus { get; private set; } = "Waiting for aim";
    public Vector3 LeftEndpoint => hazard!=null?hazard.transform.TransformPoint(localCenter-localAxis*localWidth*.5f):Vector3.zero;
    public Vector3 RightEndpoint => hazard!=null?hazard.transform.TransformPoint(localCenter+localAxis*localWidth*.5f):Vector3.zero;
    public float NormalizeHit(Vector3 hit) => hazard!=null?Vector3.Dot(hazard.transform.InverseTransformPoint(hit)-localCenter,localAxis)/Mathf.Max(.0001f,localWidth):0;
    public bool HasAttempt => hazard != null;
    public float MaximumSampleGap => Mathf.Max(.05f,maximumSampleGap);
    public Vector3 WorldSweepAxis => hazard != null ? hazard.transform.TransformDirection(localAxis).normalized : Vector3.zero;
    public string Feedback => IsComplete ? "Sweep technique complete." : CompletedPasses>0 ? "Good sweep—keep going." : "Sweep slowly from side to side.";
    private SIHFireHazard hazard;
    private Vector3 localAxis, localCenter;
    private float localWidth;
    private bool sampling, legCredited, ended;
    private float filtered, previousRaw, origin, extreme, legTime;
    private int direction, lastCompletedDirection;

    public void BeginAttempt(SIHFireHazard fire, Vector3 initialRight)
    {
        if(fire==null || hazard==fire)return; // Re-enabling or resuming never redefines the axis.
        ResetAssessment(); hazard=fire;
        Vector3 axis=Vector3.ProjectOnPlane(initialRight,fire.SurfaceNormal).normalized;
        if(axis.sqrMagnitude<.01f)axis=Vector3.Cross(fire.SurfaceNormal,fire.transform.forward).normalized;
        if(axis.sqrMagnitude<.01f)axis=fire.transform.right;
        localAxis=fire.transform.InverseTransformDirection(axis).normalized;
        localCenter=fire.transform.InverseTransformPoint(fire.TargetCenter);
        localWidth=2*fire.TargetRadius/Mathf.Max(.0001f,fire.transform.TransformVector(localAxis).magnitude);
    }
    public void Sample(Vector3 actualHit, bool eligible, float elapsed)
    {
        if(!isActiveAndEnabled || hazard==null || ended || IsComplete)return;
        if(!eligible || elapsed<=0 || elapsed>MaximumSampleGap || float.IsNaN(elapsed)) { Interrupt(); SampleStatus=!eligible?"Not spraying / invalid aim": "Sample time gap"; return; }
        // Validate the supplied hit against the actual target, not camera orientation.
        Vector3 offset=actualHit-hazard.TargetCenter;
        if(!float.IsFinite(actualHit.x)||!float.IsFinite(actualHit.y)||!float.IsFinite(actualHit.z)||
            Mathf.Abs(Vector3.Dot(offset,hazard.SurfaceNormal))>.025f || offset.sqrMagnitude>hazard.TargetRadius*hazard.TargetRadius+.0001f)
        {Interrupt();SampleStatus="Outside base target";return;}
        float raw=NormalizeHit(actualHit);
        if(!sampling){Seed(raw);SampleStatus="New segment";return;}
        if(Mathf.Abs(raw-previousRaw)>maximumSampleJump){Interrupt();Seed(raw);SampleStatus="Hit jump — segment restarted";return;}
        SampleStatus="Tracking continuous hit";
        previousRaw=raw;
        float alpha=smoothingSeconds<=0?1:1-Mathf.Exp(-elapsed/smoothingSeconds);
        filtered=Mathf.Lerp(filtered,raw,alpha);legTime+=elapsed;
        if(direction==0)
        {
            if(Mathf.Abs(filtered-origin)<reversalThreshold)return;
            direction=filtered>origin?1:-1;extreme=filtered;
        }
        if(direction*(filtered-extreme)>0)extreme=filtered;
        if(direction*(extreme-filtered)>=reversalThreshold)
        {
            origin=extreme;direction=-direction;extreme=filtered;legTime=elapsed;legCredited=false;
        }
        if(!legCredited && direction*(extreme-origin)>=minimumTravel && legTime>=minimumPassSeconds &&
            (lastCompletedDirection==0 || lastCompletedDirection!=direction))
        {
            legCredited=true;lastCompletedDirection=direction;CompletedPasses++;
            if(CompletedPasses>=Mathf.Max(1,requiredAlternatingPasses))
            {
                IsComplete=true; // Latch before callbacks, including reentrant callers.
                onSweepCompleted.Invoke();SweepCompleted?.Invoke();
            }
        }
    }
    private void Seed(float position)
    {sampling=true;filtered=previousRaw=origin=extreme=position;direction=0;legTime=0;legCredited=false;}
    public void Interrupt()
    {sampling=false;direction=0;legTime=0;legCredited=false;SampleStatus="Partial pass ended";}
    public void EndAttempt(){Interrupt();ended=true;}
    public void ResetAssessment()
    {Interrupt();hazard=null;CompletedPasses=0;IsComplete=false;ended=false;lastCompletedDirection=0;localAxis=localCenter=Vector3.zero;localWidth=0;filtered=previousRaw=origin=extreme=0;SampleStatus="Waiting for aim";}
    private void OnDisable()=>Interrupt();
    private void OnValidate()
    {minimumTravel=Mathf.Clamp(minimumTravel,.1f,1);reversalThreshold=Mathf.Clamp(reversalThreshold,.01f,minimumTravel*.5f);requiredAlternatingPasses=Mathf.Max(1,requiredAlternatingPasses);}
}

