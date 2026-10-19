using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SIHRouteUI : MonoBehaviour
{
    [SerializeField] private SIHTrainingRoute route;
    [SerializeField] private GameObject setupControls;
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Text direction;
    [SerializeField] private UnityEngine.UI.Button confirm;
    private void Awake()
    {
        CompactSetupLayout();
        // Keep the existing confirmation callbacks; perform their review prerequisite first.
        var callbacks=confirm.onClick;
        confirm.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
        confirm.onClick.AddListener(()=>{route.Review();callbacks.Invoke();});
    }
    private void CompactSetupLayout()
    {
        PlaceButton("Place Exit",.04f,.10f,.48f,.16f);
        PlaceButton("Add Waypoint",.52f,.10f,.96f,.16f);
        PlaceButton("Place Assembly",.04f,.025f,.48f,.085f);
        PlaceButton("Confirm",.52f,.025f,.96f,.085f);
        string[] edits={"Move","Delete","Earlier","Later"};
        for(int i=0;i<edits.Length;i++)PlaceButton(edits[i],.04f+i*.235f,.18f,.26f+i*.235f,.235f);
        SetSetupVisible("Review",false);SetSetupVisible("Select Next",false);
    }
    private void PlaceButton(string name,float x,float y,float right,float top)
    {
        var rect=setupControls.transform.Find(name) as RectTransform;if(rect==null)return;
        rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(right,top);rect.offsetMin=rect.offsetMax=Vector2.zero;
    }
    private void SetSetupVisible(string name,bool show)
    {var child=setupControls.transform.Find(name);if(child!=null)child.gameObject.SetActive(show);}
    public void Configure(SIHTrainingRoute value,GameObject controls,GameObject panel,TMP_Text label,TMP_Text indicator,UnityEngine.UI.Button confirmButton)
    {route=value;setupControls=controls;statusPanel=panel;status=label;direction=indicator;confirm=confirmButton;}
    private void LateUpdate()
    {
        if(route==null)return;
        setupControls.SetActive(route.IsSetup);statusPanel.SetActive(route.IsRouteStage);
        confirm.interactable=route.IsSetup&&route.Reliable&&route.ValidateRoute(out _);
        bool selected=route.IsSetup&&route.SelectedIndex>=0&&route.SelectedIndex<route.Count;
        foreach(var name in new[]{"Move","Delete","Earlier","Later"})SetSetupVisible(name,selected);
        foreach(var name in new[]{"Restart Exercise","Reconfigure Route"})
        {var child=transform.Find(name);if(child!=null)child.gameObject.SetActive(!route.IsSetup);}
        direction.text="";
        if(route.IsSetup)
        {
            // Overview is the existing read-only route summary; no route data is changed here.
            bool exit=route.Overview.Contains("Exit"),assembly=route.Overview.Contains("Assembly");
            int waypoints=route.Count-(exit?1:0)-(assembly?1:0);
            status.text=$"Training Route\nExit: {(exit?"Ready":"—")}  |  Waypoints: {waypoints}  |  Assembly: {(assembly?"Ready":"—")}\n"+
                (route.Tool!=SIHTrainingRoute.SetupTool.None?route.Notice:selected?"Selected: "+route.SelectedLabel:"Tap a marker to edit");
        }
        else if(route.IsEvacuating)
        {
            status.text="TRAINING ROUTE — follow the marked turns\nNext: "+(route.NextIndex==0?"Exit":route.NextIndex==route.Count-1?"Assembly point":"Waypoint "+route.NextIndex)+$"  {route.NextDistance:F1} m\n"+route.Arrival.Status;
            if(route.Reliable&&route.Arrival.Stable&&route.NextDistance>route.Arrival.Radius&&!route.Arrival.Armed)
            {
                var camera=route.Camera;Vector3 target=route.NextPosition;
                var screen=camera.WorldToViewportPoint(target);
                if(screen.z<=0||screen.x<.05f||screen.x>.95f||screen.y<.05f||screen.y>.95f)
                {
                    Vector3 local=camera.transform.InverseTransformDirection(Vector3.ProjectOnPlane(target-camera.transform.position,route.GroundNormal));
                    direction.text=local.z<0?"TURN AROUND":local.x<0?"<  NEXT MARKER":"NEXT MARKER  >";
                }
                else direction.text="NEXT MARKER AHEAD";
            }
        }
        else if(route.AssemblyPointReached)
            status.text=$"TRAINING ROUTE COMPLETE\nExit identified: {route.ExitIdentified} | Stops reached: {route.ReachedCount}/{route.Count}\nSweep: {SweepResult}";
        else status.text=$"TRAINING ROUTE — manually configured\nTap the green TRAINING EXIT sign\nFire extinguished | Sweep: {SweepResult}";
    }
    private string SweepResult=>(route.SweepCompletedResult?"complete":"not demonstrated")+$" ({route.SweepPassesResult} passes)";
}

