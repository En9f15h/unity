using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Sprites;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

// Bake the current pose once, preserving the source UVs. No extra live skeletons or materials.
public sealed class CombatGhostSnapshot : MonoBehaviour
{
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private Color tint;
    private float startTime, duration;
    public static void Create(SpriteRenderer source, Transform parent, Vector3 offset, Color color, float lifetime, int sortingOffset)
    {
        if (source == null || source.sprite == null || !source.enabled || !source.gameObject.activeInHierarchy) return;
        Sprite sprite=source.sprite;
        Vector2[] original=sprite.vertices;
        Vector3[] vertices=new Vector3[original.Length];
        for(int i=0;i<vertices.Length;i++) vertices[i]=original[i];
        SpriteSkin skin=source.GetComponent<SpriteSkin>();
        if(skin!=null && skin.enabled && sprite.HasVertexAttribute(VertexAttribute.BlendWeight))
        {
            var bindPoses=sprite.GetBindPoses();
            var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight);
            Transform[] bones=skin.boneTransforms;
            if(bones!=null && bones.Length==bindPoses.Length && weights.Length==vertices.Length)
            {
                Matrix4x4[] matrices=new Matrix4x4[bones.Length];
                for(int b=0;b<bones.Length;b++) matrices[b]=bones[b]!=null ? source.transform.worldToLocalMatrix*bones[b].localToWorldMatrix*bindPoses[b] : Matrix4x4.identity;
                for(int i=0;i<vertices.Length;i++)
                {
                    BoneWeight w=weights[i]; Vector3 p=vertices[i];
                    vertices[i]=Weighted(matrices,w.boneIndex0,w.weight0,p)+Weighted(matrices,w.boneIndex1,w.weight1,p)
                        +Weighted(matrices,w.boneIndex2,w.weight2,p)+Weighted(matrices,w.boneIndex3,w.weight3,p);
                }
            }
        }
        // Bake to world space: this also preserves negative scale and transformed ancestors exactly.
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=vertices[i]; if(source.flipX) p.x=-p.x; if(source.flipY) p.y=-p.y;
            vertices[i]=source.transform.TransformPoint(p)+offset;
        }
        ushort[] sourceTriangles=sprite.triangles;
        int[] triangles=new int[sourceTriangles.Length];
        for(int i=0;i<triangles.Length;i++) triangles[i]=sourceTriangles[i];
        Color[] colors=new Color[vertices.Length]; for(int i=0;i<colors.Length;i++) colors[i]=Color.white;
        GameObject go=new GameObject(source.name+"_PoseGhost");
        go.transform.SetParent(parent,true); go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        go.transform.localScale=Vector3.one;
        var snapshot=go.AddComponent<CombatGhostSnapshot>();
        snapshot.mesh=new Mesh { name="Temporary ghost pose",vertices=vertices,uv=sprite.uv,triangles=triangles,colors=colors };
        snapshot.mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=snapshot.mesh;
        snapshot.meshRenderer=go.AddComponent<MeshRenderer>();
        snapshot.meshRenderer.sharedMaterial=CombatShaderMaterials.Ghost;
        snapshot.meshRenderer.sortingLayerID=source.sortingLayerID; snapshot.meshRenderer.sortingOrder=source.sortingOrder+sortingOffset;
        snapshot.block=new MaterialPropertyBlock();
        snapshot.block.SetTexture("_MainTex",sprite.texture);
        snapshot.block.SetVector("_SpriteUVRect",DataUtility.GetOuterUV(sprite));
        snapshot.block.SetFloat("_MeshSprite",1);
        snapshot.tint=color; snapshot.duration=Mathf.Max(0.05f,lifetime); snapshot.startTime=Time.unscaledTime;
        snapshot.LateUpdate();
    }
    private static Vector3 Weighted(Matrix4x4[] matrices,int index,float weight,Vector3 vertex)
        => weight>0 && index>=0 && index<matrices.Length ? matrices[index].MultiplyPoint3x4(vertex)*weight : Vector3.zero;
    private void LateUpdate()
    {
        if(meshRenderer==null) return;
        float progress=Mathf.Clamp01((Time.unscaledTime-startTime)/duration);
        Color color=tint; color.a*=1-progress;
        block.SetColor("_Color",color); block.SetFloat("_EffectProgress",progress);
        meshRenderer.SetPropertyBlock(block);
        if(progress>=1f) Destroy(gameObject);
    }
    private void OnDestroy() { if(mesh!=null) Destroy(mesh); }
}
