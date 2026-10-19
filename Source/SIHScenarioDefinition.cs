using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class SIHScenarioAnswer
{
    public string id,label;
    [TextArea] public string feedback;
}
[Serializable] public sealed class SIHEquipmentChoice
{
    public string id,label,type,rating,simulationProfile;
    public bool accepted;
    [TextArea] public string feedback;
}
[CreateAssetMenu(menuName="SIH/Training Scenario")]
public sealed class SIHScenarioDefinition : ScriptableObject
{
    public const string PowderProfile="abc6-fxp6";
    public string scenarioId="uk-outdoor-wood",version="1",jurisdiction="UK fire classes (EN 2)";
    [TextArea(3,8)] public string description;
    public string contextLabel="SIMULATED OUTDOOR WOOD FIRE";
    public string correctHazardId="a";
    public List<SIHScenarioAnswer> hazardAnswers=new List<SIHScenarioAnswer>();
    public List<SIHEquipmentChoice> equipmentChoices=new List<SIHEquipmentChoice>();
    [TextArea(2,8)] public string[] sources;
    public bool Validate(out string reason)
    {
        reason="Scenario configuration needs review.";
        if(string.IsNullOrWhiteSpace(scenarioId)||string.IsNullOrWhiteSpace(version)||string.IsNullOrWhiteSpace(description)||sources==null||sources.Length==0||hazardAnswers==null||hazardAnswers.Count<2||equipmentChoices==null||equipmentChoices.Count<2)return false;
        var ids=new HashSet<string>();bool correct=false;
        foreach(var a in hazardAnswers){if(a==null||string.IsNullOrEmpty(a.id)||!ids.Add(a.id)||string.IsNullOrEmpty(a.label)||string.IsNullOrEmpty(a.feedback))return false;correct|=a.id==correctHazardId;}
        if(!correct)return false;ids.Clear();bool accepted=false;
        foreach(var c in equipmentChoices){if(c==null||string.IsNullOrEmpty(c.id)||!ids.Add(c.id)||string.IsNullOrEmpty(c.label)||string.IsNullOrEmpty(c.feedback))return false;if(c.accepted){accepted=true;if(c.simulationProfile!=PowderProfile||c.type!="ABC powder"||c.rating!="43A 233B C"){reason="An accepted choice needs matching model, rating and discharge support. Only the 6 kg ABC powder / 43A 233B C training profile is installed.";return false;}}}
        if(!accepted)return false;reason="";return true;
    }
}
[Serializable] public sealed class SIHQuestionSubmission
{
    public string submissionId,question,answerId,answerLabel,feedback,utc;
    public bool correct;
    public int number;
}
