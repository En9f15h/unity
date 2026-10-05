using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class CombatImpactPresentation : MonoBehaviour
{
    [SerializeField] private Material impactMaterial;
    private Mesh mesh;
    private MeshRenderer accent;
    private MaterialPropertyBlock block;
    private float started, duration=.12f;
    private bool playing;
    public ActionType LastAction { get; private set; }
    public bool LastActionReleased { get; private set; }
    public float LastActionSeconds { get; private set; }
    public int LastImpactTier { get; private set; }
    public MeshRenderer AccentRenderer => accent;

    public void BeginAction(ActionType action,bool released,float seconds)
    { LastAction=action; LastActionReleased=released; LastActionSeconds=seconds; }
    private void Awake()
    {
        if(impactMaterial==null) impactMaterial=Resources.Load<Material>("Combat/Materials/ImpactAccent");
        mesh=new Mesh { name="Impact accent quad",vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)},
            uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}, triangles=new[]{0,1,2,0,2,3} }; mesh.RecalculateBounds();
        var go=new GameObject("Resolved impact accent"); go.transform.SetParent(transform,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh; accent=go.AddComponent<MeshRenderer>(); accent.sharedMaterial=impactMaterial;
        accent.sortingLayerID=SortingLayer.NameToID("charactor"); accent.sortingOrder=120;
        accent.shadowCastingMode=ShadowCastingMode.Off; accent.receiveShadows=false; accent.enabled=false;
        block=new MaterialPropertyBlock();
    }
    public static int ResolveTier(ActionType action,bool released)
    {
        return action==ActionType.Ultimate || ((action==ActionType.HeavyAttack || action==ActionType.Rift) && released)?1:0;
    }
    // Called by the combat resolver only after a real hit or successful parry.
    public void PlayResolvedImpact(CharacterUnit attacker,bool parry=false)
    {
        if(!isActiveAndEnabled || accent==null || impactMaterial==null) return;
        var source=attacker!=null?attacker.GetComponent<CombatImpactPresentation>():null;
        int tier=parry?2:source!=null?ResolveTier(source.LastAction,source.LastActionReleased):0;
        LastImpactTier=tier;
        var collider=GetComponent<Collider2D>();
        Bounds bounds=collider!=null?collider.bounds:new Bounds(transform.position,Vector3.one);
        float direction=attacker!=null && attacker.transform.position.x<bounds.center.x?-1:1;
        Vector3 point=bounds.center+Vector3.right*direction*bounds.extents.x*.65f;
        point.y+=bounds.size.y*.08f;
        accent.transform.SetPositionAndRotation(point,Quaternion.Euler(0,0,direction<0?180:0));
        float size=bounds.size.y*(tier==1?.62f:tier==2?.5f:.32f);
        var scale=transform.lossyScale;
        accent.transform.localScale=new Vector3(size/Mathf.Max(.001f,Mathf.Abs(scale.x)),size/Mathf.Max(.001f,Mathf.Abs(scale.y)),1);
        bool oracle=attacker!=null && attacker.GetComponent<OracleVFXController>()!=null;
        Color color=parry?new Color(1.6f,1.35f,.65f):oracle?new Color(.6f,.85f,1.5f):new Color(1.5f,.95f,.45f);
        block.SetColor("_Color",color); block.SetFloat("_Mode",tier); block.SetFloat("_Progress",0); accent.SetPropertyBlock(block);
        duration=tier==1?.18f:tier==2?.14f:.095f; started=Time.unscaledTime; playing=true; accent.enabled=true;
        if(!parry) GetComponent<CharacterShaderFeedback>()?.FlashHit(tier==1?.17f:.095f,tier==1?.95f:.72f,color);
        else GetComponent<CharacterShaderFeedback>()?.FlashGuard();
        if(!parry && CameraShake.Instance!=null) CameraShake.Instance.Shake(tier==1?.12f:.055f,tier==1?.06f:.018f);
    }
    private void LateUpdate()
    {
        if(!playing) return;
        float progress=Mathf.Clamp01((Time.unscaledTime-started)/duration);
        block.SetFloat("_Progress",progress); accent.SetPropertyBlock(block);
        if(progress>=1) { playing=false; accent.enabled=false; }
    }
    private void OnDisable() { playing=false; if(accent!=null) accent.enabled=false; LastAction=ActionType.None; }
    private void OnDestroy() { if(mesh!=null) Destroy(mesh); }
}
