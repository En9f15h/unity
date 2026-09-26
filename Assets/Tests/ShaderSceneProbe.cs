#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// This probe is excluded from ordinary players. Only the explicit benchmark build defines
// CODEX_SHADER_BENCHMARK. All room actors and their readiness are simulated offline.
public sealed class ShaderSceneProbe : MonoBehaviour
{
    private sealed class SimulatedPlayer : Player
    {
        public SimulatedPlayer() : base("Offline test actor", 2, false) { }
    }
    [Serializable] private sealed class Measurement
    {
        public string map, scenario;
        public int samples, width, height;
        public double meanFrameMs, p50FrameMs, p95FrameMs, meanGpuMs, meanCpuMs, meanDrawCalls, meanGcBytes;
        public int gpuSamples, cpuSamples, drawSamples, gcSamples;
    }
    [Serializable] private sealed class Report
    {
        public string unity, gpu, graphicsApi, cpu, operatingSystem, scope, quality;
        public float renderScale;
        public bool developmentBuild;
        public List<Measurement> measurements = new List<Measurement>();
    }
    private readonly List<string> checks = new List<string>();
    private readonly List<string> runtimeErrors = new List<string>();
    private readonly Report report = new Report();
    private readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private SimulatedPlayer remote;
    private string output;
    private bool benchmark, finished;
    private bool polishPreviews;
    private bool healthHudPreviews;
    private bool stageReadabilityPreviews;
    private bool selectionPolishPreviews;
    private bool rangePreviews;
    private bool resultPreviews;
    private bool planningPreviews;
    private bool graphIntegration;
    private bool actionUsage;
    private bool menuButtons;
    private double started;
    private GameObject opponentVisual;
    private CharacterUnit localUnit;
    private OracleVFXController oracle;
    private float nextBurst;
    private bool bursting, measuring;
    private readonly FrameTiming[] timings = new FrameTiming[1];

#if CODEX_SHADER_BENCHMARK && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartPlayer()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-shader-probe-output");
        string directory = index >= 0 && index + 1 < args.Length ? args[index + 1] : "ShaderProbeResults";
        if (ActionUsageFileProbe.TryRun(args, directory)) return;
        bool ranges = Array.IndexOf(args, "-range-probe") >= 0;
        bool results = Array.IndexOf(args, "-result-probe") >= 0;
        bool planning = Array.IndexOf(args, "-planning-probe") >= 0;
        bool graphs = Array.IndexOf(args, "-graph-integration") >= 0;
        bool usage = Array.IndexOf(args, "-action-usage-probe") >= 0;
        bool menus = Array.IndexOf(args, "-menu-button-probe") >= 0;
        StartProbe(!ranges && !results && !planning && !graphs && !usage && !menus, directory, captureRangePreviews: ranges, captureResultPreviews: results, capturePlanningPreviews: planning, validateGraphs: graphs, validateActionUsage: usage, validateMenuButtons: menus);
    }
