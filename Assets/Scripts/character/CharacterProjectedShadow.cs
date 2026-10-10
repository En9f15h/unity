using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Sprites;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

// Read-only pose projection. Never changes SpriteSkin, Animator, bones, or physics.
[DisallowMultipleComponent, DefaultExecutionOrder(1700)]
public sealed class CharacterProjectedShadow : MonoBehaviour
{
    private sealed class Part
    {
        public SpriteRenderer source;
        public Sprite sprite;
        public SpriteSkin skin;
        public Mesh mesh;
        public MeshRenderer renderer;
        public MaterialPropertyBlock block=new MaterialPropertyBlock();
        public Vector3[] original, projected;
        public BoneWeight[] weights;
        public Matrix4x4[] bindPoses, matrices;
    }
    private readonly List<Part> parts=new List<Part>();
    private CharacterShaderFeedback feedback;
    private CharacterGroundPresentation contact;
    private Material material;
    public int VisiblePartCount { get; private set; }
    public int MeshCount => parts.Count;
    public Bounds ProjectionBounds { get; private set; }
    private void Awake()
    {
        feedback=GetComponent<CharacterShaderFeedback>();contact=GetComponent<CharacterGroundPresentation>();
        material=Resources.Load<Material>("HD2D/CharacterShadow");
    }
    private void Cache(Part part)
    {
        part.sprite=part.source.sprite;
        var vertices=part.sprite.vertices;
        part.original=new Vector3[vertices.Length];part.projected=new Vector3[vertices.Length];
        for(int i=0;i<vertices.Length;i++)part.original[i]=vertices[i];
        part.skin=part.source.GetComponent<SpriteSkin>();part.weights=null;part.bindPoses=null;part.matrices=null;
        if(part.skin!=null && part.sprite.HasVertexAttribute(VertexAttribute.BlendWeight))
        {
            part.weights=part.sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
            part.bindPoses=part.sprite.GetBindPoses().ToArray();part.matrices=new Matrix4x4[part.bindPoses.Length];
        }
        var indices=part.sprite.triangles;var triangles=new int[indices.Length];
        for(int i=0;i<indices.Length;i++)triangles[i]=indices[i];
        part.mesh.Clear();part.mesh.vertices=part.original;part.mesh.uv=part.sprite.uv;part.mesh.triangles=triangles;
        part.block.SetTexture("_MainTex",part.sprite.texture);
        part.block.SetVector("_UVRect",DataUtility.GetOuterUV(part.sprite));
    }
    private void CreateParts()
    {
        foreach(var source in feedback.BodyRenderers)
        {
            if(source==null || source.sprite==null)continue;
            var go=new GameObject("HD2D character pose shadow");go.transform.SetParent(transform,false);go.layer=source.gameObject.layer;
            var part=new Part { source=source,mesh=new Mesh {name="HD2D projected pose"},renderer=go.AddComponent<MeshRenderer>() };
            part.mesh.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=part.mesh;
            part.renderer.sharedMaterial=material;part.renderer.shadowCastingMode=ShadowCastingMode.Off;part.renderer.receiveShadows=false;
            part.renderer.sortingLayerID=source.sortingLayerID;part.renderer.sortingOrder=-110;
            parts.Add(part);Cache(part);
        }
    }
    private static Vector3 Weighted(Matrix4x4[] matrices,int index,float weight,Vector3 vertex)
        => weight>0 && index>=0 && index<matrices.Length ? matrices[index].MultiplyPoint3x4(vertex)*weight : Vector3.zero;
    private void Hide()
    {
        VisiblePartCount=0;
        foreach(var part in parts)if(part.renderer!=null)part.renderer.enabled=false;
    }
    private void LateUpdate()
    {
        if(material==null || feedback==null || contact==null || !contact.isActiveAndEnabled || !contact.HasGround ||
            feedback.Visibility<.01f || !HD2DStagePresentation.TryGetCharacterEnvironment(gameObject,out var environment))
        {Hide();return;}
        if(parts.Count==0 && feedback.BodyRenderers!=null)CreateParts();
        VisiblePartCount=0;bool hasBounds=false;Bounds bounds=default;
        float groundY=contact.GroundPoint.y;
        float opacity=environment.shadowOpacity*Mathf.Lerp(1,.35f,Mathf.Clamp01(contact.GroundDistance/2f))*feedback.Visibility;
        foreach(var part in parts)
        {
            var source=part.source;
            if(source==null || source.sprite==null || !source.enabled || !source.gameObject.activeInHierarchy){part.renderer.enabled=false;continue;}
            if(part.sprite!=source.sprite)Cache(part);
            var bones=part.skin!=null?part.skin.boneTransforms:null;
            bool skinned=part.skin!=null && part.skin.isActiveAndEnabled && bones!=null && part.bindPoses!=null &&
                bones.Length==part.bindPoses.Length && part.weights.Length==part.original.Length;
            Matrix4x4 toLocal=source.transform.worldToLocalMatrix;
            if(skinned)for(int b=0;b<bones.Length;b++)part.matrices[b]=bones[b]!=null?toLocal*bones[b].localToWorldMatrix*part.bindPoses[b]:Matrix4x4.identity;
            var shadowTransform=part.renderer.transform;
            for(int i=0;i<part.original.Length;i++)
            {
                Vector3 p=part.original[i];
                if(skinned)
                {
                    BoneWeight w=part.weights[i];
                    p=Weighted(part.matrices,w.boneIndex0,w.weight0,p)+Weighted(part.matrices,w.boneIndex1,w.weight1,p)
                        +Weighted(part.matrices,w.boneIndex2,w.weight2,p)+Weighted(part.matrices,w.boneIndex3,w.weight3,p);
                }
                if(source.flipX)p.x=-p.x;if(source.flipY)p.y=-p.y;
                p=source.transform.TransformPoint(p);
                float height=Mathf.Max(0,p.y-groundY);
                var projected=new Vector3(p.x+environment.shadowDirection*height*environment.shadowLength,groundY-height*environment.shadowDepth-.012f,transform.position.z+.05f);
                part.projected[i]=shadowTransform.InverseTransformPoint(projected);
            }
            part.mesh.vertices=part.projected;part.mesh.RecalculateBounds();
            part.block.SetColor("_ShadowColor",new Color(.045f,.05f,.07f,opacity*source.color.a));
            part.block.SetFloat("_Softness",.65f+Mathf.Clamp(contact.GroundDistance,0,2));
            part.renderer.SetPropertyBlock(part.block);part.renderer.enabled=true;VisiblePartCount++;
            if(!hasBounds){bounds=part.renderer.bounds;hasBounds=true;}else bounds.Encapsulate(part.renderer.bounds);
        }
        ProjectionBounds=bounds;
    }
    private void OnDisable()=>Hide();
    private void OnDestroy()
    {
        foreach(var part in parts){if(part.mesh!=null)Destroy(part.mesh);if(part.renderer!=null)Destroy(part.renderer.gameObject);}
        parts.Clear();
    }
}
