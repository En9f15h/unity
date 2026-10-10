using UnityEngine;
using UnityEngine.Rendering;

// Presentation only: never writes character, collider, animator or beat state.
[DisallowMultipleComponent, DefaultExecutionOrder(150)]
public sealed class CharacterGroundPresentation : MonoBehaviour
{
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private Transform[] footAnchors;
    [SerializeField] private Material shadowMaterial, dustMaterial;
    [SerializeField, Range(0f, 1f)] private float shadowOpacity = 0.34f;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private Mesh mesh;
    private MeshRenderer shadow;
    private ParticleSystem dust;
    private MaterialPropertyBlock block;
    private CharacterShaderFeedback feedback;
    private bool hadGround, wasGrounded, strideActive, strideMidpoint, strideEnd;
    private float previousX, footRestOffset, strideStart, strideDuration, lastDustTime = -10f;
    private float strideDistance;
    private bool calibratedFeet;
    private Vector3 contactPoint;
    public bool IsGrounded { get; private set; }
    public float GroundDistance { get; private set; }
    public int DustBurstCount { get; private set; }
    public MeshRenderer ShadowRenderer => shadow;
    public bool HasGround => hadGround && bodyCollider!=null && bodyCollider.enabled && bodyCollider.gameObject.activeInHierarchy;
    public Vector3 GroundPoint => contactPoint;

