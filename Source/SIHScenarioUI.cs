using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SIHScenarioUI : MonoBehaviour
{
    [SerializeField] private SIHScenarioInteraction flow;
    [SerializeField] private SIHAssessmentRecorder assessment;
    [SerializeField] private SIHAimSprayController exercise;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform safeArea,content;
    [SerializeField] private TMP_Text description,feedback;
    [SerializeField] private UnityEngine.UI.Button continueButton;
    [SerializeField, Range(.35f,.75f)] private float portraitPanelHeight=.52f;
    [SerializeField, Range(.35f,.85f)] private float landscapePanelHeight=.72f;
    private int revision=-1;
    private readonly System.Collections.Generic.List<GameObject> cards=new System.Collections.Generic.List<GameObject>();
    public void Configure(SIHScenarioInteraction f,SIHAssessmentRecorder a,SIHAimSprayController c,GameObject root,RectTransform safe,RectTransform list,TMP_Text text,TMP_Text status,UnityEngine.UI.Button next)
    {flow=f;assessment=a;exercise=c;panel=root;safeArea=safe;content=list;description=text;feedback=status;continueButton=next;}
    private void LateUpdate()
    {
        bool show=flow!=null&&flow.QuestionsActive&&!assessment.ShowingResults;panel.SetActive(show);if(!show)return;
        var area=Screen.safeArea;safeArea.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safeArea.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        var sheet=safeArea.Find("Question Sheet") as RectTransform;
        if(sheet!=null)sheet.anchorMax=new Vector2(1,Screen.height>=Screen.width?portraitPanelHeight:landscapePanelHeight);
        if(revision!=flow.Revision){revision=flow.Revision;Rebuild();}
        feedback.text=!exercise.TrackingReliable?"Tracking paused":flow.FeedbackPending?"Read feedback, then continue":"Scroll for description and answers";
        continueButton.gameObject.SetActive(flow.FeedbackPending);continueButton.interactable=exercise.TrackingReliable;
        foreach(var card in cards)card.GetComponent<UnityEngine.UI.Button>().interactable=!flow.FeedbackPending&&exercise.TrackingReliable;
    }
    private void Rebuild()
    {
        foreach(var card in cards){card.SetActive(false);Destroy(card);}cards.Clear();
        var s=flow.Scenario;description.text=s!=null?$"<b>{s.contextLabel}</b>\n{s.jurisdiction}\n{s.description}\n\n"+(exercise.State==SIHAimSprayController.TrainingState.IdentifyHazard?"<b>Identify the hazard — submit one answer</b>":"<b>Choose an extinguisher from this kit</b>"):"Scenario unavailable";
        if(flow.FeedbackPending)description.text="<b>Answer feedback</b>\n\n"+flow.Feedback;
        UnityEngine.Canvas.ForceUpdateCanvases();
        var scroll=content.GetComponentInParent<UnityEngine.UI.ScrollRect>();if(scroll!=null)scroll.verticalNormalizedPosition=1;
        if(flow.FeedbackPending||s==null||!s.Validate(out _))return;
        if(exercise.State==SIHAimSprayController.TrainingState.IdentifyHazard)foreach(var a in s.hazardAnswers)Card(a.id,a.label);
        else foreach(var c in s.equipmentChoices)Card(c.id,c.label+"\n"+c.type+" | Rating: "+c.rating);
    }
    private void Card(string id,string label)
    {
        var go=new GameObject("Answer "+id,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button),typeof(UnityEngine.UI.LayoutElement));go.transform.SetParent(content,false);
        var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.035f,.24f,.22f);image.raycastTarget=true;
        var layout=go.GetComponent<UnityEngine.UI.LayoutElement>();layout.minHeight=90;layout.preferredHeight=105;
        var textGo=new GameObject("Label",typeof(RectTransform));textGo.transform.SetParent(go.transform,false);var rect=(RectTransform)textGo.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(14,8);rect.offsetMax=new Vector2(-14,-8);
        var text=textGo.AddComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.fontSize=27;text.alignment=TextAlignmentOptions.Center;text.text=label;text.raycastTarget=false;
        var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(()=>flow.Submit(id));cards.Add(go);
    }
}

