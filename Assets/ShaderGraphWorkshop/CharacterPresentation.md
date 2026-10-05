# 人物 Shader Graph

`CharacterSkillPresentation.shadergraph` 是目前人物使用的 URP Sprite Unlit 原生節點圖，可直接以 Shader Graph 開啟。`CharacterPresentation.shadergraph` 為第一版基礎效果。兩者皆沒有 Custom Function。使用原圖色彩，不依賴場景 Light2D 的強度，避免人物因場景光源不足而變暗。

已掛到 `knight_0`、`knight_1`、`Oracle_0`、`Oracle_1` 的 `CharacterShaderFeedback` 與身體 SpriteRenderer。材質位於 `Assets/Resources/Combat/Materials/CharacterGraph_*.mat`，騎士採金色、預言家採藍紫色；線稿版本的光效較輕。

## 效果與控制

| 材質參數 | 用途 |
| --- | --- |
| `_RimColor` / `_RimStrength` | 職業輪廓光；仍接受場景可讀性系統調整 |
| `_PulseStrength` / `_SheenStrength` | 行動時的輪廓光、淡色身體亮光強度 |
| `_ActionPulse` | 程式讀取實際 Animator 狀態進度；50% 時為峰值 |
| `_HitAmount` / `_HitColor` | 既有受擊事件驅動的短暫閃色 |
| `_GuardFlash` | 成功招架事件驅動的短暫亮邊 |
| `_Dissolve` / `_PhaseColor` / `_PhaseWidth` | 預言家隱身的可見度與溶解邊緣；0 完全顯示、1 完全隱藏 |
| `_SpriteUVRect` | 程式提供目前 Sprite 在圖集中的範圍 |
| `_SkillPhase` / `_SkillAmount` | 實際蓄力／施法動畫進度與重拍強度；待機與受擊時為零 |
| `_SkillPattern` | 0：金色掃光；1：藍紫色交錯幾何紋理 |
| `_SkillStrength` / `_SkillDensity` | 技能掃光亮度／施法紋理密度 |
| `_LowHealthAmount` | 程式依目前 HP 與緩慢脈衝計算的低血量提示強度 |
| `_LowHealthColor` / `_LowHealthStrength` | 低血量輪廓的紅色與最大亮度 |
| `_EntrancePhase` / `_EntranceAmount` | 同步開場一拍中的進度與登場光效強度 |
| `_EntranceStrength` | 登場掃光／星光亮度 |

節拍光效依 Animator 的 normalizedTime 運行，不另外使用固定秒數推算，不修改動畫速度、動畫長度或戰鬥判定。Idle、Hit 不會產生行動重拍脈衝。進入 Hit 時仍由原有受擊事件控制閃色。防禦行動沿用同一個中點脈衝，成功招架另有事件亮邊。

程式以 MaterialPropertyBlock 更新各人物參數，保留 SpriteSkin 的其他資料，不建立每幀材質副本。輪廓使用中心與四鄰域共五次貼圖取樣，溶解使用 Simple Noise；沒有新增人物疊層或全螢幕後製。

第二批新增人物表面的蓄力掃光與施法幾何紋理。騎士在 HeavyCharge、HeavyAttack 使用掃光；預言家在 Bolt、RiftCharge、RiftRelease、Shift、Ward、Fade、Sight 使用施法紋理。流動位置同樣取自動畫進度，Animator 暫停時不會繼續飄動，完整受擊閃色會壓過技能紋理，完全隱身時兩者一起消失。紋理以數學節點形成，沒有新增貼圖取樣或人物繪製層。

第三批在同一張技能 Graph 加入低血量紅色輪廓。HP 低於最大 HP 的 30% 才啟用，越接近 0 越強；1.4 秒緩慢脈衝使用遊戲時間，因此暫停時也會停止。回血回到門檻以上、HP 歸零、尚未初始化 HP 或停用元件時不顯示。只有輪廓額外加光，不整體染紅；受擊閃色優先，隱身也會遮住警示。HP 僅供讀取，不改動戰鬥、同步或存檔。

