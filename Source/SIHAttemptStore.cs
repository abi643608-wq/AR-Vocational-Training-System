using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>One immutable final JSON per attempt; a replaceable crash-recovery checkpoint. No AR coordinates.</summary>
public sealed class SIHAttemptStore
{
    public readonly string DirectoryPath;
    public string LastError { get; private set; }
    private string Pending=>Path.Combine(DirectoryPath,"pending.json");
    public SIHAttemptStore(string directory){DirectoryPath=directory;}
    public bool Checkpoint(SIHAttemptRecord record)=>Write(Pending,record,false);
    public bool SaveFinal(SIHAttemptRecord record)
    {
        if(record==null||!Guid.TryParseExact(record.attemptId,"N",out _)||record.status=="In progress")return false;
        string path=Path.Combine(DirectoryPath,record.attemptId+".json");
        if(File.Exists(path)){
            var existing=Read(path);
            if(existing==null){LastError="Existing final record is unreadable; it was preserved.";return false;}
            ClearPending(record.attemptId);return true;
        }
        if(!Write(path,record,true))return false;
        ClearPending(record.attemptId);return true;
    }
    private bool Write(string path,SIHAttemptRecord record,bool immutable)
    {
        string temp=path+".tmp";
        try{
            Directory.CreateDirectory(DirectoryPath);
            var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(record,true));
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            // Same-directory rename/replace: never delete the last good file before committing.
            if(File.Exists(path)){if(immutable){File.Delete(temp);return true;}File.Replace(temp,path,path+".bak");}
            else File.Move(temp,path);
            LastError=null;return true;
        }catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is NotSupportedException){LastError=e.Message;return false;}
    }
    private SIHAttemptRecord Read(string path)
    {
        try{
            if(!File.Exists(path))return null;
            var record=JsonUtility.FromJson<SIHAttemptRecord>(File.ReadAllText(path));
            if(record==null||(record.schemaVersion!=1&&record.schemaVersion!=2)||!Guid.TryParseExact(record.attemptId,"N",out _)||record.rules==null||record.items==null||record.items.Count!=8||record.mistakes==null||record.hints==null||!DateTime.TryParse(record.startedUtc,out _)||double.IsNaN(record.activeSeconds)||double.IsInfinity(record.activeSeconds)||record.activeSeconds<0)throw new InvalidDataException("Invalid or unsupported assessment record.");
            if(record.schemaVersion==2&&(record.answers==null||record.answers.Exists(a=>a==null)))throw new InvalidDataException("Missing question records.");
            var ids=new HashSet<string>();foreach(var item in record.items)if(item==null||!ids.Add(item.id)||item.weight<0||item.earned<0||item.earned>item.weight)throw new InvalidDataException("Invalid assessment items.");
            foreach(var id in new[]{"pin","aim","sweep","fire","exit","route","hazard","type"})if(!ids.Contains(id))throw new InvalidDataException("Missing assessment item.");
            if(!float.IsFinite(record.rules.passPercent)||record.rules.passPercent<0||record.rules.passPercent>100||!float.IsFinite(record.rules.validAimSeconds)||record.rules.validAimSeconds<=0||record.validAimSpraySeconds<0||double.IsNaN(record.validAimSpraySeconds)||double.IsInfinity(record.validAimSpraySeconds)||record.totalSpraySeconds<record.validAimSpraySeconds||record.routeStops<0||record.sweepPasses<0||(record.status!="In progress"&&record.status!="Completed"&&record.status!="Interrupted"))throw new InvalidDataException("Invalid assessment measurements.");
            foreach(var action in record.mistakes)if(action==null)throw new InvalidDataException("Invalid mistake record.");
            return record;
        }catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){LastError=e.Message;return null;}
    }
    public SIHAttemptRecord RecoverInterrupted()
    {
        // A backup is consulted only if the primary is damaged/missing, never as a second attempt.
        var r=Read(Pending)??Read(Pending+".bak");if(r==null)return null;
        string final=Path.Combine(DirectoryPath,r.attemptId+".json");
        if(File.Exists(final)&&Read(final)!=null){ClearPending(r.attemptId);return null;}
        if(r.status=="In progress"){
            r.status="Interrupted";r.endReason="App ended before completion; recovered last checkpoint.";
            r.endedUtc=string.IsNullOrEmpty(r.lastUpdatedUtc)?r.startedUtc:r.lastUpdatedUtc;r.exerciseCompleted=false;r.Evaluate();
        }
        return SaveFinal(r)?r:null;
    }
    public List<SIHAttemptRecord> LoadHistory()
    {
        var records=new List<SIHAttemptRecord>();
        try{if(!Directory.Exists(DirectoryPath))return records;foreach(string file in Directory.GetFiles(DirectoryPath,"*.json")){if(Path.GetFileName(file)=="pending.json")continue;var r=Read(file);if(r!=null&&r.status!="In progress"&&Path.GetFileNameWithoutExtension(file)==r.attemptId)records.Add(r);}}
        catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){LastError=e.Message;}
        records.Sort((a,b)=>string.CompareOrdinal(b.startedUtc,a.startedUtc));return records;
    }
    private void ClearPending(string id)
    {
        try{foreach(var path in new[]{Pending,Pending+".bak",Pending+".tmp"}){var r=Read(path);if(r!=null&&r.attemptId==id)File.Delete(path);}}
        catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){LastError=e.Message;}
    }
}
