# Shader Graph 特效工作坊

Unity 6000.3.7f1／URP 17.3.0。兩張可直接開啟編輯的原生節點圖，使用 **URP Sprite Unlit**，不需要 Custom Function 或額外 HLSL。

## 開啟與預覽

1. 開啟 `ShaderGraphWorkshop.unity`，按 Play，查看上排金色與紫色能量流動。
2. 下排展示溶解進度 0、0.5、1。選取 `Preview Controls`，調整 `Middle Progress`；勾選 `Animate Middle` 可在 Play 時往返播放。這只是展示控制器，不會改動戰鬥或角色 Animator。
3. 雙擊 `EnergyFlow.shadergraph` 或 `EdgeDissolve.shadergraph` 編輯節點。節點依「貼圖／色彩 → 流動或進度 → 混合 → 輸出」分組。
4. 複製 `Materials` 內的材質，再將副本指定给測試物件的 SpriteRenderer。`_MainTex` 由 SpriteRenderer 的 Sprite 提供。

展示使用專案既有的 Oracle 傳送符文原圖；原素材與匯入設定未修改。材質以原圖乘上色彩，因此不會把所有像素強制染成單一顏色。

## EnergyFlow：能量流動

適用於符文、魔法陣、護盾及技能光效。

| 參數 | 功能 |
| --- | --- |
| Energy Tint (HDR) | 原圖的色彩乘數 |
| Intensity | 整體亮度 |
| Opacity | 整體透明度 |
| Flow Speed | 流動速度，負值反向 |
| Noise Scale | 流動紋理大小 |
| Flow Strength | 流動明暗差異 |

透過既有 `CombatShaderClock` 的全域 `_CombatVisualTime` 驅動。編輯模式時圖像靜止，進入 Play 才會流動；材質預覽視窗也可能呈現靜止畫面。這是裝飾流動時間，不是節拍判定時間。

## EdgeDissolve：亮邊溶解

適用於消失、傳送及幻影；也可在獨立角色副本上試用。

| 參數 | 功能 |
| --- | --- |
| Dissolve Progress 0-1 | 0 完整，1 完全消失；超出範圍會限制在端點 |
| Tint (HDR) | 原圖色彩乘數 |
| Edge Colour / Edge Intensity | 消失邊緣的色彩與亮度 |
| Edge Width / Softness | 亮邊寬度與柔和程度 |
| Noise Scale | 消失斑塊大小 |
| Opacity | 整體透明度 |

`Preview Controls` 使用 MaterialPropertyBlock 個別設定進度，不會改寫共用材質。若要在展示場景直接調材質的 Progress，先停用該控制器，並在 SpriteRenderer 清除原本的 PropertyBlock（或改用新建的測試物件）。

## 與角色／節拍配合

已接入正式 Oracle 技能：`Oracle_RiftWarningVFX`、`Oracle_ShiftVFX`、`OracleShiftEnterFX`、`Oracle_SightVFX` 使用 EnergyFlow；`Oracle_FadeVFX`、`OracleShiftExitFX` 使用 EdgeDissolve。正式材質為 `Assets/Resources/Combat/Materials/OracleGraphFlow.mat` 與 `OracleGraphDissolve.mat`，和展示材質各自獨立。

既有 `OracleVFXController` 透過 `CombatEffectShaderDriver.Play(duration)` 傳入動作時間；Driver 使用 MaterialPropertyBlock 個別驅動 `_EffectProgress`。Fade 的 `Graph Dissolve End` 設為 0.5，在原本動作中段重新顯現時已完全溶解；傳送離場設為 1，沿用控制器傳入的半段動作時間。再次播放會歸零，不會改寫共用材質。

角色除 Hit 外的 60 張／1 秒動畫與既有 50% 重拍設定保持原樣；這次沒有修改動作時長、Animator.speed 邏輯或傷害判定。展示場景的往返播放只供美術調參。護盾的接觸波紋、角色受擊閃白與骨骼姿勢殘影仍使用各自原有的材質。

這兩張圖使用 Unlit，不受場景光源影響；HDR 色彩本身不代表有螢幕泛光，展示相機未啟用後製。它們也未實作 uGUI 的 Mask／Stencil，不適合直接當作 UI Image 的材質。正式角色原本的受擊閃白等材質功能需另行整合。

## 工具與驗證

`Editor/CombatShaderGraphWorkshop.cs` 是依專案已安裝的 Shader Graph 17.3 編輯器 API 建立節點的工具；反射僅用於編輯器。圖檔生成後可獨立編輯，不需要執行生成工具。生成入口會拒絕覆寫既有 Graph。

建置工具的輸出位置預設供 `CodexUnityBatchProject` 隔離驗證專案使用。`RefreshDemoAndBuildProbe` 會重建展示場景，請勿拿它覆寫自己調整過的展示。

測試程式僅在 Editor 或定義 `CODEX_SHADER_GRAPH_WORKSHOP` 的驗證建置編譯；一般展示建置不含此測試。原遊戲的 Build Settings 未加入展示場景。