    private void Awake()
    {
        if(bodyCollider==null) bodyCollider=GetComponent<Collider2D>();
        feedback=GetComponent<CharacterShaderFeedback>();
        if(shadowMaterial==null) shadowMaterial=Resources.Load<Material>("Combat/Materials/ContactShadow");
        if(dustMaterial==null) dustMaterial=Resources.Load<Material>("Combat/Materials/ContactDust");
        CreateVisuals();
        if(GetComponent<CharacterProjectedShadow>()==null) gameObject.AddComponent<CharacterProjectedShadow>();
    }
    private void OnEnable() { hadGround=false; wasGrounded=false; calibratedFeet=false; strideActive=false; previousX=transform.position.x; }
    private void CreateVisuals()
    {
        mesh=new Mesh { name="Contact quad", vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)},
            uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}, colors=new[]{Color.white,Color.white,Color.white,Color.white}, triangles=new[]{0,1,2,0,2,3} };
        mesh.RecalculateBounds();
        var go=new GameObject("Foot contact shadow"); go.transform.SetParent(transform,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh; shadow=go.AddComponent<MeshRenderer>(); shadow.sharedMaterial=shadowMaterial;
        shadow.shadowCastingMode=ShadowCastingMode.Off; shadow.receiveShadows=false;
        shadow.sortingLayerID=SortingLayer.NameToID("charactor"); shadow.sortingOrder=-100;
        block=new MaterialPropertyBlock();
        var dustObject=new GameObject("Contact dust"); dustObject.transform.SetParent(transform,false);
        dust=dustObject.AddComponent<ParticleSystem>(); dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=dust.main; main.playOnAwake=false; main.loop=false; main.maxParticles=32; main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.useUnscaledTime=false; main.startSpeed=0; main.startLifetime=.32f; main.startSize=.12f;
        var emission=dust.emission; emission.enabled=false; var shape=dust.shape; shape.enabled=false;
        var color=dust.colorOverLifetime; color.enabled=true;
        var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.12f),new GradientAlphaKey(0,1)}); color.color=gradient;
        var size=dust.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.45f,1,1.6f));
        var renderer=dust.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=dustMaterial;
        renderer.sortingLayerID=SortingLayer.NameToID("charactor"); renderer.sortingOrder=-90;
    }
    public void BeginStride(float seconds)
    {
        // Called when BattleStepPlayer actually begins movement, after its existing delay.
        strideStart=Time.unscaledTime; strideDuration=Mathf.Max(.01f,seconds);
        strideActive=true; strideMidpoint=false; strideEnd=false; strideDistance=0; previousX=transform.position.x;
    }
    private float LowestFootY(float fallback)
    {
        float y=float.PositiveInfinity;
        if(footAnchors!=null) foreach(var foot in footAnchors) if(foot!=null) y=Mathf.Min(y,foot.position.y);
        return float.IsPositiveInfinity(y)?fallback:y;
    }
    private void LateUpdate()
    {
        if(bodyCollider==null || !bodyCollider.enabled || !bodyCollider.gameObject.activeInHierarchy) { if(shadow!=null) shadow.enabled=false; return; }
        Bounds bounds=bodyCollider.bounds;
        float height=Mathf.Max(.2f,bounds.size.y), width=Mathf.Max(.1f,bounds.size.x);
        var filter=new ContactFilter2D(); filter.useTriggers=false; filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        int count=Physics2D.Raycast(new Vector2(bounds.center.x,bounds.min.y+height*.12f),Vector2.down,filter,hits,height*2f);
        bool found=false; RaycastHit2D ground=default;
        for(int i=0;i<count;i++)
        {
            var candidate=hits[i];
            if(candidate.collider==null || candidate.normal.y<.65f || candidate.collider.transform.IsChildOf(transform) ||
                candidate.collider.GetComponentInParent<CharacterUnit>()!=null) continue;
            if(!found || candidate.distance<ground.distance) { ground=candidate; found=true; }
        }
        if(!found) { shadow.enabled=false; IsGrounded=false; hadGround=false; wasGrounded=false; previousX=transform.position.x; return; }
        if(!calibratedFeet && bounds.min.y-ground.point.y<height*.08f)
        { footRestOffset=LowestFootY(bounds.min.y)-bounds.min.y; calibratedFeet=true; }
        float footY=calibratedFeet?LowestFootY(bounds.min.y+footRestOffset)-footRestOffset:bounds.min.y;
        GroundDistance=Mathf.Max(0,Mathf.Max(bounds.min.y,footY)-ground.point.y);
        IsGrounded=GroundDistance<Mathf.Max(.025f,height*.035f);
        contactPoint=new Vector3(bounds.center.x,ground.point.y+.018f,transform.position.z);
        float lift=Mathf.Clamp01(GroundDistance/height), visibility=feedback!=null?feedback.Visibility:1f;
        shadow.enabled=visibility>.01f && shadowMaterial!=null;
        shadow.transform.SetPositionAndRotation(contactPoint,Quaternion.identity);
        Vector3 desired=new Vector3(width*Mathf.Lerp(1.25f,.65f,lift),height*.07f,1);
        bool hdLighting=HD2DStagePresentation.TryGetCharacterLighting(gameObject,transform.position,out _,out _,out float shadowDirection);
        if(hdLighting)
        {
            // Soft projected footprint. It is deliberately not a fake skeletal silhouette.
            desired.x*=Mathf.Lerp(1.3f,1.65f,lift);
            desired.y*=Mathf.Lerp(1.9f,2.6f,lift);
            shadow.transform.position+=new Vector3(shadowDirection*height*(.025f+.06f*lift),-height*.025f,0);
        }
        Vector3 parentScale=transform.lossyScale;
        shadow.transform.localScale=new Vector3(desired.x/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),desired.y/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),1);
        block.SetColor("_Color",new Color(.08f,.075f,.09f,shadowOpacity*Mathf.Lerp(1,.25f,lift)*visibility)); shadow.SetPropertyBlock(block);
        float travel=Mathf.Abs(transform.position.x-previousX);
        if(hadGround && !wasGrounded && IsGrounded) EmitDust(width,1f,visibility);
        if(strideActive)
        {
            if(travel<width*2f) strideDistance+=travel;
            float progress=(Time.unscaledTime-strideStart)/strideDuration;
            bool mid=!strideMidpoint && progress>=.5f, end=!strideEnd && progress>=.95f;
            if(mid) strideMidpoint=true; if(end) strideEnd=true;
            if((mid || end) && IsGrounded && strideDistance>width*.03f && travel<width*2f)
            { EmitDust(width,.6f,visibility); strideDistance=0; }
            if(progress>=1) strideActive=false;
        }
        hadGround=true; wasGrounded=IsGrounded; previousX=transform.position.x;
    }
    private void EmitDust(float width,float strength,float visibility)
    {
        if(dustMaterial==null || visibility<.1f || Time.timeScale<=0 || Time.unscaledTime-lastDustTime<.08f) return;
        lastDustTime=Time.unscaledTime; DustBurstCount++;
        dust.transform.position=contactPoint; dust.Play();
        int count=strength>.8f?7:4;
        for(int i=0;i<count;i++)
        {
            // Local deterministic variation avoids consuming gameplay's UnityEngine.Random stream.
            float side=(i-(count-1)*.5f)/Mathf.Max(1,count-1);
            var emit=new ParticleSystem.EmitParams { position=contactPoint+Vector3.right*side*width*.5f,
                velocity=new Vector3(side*width*.65f,width*(.13f+.025f*(i%3)),0),
                startSize=width*(.16f+.03f*(i%3))*strength, startLifetime=.24f+.035f*(i%3),
                startColor=new Color(.6f,.56f,.48f,.45f*visibility), randomSeed=(uint)(i+1) };
            dust.Emit(emit,1);
        }
    }
    private void OnDisable()
    {
        if(shadow!=null) shadow.enabled=false;
        if(dust!=null) dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        strideActive=false; IsGrounded=false;
    }
    private void OnDestroy() { if(mesh!=null) Destroy(mesh); }
}
