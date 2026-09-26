using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StageAtmosphereController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private StageAtmospherePalette palette;
    private GameObject overlay;
    private Mesh mesh;
    private MeshRenderer mistRenderer;
    private UIShaderFeedback mistUI;
    private MaterialPropertyBlock block;
    public static void ForImage(Image image)
    {
        if(image==null) return;
        var controller=image.GetComponent<StageAtmosphereController>() ?? image.gameObject.AddComponent<StageAtmosphereController>();
        controller.backgroundImage=image;
        StageReadabilityController.ForImage(image);
    }
    private void LateUpdate()=>Refresh();
    public void Refresh()
    {
        if(palette==null) palette=Resources.Load<StageAtmospherePalette>("Combat/StageAtmospherePalette");
        Sprite source=backgroundRenderer!=null ? backgroundRenderer.sprite : backgroundImage!=null ? backgroundImage.sprite : null;
        if(palette==null || !palette.TryGet(source,out var entry)) { if(overlay!=null) overlay.SetActive(false); return; }
        if(overlay==null) CreateOverlay();
        if(overlay==null) return;
        overlay.SetActive(true);
        if(backgroundRenderer!=null)
        {
            Bounds bounds=backgroundRenderer.bounds;
            overlay.transform.position=bounds.center;
            overlay.transform.localScale=new Vector3(bounds.size.x,bounds.size.y,1);
            mistRenderer.sortingLayerID=backgroundRenderer.sortingLayerID; mistRenderer.sortingOrder=backgroundRenderer.sortingOrder+1;
            block.SetColor("_MistColor",entry.color); block.SetFloat("_Density",entry.density); mistRenderer.SetPropertyBlock(block);
        }
        else
        {
            Rect rect=backgroundImage.GetPixelAdjustedRect();
            Vector2 size=rect.size;
            if(backgroundImage.preserveAspect && source.rect.height>0)
            {
                float aspect=source.rect.width/source.rect.height;
                if(size.x/Mathf.Max(0.001f,size.y)>aspect) size.x=size.y*aspect; else size.y=size.x/aspect;
            }
            var overlayRect=(RectTransform)overlay.transform;
            if(overlayRect.sizeDelta!=size) overlayRect.sizeDelta=size;
            mistUI.SetAtmosphere(entry.color,entry.density*0.65f);
        }
    }
    private void CreateOverlay()
    {
        if(backgroundRenderer!=null)
        {
            overlay=new GameObject("Stage Atmosphere");
            mesh=new Mesh{name="Stage atmosphere quad",vertices=new[]{new Vector3(-0.5f,-0.5f,0),new Vector3(0.5f,-0.5f,0),new Vector3(0.5f,0.5f,0),new Vector3(-0.5f,0.5f,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},triangles=new[]{0,1,2,0,2,3}};
            mesh.RecalculateBounds(); overlay.AddComponent<MeshFilter>().sharedMesh=mesh;
            mistRenderer=overlay.AddComponent<MeshRenderer>(); mistRenderer.sharedMaterial=Resources.Load<Material>("Combat/Materials/StageMist");
            block=new MaterialPropertyBlock();
        }
        else if(backgroundImage!=null)
        {
            overlay=new GameObject("Stage Atmosphere",typeof(RectTransform)); overlay.transform.SetParent(backgroundImage.transform,false);
            var rect=(RectTransform)overlay.transform; rect.anchorMin=rect.anchorMax=new Vector2(0.5f,0.5f); rect.anchoredPosition=Vector2.zero;
            var image=overlay.AddComponent<Image>(); image.raycastTarget=false; mistUI=UIShaderFeedback.Ensure(image,"UI_Mist");
        }
    }
    private void OnDisable() { if(overlay!=null) overlay.SetActive(false); }
    private void OnDestroy() { if(overlay!=null) Destroy(overlay); if(mesh!=null) Destroy(mesh); }
}
