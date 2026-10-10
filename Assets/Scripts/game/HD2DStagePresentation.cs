using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A stage-only 3D pass. Gameplay objects and their 2D materials never enter this camera.
[DisallowMultipleComponent, DefaultExecutionOrder(1600)]
public sealed class HD2DStagePresentation : MonoBehaviour
{
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private SpriteRenderer background;
    [SerializeField] private Collider2D ground;
    [SerializeField] private Sprite supportedBackground;
    [SerializeField] private int rendererIndex = 1;
    [SerializeField, Range(360, 1080)] private int maximumRenderHeight = 900;
    [SerializeField, Range(0, 2)] private float backgroundSoftness = .8f;
    [SerializeField, Range(0, .15f)] private float mistDensity = .035f;
    [SerializeField, Range(0, .4f)] private float bloomIntensity = .12f;
    [SerializeField, Range(.3f, 2)] private float keyIntensity = 1.15f;
    [SerializeField] private bool characterLighting = true;
    [SerializeField] private CharacterEnvironmentPalette characterPalette;
    [Header("Overhead daylight")]
    [Tooltip("Optional light. When empty, a soft overhead daylight source is created at runtime.")]
    [SerializeField] private Light characterLightSource;
    [SerializeField] private bool animateCharacterLight = true;
    [SerializeField, Min(2)] private float characterLightPeriod = 24;
    [SerializeField, Range(0, 2)] private float characterLightSweep = .65f;
    [SerializeField, Range(0, .5f)] private float sunlightCanopy = .24f;
    private bool ownsCharacterLight;
    private Sprite lightProfileSprite;
    public Light CharacterLightSource => characterLightSource;
    public bool AnimateCharacterLight { get => animateCharacterLight; set => animateCharacterLight=value; }
    private static readonly Dictionary<int, HD2DStagePresentation> stages = new Dictionary<int, HD2DStagePresentation>();
    public bool CharacterLightingEnabled { get => characterLighting; set => characterLighting=value; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStages() => stages.Clear();
    private void OnEnable()
    {
        if(characterPalette==null)characterPalette=Resources.Load<CharacterEnvironmentPalette>("HD2D/CharacterEnvironmentPalette");
        stages[gameObject.scene.handle]=this;
        if(Application.isPlaying)EnsureCharacterLight();
    }
    private void EnsureCharacterLight()
    {
        if(characterLightSource!=null)return;
        var obj=new GameObject("Overhead daylight source");
        obj.transform.SetParent(transform,false);
        characterLightSource=obj.AddComponent<Light>(); ownsCharacterLight=true;
        characterLightSource.type=LightType.Point;
        characterLightSource.color=new Color(.94f,.97f,1f);
        characterLightSource.intensity=2.1f; characterLightSource.range=26f;
        characterLightSource.shadows=LightShadows.None;
        characterLightSource.cullingMask=LayerMask.GetMask("HD2DStage");
        float floor=ground!=null?ground.bounds.max.y:-2.5f;
        obj.transform.position=new Vector3(DaylightCenterX,floor+10f,6.8f);
        ApplyOwnedLightProfile();
    }
    private float DaylightCenterX => (background!=null?background.bounds.center.x:0)-3f;
    private bool TryGetDaylightProfile(out CharacterEnvironmentPalette.Entry entry)
    {
        entry=default;
        return characterPalette!=null && background!=null && characterPalette.TryGet(background.sprite,out entry);
    }
    private void ApplyOwnedLightProfile()
    {
        if(!ownsCharacterLight || !TryGetDaylightProfile(out var entry) || lightProfileSprite==background.sprite)return;
        lightProfileSprite=background.sprite;
        characterLightSource.color=entry.daylightColor;
        characterLightSource.intensity=entry.daylightIntensity;
        float floor=ground!=null?ground.bounds.max.y:-2.5f;
        characterLightSource.transform.position=new Vector3(background.bounds.center.x+entry.daylightOffset.x,
            floor+Mathf.Max(4,entry.daylightOffset.y),6.8f);
    }
    private void Update()
    {
        if(!Application.isPlaying)return;
        EnsureCharacterLight();
        ApplyOwnedLightProfile();
        bool available=characterLighting && background!=null && background.enabled && characterPalette!=null &&
            characterPalette.TryGet(background.sprite,out _);
        if(ownsCharacterLight)characterLightSource.gameObject.SetActive(available);
        if(!available || !animateCharacterLight || !ownsCharacterLight)return;
        // Read a shared visual clock. Never alter Photon state, turn clocks or animation speed.
        double clock=PhotonNetwork.InRoom?PhotonNetwork.Time:Time.timeAsDouble;
        double period=System.Math.Max(2,characterLightPeriod);
        float phase=(float)((clock%period)/period*System.Math.PI*2);
        float floor=ground!=null?ground.bounds.max.y:-2.5f;
        TryGetDaylightProfile(out var entry);
        // Small, slow drift above the arena, rather than a side-to-side spotlight.
        characterLightSource.transform.position=new Vector3(background.bounds.center.x+entry.daylightOffset.x+
            Mathf.Sin(phase)*characterLightSweep*entry.daylightDrift,
            floor+Mathf.Max(4,entry.daylightOffset.y),6.8f);
    }
    private float CharacterLightAmount(Vector3 position)
    {
        if(characterLightSource==null || !characterLightSource.isActiveAndEnabled)return 0;
        Vector2 delta=position-characterLightSource.transform.position;
        float range=Mathf.Max(.01f,characterLightSource.range);
        float falloff=Mathf.Clamp01(1-delta.sqrMagnitude/(range*range));
        return falloff*falloff*Mathf.Clamp(characterLightSource.intensity,0,4);
    }
    private float CharacterShadowDirection(Vector3 position)
    {
        float floor=ground!=null?ground.bounds.max.y:position.y-1;
        return Mathf.Clamp((position.x-characterLightSource.transform.position.x)/
            Mathf.Max(1.5f,characterLightSource.transform.position.y-floor),-1,1);
    }
    public static bool TryGetCharacterEnvironment(GameObject character,out CharacterEnvironmentPalette.Entry entry)
    {
        entry=default;
        bool available=stages.TryGetValue(character.scene.handle,out var stage) && stage!=null && stage.isActiveAndEnabled &&
            stage.characterLighting && stage.background!=null && stage.background.enabled && stage.characterPalette!=null &&
            stage.characterPalette.TryGet(stage.background.sprite,out entry);
        if(!available || stage.characterLightSource==null || !stage.characterLightSource.isActiveAndEnabled ||
            stage.characterLightSource.intensity<=0)return false;
        entry.arenaBraziers=false;
        entry.shadowDirection=stage.CharacterShadowDirection(character.transform.position);
        entry.shadowOpacity*=Mathf.Clamp01(.25f+.65f*stage.CharacterLightAmount(character.transform.position));
        return true;
    }
    public static bool TryGetCharacterLighting(GameObject character, Vector3 position, out Color body, out Color edge, out float shadowDirection)
    {
        body=Color.white; edge=Color.clear; shadowDirection=0;
        if(!TryGetCharacterEnvironment(character,out var entry))return false;
        var stage=stages[character.scene.handle];
        float amount=stage.CharacterLightAmount(position);
        Color color=stage.characterLightSource.color;
        // Keep the artwork's albedo: broad lighting stays nearly neutral, while
        // the directional edge carries the more visible cool accent.
        body=Color.Lerp(Color.white,color,.12f)*(.94f+amount*.08f); body.a=1;
        edge=color*(amount*.16f); edge.a=1;
        shadowDirection=stage.CharacterShadowDirection(position);
        return true;
    }
    private GameObject stageRoot, composite;
    private GameObject daylightOverlay;
    private MeshRenderer daylightRenderer;
    private Material daylightMaterial;
    public bool IsDaylightCanopyActive => daylightOverlay!=null && daylightOverlay.activeInHierarchy;
    private Camera stageCamera;
    private MeshRenderer compositeRenderer;
    private Material stone, backdrop, compositeMaterial;
    private RenderTexture target;
    private VolumeProfile profile;
    private Light key;
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Mesh> meshes = new List<Mesh>();
    private StageAtmosphereController atmosphere;
    private bool atmosphereWasEnabled, controlsAtmosphere, excludedLayer;
    private int stageLayer, originalCullingMask;
    public bool IsStageActive => stageRoot != null && stageRoot.activeSelf;
    public Camera StageCamera => stageCamera;
    public RenderTexture StageTexture => target;
    public Light KeyLight => key;
    public float BackgroundSoftness { get => backgroundSoftness; set => backgroundSoftness = Mathf.Clamp(value, 0, 2); }
    public float MistDensity { get => mistDensity; set => mistDensity = Mathf.Clamp(value, 0, .15f); }

    private void LateUpdate()
    {
        UpdateDaylightCanopy();
        bool wanted = background != null && background.sprite == supportedBackground && supportedBackground != null && gameplayCamera != null && ground != null;
        if (!wanted) { Suspend(); return; }
        if (stageRoot == null && !CreateStage()) return;
        stageRoot.SetActive(true); composite.SetActive(true);
        if (!excludedLayer)
        {
            originalCullingMask = gameplayCamera.cullingMask;
            gameplayCamera.cullingMask &= ~(1 << stageLayer); excludedLayer = true;
        }
        if (!controlsAtmosphere && atmosphere != null)
        {
            atmosphereWasEnabled = atmosphere.enabled; atmosphere.enabled = false; controlsAtmosphere = true;
        }
        ResizeTarget();
        stageCamera.transform.SetPositionAndRotation(gameplayCamera.transform.position, gameplayCamera.transform.rotation);
        stageCamera.orthographicSize = gameplayCamera.orthographicSize;
        stageCamera.aspect = gameplayCamera.aspect;
        stageCamera.depth = gameplayCamera.depth - 10;
        composite.transform.SetPositionAndRotation(gameplayCamera.transform.TransformPoint(0,0,20),gameplayCamera.transform.rotation);
        composite.transform.localScale = new Vector3(stageCamera.orthographicSize*2*stageCamera.aspect,stageCamera.orthographicSize*2,1);
        backdrop.SetFloat("_Softness",backgroundSoftness); backdrop.SetFloat("_Mist",mistDensity);
        ConfigureCanopy(backdrop);
        key.intensity=keyIntensity;
        if(TryGetDaylightProfile(out var lightProfile))
        {
            key.color=Color.Lerp(Color.white,lightProfile.daylightColor,.45f);
            key.transform.rotation=Quaternion.LookRotation(new Vector3(lightProfile.beamSlope,-1,-.4f));
        }
        if(profile.TryGet<Bloom>(out var bloom)) bloom.intensity.value=bloomIntensity;
    }

    private float DaylightStrength => characterPalette!=null && background!=null &&
        characterPalette.TryGet(background.sprite,out var entry)?sunlightCanopy*entry.sunlightMultiplier:0;

    private void ConfigureCanopy(Material material)
    {
        if(!TryGetDaylightProfile(out var entry)) { material.SetFloat("_SunCanopy",0); return; }
        Vector3 position=characterLightSource!=null?characterLightSource.transform.position:
            new Vector3(background.bounds.center.x+entry.daylightOffset.x,ground.bounds.max.y+entry.daylightOffset.y,6.8f);
        material.SetVector("_SunOrigin",new Vector4(position.x,position.y,ground.bounds.max.y,0));
        material.SetFloat("_SunCanopy",DaylightStrength);
        material.SetColor("_SunColor",entry.daylightColor);
        material.SetVector("_BeamShape",new Vector4(entry.beamSlope,entry.beamWidth,entry.beamSpread,entry.beamFocus));
        material.SetVector("_BeamDetail",new Vector4(entry.secondaryBeam,entry.crownStrength,0,0));
    }

    private void UpdateDaylightCanopy()
    {
        // Other maps keep their original SpriteRenderer and camera; add only the veil.
        bool wanted=background!=null && background.enabled && background.sprite!=supportedBackground &&
            ground!=null && DaylightStrength>0;
        if(!wanted) { if(daylightOverlay!=null)daylightOverlay.SetActive(false); return; }
        if(daylightOverlay==null)
        {
            daylightMaterial=CreateMaterial("DaylightCanopy");
            if(daylightMaterial==null)return;
            daylightOverlay=Quad("Overhead daylight canopy",transform,daylightMaterial,background.gameObject.layer);
            daylightRenderer=daylightOverlay.GetComponent<MeshRenderer>();
        }
        daylightOverlay.SetActive(true);
        Bounds bounds=background.bounds;
        daylightOverlay.transform.SetPositionAndRotation(bounds.center,Quaternion.identity);
        daylightOverlay.transform.localScale=Vector3.one;
        // Set world size even when the authored background has a scaled parent.
        Vector3 parentScale=transform.lossyScale;
        daylightOverlay.transform.localScale=new Vector3(bounds.size.x/Mathf.Max(.0001f,Mathf.Abs(parentScale.x)),
            bounds.size.y/Mathf.Max(.0001f,Mathf.Abs(parentScale.y)),1);
        daylightRenderer.sortingLayerID=background.sortingLayerID;
        daylightRenderer.sortingOrder=background.sortingOrder+2;
        ConfigureCanopy(daylightMaterial);
    }

    private Material CreateMaterial(string name)
    {
        var template=Resources.Load<Material>("HD2D/"+name);
        if(template==null || template.shader==null || !template.shader.isSupported) return null;
        var material=new Material(template) { name="HD2D instance "+name }; materials.Add(material); return material;
    }
    private bool CreateStage()
    {
        stageLayer=LayerMask.NameToLayer("HD2DStage");
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(stageLayer<0 || pipeline==null) { Debug.LogWarning("HD2D stage needs its dedicated layer and URP renderer.",this); enabled=false; return false; }
        stone=CreateMaterial("Stone");
        backdrop=CreateMaterial("Backdrop"); compositeMaterial=CreateMaterial("Composite");
        if(stone==null || backdrop==null || compositeMaterial==null)
        { Debug.LogWarning("HD2D stage assets unavailable; keeping original 2D stage.",this); Release(); enabled=false; return false; }
        atmosphere=background.GetComponent<StageAtmosphereController>();
        stageRoot=new GameObject("HD2D stage (visual only)"); stageRoot.transform.SetParent(transform,false);
        stageRoot.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); stageRoot.transform.localScale=Vector3.one;
        var cameraObject=Child("Stage camera"); stageCamera=cameraObject.AddComponent<Camera>();
        stageCamera.orthographic=true; stageCamera.nearClipPlane=.1f; stageCamera.farClipPlane=60;
        stageCamera.clearFlags=CameraClearFlags.SolidColor; stageCamera.backgroundColor=new Color(.12f,.14f,.17f,1);
        stageCamera.cullingMask=1<<stageLayer; stageCamera.allowHDR=true; stageCamera.allowMSAA=false; stageCamera.useOcclusionCulling=false;
        var cameraData=stageCamera.GetUniversalAdditionalCameraData(); cameraData.SetRenderer(rendererIndex);
        cameraData.renderPostProcessing=true; cameraData.requiresDepthTexture=true; cameraData.requiresColorTexture=false;
        cameraData.volumeLayerMask=1<<stageLayer; cameraData.volumeTrigger=stageCamera.transform;
        var volume=Child("Stage-only post processing").AddComponent<Volume>(); volume.isGlobal=true; volume.priority=20;
        profile=ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile=profile;
        var templateProfile=Resources.Load<VolumeProfile>("HD2D/StagePost");
        if(templateProfile!=null) foreach(var component in templateProfile.components) profile.components.Add(Instantiate(component));

        var lightObject=Child("Overhead sun key"); key=lightObject.AddComponent<Light>();
        key.type=LightType.Directional; key.color=new Color(1f,.98f,.94f); key.intensity=keyIntensity;
        key.shadows=LightShadows.Soft; key.shadowStrength=.55f; key.shadowBias=.035f; key.shadowNormalBias=.12f;
        key.cullingMask=1<<stageLayer; lightObject.transform.rotation=Quaternion.LookRotation(new Vector3(.3f,-1f,-.4f));
        float floorY=ground.bounds.max.y;
        Bounds bg=background.bounds;
        // An inclined receiving plane projects into the lower part of the XY game view.
        float angle=28*Mathf.Deg2Rad, depth=8.6f;
        float centerY=floorY-Mathf.Sin(angle)*depth*.5f-.1f;
        // Project the existing whole background onto the receiving floor, retaining
        // the authored stone detail without cutting or generating any image files.
        var floorMaterial=new Material(stone) { name="HD2D existing floor detail" };materials.Add(floorMaterial);
        floorMaterial.SetTexture("_BackdropTex",background.sprite.texture);floorMaterial.SetFloat("_ProjectBackdrop",1);
        floorMaterial.SetFloat("_Ambient",.6f);
        floorMaterial.SetVector("_ProjectionRect",new Vector4(bg.min.x,bg.min.y,bg.max.x,bg.max.y));
        floorMaterial.SetVector("_BackdropUV",UnityEngine.Sprites.DataUtility.GetOuterUV(background.sprite));
        Primitive("Stone receiving floor",PrimitiveType.Cube,new Vector3(bg.center.x,centerY,5),new Vector3(bg.size.x,.18f,depth),floorMaterial,Quaternion.Euler(-28,0,0));
        Primitive("Rear stone course",PrimitiveType.Cube,new Vector3(bg.center.x,floorY-.14f,8.75f),new Vector3(bg.size.x,.22f,.4f),stone,Quaternion.identity);
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*8.65f;
            Primitive("Pillar foot",PrimitiveType.Cube,new Vector3(x,floorY+.12f,8.5f),new Vector3(.95f,.28f,1),stone,Quaternion.identity);
            Primitive("Pillar base",PrimitiveType.Cube,new Vector3(x,floorY+.36f,8.5f),new Vector3(.72f,.2f,.74f),stone,Quaternion.identity);
            Primitive("Pillar shaft",PrimitiveType.Cylinder,new Vector3(x,floorY+1.95f,8.5f),new Vector3(.55f,1.52f,.55f),stone,Quaternion.identity);
            Primitive("Pillar capital",PrimitiveType.Cube,new Vector3(x,floorY+3.54f,8.5f),new Vector3(.88f,.25f,.9f),stone,Quaternion.identity);
        }
        var back=Quad("Whole original background",stageRoot.transform,backdrop,stageLayer);
        back.transform.position=new Vector3(bg.center.x,bg.center.y,20); back.transform.localScale=new Vector3(bg.size.x,bg.size.y,1);
        backdrop.SetTexture("_MainTex",background.sprite.texture);
        backdrop.SetVector("_UVRect",UnityEngine.Sprites.DataUtility.GetOuterUV(background.sprite));
        composite=Quad("HD2D stage composite",transform,compositeMaterial,gameplayCamera.gameObject.layer);
        compositeRenderer=composite.GetComponent<MeshRenderer>(); compositeRenderer.sortingLayerID=background.sortingLayerID;
        compositeRenderer.sortingOrder=background.sortingOrder+1;
        return true;
    }
    private GameObject Child(string name)
    {
        var child=new GameObject(name); child.layer=stageLayer; child.transform.SetParent(stageRoot.transform,false); return child;
    }
    private void Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Quaternion rotation)
    {
        var obj=GameObject.CreatePrimitive(type); obj.name=name; obj.layer=stageLayer; obj.transform.SetParent(stageRoot.transform,false);
        obj.transform.SetPositionAndRotation(position,rotation); obj.transform.localScale=scale;
        var collider=obj.GetComponent<Collider>(); if(collider!=null) { collider.enabled=false; Destroy(collider); }
        var renderer=obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.receiveShadows=true;
        renderer.shadowCastingMode=ShadowCastingMode.On;
    }
    private GameObject Quad(string name,Transform parent,Material material,int layer)
    {
        var mesh=new Mesh { name=name, vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)},
            uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},triangles=new[]{0,2,1,0,3,2} };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
        var obj=new GameObject(name); obj.layer=layer; obj.transform.SetParent(parent,false);
        obj.AddComponent<MeshFilter>().sharedMesh=mesh; var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; return obj;
    }
    private void ResizeTarget()
    {
        int height=Mathf.Clamp(gameplayCamera.pixelHeight,360,maximumRenderHeight);
        int width=Mathf.Max(1,Mathf.RoundToInt(height*gameplayCamera.aspect));
        if(target!=null && target.width==width && target.height==height) return;
        if(target!=null) { stageCamera.targetTexture=null; target.Release(); Destroy(target); }
        target=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf) { name="HD2D stage target",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp };
        target.Create(); stageCamera.targetTexture=target; compositeMaterial.SetTexture("_MainTex",target);
    }
    private void Suspend()
    {
        if(stageRoot!=null) stageRoot.SetActive(false);
        if(composite!=null) composite.SetActive(false);
        if(controlsAtmosphere) { if(atmosphere!=null) atmosphere.enabled=atmosphereWasEnabled; controlsAtmosphere=false; }
        if(excludedLayer) { if(gameplayCamera!=null) gameplayCamera.cullingMask=(gameplayCamera.cullingMask&~(1<<stageLayer))|(originalCullingMask&(1<<stageLayer)); excludedLayer=false; }
    }
    private void Release()
    {
        Suspend();
        if(daylightOverlay!=null)Destroy(daylightOverlay);
        daylightOverlay=null; daylightRenderer=null; daylightMaterial=null;
        if(ownsCharacterLight && characterLightSource!=null)Destroy(characterLightSource.gameObject);
        if(ownsCharacterLight)characterLightSource=null;
        ownsCharacterLight=false;
        lightProfileSprite=null;
        if(stageCamera!=null) stageCamera.targetTexture=null;
        if(target!=null) { target.Release(); Destroy(target); } target=null;
        if(stageRoot!=null) Destroy(stageRoot); if(composite!=null) Destroy(composite);
        stageRoot=null; composite=null; stageCamera=null;
        foreach(var material in materials) if(material!=null) Destroy(material); materials.Clear();
        foreach(var mesh in meshes) if(mesh!=null) Destroy(mesh); meshes.Clear();
        if(profile!=null) { foreach(var component in profile.components) if(component!=null) Destroy(component); Destroy(profile); } profile=null;
    }
    private void OnDisable()
    {
        if(stages.TryGetValue(gameObject.scene.handle,out var stage) && stage==this) stages.Remove(gameObject.scene.handle);
        Release();
    }
    private void OnDestroy()=>Release();
}
