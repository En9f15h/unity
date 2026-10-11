#if UNITY_EDITOR || CODEX_SIGHT_SLOT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public sealed class SightSlotSizeProbe : MonoBehaviour
{
    public Sprite watchedFrame;
    readonly List<string> checks=new List<string>(), errors=new List<string>();
    string output;
    void Check(bool value,string message) { checks.Add((value?"PASS: ":"FAILED: ")+message); }
    void Log(string text,string trace,LogType type) { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(text+"\n"+trace); }
    IEnumerator Run()
    {
        var canvas=new GameObject("Slot test canvas",typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var row=new GameObject("Enemy slots",typeof(RectTransform),typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        row.SetParent(canvas.transform,false);row.sizeDelta=new Vector2(430,100);
        var layout=row.GetComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.childControlWidth=false;layout.childControlHeight=false;
        layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        SightSlotView view=null;RectTransform rect=null;Image icon=null;
        for(int i=0;i<4;i++)
        {
            var cell=new GameObject("EnemyPreview_"+i,typeof(RectTransform),typeof(Image));
            cell.transform.SetParent(row,false);var r=cell.GetComponent<RectTransform>();r.sizeDelta=new Vector2(100,100);
            var v=cell.AddComponent<SightSlotView>();var img=v.GetOrCreateActionIconImage();v.SetWatchedSpriteTarget(img);
            if(i==0){view=v;rect=r;icon=img;}
        }
        var sprite=watchedFrame;
        Check(sprite!=null,"Uses the real Oracle watched frame");view.SetWatchedSlotSprite(sprite);
        var baseline=new Vector3(.85f,1.1f,1);rect.localScale=baseline;
        Canvas.ForceUpdateCanvases();Vector2 size=rect.sizeDelta,position=rect.anchoredPosition;
        var snap=new SlotSnapshot{actionId=(int)ActionType.None,isEmpty=true};
        float peak=1;
        for(int i=0;i<60;i++)
        {
            snap.revision=i;snap.isLockedContinuation=i%3==1;snap.isEmpty=i%3==0;
            view.ShowSnapshot(snap,sprite,sprite);
            float until=Time.unscaledTime+.045f;
            do { peak=Mathf.Max(peak,rect.localScale.x/baseline.x);yield return null; } while(Time.unscaledTime<until);
        }
        Check(peak<=1.081f,"60 interrupted snapshot pulses stay within 1.08x; peak="+peak);
        yield return new WaitForSecondsRealtime(.3f);
        Check(Vector3.Distance(rect.localScale,baseline)<.0001f,"Completed pulse restores the authored non-unit scale");
        Canvas.ForceUpdateCanvases();
        Check(rect.sizeDelta==size && Vector2.Distance(rect.anchoredPosition,position)<.001f,"Frame sprite changes preserve slot size and row position");
        Check(icon.rectTransform.rect.size==rect.rect.size,"Watched image stays stretched inside its slot");
        view.ShowSnapshot(snap,sprite,sprite);yield return new WaitForSecondsRealtime(.06f);view.ClearReveal();
        Check(Vector3.Distance(rect.localScale,baseline)<.0001f,"Clearing a reveal restores an interrupted pulse");
        view.ShowSnapshot(snap,sprite,sprite);yield return new WaitForSecondsRealtime(.06f);view.SetWatched(false);
        Check(Vector3.Distance(rect.localScale,baseline)<.0001f,"Ending Sight restores slot scale");
        Check(icon.sprite==null,"Ending Sight restores the original sprite");
        view.ShowSnapshot(snap,sprite,sprite);yield return new WaitForSecondsRealtime(.06f);view.enabled=false;
        Check(Vector3.Distance(rect.localScale,baseline)<.0001f,"Disabling the view restores slot scale");view.enabled=true;
        view.ShowSnapshot(snap,sprite,sprite);yield return new WaitForSecondsRealtime(.06f);view.gameObject.SetActive(false);
        Check(Vector3.Distance(rect.localScale,baseline)<.0001f,"Deactivating the slot restores slot scale");view.gameObject.SetActive(true);
        Time.timeScale=0;view.ShowSnapshot(snap,sprite,sprite);yield return new WaitForSecondsRealtime(.3f);
        Check(Vector3.Distance(rect.localScale,baseline)<.0001f,"Unscaled pulse completes while gameplay time is paused");Time.timeScale=1;
        view.SetWatched(false);view.ClearReveal();Destroy(canvas.gameObject);
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;Application.targetFrameRate=120;
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-sight-output");output=i>=0?args[i+1]:"SightSlotResults";
        Directory.CreateDirectory(output);Application.logMessageReceived+=Log;
        var routine=Run();while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){checks.Add("FAILED: "+e);break;}yield return next;}
        File.WriteAllLines(output+"/validation.txt",checks);File.WriteAllLines(output+"/errors.txt",errors);
        Application.logMessageReceived-=Log;Application.Quit(errors.Count==0 && !checks.Exists(c=>c.StartsWith("FAILED:"))?0:1);
    }
}
#endif
