using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

[DisallowMultipleComponent]
public sealed class SIHAimSprayController : MonoBehaviour
{
    public enum TrainingState { Locked, Aim, Spraying, Extinguished, RouteSetup, FindExit, Evacuate, ScenarioComplete, IdentifyHazard, ChooseExtinguisher, SelectEquipment, PullPin, WaitingForEquipment }
    [SerializeField] private SIHScenarioInteraction scenarioInteraction;
    public void ConfigureScenario(SIHScenarioInteraction value)=>scenarioInteraction=value;
    [SerializeField] private ARPlacementController placement;
    [SerializeField] private SIHExtinguisherSelection equipment;
    [SerializeField] private Camera arCamera;
    [SerializeField] private SIHPowderSpray powder;
    [SerializeField] private GameObject aimUI;
    [SerializeField] private TMP_Text reticle;
    [SerializeField] private TMP_Text aimStatus;
    [SerializeField] private UnityEngine.UI.Image sprayButton;
    [SerializeField] private UnityEvent onFireExtinguished=new UnityEvent();
    public event Action<TrainingState> StateChanged;
    public event Action FireExtinguished;
    // Emitted from the damage update, before extinction can finish an assessment.
    public event Action<float,bool> SprayMeasured;
    public UnityEvent OnFireExtinguished=>onFireExtinguished;
    public TrainingState State { get; private set; }
    public SIHFireHazard Hazard { get; private set; }
    public bool IsOnTarget { get; private set; }
    public bool IsSpraying=>State==TrainingState.Spraying;
    [SerializeField] private SIHTrainingRoute trainingRoute;
    public SIHTrainingRoute TrainingRoute=>trainingRoute;
    public void ConfigureRoute(SIHTrainingRoute value)=>trainingRoute=value;
    public void SetRouteState(TrainingState value){StopSpray();SetState(value);}
    public string Prompt
    {
        get
        {
            if(trainingRoute!=null&&trainingRoute.Prompt!=null)return trainingRoute.Prompt;


            if(State==TrainingState.WaitingForEquipment||State==TrainingState.SelectEquipment)return equipment!=null&&equipment.SpawnedExtinguisher!=null?"Tap the extinguisher to select it":"Tap the floor to place the extinguisher.";
            if(State==TrainingState.Extinguished)return sweep!=null&&!sweep.IsComplete?"Fire extinguished; sweep technique not demonstrated.":"Fire extinguished";
            if(State!=TrainingState.Aim&&!IsSpraying)return null;
            if(!TrackingReliable)return "Tracking paused — resume when stable.";
            if(sweep!=null&&sweep.IsComplete)return sweep.Feedback;
            if(!IsOnTarget)return "Aim at the base.";
            return sweep!=null?sweep.Feedback:"Aim at the base.";
        }
    }
    [SerializeField] private SIHSweepAssessment sweep;
    public Camera AimCamera => arCamera;
    public SIHSweepAssessment Sweep => sweep;
    public Vector3 SprayHitPosition { get; private set; }
    public bool TrackingReliable => ARSession.state==ARSessionState.SessionTracking && ARSession.notTrackingReason==UnityEngine.XR.ARSubsystems.NotTrackingReason.None;
    public void ConfigureSweep(SIHSweepAssessment value) => sweep=value;
    private bool focused=true, paused;
    private SIHExtinguisher bound;
    public void Configure(ARPlacementController p,SIHExtinguisherSelection e,Camera camera,SIHPowderSpray effect,GameObject ui,TMP_Text crosshair,TMP_Text status,UnityEngine.UI.Image button)
    {placement=p;equipment=e;arCamera=camera;powder=effect;aimUI=ui;reticle=crosshair;aimStatus=status;sprayButton=button;}
    private void OnEnable()
    {
        if(placement==null||equipment==null||arCamera==null||powder==null)return;
        if(sweep==null)sweep=GetComponent<SIHSweepAssessment>();
        ARSession.stateChanged+=OnTrackingStateChanged;
        placement.Placed+=OnPlaced;placement.PlacementReset+=ResetTraining;
        equipment.PinRemoved+=OnPinRemoved;equipment.SelectionCleared+=ResetTraining;
        if(placement.HasPlacedObject)OnPlaced(placement.PlacementSurfacePose);
        if(equipment.SafetyPin!=null&&equipment.SafetyPin.IsRemoved)OnPinRemoved(equipment.SpawnedExtinguisher);
    }
    private void OnDisable()
    {
        ARSession.stateChanged-=OnTrackingStateChanged;
        StopSpray();if(aimUI!=null)aimUI.SetActive(false);
        if(placement!=null){placement.Placed-=OnPlaced;placement.PlacementReset-=ResetTraining;}
        if(equipment!=null){equipment.PinRemoved-=OnPinRemoved;equipment.SelectionCleared-=ResetTraining;}
        if(Hazard!=null)Hazard.Extinguished-=OnExtinguished;
    }
    private void OnPlaced(Pose pose)
    {
        if(Hazard!=null)Hazard.Extinguished-=OnExtinguished;
        Hazard=placement.SpawnedObject!=null?placement.SpawnedObject.GetComponent<SIHFireHazard>():null;
        if(Hazard==null)return;
        Hazard.Ground(pose);Hazard.Extinguished+=OnExtinguished;
        if(Hazard.IsExtinguished)SetState(TrainingState.Extinguished);else SetState(TrainingState.SelectEquipment);
    }
    private void OnPinRemoved(SIHExtinguisher selected)
    {
        if(selected==null||Hazard==null||!equipment.IsSelected||equipment.SafetyPin==null||!equipment.SafetyPin.IsRemoved||Hazard.IsExtinguished)return;
        if(bound!=selected){powder.Bind(selected);bound=selected;}
        if(sweep!=null)sweep.BeginAttempt(Hazard,arCamera.transform.right);
        SetState(TrainingState.Aim);
    }
    private bool CanSpray=>isActiveAndEnabled&&focused&&!paused&&equipment!=null&&equipment.isActiveAndEnabled&&equipment.IsSelected&&equipment.SafetyPin!=null&&equipment.SafetyPin.IsRemoved&&Hazard!=null&&!Hazard.IsExtinguished&&TrackingReliable;
    public bool BeginSpray()
    {
        if(State!=TrainingState.Aim||!CanSpray)return false;
        UpdateAim();SetState(TrainingState.Spraying);powder.SetSpraying(true);return true;
    }
    public void StopSpray()
    {if(sweep!=null)sweep.Interrupt();if(powder!=null)powder.SetSpraying(false);if(IsSpraying)SetState(TrainingState.Aim);}
    private void UpdateAim()
    {
        if(Hazard==null||arCamera==null){IsOnTarget=false;return;}
        IsOnTarget=Hazard.TryAim(arCamera.ViewportPointToRay(new Vector3(.5f,.5f,0)),out Vector3 point);
        SprayHitPosition=point;
        IsOnTarget &= TrackingReliable;
        if(bound!=null)powder.Aim(point);
    }
    private void Update()
    {
        if(State==TrainingState.WaitingForEquipment&&equipment!=null&&equipment.SpawnedExtinguisher!=null)SetState(TrainingState.SelectEquipment);
        UpdateAim();
        if(IsSpraying && (!CanSpray || (sweep!=null && Time.unscaledDeltaTime>sweep.MaximumSampleGap)))StopSpray();
        // Assess the same geometric hit used to aim the nozzle, before this frame can extinguish the fire.
        if(sweep!=null)sweep.Sample(SprayHitPosition,IsSpraying&&CanSpray&&IsOnTarget,Time.unscaledDeltaTime);
        if(IsSpraying){SprayMeasured?.Invoke(Time.unscaledDeltaTime,IsOnTarget);Hazard.ApplySpray(Time.deltaTime,IsOnTarget);}
        bool visible=State==TrainingState.Aim||IsSpraying;
        if(aimUI!=null)aimUI.SetActive(visible);
        if(reticle!=null)reticle.color=IsOnTarget?new Color(.2f,1,.5f):new Color(1,.65f,.18f);
        if(aimStatus!=null)aimStatus.text=Prompt;
        if(sprayButton!=null)sprayButton.color=IsSpraying?new Color(.65f,.18f,.06f):new Color(.12f,.28f,.40f);
    }
    private void OnExtinguished(SIHFireHazard hazard)
    {
        if(hazard!=Hazard||State==TrainingState.Extinguished||State==TrainingState.FindExit||State==TrainingState.Evacuate||State==TrainingState.ScenarioComplete)return;
        if(sweep!=null)sweep.EndAttempt();
        powder.SetSpraying(false);SetState(TrainingState.Extinguished);
        onFireExtinguished.Invoke();FireExtinguished?.Invoke();
    }
    private void SetState(TrainingState state)
    {
        // Old serialized callbacks/restart paths cannot re-enter retired question stages.
        if(state==TrainingState.IdentifyHazard||state==TrainingState.ChooseExtinguisher||state==TrainingState.SelectEquipment)
            state=equipment!=null&&equipment.SpawnedExtinguisher!=null?TrainingState.SelectEquipment:TrainingState.WaitingForEquipment;
        if(State==state)return;State=state;StateChanged?.Invoke(state);
    }
    private void ResetTraining()
    {
        StopSpray();if(Hazard!=null)Hazard.Extinguished-=OnExtinguished;
        if(sweep!=null)sweep.ResetAssessment();
        Hazard=null;bound=null;IsOnTarget=false;SprayHitPosition=Vector3.zero;if(powder!=null)powder.Clear();
        SetState(trainingRoute!=null&&!trainingRoute.Confirmed?TrainingState.RouteSetup:TrainingState.Locked);if(aimUI!=null)aimUI.SetActive(false);
    }
    private void OnTrackingStateChanged(ARSessionStateChangedEventArgs args){if(!TrackingReliable)StopSpray();}
    public void ResetPlacement(){if(trainingRoute!=null)trainingRoute.RestartExercise();else{StopSpray();placement.ResetPlacement();}}
    private void OnApplicationFocus(bool value){focused=value;if(!value)StopSpray();}
    private void OnApplicationPause(bool value){paused=value;if(value)StopSpray();}
}








