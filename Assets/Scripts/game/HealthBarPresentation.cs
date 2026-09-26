using UnityEngine;
using UnityEngine.Sprites;

// The original fill transform remains the immediate gameplay-facing value.
// A single full-width sprite renders the well, current health and damage trail.
[DisallowMultipleComponent]
public sealed class HealthBarPresentation : MonoBehaviour
{
    [SerializeField] private Material gaugeMaterial;
    [SerializeField, Min(0f)] private float damageHoldSeconds=.2f;
    [SerializeField, Min(.01f)] private float damageFadeSeconds=.35f;
    private SpriteRenderer source, gauge;
    private MaterialPropertyBlock block;
    private bool sourceWasEnabled, hasValue, fillFromRight, isLocal=true;
    private int lastMax;
    private float current=1, trail=1, trailStart=1, damageStarted=-1000;
    public float CurrentRatio => current;
    public float DelayedRatio => trail;
    public SpriteRenderer GaugeRenderer => gauge;

    public void Bind(SpriteRenderer renderer,Vector3 fullScale,Vector3 fullPosition,bool shrinkToLeft)
    {
        if(renderer==null || (source==renderer && gauge!=null)) return;
        if(gaugeMaterial==null) gaugeMaterial=Resources.Load<Material>("Combat/Materials/HealthGauge");
        if(gaugeMaterial==null) return;
        ReleaseGauge(); source=renderer; sourceWasEnabled=source.enabled; fillFromRight=shrinkToLeft;
        var go=new GameObject("Health gauge presentation"); go.layer=renderer.gameObject.layer;
        go.transform.SetParent(source.transform.parent,false); go.transform.localPosition=fullPosition;
        go.transform.localRotation=source.transform.localRotation; go.transform.localScale=fullScale;
        gauge=go.AddComponent<SpriteRenderer>(); gauge.sharedMaterial=gaugeMaterial;
        gauge.sprite=source.sprite; gauge.drawMode=source.drawMode; gauge.size=source.size;
        gauge.spriteSortPoint=source.spriteSortPoint;
        block=new MaterialPropertyBlock(); Apply();
    }
    public void SetHealth(int hp,int maxHP,bool instant=false)
    {
        if(maxHP<=0) return;
        float ratio=Mathf.Clamp01((float)hp/maxHP);
        if(!hasValue || maxHP!=lastMax || instant || ratio>current)
        { current=trail=trailStart=ratio; damageStarted=-1000; }
        else if(ratio<current)
        { trailStart=Mathf.Max(trail,current); trail=trailStart; current=ratio; damageStarted=Time.unscaledTime; }
        // Repeated synchronization of the same HP must not restart the damage hold.
        lastMax=maxHP; hasValue=true; Apply();
    }
    public void SetPerspective(bool local) { isLocal=local; Apply(); }
    public void SnapToCurrent() { trail=trailStart=current; damageStarted=-1000; Apply(); }
    private void OnEnable() { SnapToCurrent(); }
    private void LateUpdate()
    {
        if(trail>current)
        {
            float progress=Mathf.Clamp01((Time.unscaledTime-damageStarted-damageHoldSeconds)/Mathf.Max(.01f,damageFadeSeconds));
            trail=Mathf.Lerp(trailStart,current,Mathf.SmoothStep(0,1,progress));
        }
        Apply();
    }
    private void Apply()
    {
        if(gauge==null || source==null) return;
        bool active=isActiveAndEnabled;
        gauge.enabled=active && sourceWasEnabled && source.gameObject.activeInHierarchy;
        source.enabled=active?false:sourceWasEnabled;
        gauge.flipX=source.flipX; gauge.flipY=source.flipY;
        gauge.sortingLayerID=source.sortingLayerID; gauge.sortingOrder=source.sortingOrder;
        gauge.maskInteraction=source.maskInteraction;
        gauge.color=new Color(1,1,1,source.color.a);
        block.SetVector("_SpriteUVRect",source.sprite!=null?DataUtility.GetOuterUV(source.sprite):new Vector4(0,0,1,1));
        block.SetFloat("_FillAmount",current); block.SetFloat("_TrailAmount",trail); block.SetFloat("_Reverse",fillFromRight!=source.flipX?1:0);
        block.SetFloat("_DamageFlash",Mathf.Clamp01(1-(Time.unscaledTime-damageStarted)/.12f));
        block.SetColor("_HealthColor",isLocal?new Color(.18f,.58f,.5f):new Color(.7f,.24f,.29f));
        gauge.SetPropertyBlock(block);
    }
    private void OnDisable() { SnapToCurrent(); if(source!=null) source.enabled=sourceWasEnabled; if(gauge!=null) gauge.enabled=false; }
    private void ReleaseGauge()
    {
        if(source!=null) source.enabled=sourceWasEnabled;
        if(gauge!=null) Destroy(gauge.gameObject);
        gauge=null; source=null;
    }
    private void OnDestroy()=>ReleaseGauge();
}
