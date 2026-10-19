using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

/// <summary>Manually configured, session-only training route. No path finding or real-exit detection.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SIHTrainingRoute : MonoBehaviour
{
    public enum Kind { Exit, Waypoint, Assembly }
    public enum SetupTool { None, Exit, Waypoint, Assembly, Move }
    private sealed class Node { public Kind kind; public Vector3 position,normal; public SIHRouteMarker marker; }
    [SerializeField] private ARPlacementController placement;
    [SerializeField] private SIHAimSprayController exercise;
    [SerializeField] private XROrigin origin;
    [SerializeField] private ARSession session;
    [SerializeField] private ARRaycastManager raycasts;
    [SerializeField] private ARPlaneManager planes;
    [SerializeField] private SIHRouteArrival arrival;
    [SerializeField] private Material markerMaterial;
    [SerializeField,Min(.1f)] private float spacingMargin=.3f;
    [SerializeField,Min(.02f)] private float groundHeightTolerance=.15f;
    [SerializeField] private UnityEvent onExitIdentified=new UnityEvent();
    [SerializeField] private UnityEvent onRouteCompleted=new UnityEvent();
    [SerializeField] private UnityEvent onAssemblyReached=new UnityEvent();
    public event Action ExitIdentificationCompleted;
    public event Action<int> WaypointReached;
    public event Action RouteCompleted;
    public event Action AssemblyReached;
    public event Action ExerciseStarted;
    public event Action<string> ExerciseEnding;
    public UnityEvent OnExitIdentified=>onExitIdentified;
    public UnityEvent OnRouteCompleted=>onRouteCompleted;
    public UnityEvent OnAssemblyReached=>onAssemblyReached;
    public bool Confirmed { get; private set; }
    public bool ReviewMode { get; private set; }
    public bool ExitIdentified { get; private set; }
    public bool OrderedRouteCompleted { get; private set; }
    public bool AssemblyPointReached { get; private set; }
    public bool FireExtinguishedResult { get; private set; }
    public bool SweepCompletedResult { get; private set; }
    public int SweepPassesResult { get; private set; }
    public int ReachedCount { get; private set; }
    public int NextIndex { get; private set; }
    public int SelectedIndex { get; private set; }=-1;
    public SetupTool Tool { get; private set; }
    public string Notice { get; private set; }="Place the training exit first";
    public int Count=>nodes.Count;
    public bool IsSetup=>exercise!=null&&exercise.State==SIHAimSprayController.TrainingState.RouteSetup;
    public bool IsEvacuating=>exercise!=null&&exercise.State==SIHAimSprayController.TrainingState.Evacuate;
    public bool IsRouteStage=>IsSetup||exercise.State==SIHAimSprayController.TrainingState.FindExit||IsEvacuating||exercise.State==SIHAimSprayController.TrainingState.ScenarioComplete;
    public Camera Camera=>origin!=null?origin.Camera:null;
    public SIHRouteArrival Arrival=>arrival;
    public bool Reliable=>focused&&!paused&&exercise!=null&&exercise.TrackingReliable&&session!=null&&session.isActiveAndEnabled;
    public bool CanPlaceFire=>Confirmed&&exercise.State==SIHAimSprayController.TrainingState.Locked&&Reliable&&StartClear(Camera.transform.position);
    public string Prompt
    {
        get
        {
            if(IsSetup)return "Configure training route — "+Notice;
            if(!Confirmed&&FireExtinguishedResult)return "Training route missing — reconfigure route";
            if(exercise.State==SIHAimSprayController.TrainingState.FindExit)return Reliable?"Find and select the training exit":"Tracking paused";
            if(IsEvacuating)return !Reliable||!arrival.Stable?"Tracking paused — wait for stable tracking":"Follow the configured training route";
            if(exercise.State==SIHAimSprayController.TrainingState.ScenarioComplete)return "Assembly point reached — training scenario complete.";
            if(Confirmed&&!placement.HasPlacedObject&&!StartClear(Camera.transform.position))return "Move outside all route marker radii before placing the fire";
            return null;
        }
    }
    public string SelectedLabel=>SelectedIndex>=0&&SelectedIndex<nodes.Count?Title(SelectedIndex):"none";
    public string Overview
    {get{var text="";for(int i=0;i<nodes.Count;i++)text+=(i==SelectedIndex?"[":"")+(nodes[i].kind==Kind.Exit?"Exit":nodes[i].kind==Kind.Assembly?"Assembly":"W"+i)+(i==SelectedIndex?"]":"")+(i<nodes.Count-1?" > ":"");return text;}}
    public Vector3 NextPosition=>NextIndex<nodes.Count?World(NextIndex):Vector3.zero;
    public Vector3 GroundNormal=>nodes.Count>0?Frame.TransformDirection(nodes[0].normal).normalized:Vector3.up;
    public float NextDistance=>Camera!=null&&NextIndex<nodes.Count?SIHRouteArrival.HorizontalDistance(Camera.transform.position,NextPosition):0;
    private readonly List<Node> nodes=new List<Node>();
    private readonly List<ARRaycastHit> hits=new List<ARRaycastHit>();
    private readonly List<GameObject> arrows=new List<GameObject>();
    private Transform markerRoot;
    private Guid sessionIdentity;
    private bool haveIdentity,focused=true,paused,reconfiguring;
    private int inputBlockedFrame=-1;
    private bool started;
    private Transform Frame=>origin.TrackablesParent;
    public void Configure(ARPlacementController p,SIHAimSprayController e,XROrigin xr,ARSession s,SIHRouteArrival detector,Material material)
    {placement=p;exercise=e;origin=xr;session=s;arrival=detector;markerMaterial=material;raycasts=p.GetComponent<ARRaycastManager>();planes=p.GetComponent<ARPlaneManager>();}
    private void OnEnable()
    {
        if(exercise!=null)exercise.FireExtinguished+=OnFireExtinguished;
        if(placement!=null)placement.PlacementReset+=OnPlacementReset;
        ARSession.stateChanged+=OnSessionState;
        if(started)ReconfigureRoute();
    }
    private void Start(){started=true;ReconfigureRoute();}
    private void OnDisable()
    {
        ExerciseEnding?.Invoke("Training route disabled");
        if(exercise!=null){exercise.FireExtinguished-=OnFireExtinguished;exercise.StopSpray();}
        if(placement!=null)placement.PlacementReset-=OnPlacementReset;
        ARSession.stateChanged-=OnSessionState;
        if(arrival!=null)arrival.Pause();
        // Never retain an unverifiable alignment across component/session replacement.
        ClearRoute();
    }
    private void Update()
    {
        CheckSessionIdentity();
        bool reliable=Reliable;
        if(!reliable)arrival.Pause();
        foreach(var node in nodes)if(node.marker!=null){node.marker.gameObject.SetActive(reliable);node.marker.Face(Camera,GroundNormal);}
        foreach(var arrow in arrows)if(arrow!=null)arrow.SetActive(reliable);
        if(!reliable)return;
        if(IsEvacuating)ProcessArrival(Camera.transform.position,true,Time.unscaledDeltaTime);
        if(Time.frameCount<=inputBlockedFrame||Input.touchCount==0)return;
        var touch=Input.GetTouch(0);
        if(touch.phase!=TouchPhase.Began||SIHUIInput.Blocks(touch.position))return;
        if(IsSetup)
        {
            if(Tool!=SetupTool.None&&!ReviewMode)TryPlace(touch.position);
            else SelectMarker(touch.position);
        }
        else if(exercise.State==SIHAimSprayController.TrainingState.FindExit)IdentifyExit(touch.position);
    }
    private void CheckSessionIdentity()
    {
        if(nodes.Count==0)return;
        if(session==null||!session.isActiveAndEnabled||session.subsystem==null){InvalidateSession("AR session unavailable — place the route again");return;}
        Guid current=session.subsystem.sessionId;
        if(haveIdentity&&current!=sessionIdentity){InvalidateSession("New AR session — place the route again");return;}
    }
    private void OnSessionState(ARSessionStateChangedEventArgs args)
    {
        if(arrival!=null)arrival.Pause();
        if(nodes.Count==0)return;
        if(args.state<=ARSessionState.Ready || (args.state==ARSessionState.SessionInitializing&&sessionIdentity==Guid.Empty))
            InvalidateSession("AR alignment changed — place the route again");
    }
    public void InvalidateSession(string reason){ReconfigureRoute();Notice=reason;}
    private void EnsureMarkerRoot()
    {
        if(markerRoot!=null)return;
        markerRoot=new GameObject("SIH Session Training Route").transform;markerRoot.SetParent(Frame,false);
    }
    public void ChooseExit(){Choose(SetupTool.Exit);}
    public void ChooseWaypoint(){Choose(SetupTool.Waypoint);}
    public void ChooseAssembly(){Choose(SetupTool.Assembly);}
    private void Choose(SetupTool tool){if(!IsSetup)return;ReviewMode=false;Tool=tool;Notice="Tap a tracked horizontal floor for "+tool;inputBlockedFrame=Time.frameCount;}
    public void MoveSelected(){if(!IsSetup||SelectedIndex<0)return;Choose(SetupTool.Move);}
    public void NextSelected(){if(!IsSetup||Count==0)return;SelectedIndex=(SelectedIndex+1)%Count;Tool=SetupTool.None;Notice="Selected "+SelectedLabel;}
    public void DeleteSelected()
    {
        if(!IsSetup||SelectedIndex<0)return;
        Destroy(nodes[SelectedIndex].marker.gameObject);nodes.RemoveAt(SelectedIndex);SelectedIndex=Mathf.Min(SelectedIndex,nodes.Count-1);ReviewMode=false;Tool=SetupTool.None;RebuildArrows();Notice="Marker deleted; review the route";
    }
    public void Earlier()=>Reorder(-1);
    public void Later()=>Reorder(1);
    private void Reorder(int delta)
    {
        if(!IsSetup||SelectedIndex<0||nodes[SelectedIndex].kind!=Kind.Waypoint)return;
        int next=SelectedIndex+delta;if(next<0||next>=Count||nodes[next].kind!=Kind.Waypoint)return;
        var node=nodes[SelectedIndex];nodes[SelectedIndex]=nodes[next];nodes[next]=node;SelectedIndex=next;ReviewMode=false;RebuildArrows();
    }
    private bool TryPlace(Vector2 screen)
    {
        if(!Reliable||!raycasts.Raycast(screen,hits,TrackableType.Planes))return false;
        foreach(var hit in hits)
        {
            var plane=planes.GetPlane(hit.trackableId);
            if(plane==null||plane.trackingState!=TrackingState.Tracking||plane.alignment!=PlaneAlignment.HorizontalUp||Vector3.Dot(plane.transform.up,Vector3.up)<.985f||(hit.hitType&TrackableType.PlaneWithinPolygon)==0)continue;
            var local=plane.transform.InverseTransformPoint(hit.pose.position);
            if(!SIHEquipmentGrounding.ContainsFootprint(plane.boundary,new Vector2(local.x,local.z),.16f))continue;
            Kind kind=Tool==SetupTool.Exit?Kind.Exit:Tool==SetupTool.Assembly?Kind.Assembly:Kind.Waypoint;
            return PlaceGroundMarker(kind,hit.pose,Tool==SetupTool.Move?SelectedIndex:-1);
        }
        Notice="Scan a wider, upward-facing horizontal floor";return false;
    }
    public bool PlaceGroundMarker(Kind kind,Pose surface,int replace=-1)
    {
        if(!IsSetup||Confirmed)return false;
        if(Vector3.Dot(surface.rotation*Vector3.up,Vector3.up)<.985f){Notice="Use an upward horizontal surface";return false;}
        if(replace<0&&kind!=Kind.Waypoint)replace=nodes.FindIndex(n=>n.kind==kind);
        if(replace>=Count)return false;
        if(kind!=Kind.Exit&&!nodes.Exists(n=>n.kind==Kind.Exit)){Notice="Place the training exit first";return false;}
        if(nodes.Count>=16&&replace<0){Notice="Maximum 16 route markers";return false;}
        float separation=arrival.OuterRadius*2+spacingMargin;
        for(int i=0;i<nodes.Count;i++)if(i!=replace)
        {
            if(Mathf.Abs(Vector3.Dot(surface.position-World(i),GroundNormal))>groundHeightTolerance){Notice="Use the same ground level for this prototype";return false;}
            if(SIHRouteArrival.HorizontalDistance(surface.position,World(i))<separation){Notice="Markers need separate arrival areas";return false;}
        }
        EnsureMarkerRoot();
        if(!haveIdentity){sessionIdentity=session!=null&&session.subsystem!=null?session.subsystem.sessionId:Guid.Empty;haveIdentity=true;}
        Node node;
        if(replace>=0){node=nodes[replace];SelectedIndex=replace;}
        else
        {
            node=new Node{kind=kind};int index=kind==Kind.Exit?0:kind==Kind.Waypoint&&nodes.Exists(n=>n.kind==Kind.Assembly)?nodes.Count-1:nodes.Count;
            nodes.Insert(index,node);SelectedIndex=index;
            var go=new GameObject("Training route marker");go.transform.SetParent(markerRoot,false);node.marker=go.AddComponent<SIHRouteMarker>();
            node.marker.Build(markerMaterial,"",kind==Kind.Exit?new Color(.02f,.5f,.17f):kind==Kind.Assembly?new Color(.02f,.35f,.65f):new Color(.65f,.4f,.03f));
        }
        node.position=Frame.InverseTransformPoint(surface.position);node.normal=Frame.InverseTransformDirection(surface.rotation*Vector3.up);
        node.marker.transform.localPosition=node.position;node.marker.transform.rotation=surface.rotation;
        Tool=SetupTool.None;ReviewMode=false;Notice="Placed "+SelectedLabel+"; add waypoints or review";RebuildArrows();return true;
    }
    public void Review(){if(!IsSetup)return;ReviewMode=true;Tool=SetupTool.None;Notice=ValidateRoute(out string reason)?"Review marker order, then confirm":reason;}
    public void ConfirmFromUI()=>ConfirmRoute();
    public bool ConfirmRoute()
    {
        if(!IsSetup||!ReviewMode||!Reliable||!ValidateRoute(out string reason)){Notice="Review a complete route with stable tracking";return false;}
        if(!StartClear(Camera.transform.position)){Notice="Step outside all marker arrival areas before confirming";return false;}
        Confirmed=true;ReviewMode=false;Tool=SetupTool.None;ClearResults();exercise.SetRouteState(SIHAimSprayController.TrainingState.Locked);inputBlockedFrame=Time.frameCount;Notice="Training route confirmed — place the fire";ExerciseStarted?.Invoke();return true;
    }
    public bool ValidateRoute(out string reason)
    {
        reason="Place an exit and assembly point";
        if(nodes.Count<2||nodes[0].kind!=Kind.Exit||nodes[nodes.Count-1].kind!=Kind.Assembly)return false;
        for(int i=1;i<nodes.Count-1;i++)if(nodes[i].kind!=Kind.Waypoint)return false;
        for(int i=0;i<Count;i++)for(int j=i+1;j<Count;j++)if(SIHRouteArrival.HorizontalDistance(World(i),World(j))<arrival.OuterRadius*2+spacingMargin){reason="Markers overlap arrival areas";return false;}
        reason="";return true;
    }
    private bool StartClear(Vector3 start)
    {foreach(var node in nodes)if(Vector3.ProjectOnPlane(start-Frame.TransformPoint(node.position),GroundNormal).magnitude<=arrival.Radius+spacingMargin)return false;return true;}
    private void OnFireExtinguished()
    {
        if(FireExtinguishedResult)return;
        exercise.StopSpray();FireExtinguishedResult=true;SweepCompletedResult=exercise.Sweep!=null&&exercise.Sweep.IsComplete;SweepPassesResult=exercise.Sweep!=null?exercise.Sweep.CompletedPasses:0;
        if(!Confirmed||!ValidateRoute(out _)){Notice="Missing confirmed route";return;}
        exercise.SetRouteState(SIHAimSprayController.TrainingState.FindExit);arrival.ResetTarget();inputBlockedFrame=Time.frameCount;
    }
    public bool IdentifyExit(Vector2 screen)
    {
        if(!Reliable||!Confirmed||exercise.State!=SIHAimSprayController.TrainingState.FindExit||SIHUIInput.Blocks(screen)||Count==0)return false;
        if(!nodes[0].marker.SelectionCollider.Raycast(Camera.ScreenPointToRay(screen),out _,30))return false;
        ExitIdentified=true;NextIndex=0;arrival.ResetTarget();exercise.SetRouteState(SIHAimSprayController.TrainingState.Evacuate);
        onExitIdentified.Invoke();ExitIdentificationCompleted?.Invoke();return true;
    }
    public void ProcessArrival(Vector3 cameraPosition,bool reliable,float elapsed)
    {
        if(!Confirmed||!IsEvacuating||NextIndex>=Count)return;
        if(!arrival.Observe(cameraPosition,NextPosition,GroundNormal,reliable,elapsed))return;
        int reached=NextIndex;ReachedCount++;NextIndex++;WaypointReached?.Invoke(reached);
        if(NextIndex<Count){arrival.ResetTarget();return;}
        if(OrderedRouteCompleted)return;
        OrderedRouteCompleted=true;AssemblyPointReached=true;exercise.SetRouteState(SIHAimSprayController.TrainingState.ScenarioComplete);
        onRouteCompleted.Invoke();RouteCompleted?.Invoke();onAssemblyReached.Invoke();AssemblyReached?.Invoke();
    }
    public void RestartExercise()
    {
        if(!Confirmed){ReconfigureRoute();return;}
        if(session!=null&&session.subsystem!=null&&haveIdentity&&session.subsystem.sessionId!=sessionIdentity){InvalidateSession("New AR session — reconfigure route");return;}
        ExerciseEnding?.Invoke("Exercise restarted");
        exercise.StopSpray();
        if(!placement.RestartPlacedObject())placement.ResetPlacement();
        ClearResults();exercise.SetRouteState(placement.HasPlacedObject&&exercise.GetComponent<SIHScenarioInteraction>()!=null?SIHAimSprayController.TrainingState.IdentifyHazard:SIHAimSprayController.TrainingState.Locked);inputBlockedFrame=Time.frameCount;ExerciseStarted?.Invoke();
    }
    public void ReconfigureRoute()
    {
        if(reconfiguring)return;reconfiguring=true;
        ExerciseEnding?.Invoke("Route reconfigured or AR alignment invalidated");
        exercise.StopSpray();placement.ResetPlacement();ClearRoute();ClearResults();Tool=SetupTool.None;ReviewMode=false;SelectedIndex=-1;
        exercise.SetRouteState(SIHAimSprayController.TrainingState.RouteSetup);Notice="Place the training exit first";inputBlockedFrame=Time.frameCount;reconfiguring=false;
    }
    private void OnPlacementReset(){ClearResults();if(!reconfiguring)exercise.SetRouteState(Confirmed?SIHAimSprayController.TrainingState.Locked:SIHAimSprayController.TrainingState.RouteSetup);}
    private void ClearResults(){ExitIdentified=OrderedRouteCompleted=AssemblyPointReached=FireExtinguishedResult=SweepCompletedResult=false;SweepPassesResult=ReachedCount=NextIndex=0;arrival.ResetTarget();}
    private void ClearRoute(){Confirmed=false;nodes.Clear();arrows.Clear();if(markerRoot!=null){markerRoot.gameObject.SetActive(false);Destroy(markerRoot.gameObject);}markerRoot=null;haveIdentity=false;}
    private Vector3 World(int index)=>Frame.TransformPoint(nodes[index].position);
    private string Title(int i)=>nodes[i].kind==Kind.Exit?"TRAINING EXIT":nodes[i].kind==Kind.Assembly?"ASSEMBLY POINT":"WAYPOINT "+i;
    private void SelectMarker(Vector2 screen)
    {
        var ray=Camera.ScreenPointToRay(screen);float nearest=30;int selected=-1;
        for(int i=0;i<nodes.Count;i++)if(nodes[i].marker.SelectionCollider.Raycast(ray,out RaycastHit hit,nearest)){nearest=hit.distance;selected=i;}
        if(selected>=0){SelectedIndex=selected;Notice="Selected "+SelectedLabel;}
    }
    private void RebuildArrows()
    {
        foreach(var arrow in arrows)if(arrow!=null){arrow.SetActive(false);Destroy(arrow);}arrows.Clear();
        for(int i=0;i<nodes.Count;i++)nodes[i].marker.SetTitle(Title(i));
        for(int i=0;i<nodes.Count-1;i++)
        {
            Vector3 from=World(i),to=World(i+1),direction=(to-from).normalized,normal=GroundNormal;
            Vector3 middle=(from+to)*.5f+normal*.04f,side=Vector3.Cross(normal,direction)*.15f;
            var go=new GameObject("Route segment "+i);go.transform.SetParent(markerRoot,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=markerMaterial;line.useWorldSpace=false;line.widthMultiplier=.025f;line.positionCount=7;
            var positions=new[]{from+normal*.035f,middle,middle-direction*.2f+side,middle,middle-direction*.2f-side,middle,to+normal*.035f};
            for(int k=0;k<positions.Length;k++)positions[k]=markerRoot.InverseTransformPoint(positions[k]);line.SetPositions(positions);arrows.Add(go);
        }
    }
    private void OnApplicationFocus(bool value){focused=value;if(!value){arrival.Pause();exercise.StopSpray();}}
    private void OnApplicationPause(bool value){paused=value;if(value){arrival.Pause();exercise.StopSpray();}}
}




