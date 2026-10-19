using TMPro;
using UnityEngine;

/// <summary>Presentation only. Endpoints follow the captured sweep frame, never particle bounds.</summary>
[DisallowMultipleComponent]
public sealed class SIHSweepFeedback : MonoBehaviour
{
    [SerializeField] private SIHAimSprayController controller;
    [SerializeField] private TMP_Text progress;
    [SerializeField] private TMP_Text leftMarker;
    [SerializeField] private TMP_Text rightMarker;
    [SerializeField] private TMP_Text diagnostics;
    [SerializeField] private bool showDevelopmentDiagnostics = true;
    public void Configure(SIHAimSprayController source,TMP_Text counter,TMP_Text left,TMP_Text right,TMP_Text debug)
    {controller=source;progress=counter;leftMarker=left;rightMarker=right;diagnostics=debug;}
    private void Awake(){if(diagnostics!=null)diagnostics.transform.parent.gameObject.SetActive(false);}
    private void LateUpdate()
    {
        var sweep=controller!=null?controller.Sweep:null;
        bool active=controller!=null&&controller.isActiveAndEnabled&&sweep!=null&&sweep.HasAttempt&&
            (controller.TrainingRoute==null||!controller.TrainingRoute.IsRouteStage);
        progress.transform.parent.gameObject.SetActive(active);
        if(active)
        {
            string badges="";
            for(int i=0;i<sweep.RequiredPasses;i++)badges+=(i<sweep.CompletedPasses?"<color=#63FF9A>["+(i+1)+" DONE]</color>":"<color=#ADB6C0>["+(i+1)+"]</color>")+" ";
            progress.text=$"Sweep passes: {sweep.CompletedPasses}/{sweep.RequiredPasses}\n{badges}";
        }
        bool showMarkers=active&&controller.TrackingReliable&&controller.State!=SIHAimSprayController.TrainingState.Extinguished;
        Position(leftMarker,showMarkers?sweep.LeftEndpoint:Vector3.zero,showMarkers);
        Position(rightMarker,showMarkers?sweep.RightEndpoint:Vector3.zero,showMarkers);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool debugActive=active&&showDevelopmentDiagnostics;
        diagnostics.transform.parent.gameObject.SetActive(debugActive);
        if(debugActive)
        {
            string direction=sweep.CurrentDirection>0?"L -> R":sweep.CurrentDirection<0?"R -> L":"none";
            diagnostics.text=$"DEV  aim: {controller.IsOnTarget}  spray: {controller.IsSpraying}  tracking: {controller.TrackingReliable}\n"+
                $"hit x: {sweep.NormalizeHit(controller.SprayHitPosition):F3}  smooth: {sweep.SmoothedPosition:F3}  dir: {direction}\n"+
                $"passes: {sweep.CompletedPasses}/{sweep.RequiredPasses}  {sweep.SampleStatus}";
        }
#else
        if(diagnostics!=null)diagnostics.transform.parent.gameObject.SetActive(false);
#endif
    }
    private void Position(TMP_Text marker,Vector3 point,bool visible)
    {
        var camera=controller!=null?controller.AimCamera:null;
        if(camera==null)visible=false;
        Vector3 screen=visible?camera.WorldToScreenPoint(point):Vector3.zero;
        visible &= screen.z>0&&screen.x>=0&&screen.x<=Screen.width&&screen.y>=0&&screen.y<=Screen.height;
        var panel=(RectTransform)marker.transform.parent;
        panel.gameObject.SetActive(visible);
        if(!visible)return;
        var parent=(RectTransform)panel.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,null,out Vector2 position);
        panel.anchoredPosition=position;
        marker.color=controller.Sweep.IsComplete?new Color(.39f,1,.6f):Color.white;
    }
    private void OnDisable()
    {
        if(progress!=null)progress.transform.parent.gameObject.SetActive(false);
        if(leftMarker!=null)leftMarker.transform.parent.gameObject.SetActive(false);
        if(rightMarker!=null)rightMarker.transform.parent.gameObject.SetActive(false);
        if(diagnostics!=null)diagnostics.transform.parent.gameObject.SetActive(false);
    }
}

