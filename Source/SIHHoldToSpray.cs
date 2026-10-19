using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class SIHHoldToSpray : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    [SerializeField] private SIHAimSprayController controller;
    private int pointer=int.MinValue;
    public void Configure(SIHAimSprayController value) => controller=value;
    public void OnPointerDown(PointerEventData e)
    {
        if(pointer!=int.MinValue || e.button!=PointerEventData.InputButton.Left || controller==null)return;
        if(controller.BeginSpray())pointer=e.pointerId;
    }
    public void OnPointerUp(PointerEventData e) { if(e.pointerId==pointer)Cancel(); }
    public void OnPointerExit(PointerEventData e) { if(e.pointerId==pointer)Cancel(); }
    public void OnCancel(BaseEventData e) => Cancel();
    public void Cancel() { pointer=int.MinValue; if(controller!=null)controller.StopSpray(); }
    private void Update()
    {
        if(pointer==int.MinValue)return;
        if(controller==null || !controller.IsSpraying){Cancel();return;}
        if(pointer<0) { if(!Input.GetMouseButton(0))Cancel(); return; }
        for(int i=0;i<Input.touchCount;i++)
        {
            var t=Input.GetTouch(i);
            if(t.fingerId!=pointer)continue;
            if(t.phase==TouchPhase.Ended || t.phase==TouchPhase.Canceled)Cancel();
            return;
        }
        Cancel();
    }
    private void OnDisable()=>Cancel();
    private void OnApplicationFocus(bool focused){if(!focused)Cancel();}
    private void OnApplicationPause(bool paused){if(paused)Cancel();}
}