#endif
    public static void StartProbe(bool measurePerformance, string directory, bool capturePolishPreviews = false, bool captureHealthHudPreviews = false, bool captureStageReadabilityPreviews = false, bool captureSelectionPolishPreviews = false, bool captureRangePreviews = false, bool captureResultPreviews = false, bool capturePlanningPreviews = false, bool validateGraphs = false, bool validateActionUsage = false, bool validateMenuButtons = false)
    {
        var probe = new GameObject("Offline shader scene probe").AddComponent<ShaderSceneProbe>();
        DontDestroyOnLoad(probe.gameObject);
        probe.benchmark = measurePerformance;
        probe.polishPreviews = capturePolishPreviews;
        probe.healthHudPreviews = captureHealthHudPreviews;
        probe.stageReadabilityPreviews = captureStageReadabilityPreviews;
        probe.selectionPolishPreviews = captureSelectionPolishPreviews;
        probe.rangePreviews = captureRangePreviews;
        probe.resultPreviews = captureResultPreviews;
        probe.planningPreviews = capturePlanningPreviews;
        probe.graphIntegration = validateGraphs;
        probe.actionUsage = validateActionUsage;
        probe.menuButtons = validateMenuButtons;
        probe.output = Path.GetFullPath(directory);
        Directory.CreateDirectory(probe.output);
        probe.started = Time.realtimeSinceStartupAsDouble;
        probe.report.unity = Application.unityVersion;
        probe.report.gpu = SystemInfo.graphicsDeviceName;
        probe.report.graphicsApi = SystemInfo.graphicsDeviceVersion;
        probe.report.cpu = SystemInfo.processorType;
        probe.report.operatingSystem = SystemInfo.operatingSystem;
        probe.report.quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
        probe.report.renderScale = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset)?.renderScale ?? 1;
        probe.report.developmentBuild = Debug.isDebugBuild;
        probe.report.scope = "Actual scenes; Photon OfflineMode; one simulated remote readiness actor; no network or full-match validation";
        Application.logMessageReceived += probe.OnLog;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        AudioListener.volume = 0;
        probe.steps.Push(probe.Run());
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            runtimeErrors.Add(message + "\n" + stack);
    }
    private void Update()
    {
        if (finished) return;
        try
        {
            if (Time.realtimeSinceStartupAsDouble - started > 600) throw new TimeoutException("Probe exceeded 600 seconds");
            if (!measuring) MirrorRemoteReadiness();
            if (bursting && oracle != null && Time.unscaledTime >= nextBurst)
            {
                nextBurst = Time.unscaledTime + 0.8f;
                oracle.PlayWard(1.1f);
                oracle.PlayWardSuccess(0.5f);
                oracle.PlayRiftBurst(localUnit, 0.65f);
                localUnit.GetComponentInChildren<CharacterShaderFeedback>()?.FlashHit();
            }
            while (steps.Count > 0)
            {
                if (!steps.Peek().MoveNext()) { steps.Pop(); continue; }
                if (steps.Peek().Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Require(runtimeErrors.Count == 0, "No runtime errors during actual scene integration");
            Finish(0);
        }
        catch (Exception e) { checks.Add("FAILED: " + e); Finish(1); }
    }
    private void MirrorRemoteReadiness()
    {
        if (remote == null || !PhotonNetwork.InRoom) return;
        Hashtable changed = new Hashtable();
        bool hasChange = false;
        foreach (string key in new[]{"sceneTransitionScene","sceneTransitionToken","sceneTransitionInReady","sceneTransitionReady"})
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(key, out object value))
            {
                hasChange |= !remote.CustomProperties.TryGetValue(key, out object old) || !Equals(value, old);
                remote.CustomProperties[key] = value; changed[key] = value;
            }
        }
        remote.CustomProperties["GameSceneReady"] = SceneManager.GetActiveScene().name == "GameScene";
        if (actionUsage && SceneManager.GetActiveScene().name == "GameScene") ActionUsageProbe.PublishRemote(remote);
        // A real peer reports all four transition fields together, even when a bool has not
        // changed since the previous match. Mirror that payload when the token changes.
        if (hasChange)
            FindFirstObjectByType<SceneTransitionManager>()?.OnPlayerPropertiesUpdate(remote, changed);
    }
    private IEnumerator Run()
    {
        if (menuButtons) yield return MenuButtonProbe.Menus(Capture, Require);
        if (actionUsage) ActionUsageProbe.Prepare(Require);
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("Shader probe offline", new RoomOptions { MaxPlayers = 2 });
        yield return Until(() => PhotonNetwork.InRoom, 15, "Offline room created");
        remote = new SimulatedPlayer(); PhotonNetwork.CurrentRoom.AddPlayer(remote);
        Require(PhotonNetwork.OfflineMode && PhotonNetwork.PlayerList.Length == 2, "Two offline actor records; no network connection");
        int count = benchmark ? 1 : 7;
        for (int mapIndex = 0; mapIndex < count; mapIndex++)
        {
            yield return EnterSelection();
            if (menuButtons && mapIndex == 0) yield return MenuButtonProbe.CheckScene(Capture, Require);
            var manager = FindFirstObjectByType<CharacterSelectionManager>();
            var maps = Field<MapSelectionDefinition[]>(manager, "maps");
            var selector = Field<MapSelectorController>(manager, "mapSelectorController");
            Require(maps.Length == 7 && maps[3].mapId == "tHeBEst", "Selection keeps three new maps first and background.png at index 3");
            MapSelectionDefinition map = maps[mapIndex];
            manager.RequestMapSelectionChange(map);
            yield return Frames(4);
            Require(selector.GetCurrentMapId() == map.mapId, "Map selector previews " + map.mapId);
            Require(Convert.ToInt32(PhotonNetwork.CurrentRoom.CustomProperties[CharacterSelectPhotonKeys.LegacyStageIndex]) == mapIndex,
                "Selection publishes room stage index " + mapIndex);
            Require((string)PhotonNetwork.CurrentRoom.CustomProperties[CharacterSelectPhotonKeys.SelectedMapId] == map.mapId,
                "Selection publishes matching map ID " + map.mapId);
            var image = Field<Image>(selector, "mapPreviewImage");
            Require(image.sprite == map.previewSprite, "Actual map preview uses correct sprite " + mapIndex);
            var overlay = image.transform.Find("Stage Atmosphere");
            Require((overlay != null && overlay.gameObject.activeInHierarchy) == (mapIndex < 3), "Preview atmosphere matches map " + mapIndex);
            if (stageReadabilityPreviews)
            {
                var previewStyle = image.GetComponent<StageReadabilityController>();
                Require(previewStyle != null && previewStyle.HasStyle == (mapIndex < 3), "Preview grade matches selected sprite " + mapIndex);
                Require((image.materialForRendering.shader.name == "Combat/Stage Background UI") == (mapIndex < 3), "Preview restores correct material across map changes " + mapIndex);
                Capture("Selection-" + mapIndex + ".png");
            }
            if (mapIndex == 0 || mapIndex == 3) Capture("Selection-" + mapIndex + ".png");
            if (selectionPolishPreviews) yield return CheckSelectionPresentation(manager,mapIndex);

            var characterSelector = Field<CharacterSelectorController>(manager, "characterSelector");
            string characterId = benchmark || mapIndex % 2 == 0 ? "knight" : "oracle";
            int skin = benchmark ? 1 : (mapIndex / 2) % 2;
            characterSelector.SetCharacterById(characterId);
            if (characterSelector.GetCurrentSkinIndex() != skin) characterSelector.SelectNextSkin();
            remote.CustomProperties[CharacterSelectPhotonKeys.SelectedCharacterId] = characterId == "knight" ? "oracle" : "knight";
            remote.CustomProperties[CharacterSelectPhotonKeys.SelectedSkinIndex] = 1;
            remote.CustomProperties[CharacterSelectPhotonKeys.IsReady] = true;
            remote.CustomProperties[CharacterSelectPhotonKeys.LegacyReady] = true;
            manager.ConfirmReady();
            Require((bool)PhotonNetwork.LocalPlayer.CustomProperties[CharacterSelectPhotonKeys.IsReady], "Ready publishes local state");
            yield return Until(() => SceneManager.GetActiveScene().name == "GameScene", 35, "Ready, reveal, countdown and transition load GameScene");
            yield return Until(() => GameSceneStartSync.Instance != null && GameSceneStartSync.Instance.HasBeatStarted(), 20, "Offline readiness reaches battle start beat");
            yield return Until(() => FindFirstObjectByType<CharacterUnit>() != null, 10, "GameManager spawns selected character");
            yield return Seconds(1.5f);
            localUnit = FindObjectsByType<CharacterUnit>(FindObjectsSortMode.None).First(u => u.photonView.IsMine);
            Require(localUnit.gameObject.name.Contains(characterId == "knight" ? "knight_" + skin : "Oracle_" + skin), "Selected class and skin reach actual spawned prefab");
            Require(localUnit.GetComponentInChildren<CharacterShaderFeedback>() != null, "Actual spawned skin includes character shader feedback");
            if (polishPreviews)
            {
                var contact = localUnit.GetComponent<CharacterGroundPresentation>();
                Require(contact != null && contact.IsGrounded && contact.ShadowRenderer.enabled, "Actual scene character has a grounded contact shadow");
                Require(localUnit.GetComponent<CombatImpactPresentation>() != null, "Actual selected skin includes graded impact feedback");
            }
            ValidateBattleMap(mapIndex, map);
            if (stageReadabilityPreviews)
            {
                // The authored controller belongs to the referenced background, not the sync object.
                var background = Field<SpriteRenderer>(FindFirstObjectByType<BackgroundSpriteSync>(), "backgroundRenderer");
                var style = background.GetComponent<StageReadabilityController>();
                Require(style != null && style.HasStyle == (mapIndex < 3), "Battle grade matches actual selected background " + mapIndex);
                Require((background.sharedMaterial.shader.name == "Combat/Stage Background") == (mapIndex < 3), "Battle restores original material for legacy maps " + mapIndex);
                var feedback = localUnit.GetComponentInChildren<CharacterShaderFeedback>();
                bool hasStyle = StageReadabilityController.TryGetCharacterStyle(feedback.gameObject, out var entry);
                Require(hasStyle == (mapIndex < 3), "Late-spawned selected skin resolves its scene palette " + mapIndex);
                var block = new MaterialPropertyBlock();
                Require(feedback.BodyRenderers.All(r => { r.GetPropertyBlock(block); return Mathf.Approximately(block.GetFloat("_RimStrength"), r.sharedMaterial.GetFloat("_RimStrength") * (hasStyle ? entry.edgeMultiplier : 1)); }), "Actual skin keeps authored edge strength and map multiplier " + mapIndex);
            }
            if (healthHudPreviews)
            {
                var bars = FindObjectsByType<DirectionalHealthBarUI>(FindObjectsSortMode.None);
                Require(bars.Length == 2 && bars.All(b => b.GetComponent<HealthBarPresentation>()?.GaugeRenderer != null), "Actual scene loads both health gauge presentations");
                foreach (var bar in bars)
                {
                    var text = Field<TextMesh>(bar,"perspectiveLabel");
                    Require(text != null && Quaternion.Angle(text.transform.rotation,Camera.main.transform.rotation)<.1f,"Actual health labels face camera");
                }
            }
            Require(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing, "GameScene camera enables combat post-processing");
            Require(FindObjectsByType<Volume>(FindObjectsSortMode.None).Any(v => v.sharedProfile != null && v.sharedProfile.name == "CombatBloom"), "GameScene loads combat Bloom profile");
            Require(FindObjectsByType<CombatShaderClock>(FindObjectsSortMode.None).Length == 1, "Scene transitions preserve exactly one shader clock");
            Require(FindObjectsByType<UIShaderFeedback>(FindObjectsSortMode.None).Length > 0, "Selected class gameplay UI includes shader feedback");
            if (benchmark) { yield return BenchmarkMaps(maps); }
            else Capture("GameScene-" + mapIndex + ".png");
            if (graphIntegration) yield return CombatShaderGraphIntegrationProbe.Run(mapIndex, localUnit, Capture, Require);
            if (actionUsage) yield return ActionUsageProbe.Run(mapIndex, localUnit, remote, Capture, Require);
            if (rangePreviews)
            {
                yield return RangePresentationProbe.Run(mapIndex, localUnit, Capture, Require);
                yield return RangeTogglePresentationProbe.Run(mapIndex, localUnit, Capture, Require);
            }
            if (resultPreviews) yield return ResultPresentationProbe.Run(mapIndex, Capture, Require);
            if (planningPreviews)
            {
                yield return CountdownPresentationProbe.Run(mapIndex, Capture, Require);
                yield return PlanningPresentationProbe.Run(mapIndex, Capture, Require);
                yield return ReadyPresentationProbe.Run(mapIndex, Capture, Require);
            }
            if (polishPreviews && mapIndex == 2) yield return PolishShowcase(maps, mapIndex);
            if (healthHudPreviews && (mapIndex == 2 || mapIndex == 3)) yield return HealthHudShowcase(mapIndex);
            if (stageReadabilityPreviews && mapIndex == 2) yield return PolishShowcase(maps, mapIndex);
            checks.Add("INFO: completed actual scene route for " + map.mapId);
            File.WriteAllLines(Path.Combine(output, "checks-in-progress.txt"), checks);
        }
    }
    private IEnumerator EnterSelection()
    {
        remote.CustomProperties[CharacterSelectPhotonKeys.IsReady] = false;
        remote.CustomProperties[CharacterSelectPhotonKeys.LegacyReady] = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable {{CharacterSelectPhotonKeys.IsReady, false},{CharacterSelectPhotonKeys.LegacyReady, false}});
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {{CharacterSelectPhotonKeys.SelectionPhase, 0},{CharacterSelectPhotonKeys.PhaseStartTime, -1d}});
        if (SceneManager.GetActiveScene().name == "GameScene")
            SceneTransitionManager.RequestSceneTransition("CharacterSelectScene");
        else SceneManager.LoadScene("CharacterSelectScene");
        yield return Until(() => SceneManager.GetActiveScene().name == "CharacterSelectScene" && FindFirstObjectByType<CharacterSelectionManager>() != null, 20, "Actual selection scene loaded");
        yield return Frames(10);
    }
    private void ValidateBattleMap(int index, MapSelectionDefinition map)
    {
        var sync = FindFirstObjectByType<BackgroundSpriteSync>();
        Require(sync != null, "GameScene contains background sync");
        var background = Field<SpriteRenderer>(sync, "backgroundRenderer");
        var floor = Field<SpriteRenderer>(sync, "floorRenderer");
        var backgrounds = Field<Sprite[]>(sync, "backgroundSprites");
        var floors = Field<Sprite[]>(sync, "floorSprites");
        Require(backgrounds.Length == 7 && floors.Length == 7, "Seven paired background and floor entries");
        Require(background.sprite == backgrounds[index] && background.sprite == map.previewSprite, "Room index applies matching battle background " + index);
        Require(floor.sprite == floors[index], "Room index applies matching battle floor " + index);
        var atmosphere = background.GetComponent<StageAtmosphereController>(); atmosphere.Refresh();
        bool active = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Any(r => r.name == "Stage Atmosphere");
        Require(active == (index < 3), "Actual GameScene atmosphere matches map " + index);
    }
    private IEnumerator BenchmarkMaps(MapSelectionDefinition[] maps)
    {
        // Freeze turn simulation at its established start state. Render real scene/UI and two
        // animated skins; the opponent is a visual fixture, not a second network client.
        foreach (var planner in FindObjectsByType<TurnPlanningManager>(FindObjectsSortMode.None))
        { planner.StopAllCoroutines(); planner.enabled = false; }
        var manager = FindFirstObjectByType<GameManager>();
        Transform opponentSpawn = Field<Transform>(manager, "clientSpawnPoint");
        var staging = new GameObject("Inactive opponent staging"); staging.SetActive(false);
        opponentVisual = Instantiate(Resources.Load<GameObject>("Prefab/Oracle/Oracle_1"), staging.transform);
        foreach (MonoBehaviour component in opponentVisual.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is CharacterShaderFeedback) && !(component is OracleVFXController) &&
                !(component is CharacterGroundPresentation) && !(component is CombatImpactPresentation) &&
                !(component is UnityEngine.U2D.Animation.SpriteSkin) && !(component is CombatEffectShaderDriver)) component.enabled = false;
        opponentVisual.transform.SetParent(null);
        opponentVisual.transform.position = opponentSpawn.position;
        opponentVisual.SetActive(true); Destroy(staging);
        oracle = opponentVisual.GetComponentInChildren<OracleVFXController>();
        Camera.main.GetComponent<DynamicBattleCamera2D>()?.SetTargets(localUnit.transform, opponentVisual.transform);
        var localBody = localUnit.GetComponent<Rigidbody2D>(); if (localBody != null) localBody.simulated = false;
        yield return Seconds(2);
        foreach (var body in opponentVisual.GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
        foreach (int index in new[]{0,1,2})
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {{CharacterSelectPhotonKeys.LegacyStageIndex,index},{CharacterSelectPhotonKeys.SelectedMapId,maps[index].mapId}});
            FindFirstObjectByType<BackgroundSpriteSync>().TrySyncBackground(); yield return Frames(10);
            ValidateBattleMap(index, maps[index]);
            foreach (string scenario in new[]{"idle", "idle_without_bloom_and_mist", "effect_burst"})
            {
                bool normal = scenario != "idle_without_bloom_and_mist";
                foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (volume.sharedProfile != null && volume.sharedProfile.name == "CombatBloom") volume.weight = normal ? 1 : 0;
                foreach (var atmosphere in FindObjectsByType<StageAtmosphereController>(FindObjectsSortMode.None)) atmosphere.enabled = normal;
                bursting = scenario == "effect_burst"; nextBurst = 0;
                yield return Seconds(2.5f);
                yield return Measure(maps[index].mapId, scenario);
                Capture("Benchmark-" + index + "-" + scenario + ".png");
                bursting = false;
            }
        }
    }
    private IEnumerator CheckSelectionPresentation(CharacterSelectionManager manager, int index)
    {
        var layout = FindFirstObjectByType<SelectionPresentationLayout>();
        Require(layout != null && !layout.VolumeOpen, "Selection starts with audio settings collapsed " + index);
        var button = Field<Button>(layout,"volumeButton");
        var panel = Field<GameObject>(layout,"volumePanel");
        var volumeLabel = Field<TMPro.TMP_Text>(layout,"volumeLabel");
        Require(volumeLabel.font.HasCharacters("音量Audio ×"),"Volume label contains all required glyphs " + index);
        var channels = (AudioVolumeChannel[])Enum.GetValues(typeof(AudioVolumeChannel));
        var volumes = channels.Select(c => AudioManager.Instance.GetVolume(c)).ToArray();
        button.onClick.Invoke(); yield return Frames(2);
        Require(layout.VolumeOpen && panel.GetComponentsInChildren<AudioVolumeControlUI>().Length == 3, "Audio button opens all three existing volume controls " + index);
        Require(panel.GetComponentsInChildren<Text>().All(t=>t.fontSize==16),"Scene instance keeps readable audio label sizes " + index);
        if (index == 0) Capture("Selection-audio-open.png");
        button.onClick.Invoke(); yield return Frames(2);
        Require(!layout.VolumeOpen && channels.Select((c,i) => AudioManager.Instance.GetVolume(c) == volumes[i]).All(v=>v), "Closing audio panel preserves volume values " + index);
        layout.enabled=false; layout.enabled=true; button.onClick.Invoke();
        Require(layout.VolumeOpen,"Re-enabling layout does not duplicate button listeners " + index); button.onClick.Invoke();
        CheckSkillPanelBounds(layout,"actual scene " + index);
        Capture("Selection-clean-" + index + ".png");
        if (index != 0) yield break;
        var canvas = layout.GetComponent<Canvas>(); var root = (RectTransform)canvas.transform;
        var mode = canvas.renderMode; var size = root.sizeDelta; var scale = root.localScale; var position = root.position;
        try
        {
            canvas.renderMode = RenderMode.WorldSpace;
            foreach (var dimensions in new[]{new Vector2(1920,1080),new Vector2(1662,1247),new Vector2(1821,1138)})
            {
                root.sizeDelta = dimensions; layout.ApplyLayout(); CheckSkillPanelBounds(layout,"reference " + dimensions);
            }
        }
        finally { root.sizeDelta=size; root.localScale=scale; root.position=position; canvas.renderMode=mode; Canvas.ForceUpdateCanvases(); layout.ApplyLayout(); }
        var you = Field<CharacterSkillPopup>(manager,"youSkillPopup");
        var enemy = Field<CharacterSkillPopup>(manager,"enemySkillPopup");
        var original = Field<List<SkillDisplayButton>>(you,"pooledButtons").Where(b=>b.gameObject.activeSelf).Select(b=>Field<SkillDisplayData>(b,"data")).ToArray();
        foreach (var character in Field<CharacterSelectionDefinition[]>(manager,"characters"))
        {
            you.SetAccessible(true); you.BindSkills(character.skills); layout.ApplyLayout(); yield return Frames(3);
            var buttons = Field<List<SkillDisplayButton>>(you,"pooledButtons").Where(b=>b.gameObject.activeSelf).ToArray();
            Require(buttons.Length==character.skills.Count(s=>s!=null),character.characterId+" keeps every skill button");
            Require(!BoundsIn((RectTransform)you.transform,(RectTransform)Field<Transform>(you,"buttonRoot")).Overlaps(BoundsIn((RectTransform)you.transform,Field<TMPro.TMP_Text>(you,"descriptionText").rectTransform)),character.characterId+" skill icons and descriptions occupy separate areas");
            foreach (var skillButton in buttons)
            {
                skillButton.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                Require(Field<SkillDisplayData>(you,"currentSkill")==Field<SkillDisplayData>(skillButton,"data"),"Hover displays "+Field<SkillDisplayData>(skillButton,"data").skillName);
                skillButton.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                var text = Field<TMPro.TMP_Text>(you,"descriptionText"); text.ForceMeshUpdate();
                Require(!text.isTextOverflowing,"Compact panel fits description: "+Field<SkillDisplayData>(skillButton,"data").skillName);
            }
            Capture("Selection-skills-"+character.characterId+".png");
        }
        enemy.SetAccessible(false); enemy.ShowSkill(original[0]);
        Require(!Field<GameObject>(enemy,"popupRoot").activeSelf,"Private opponent cannot reveal skills through the presentation layout");
        you.BindSkills(original); layout.ApplyLayout(); yield return Frames(2);
        var publicSelection = new Hashtable {
            {CharacterSelectPhotonKeys.SelectedCharacterId,"oracle"},
            {CharacterSelectPhotonKeys.SelectedSkinIndex,1}
        };
        foreach (System.Collections.DictionaryEntry property in publicSelection) remote.CustomProperties[property.Key]=property.Value;
        manager.RequestPublicSelectionChange(true); manager.OnPlayerPropertiesUpdate(remote,publicSelection); yield return Frames(5);
        Require(Field<GameObject>(enemy,"popupRoot").activeInHierarchy,"Public selection displays both skill panels");
        CaptureSelectionCanvas(layout,"Selection-public-16x9.png",1920,1080);
        CaptureSelectionCanvas(layout,"Selection-public-16x10.png",1821,1138);
        CaptureSelectionCanvas(layout,"Selection-public-4x3.png",1662,1247);
        manager.RequestPublicSelectionChange(false); yield return Frames(3);
        Require(!Field<GameObject>(enemy,"popupRoot").activeSelf,"Returning to private selection hides opponent skills");
    }
    private void CaptureSelectionCanvas(SelectionPresentationLayout layout,string name,int width,int height)
    {
        // Render the real canvas at specified logical dimensions without opening a desktop window.
        var canvas=layout.GetComponent<Canvas>(); var root=(RectTransform)canvas.transform;
        var mode=canvas.renderMode; var oldCamera=canvas.worldCamera; var size=root.sizeDelta;
        var scale=root.localScale; var position=root.position; var rotation=root.rotation;
        var proofCamera=new GameObject("Selection layout proof camera").AddComponent<Camera>();
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var oldTarget=RenderTexture.active; var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=proofCamera;
            root.position=Vector3.zero; root.rotation=Quaternion.identity; root.localScale=Vector3.one; root.sizeDelta=new Vector2(width,height);
            layout.ApplyLayout(); Canvas.ForceUpdateCanvases();
            proofCamera.orthographic=true; proofCamera.orthographicSize=height*.5f; proofCamera.aspect=width/(float)height;
            proofCamera.transform.position=new Vector3(root.rect.center.x,root.rect.center.y,-10);
            proofCamera.clearFlags=CameraClearFlags.SolidColor; proofCamera.backgroundColor=Color.black;
            RenderPipeline.SubmitRenderRequest(proofCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output,name),texture.EncodeToPNG());
        }
        finally
        {
            root.sizeDelta=size; root.localScale=scale; root.position=position; root.rotation=rotation;
            canvas.renderMode=mode; canvas.worldCamera=oldCamera; Canvas.ForceUpdateCanvases(); layout.ApplyLayout();
            RenderTexture.active=oldTarget; RenderTexture.ReleaseTemporary(rt); Destroy(texture); Destroy(proofCamera.gameObject);
        }
    }
    private void CheckSkillPanelBounds(SelectionPresentationLayout layout, string label)
    {
        var root=(RectTransform)layout.transform;
        var you=Field<CharacterSkillPopup>(layout,"youSkills"); var enemy=Field<CharacterSkillPopup>(layout,"enemySkills");
        Rect local=BoundsIn(root,(RectTransform)you.transform), remote=BoundsIn(root,(RectTransform)enemy.transform);
        Rect left=BoundsIn(root,Field<RectTransform>(layout,"youPanel")), right=BoundsIn(root,Field<RectTransform>(layout,"enemyPanel"));
        Rect map=BoundsIn(root,Field<RectTransform>(layout,"mapPanel"));
        Rect vs=BoundsIn(root,Field<RectTransform>(layout.GetComponent<CharacterSelectionUILayoutController>(),"centerPanel"));
        Require(!local.Overlaps(left)&&!local.Overlaps(right)&&!remote.Overlaps(left)&&!remote.Overlaps(right),label+" skill panels do not cover character data");
        Require(!local.Overlaps(remote)&&!local.Overlaps(map)&&!remote.Overlaps(map),label+" skill panels and map remain separate");
        Require(!local.Overlaps(vs)&&!remote.Overlaps(vs),label+" skill panels leave the VS region clear");
        Require(map.yMin>=root.rect.yMin+23,label+" map has bottom safety margin");
        Require(root.rect.Contains(local.min)&&root.rect.Contains(local.max)&&root.rect.Contains(remote.min)&&root.rect.Contains(remote.max),label+" both skill panels remain inside canvas");
    }
    private static Rect BoundsIn(RectTransform root, RectTransform child)
    {
        var corners=new Vector3[4]; child.GetWorldCorners(corners);
        Vector2 min=root.InverseTransformPoint(corners[0]),max=min;
        foreach(var corner in corners) { Vector2 p=root.InverseTransformPoint(corner); min=Vector2.Min(min,p); max=Vector2.Max(max,p); }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    private IEnumerator HealthHudShowcase(int mapIndex)
    {
        // Exercise presentation only; these temporary values are not match damage.
        var bars=FindObjectsByType<DirectionalHealthBarUI>(FindObjectsSortMode.None);
        foreach(var bar in bars) bar.Init(100,100);
        yield return Frames(3); Capture("HealthHUD-"+mapIndex+"-full.png");
        bars[0].SetHP(65,100); bars[1].SetHP(35,100);
        yield return Frames(1); Capture("HealthHUD-"+mapIndex+"-damage.png");
        yield return Seconds(.65f); Capture("HealthHUD-"+mapIndex+"-settled.png");
        Require(bars.All(b=>Mathf.Approximately(b.GetComponent<HealthBarPresentation>().CurrentRatio,b.GetComponent<HealthBarPresentation>().DelayedRatio)),"Actual scene damage trails settle to current HP");
    }
    private IEnumerator PolishShowcase(MapSelectionDefinition[] maps, int restoreIndex)
    {
        // A visual opponent fixture; this does not represent a second network client.
        FindFirstObjectByType<TurnPlanningManager>().StopAllCoroutines();
        var staging = new GameObject("Inactive polish staging"); staging.SetActive(false);
        var fixture = Instantiate(Resources.Load<GameObject>("Prefab/Oracle/Oracle_1"), staging.transform);
        foreach (var component in fixture.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is CharacterShaderFeedback) && !(component is OracleVFXController) &&
                !(component is CharacterGroundPresentation) && !(component is CombatImpactPresentation) &&
                !(component is UnityEngine.U2D.Animation.SpriteSkin) && !(component is CombatEffectShaderDriver)) component.enabled = false;
        fixture.transform.SetParent(null);
        fixture.transform.position = Field<Transform>(FindFirstObjectByType<GameManager>(), "clientSpawnPoint").position;
        fixture.SetActive(true); Destroy(staging);
        var target = fixture.GetComponentInChildren<CharacterUnit>();
        var vfx = fixture.GetComponentInChildren<OracleVFXController>();
        Camera.main.GetComponent<DynamicBattleCamera2D>()?.SetTargets(localUnit.transform, target.transform);
        yield return Seconds(2);
        foreach (var body in fixture.GetComponentsInChildren<Rigidbody2D>()) { body.bodyType=RigidbodyType2D.Kinematic; body.linearVelocity=Vector2.zero; }
        var localBody = localUnit.GetComponent<Rigidbody2D>();
        if(localBody!=null) { localBody.bodyType=RigidbodyType2D.Kinematic; localBody.linearVelocity=Vector2.zero; }
        Require(target.GetComponent<CharacterGroundPresentation>().IsGrounded,"Polish opponent fixture contacts actual GameScene floor");
        foreach (int index in new[]{0,1,2})
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {{CharacterSelectPhotonKeys.LegacyStageIndex,index},{CharacterSelectPhotonKeys.SelectedMapId,maps[index].mapId}});
            FindFirstObjectByType<BackgroundSpriteSync>().TrySyncBackground(); yield return Frames(8);
            if (stageReadabilityPreviews)
            {
                var background = Field<SpriteRenderer>(FindFirstObjectByType<BackgroundSpriteSync>(), "backgroundRenderer");
                var style = background.GetComponent<StageReadabilityController>();
                var floor = Field<SpriteRenderer>(FindFirstObjectByType<BackgroundSpriteSync>(), "floorRenderer");
                var floorMaterial = floor.sharedMaterial; var floorColor = floor.color;
                var feedbacks = new[] { localUnit.GetComponent<CharacterShaderFeedback>(), target.GetComponent<CharacterShaderFeedback>() };
                // Render both variants in this same frame: camera, animation, mist, light and UI are identical.
                style.enabled = false; foreach (var feedback in feedbacks) feedback.ResetPresentation();
                Capture("Readability-" + index + "-before.png");
                style.enabled = true; style.Refresh(); foreach (var feedback in feedbacks) feedback.ResetPresentation();
                Capture("Readability-" + index + "-after.png");
                Require(floor.sharedMaterial == floorMaterial && floor.color == floorColor, "Readability leaves gameplay floor material and tint unchanged " + index);
                continue;
            }
            Capture("Polish-"+index+"-contact.png");
            vfx.PlayWard(1f); yield return Seconds(.2f); Capture("Polish-"+index+"-ward.png");
            vfx.PlayWardSuccess(.5f,localUnit.transform.position); yield return Seconds(.1f); Capture("Polish-"+index+"-ward-impact.png");
            yield return Seconds(.9f);
            localUnit.GetComponent<CombatImpactPresentation>().BeginAction(ActionType.HeavyAttack,true,1f);
            target.GetComponent<CombatImpactPresentation>().PlayResolvedImpact(localUnit);
            Capture("Polish-"+index+"-heavy.png"); yield return Seconds(.3f);
        }
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {{CharacterSelectPhotonKeys.LegacyStageIndex,restoreIndex},{CharacterSelectPhotonKeys.SelectedMapId,maps[restoreIndex].mapId}});
        FindFirstObjectByType<BackgroundSpriteSync>().TrySyncBackground(); Destroy(fixture); yield return Frames(4);
    }
    private IEnumerator Measure(string map, string scenario)
    {
        var frame = new List<double>(8192); var gpu = new List<double>(8192); var cpu = new List<double>(8192);
        var draws = new List<double>(8192); var gc = new List<double>(8192);
        measuring = true;
        using (var drawRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
        using (var gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
        {
            double end = Time.realtimeSinceStartupAsDouble + 5;
            double previous = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                FrameTimingManager.CaptureFrameTimings(); yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                frame.Add((now - previous) * 1000); previous = now;
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                { if (timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime); if (timings[0].cpuFrameTime > 0) cpu.Add(timings[0].cpuFrameTime); }
                if (drawRecorder.Valid) draws.Add(drawRecorder.LastValue);
                if (gcRecorder.Valid) gc.Add(gcRecorder.LastValue);
            }
        }
        measuring = false;
        frame.Sort();
        report.measurements.Add(new Measurement {map=map,scenario=scenario,samples=frame.Count,width=Screen.width,height=Screen.height,
            meanFrameMs=frame.Average(),p50FrameMs=frame[frame.Count/2],p95FrameMs=frame[Math.Min(frame.Count-1,(int)(frame.Count*0.95))],
            meanGpuMs=gpu.Count>0?gpu.Average():-1,meanCpuMs=cpu.Count>0?cpu.Average():-1,
            meanDrawCalls=draws.Count>0?draws.Average():-1,meanGcBytes=gc.Count>0?gc.Average():-1,
            gpuSamples=gpu.Count,cpuSamples=cpu.Count,drawSamples=draws.Count,gcSamples=gc.Count});
        File.WriteAllText(Path.Combine(output,"performance.json"),JsonUtility.ToJson(report,true));
    }
    private IEnumerator Until(Func<bool> condition, float timeout, string description)
    {
        double end = Time.realtimeSinceStartupAsDouble + timeout;
        while (!condition())
        { if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException(description); yield return null; }
        Require(true, description);
    }
    private static IEnumerator Frames(int count) { for (int i=0;i<count;i++) yield return null; }
    private static IEnumerator Seconds(float duration)
    { double end=Time.realtimeSinceStartupAsDouble+duration; while(Time.realtimeSinceStartupAsDouble<end) yield return null; }
    private static T Field<T>(object target,string name)
    { return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(target); }
    private void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); checks.Add("PASS: " + message); }
    private void Capture(string name)
    {
        var camera = Camera.main ?? FindFirstObjectByType<Camera>();
        bool temporaryCamera = camera == null;
        if (temporaryCamera)
        {
            camera = new GameObject("UI capture camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0,0,-10); camera.orthographic = true;
            camera.backgroundColor = Color.black; camera.clearFlags = CameraClearFlags.SolidColor;
        }
        var overlays = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var oldCameras=overlays.Select(c=>c.worldCamera).ToArray();
        var distances=overlays.Select(c=>c.planeDistance).ToArray();
        var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        RenderTexture previous=RenderTexture.active;
        var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            foreach(var canvas in overlays) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; }
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output,name),texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<overlays.Length;i++) { overlays[i].renderMode=RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera=oldCameras[i]; overlays[i].planeDistance=distances[i]; }
            RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); Destroy(texture);
            if (temporaryCamera) Destroy(camera.gameObject);
        }
    }
    private void Finish(int code)
    {
        if (actionUsage) LocalActionUsage.ClearTestStorage();
        finished=true; Application.logMessageReceived-=OnLog;
        File.WriteAllLines(Path.Combine(output,"validation.txt"),checks);
        File.WriteAllLines(Path.Combine(output,"runtime-errors.txt"),runtimeErrors);
        File.WriteAllText(Path.Combine(output,"performance.json"),JsonUtility.ToJson(report,true));
        Debug.Log("SHADER_SCENE_PROBE " + (code==0?"PASSED":"FAILED") + " / " + output);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
}
#endif