調整外觀時直接修改 Graph 與材質。`CharacterGraphSetup.Install` 與 `InstallSkillUpgrade` 只供初次產生，已有對應 Graph 時會拒絕覆寫手動編輯。`InstallHealthUpgrade` 只接受上一批未修改版本的 SHA256，升級後保留 Graph GUID 與材質引用；自行調整過的 Graph 請手動合併節點。

第四批加入一拍長度的登場光效：騎士金色掃光、預言家藍紫色星光。從 `GameSceneStartSync` 已有的共同開場拍啟動，以共同時間戳補上遲到生成的進度；晚於一拍則跳過，重複註冊不重播。人物仍依原流程在開場拍顯示，登場光效只加亮、不改透明度；第一個行動或受擊會取消登場效果，既有行動重拍照常運作。停用元件時也會清除登場光效。`InstallEntranceUpgrade` 僅接受未修改的上一批 Graph，保留 GUID 與材質引用。

最後一批已加入 `WeaponRibbon.shadergraph` 與 `CharacterGhost.shadergraph`，皆為可編輯的原生 Universal Unlit 節點圖，沒有 Custom Function。

- 騎士刀光使用金色中心與柔和邊緣，流動參數 `_RibbonPhase` 取自實際動畫進度；沿用既有出刀區間。
- HeavyCharge 新增劍身蓄力光，連接現有 swordBase／swordTip 定位點，動畫 50% 最亮；結束、受擊或停用時清除。每個角色只建立並重用一個 LineRenderer。
- 預言家既有姿勢殘影改用 Graph，保留骨架快照與原有出現時機，依 `_EffectProgress` 逐步噪聲溶解。沒有額外增加殘影生成次數。

武器效果依賴已設定的劍身定位點，沒有從人物貼圖自動分割武器。獨立符文貼花不包含在本輪；施法紋理直接作用於人物表面。

驗證：Windows DX11 離屏實際繪製與 Animator／同步入口檢查共 94 項通過，包含透明度、原圖亮度、殘影溶解、刀光流動、受擊優先、生命值邊界及元件重用。尚未進行兩台客戶端的真實連線驗證，也未將隱藏視窗的數值當成 GPU 效能量測。

測試程式 `CharacterGraphProbe` 只在 Editor 或 `CODEX_CHARACTER_GRAPH` 測試組態編譯，不會進入一般 Build。

補充場景驗證（2026-09-29）：以一般 DX11 測試 Player，在 Photon OfflineMode 模擬另一位玩家的準備狀態，完成七張地圖的 CharacterSelectScene → GameScene 流程，涵蓋四種角色造型。共 391 項檢查通過，沒有捕捉到執行錯誤。確認選圖索引、實際生成角色的 Graph 材質、登場參數清除及既有施法 API；不代表雙端網路對局驗證。結果位於 `CodexLogs/CharacterSceneGraphs-20260929/verification-summary.json`。本輪只更新 Editor／測試組態下的整合測試與文件，一般遊戲 Build 沿用已完成版本。

刀光時序修正（2026-09-29）：出刀後改以 Animator normalizedTime 決定存續，不再受現實時間逾時影響；暫停 Animator 時停止新增軌跡、恢復後沿用進度，轉入受擊等其他狀態時立刻清除殘留軌跡。出刀區間仍為 30%～70%，50% 最強。DX11 與實際 Animator 回歸共 100 項通過。

效果強化（2026-09-29）：四個人物材質均提高待機輪廓、行動重拍、身體亮光、技能掃光／紋理、低血量輪廓與登場光效，並加寬隱身溶解亮邊。招架峰值為原本 1.6 倍，受擊閃色提高但保留輕重差異；刀光亮度 1.1 → 1.6、原有寬度 × 1.4，劍身蓄力光提高透明度；殘影亮度 0.85 → 1.3、光色強度 0.32 → 0.65。維持所有節拍、持續時間、隱身終點及傷害判定。100 項 DX11 檢查通過，四種造型共 20 張效果截圖皆比原版更明亮，數值記錄在 `CodexLogs/CharacterStronger-20260929/tuning.json`。
