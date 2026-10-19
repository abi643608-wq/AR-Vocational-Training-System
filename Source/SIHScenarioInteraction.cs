using System;
using TMPro;
using UnityEngine;

/// <summary>Explicit questions. Uses the existing state owner; never classifies camera images.</summary>
[DisallowMultipleComponent]
public sealed class SIHScenarioInteraction : MonoBehaviour
{
    [SerializeField] private SIHScenarioDefinition scenario;
    [SerializeField] private ARPlacementController placement;
    [SerializeField] private SIHAimSprayController exercise;
    [SerializeField] private SIHExtinguisherSelection equipment;
    public SIHScenarioDefinition Scenario=>snapshot!=null?snapshot:scenario;
    public bool HazardAnswered { get; private set; }
    public bool ChoiceAccepted { get; private set; }
    public bool FeedbackPending { get; private set; }
    public string Feedback { get; private set; }
    public string ChosenLabel { get; private set; }
    public int HazardSubmissions { get; private set; }
    public int ChoiceSubmissions { get; private set; }
    public int Revision { get; private set; }
    public bool Ready=>isActiveAndEnabled&&HazardAnswered&&ChoiceAccepted&&!FeedbackPending;
    public bool QuestionsActive=>false; // Retired quiz; retain serialized scenario assets for history.
    public event Action<SIHQuestionSubmission> AnswerSubmitted;
    private SIHScenarioDefinition snapshot;
    private TMP_Text context;
    private int blockedFrame=-1;
    public void Configure(SIHScenarioDefinition s,ARPlacementController p,SIHAimSprayController c,SIHExtinguisherSelection e){scenario=s;placement=p;exercise=c;equipment=e;}
    private void OnEnable(){ResetQuestions();}
    public void ResetQuestions()
    {
        HazardAnswered=ChoiceAccepted=FeedbackPending=false;HazardSubmissions=ChoiceSubmissions=0;Feedback=ChosenLabel="";blockedFrame=Time.frameCount;Revision++;
        if(context!=null)Destroy(context.gameObject);context=null;if(snapshot!=null)Destroy(snapshot);snapshot=null;
    }
    // Compatibility entry points for existing serialized UI callbacks. No active quiz or world text.
    public bool Submit(string id)=>false;
    public void ContinueAfterFeedback(){}
}
