using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SIHAssessmentResultsUI : MonoBehaviour
{
    [SerializeField] private SIHAssessmentRecorder assessment;
    [SerializeField] private SIHTrainingRoute route;
    [SerializeField] private GameObject panel,lastResultsButton;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private TMP_Text heading,details;
    [SerializeField] private UnityEngine.UI.ScrollRect scroll;
    [SerializeField] private GameObject[] oldHud;
    private bool visible;
    private bool[] oldStates;
    private string shownId;
    public void Configure(SIHAssessmentRecorder a,SIHTrainingRoute r,GameObject root,RectTransform safe,TMP_Text title,TMP_Text body,UnityEngine.UI.ScrollRect view,GameObject history,GameObject[] hud)
    {assessment=a;route=r;panel=root;safeArea=safe;heading=title;details=body;scroll=view;lastResultsButton=history;oldHud=hud;}
    private void LateUpdate()
    {
        if(assessment==null)return;
        bool show=assessment.ShowingResults&&assessment.LastResult!=null;
        if(show!=visible){visible=show;panel.SetActive(show);if(show){oldStates=new bool[oldHud.Length];for(int i=0;i<oldHud.Length;i++){oldStates[i]=oldHud[i].activeSelf;oldHud[i].SetActive(false);}}else if(oldStates!=null){for(int i=0;i<oldHud.Length;i++)oldHud[i].SetActive(oldStates[i]);shownId=null;}}
        lastResultsButton.SetActive(!show&&!route.IsSetup&&route.AssemblyPointReached&&assessment.LastResult!=null);
        if(!show)return;
        Rect area=Screen.safeArea;safeArea.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safeArea.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        var r=assessment.LastResult;
        if(shownId==r.attemptId)return;
        shownId=r.attemptId;
        heading.text=$"TRAINING RESULTS\nExercise: {(r.exerciseCompleted?"Complete":"Incomplete")}  |  {r.outcome}\n{r.earnedPoints}/{r.availablePoints} points";
        var b=new StringBuilder();
        b.AppendLine($"<b>Prototype pass threshold: {r.rules.passPercent:F0}%</b>\nProject setting; not an official safety standard.\nPassing also requires exercise completion and all configured required items to be assessed.\n");
        if(r.rules.questionScoring)b.AppendLine("<b>Question scoring policy — prototype-2</b>\nFull question points only for a correct first submission. Correct after retry allows progression but earns zero for that question. Recorded wrong answers cause no further deduction.\n");
        foreach(var item in r.items){b.AppendLine($"<b>{item.title}: {(item.assessed?$"{item.earned}/{item.weight}":"Not assessed")}</b>");b.AppendLine(item.explanation);if(!item.assessed)b.AppendLine("Excluded from available points." );b.AppendLine();}
        b.AppendLine($"<b>Recorded actions</b>\nSweep passes: {r.sweepPasses}/{r.requiredSweepPasses}\nRoute stops: {r.routeStops}/{r.requiredRouteStops}\nValid aim spray: {r.validAimSpraySeconds:F1} s / total spray: {r.totalSpraySeconds:F1} s\nActive duration: {System.TimeSpan.FromSeconds(r.activeSeconds):hh\\:mm\\:ss}\nDuration has no score effect.\n");
        b.AppendLine($"<b>Recorded mistakes: {r.mistakes.Count}</b>\n"+(r.rules.questionScoring?"Incorrect submitted answers and sustained off-target spraying. No additional point deductions.":"Only sustained off-target spraying is defined as a mistake in this version. No point deductions."));
        foreach(var mistake in r.mistakes)b.AppendLine($"• At {mistake.activeSeconds:F1} active seconds: {mistake.description}");
        b.AppendLine("\n<b>Hints: Not available</b>\nAutomatic prompts are not counted as requested hints.\n");
        if(r.rules.questionScoring){b.AppendLine($"<b>Scenario {r.scenarioId} v{r.scenarioVersion}</b>\n{r.scenarioJurisdiction}\n{r.scenarioDescription}");foreach(var a in r.answers)b.AppendLine($"{a.question} submission {a.number}: {a.answerLabel} — {(a.correct?"Correct":"Incorrect")}. {a.feedback}");b.AppendLine();}
        b.AppendLine($"<b>Attempt details</b>\n{r.attemptId}\nRules: {r.rulesVersion}; record schema: {r.schemaVersion}\nStarted (UTC): {r.startedUtc}\nEnded (UTC): {r.endedUtc}\n{r.status}: {r.endReason}");
        if(!string.IsNullOrEmpty(assessment.StorageError))b.AppendLine("\nLocal save warning: "+assessment.StorageError);
        details.text=b.ToString();UnityEngine.Canvas.ForceUpdateCanvases();UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);scroll.verticalNormalizedPosition=1;
    }
}
