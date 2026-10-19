using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SIHAssessmentRules
{
    public string version="prototype-1";
    public bool questionScoring;
    public int hazardPoints,typePoints;
    [Range(0,100)] public float passPercent=70;
    [Min(.1f)] public float validAimSeconds=3;
    [Min(.1f)] public float missedSprayMistakeSeconds=1;
    public int pinPoints=20, aimPoints=20, sweepPoints=20, firePoints=20, exitPoints=10, routePoints=10;
    public bool requirePinEvidence=true, requireAimEvidence=true, requireSweepEvidence=true, requireFireEvidence=true, requireExitEvidence=true, requireRouteEvidence=true;
}
[Serializable]
public sealed class SIHAssessmentItem
{
    public string id,title,explanation;
    public int weight,earned;
    public bool assessed,achieved,required;
}
[Serializable]
public sealed class SIHAssessmentAction
{
    public string code,description,utc;
    public double activeSeconds;
}
[Serializable]
public sealed class SIHAttemptRecord
{
    public int schemaVersion=1;
    public string attemptId,rulesVersion,startedUtc,endedUtc,lastUpdatedUtc,status="In progress",endReason,outcome="Incomplete";
    public SIHAssessmentRules rules;
    public double activeSeconds,validAimSpraySeconds,totalSpraySeconds;
    public int sweepPasses,requiredSweepPasses,routeStops,requiredRouteStops,earnedPoints,availablePoints;
    public bool selectionObserved,pinRemovedCorrectly,fireExtinguished,exitIdentified,orderedRouteCompleted,assemblyReached,exerciseCompleted;
    public bool hintsAvailable;
    public string scenarioId,scenarioVersion,scenarioDescription,scenarioJurisdiction;
    public string[] scenarioSources;
    public bool hazardAnswered,equipmentChosen;
    public int hazardSubmissions,equipmentSubmissions;
    public List<SIHQuestionSubmission> answers=new List<SIHQuestionSubmission>();
    public List<SIHAssessmentItem> items=new List<SIHAssessmentItem>();
    public List<SIHAssessmentAction> mistakes=new List<SIHAssessmentAction>();
    public List<SIHAssessmentAction> hints=new List<SIHAssessmentAction>();
    public static string UtcNow()=>DateTime.UtcNow.ToString("o");
    public static SIHAttemptRecord Create(SIHAssessmentRules settings,int passes,int stops)
    {
        var r=new SIHAttemptRecord{attemptId=Guid.NewGuid().ToString("N"),rulesVersion=settings.version,startedUtc=UtcNow(),lastUpdatedUtc=UtcNow(),rules=JsonUtility.FromJson<SIHAssessmentRules>(JsonUtility.ToJson(settings)),requiredSweepPasses=passes,requiredRouteStops=stops};
        r.rules.validAimSeconds=Mathf.Max(.1f,r.rules.validAimSeconds);r.rules.missedSprayMistakeSeconds=Mathf.Max(.1f,r.rules.missedSprayMistakeSeconds);r.rules.passPercent=Mathf.Clamp(r.rules.passPercent,0,100);
        r.Add("pin","Pin removed in sequence",settings.pinPoints,settings.requirePinEvidence);
        r.Add("aim","Valid aim while spraying",settings.aimPoints,settings.requireAimEvidence);
        r.Add("sweep","Required sweep passes",settings.sweepPoints,settings.requireSweepEvidence);
        r.Add("fire","Fire extinguished",settings.firePoints,settings.requireFireEvidence);
        r.Add("exit","Training exit identified",settings.exitPoints,settings.requireExitEvidence);
        r.Add("route","Ordered route and assembly",settings.routePoints,settings.requireRouteEvidence);
        r.Add("hazard","Hazard identification",settings.questionScoring?settings.hazardPoints:0,settings.questionScoring);
        r.Add("type","Suitable extinguisher choice",settings.questionScoring?settings.typePoints:0,settings.questionScoring);
        if(settings.questionScoring)r.schemaVersion=2;
        else{r.Missing("hazard","Interaction not implemented.");r.Missing("type","Type-choice interaction not implemented.");}
        r.Evaluate();return r;
    }
    private void Add(string id,string title,int points,bool required)=>items.Add(new SIHAssessmentItem{id=id,title=title,weight=Mathf.Max(0,points),required=required,assessed=true});
    public SIHAssessmentItem Item(string id)=>items.Find(i=>i.id==id);
    // Once observation has a gap, never silently restore evidence during this attempt.
    public void Missing(string id,string reason){var item=Item(id);if(item!=null&&!item.achieved){item.assessed=false;item.explanation=reason;}}
    public void Evaluate()
    {
        if(rules.questionScoring){Set("hazard",hazardAnswered&&hazardSubmissions==1,QuestionFeedback(hazardAnswered,hazardSubmissions));Set("type",equipmentChosen&&equipmentSubmissions==1,QuestionFeedback(equipmentChosen,equipmentSubmissions));}
        Set("pin",pinRemovedCorrectly,pinRemovedCorrectly?"Selection was observed before pin removal.":"Select the extinguisher, then remove its pin before spraying.");
        Set("aim",validAimSpraySeconds>=rules.validAimSeconds,$"Valid spray: {validAimSpraySeconds:F1}/{rules.validAimSeconds:F1} seconds. Aim at the fire base while holding spray.");
        Set("sweep",sweepPasses>=requiredSweepPasses&&requiredSweepPasses>0,$"Completed {sweepPasses}/{requiredSweepPasses} alternating passes. Sweep across the base while spraying.");
        Set("fire",fireExtinguished,fireExtinguished?"Fire-extinguished event recorded.":"The fire was not extinguished in this attempt.");
        Set("exit",exitIdentified,exitIdentified?"Correct exit selection recorded.":"Find and select the green training exit.");
        Set("route",orderedRouteCompleted&&assemblyReached&&routeStops==requiredRouteStops&&requiredRouteStops>0,$"Reached {routeStops}/{requiredRouteStops} ordered stops. Reach assembly after all prior stops.");
        earnedPoints=availablePoints=0;bool missing=false;
        foreach(var item in items){item.earned=item.assessed&&item.achieved?item.weight:0;if(item.assessed)availablePoints+=item.weight;earnedPoints+=item.earned;if(item.required&&!item.assessed)missing=true;}
        outcome=!exerciseCompleted?"Incomplete":missing||availablePoints==0?"Assessment incomplete":100f*earnedPoints/availablePoints>=rules.passPercent?"Pass":"Needs practice";
    }
    private void Set(string id,bool value,string explanation){var item=Item(id);if(!item.assessed)return;item.achieved|=value;item.explanation=explanation;}
    private string QuestionFeedback(bool complete,int count)=>complete?(count==1?"Correct first submission.":"Correct after retry — 0 points under the first-submission prototype policy."):count>0?"Incorrect submission recorded; answer correctly to continue.":"No answer submitted.";
    public bool RecordAnswer(SIHQuestionSubmission answer)
    {
        if(!rules.questionScoring||status!="In progress"||answer==null||string.IsNullOrEmpty(answer.submissionId)||answers.Exists(x=>x.submissionId==answer.submissionId))return false;
        bool hazard=answer.question=="hazard";if(!hazard&&answer.question!="type")return false;
        if(!hazard&&!hazardAnswered)return false;
        if((hazard?hazardAnswered:equipmentChosen)||answer.number!=(hazard?hazardSubmissions:equipmentSubmissions)+1)return false;
        answers.Add(answer);if(hazard){hazardSubmissions++;hazardAnswered=answer.correct;}else{equipmentSubmissions++;equipmentChosen=answer.correct;}
        if(!answer.correct)mistakes.Add(new SIHAssessmentAction{code="incorrect_"+answer.question,description=answer.answerLabel+": "+answer.feedback,utc=answer.utc,activeSeconds=activeSeconds});Evaluate();return true;
    }
}
