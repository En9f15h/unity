using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Image))]
public sealed class UIShaderFeedback : MonoBehaviour, IMaterialModifier
{
    [SerializeField] private Material styleMaterial;
    [SerializeField] private bool pulseOnEnable;
    private Image graphic;
    private Material sourceMaterial, runtimeMaterial;
    private float state, persistent, pulseStarted=-1000, pulseDuration=0.5f;
    private bool atmosphereOverride;
    private Color atmosphereColor;
    private float atmosphereDensity;
    public static UIShaderFeedback Ensure(Image image,string style="UI_Gold")
    {
        if(image==null) return null;
        var feedback=image.GetComponent<UIShaderFeedback>() ?? image.gameObject.AddComponent<UIShaderFeedback>();
        if(feedback.styleMaterial==null) feedback.SetStyle(Resources.Load<Material>("Combat/Materials/"+style));
        return feedback;
    }
    public void SetStyle(Material material)
    {
        if(styleMaterial==material) return;
        styleMaterial=material; ReleaseMaterial();
        Resolve(); graphic.SetMaterialDirty();
    }
    private void Resolve() { if(graphic==null) graphic=GetComponent<Image>(); }
    private void OnEnable() { Resolve(); if(pulseOnEnable) Pulse(); graphic.SetMaterialDirty(); }
    private void OnDisable() { state=0; persistent=0; pulseStarted=-1000; Resolve(); graphic.SetMaterialDirty(); ReleaseMaterial(); }
    private void OnDestroy()=>ReleaseMaterial();
    public void SetState(float amount,bool active)
    {
        if(amount>state+0.001f || (active && persistent==0)) Pulse();
        state=Mathf.Clamp01(amount); persistent=active?1:0; Apply();
    }
    public void Pulse(float seconds=0.55f) { pulseDuration=Mathf.Max(0.05f,seconds); pulseStarted=Time.unscaledTime; Apply(); }
    public void ResetVisuals() { state=0; persistent=0; pulseStarted=-1000; Apply(); }
    public void SetAtmosphere(Color color,float density) { atmosphereOverride=true; atmosphereColor=color; atmosphereDensity=density; Apply(); }
    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if(!isActiveAndEnabled || styleMaterial==null) return baseMaterial;
        if(runtimeMaterial==null || sourceMaterial!=baseMaterial)
        {
            ReleaseMaterial(); sourceMaterial=baseMaterial;
            // Preserve the stencil settings already supplied by MaskableGraphic.
            runtimeMaterial=new Material(baseMaterial){name=styleMaterial.name+" (UI instance)",shader=styleMaterial.shader,hideFlags=HideFlags.HideAndDontSave};
            foreach(string property in new[]{"_Mode","_Strength","_Density"}) runtimeMaterial.SetFloat(property,styleMaterial.GetFloat(property));
            runtimeMaterial.SetColor("_AccentColor",styleMaterial.GetColor("_AccentColor"));
        }
        Apply(); return runtimeMaterial;
    }
    private void LateUpdate()=>Apply();
    private void Apply()
    {
        if(runtimeMaterial==null) return;
        Resolve(); Rect rect=graphic.rectTransform.rect;
        runtimeMaterial.SetVector("_LocalRect",new Vector4(rect.xMin,rect.yMin,rect.width,rect.height));
        runtimeMaterial.SetFloat("_StateAmount",state); runtimeMaterial.SetFloat("_Persistent",persistent);
        runtimeMaterial.SetFloat("_PulseProgress",Mathf.Clamp01((Time.unscaledTime-pulseStarted)/pulseDuration));
        if(atmosphereOverride) { runtimeMaterial.SetColor("_AccentColor",atmosphereColor); runtimeMaterial.SetFloat("_Density",atmosphereDensity); }
    }
    private void ReleaseMaterial()
    {
        if(runtimeMaterial!=null) { if(Application.isPlaying) Destroy(runtimeMaterial); else DestroyImmediate(runtimeMaterial); }
        runtimeMaterial=null; sourceMaterial=null;
    }
}
