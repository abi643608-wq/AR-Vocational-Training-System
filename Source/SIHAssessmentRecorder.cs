using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>Evidence recorder only. The existing aim/route controllers still own exercise state.</summary>
[DisallowMultipleComponent]
public sealed class SIHAssessmentRecorder : MonoBehaviour
{
    [SerializeField] private SIHAimSprayController exercise;
    [SerializeField] private SIHExtinguisherSelection equipment;
    [SerializeField] private ARPlacementController placement;
    [SerializeField] private SIHTrainingRoute route;
    [SerializeField] private SIHSweepAssessment sweep;
    [SerializeField] private SIHScenarioInteraction scenarioInteraction;
    public void ConfigureScenario(SIHScenarioInteraction value)
    {
        scenarioInteraction=value;
        // Scenario metadata is retained; retired questions never change practical scoring.
    }
    [SerializeField] private SIHAssessmentRules rules=new SIHAssessmentRules();
    [SerializeField,Min(.5f)] private float checkpointSeconds=2;
    public SIHAttemptRecord Current { get; private set; }
    public SIHAttemptRecord LastResult { get; private set; }
    public bool ShowingResults { get; private set; }
    public string StorageError=>store?.LastError;
    public string StoragePath=>store?.DirectoryPath;
    public bool Running=>Current!=null&&Current.status=="In progress";
    public event Action ResultsChanged;
    private SIHAttemptStore store;
    private bool focused=true,paused,previousClockEligible;
    private float sinceSave,missedEpisode;
    private bool mistakeLatched,subscribed;
    private SIHExtinguisher selected;
    public void Configure(SIHAimSprayController c,SIHExtinguisherSelection e,ARPlacementController p,SIHTrainingRoute r,SIHSweepAssessment s)
    {exercise=c;equipment=e;placement=p;route=r;sweep=s;}
    private void EnsureStore(){if(store==null)store=new SIHAttemptStore(Path.Combine(Application.persistentDataPath,"SIHAssessments","v1"));}
    private void OnEnable()
    {
        if(exercise==null||equipment==null||placement==null||route==null)return;
        EnsureStore();route.ExerciseStarted+=BeginAttempt;route.ExerciseEnding+=Interrupt;
        route.ExitIdentificationCompleted+=OnExit;route.WaypointReached+=OnStop;route.AssemblyReached+=OnAssembly;
        equipment.ExtinguisherSelected+=OnSelected;equipment.PinRemoved+=OnPin;
        placement.Placed+=OnPlaced;placement.PlacementReset+=OnReset;
        exercise.SprayMeasured+=OnSpray;exercise.FireExtinguished+=OnFire;exercise.StateChanged+=OnState;
        if(sweep!=null)sweep.SweepCompleted+=OnSweep;
        ARSession.stateChanged+=OnTracking;subscribed=true;
        if(scenarioInteraction!=null)scenarioInteraction.AnswerSubmitted+=OnAnswer;
    }
    private void Start()
    {
        EnsureStore();var recovered=store.RecoverInterrupted();var history=store.LoadHistory();LastResult=recovered??(history.Count>0?history[0]:null);
        if(recovered!=null){ShowingResults=true;ResultsChanged?.Invoke();}
    }
    private void OnDisable()
    {
        Interrupt("Assessment recorder disabled");
        if(scenarioInteraction!=null)scenarioInteraction.AnswerSubmitted-=OnAnswer;
        if(!subscribed)return;
        route.ExerciseStarted-=BeginAttempt;route.ExerciseEnding-=Interrupt;route.ExitIdentificationCompleted-=OnExit;route.WaypointReached-=OnStop;route.AssemblyReached-=OnAssembly;
        equipment.ExtinguisherSelected-=OnSelected;equipment.PinRemoved-=OnPin;placement.Placed-=OnPlaced;placement.PlacementReset-=OnReset;
        exercise.SprayMeasured-=OnSpray;exercise.FireExtinguished-=OnFire;exercise.StateChanged-=OnState;if(sweep!=null)sweep.SweepCompleted-=OnSweep;ARSession.stateChanged-=OnTracking;subscribed=false;
    }
    public void BeginAttempt()
    {
        Interrupt("New attempt started");EnsureStore();ShowingResults=false;selected=null;
        var practical=JsonUtility.FromJson<SIHAssessmentRules>(JsonUtility.ToJson(rules));
        practical.version="prototype-3-practical";practical.questionScoring=false;practical.hazardPoints=practical.typePoints=0;
        Current=SIHAttemptRecord.Create(practical,sweep!=null?sweep.RequiredPasses:0,route.Count);
        Current.items.RemoveAll(item=>item.id=="hazard"||item.id=="type");
        CaptureScenario();
        previousClockEligible=false;sinceSave=0;ResetMiss();CheckEvidence();
        if(placement.HasPlacedObject)OnPlaced(placement.PlacementSurfacePose);
        SaveCheckpoint();ResultsChanged?.Invoke();
    }
    private bool Eligible=>focused&&!paused&&exercise!=null&&exercise.isActiveAndEnabled&&route!=null&&route.Reliable&&!route.IsSetup;
    private void Update()
    {
        if(!Running)return;
        bool eligible=Eligible;
        // First frame after recovery is discarded so background/relocalization gaps cannot add time.
        AdvanceClock(Time.unscaledDeltaTime,eligible&&previousClockEligible);previousClockEligible=eligible;
        if(!eligible)ResetMiss();CheckEvidence();
        if(sweep!=null&&sweep.isActiveAndEnabled)Current.sweepPasses=Mathf.Max(Current.sweepPasses,sweep.CompletedPasses);
        sinceSave+=Time.unscaledDeltaTime;if(sinceSave>=checkpointSeconds)SaveCheckpoint();
    }
    public void AdvanceClock(float elapsed,bool eligible)
    {if(Running&&eligible&&float.IsFinite(elapsed)&&elapsed>0&&elapsed<=.5f)Current.activeSeconds+=elapsed;}
    private void CheckEvidence()
    {
        if(!Running)return;
        Current.Evaluate();
        if(Current.rules.questionScoring&&(scenarioInteraction==null||!scenarioInteraction.isActiveAndEnabled||scenarioInteraction.Scenario==null||!scenarioInteraction.Scenario.Validate(out _))){if(!Current.hazardAnswered)Current.Missing("hazard","Scenario evidence unavailable.");if(!Current.equipmentChosen)Current.Missing("type","Scenario choice evidence unavailable.");}
        if(equipment==null||!equipment.isActiveAndEnabled)Current.Missing("pin","Equipment/pin observation unavailable during the attempt.");
        if(equipment!=null&&equipment.SpawnedExtinguisher!=null&&(equipment.SafetyPin==null||!equipment.SafetyPin.isActiveAndEnabled||!equipment.SafetyPin.IsConfigured))Current.Missing("pin","Configured safety-pin observation unavailable.");
        if(placement!=null&&placement.HasPlacedObject&&(exercise.Hazard==null||!exercise.Hazard.isActiveAndEnabled)){Current.Missing("aim","Fire-base target unavailable.");Current.Missing("fire","Fire-health source unavailable.");}
        if(exercise==null||!exercise.isActiveAndEnabled){Current.Missing("aim","Spray measurement source unavailable.");Current.Missing("fire","Fire completion source unavailable.");}
        if(sweep==null||!sweep.isActiveAndEnabled)Current.Missing("sweep","Sweep assessment source unavailable.");
        if(route==null||!route.isActiveAndEnabled){Current.Missing("exit","Exit observation unavailable.");Current.Missing("route","Route observation unavailable.");}
    }
    private void OnPlaced(Pose _)
    {
        if(!Running)return;
        CaptureScenario();
        var hazard=placement.SpawnedObject!=null?placement.SpawnedObject.GetComponent<SIHFireHazard>():null;
        if(hazard==null||!hazard.isActiveAndEnabled){Current.Missing("aim","Fire-base target unavailable.");Current.Missing("fire","Fire-health evidence unavailable.");Current.Missing("sweep","Fire-base sweep target unavailable.");}
    }
    private void OnSelected(SIHExtinguisher value)
    {if(!Running||value==null)return;selected=value;Current.selectionObserved=true;if(equipment.SafetyPin==null)Current.Missing("pin","Safety-pin component unavailable.");SaveCheckpoint();}
    private void OnPin(SIHExtinguisher value)
    {
        if(!Running||Current.pinRemovedCorrectly)return;
        if(value!=null&&value==selected&&Current.selectionObserved&&equipment.IsSelected&&equipment.SafetyPin!=null&&equipment.SafetyPin.IsRemoved&&Current.totalSpraySeconds==0)Current.pinRemovedCorrectly=true;
        else Current.Missing("pin","A complete selection-before-pin event sequence was not observed.");
        SaveCheckpoint();
    }
    private void OnSpray(float elapsed,bool valid)
    {
        if(!Running||!Eligible||!float.IsFinite(elapsed)||elapsed<=0||elapsed>.25f){ResetMiss();return;}
        Current.totalSpraySeconds+=elapsed;
        if(valid){Current.validAimSpraySeconds+=elapsed;ResetMiss();}
        else {
            missedEpisode+=elapsed;
            if(!mistakeLatched&&missedEpisode>=Current.rules.missedSprayMistakeSeconds){mistakeLatched=true;Current.mistakes.Add(new SIHAssessmentAction{code="sustained_off_target_spray",description=$"Sprayed outside the valid fire-base target for at least {Current.rules.missedSprayMistakeSeconds:F1} seconds continuously.",utc=SIHAttemptRecord.UtcNow(),activeSeconds=Current.activeSeconds});SaveCheckpoint();}
        }
    }
    private void ResetMiss(){missedEpisode=0;mistakeLatched=false;}
    private void OnState(SIHAimSprayController.TrainingState state){if(state!=SIHAimSprayController.TrainingState.Spraying)ResetMiss();}
    private void OnSweep(){if(!Running)return;Current.sweepPasses=Mathf.Max(Current.sweepPasses,sweep.CompletedPasses);SaveCheckpoint();}
    private void OnFire(){if(!Running)return;Current.fireExtinguished=exercise.Hazard!=null&&exercise.Hazard.IsExtinguished;if(sweep!=null)Current.sweepPasses=Mathf.Max(Current.sweepPasses,sweep.CompletedPasses);SaveCheckpoint();}
    private void OnExit(){if(!Running)return;Current.exitIdentified=route.ExitIdentified;SaveCheckpoint();}
    private void OnStop(int index){if(!Running)return;if(index==Current.routeStops&&route.ReachedCount==index+1)Current.routeStops++;else if(index>=Current.routeStops)Current.Missing("route","Ordered stop evidence has a gap.");SaveCheckpoint();}
    private void OnAssembly()
    {
        if(!Running||!route.AssemblyPointReached)return;
        Current.orderedRouteCompleted=route.OrderedRouteCompleted;Current.assemblyReached=true;
        Finish(true,"Assembly point reached");
    }
    private void OnReset(){Interrupt("Placement reset");}
    public void Interrupt(string reason){if(Running)Finish(false,reason);ShowingResults=false;ResultsChanged?.Invoke();}
    private void Finish(bool completed,string reason)
    {
        if(!Running)return;CheckEvidence();Current.exerciseCompleted=completed;Current.status=completed?"Completed":"Interrupted";
        Current.endReason=reason;Current.endedUtc=SIHAttemptRecord.UtcNow();Current.lastUpdatedUtc=Current.endedUtc;Current.Evaluate();
        LastResult=Current;ShowingResults=completed;
        // Persist a final-state checkpoint first: recovery can finish committing if the app dies during rename.
        store.Checkpoint(Current);store.SaveFinal(Current);ResultsChanged?.Invoke();
    }
    private void SaveCheckpoint(){if(!Running)return;Current.lastUpdatedUtc=SIHAttemptRecord.UtcNow();Current.Evaluate();store.Checkpoint(Current);sinceSave=0;}
    private void OnTracking(ARSessionStateChangedEventArgs _){previousClockEligible=false;ResetMiss();SaveCheckpoint();}
    private void OnApplicationFocus(bool value){focused=value;previousClockEligible=false;if(!value){ResetMiss();SaveCheckpoint();}}
    private void OnApplicationPause(bool value){paused=value;previousClockEligible=false;if(value){ResetMiss();SaveCheckpoint();}}
    private void OnApplicationQuit(){Interrupt("Application closed");}
    public void ShowLastResult(){if(Running||LastResult==null)return;ShowingResults=true;ResultsChanged?.Invoke();}
    public void RestartExercise(){ShowingResults=false;route.RestartExercise();ResultsChanged?.Invoke();}
    public void ReconfigureRoute(){ShowingResults=false;route.ReconfigureRoute();ResultsChanged?.Invoke();}
    private void CaptureScenario(){var s=scenarioInteraction!=null?scenarioInteraction.Scenario:null;if(Current==null||s==null)return;Current.scenarioId=s.scenarioId;Current.scenarioVersion=s.version;Current.scenarioDescription=s.description;Current.scenarioJurisdiction=s.jurisdiction;Current.scenarioSources=s.sources;}
    private void OnAnswer(SIHQuestionSubmission answer){if(Running&&Current.RecordAnswer(answer))SaveCheckpoint();}
}

