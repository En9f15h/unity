# 地圖與角色明暗分離

前三張新地圖使用 `StageReadabilityPalette`，以實際 Sprite 參照配對。GameScene 的背景上掛 `StageReadabilityController`；選圖卡片與主預覽透過既有 `StageAtmosphereController.ForImage` 安裝同一套配色。

| 地圖 | 亮度倍率 | 對比 | 飽和度 | 角色邊緣光倍率 |
| --- | ---: | ---: | ---: | ---: |
| Grand Arena | 0.88 | 0.93 | 0.88 | 1.35 |
| Highland Castle | 0.72 | 0.90 | 0.78 | 1.30 |
| Dark Stone Arena | 1.00 | 0.992 | 0.95 | 1.50 |

亮度參數作用在線性 RGB，不能直接當作畫面感知亮度百分比。對比以 0.18 中灰為中心壓縮，保留陰影；暗色競技場只做很小幅度的調整。

`StageBackground.shader` 保留 URP 2D 光照、法線與 Sprite 透明度，在背景的原有繪製中調整 RGB。`StageBackgroundUI.shader` 使用相同函式，保留 Image 顏色、CanvasGroup 透明度、Mask stencil 與 RectMask2D 軟遮罩。UI 材質依實例重用，停用、舊地圖或卸載時釋放。

角色沿用 `CharacterLit.shader` 的一像素 Sprite 內緣光，沒有新增整體外描邊或全螢幕效果。`CharacterShaderFeedback` 依角色所屬場景查找地圖配色，以 MaterialPropertyBlock 混入 35% 地圖邊緣色並乘上原材質強度。因此寫實皮膚原始 0.08 約為 0.104–0.12，草稿皮膚原始 0.025 約為 0.0325–0.0375。沒有改動角色原色、SpriteSkin 資料、受擊和消散公式。

未配對的 Sprite、停用元件和場景卸載都恢復原本呈現。背景調色不套到實體地板、血條、技能或全場 Bloom。原地圖索引、背景／地板對應、動畫、節拍和玩法不變：Hit 以外的角色動畫維持原 60 FPS／1 秒，50%／第 30 張的重拍規則仍由既有系統控制。

安裝選單：`Tools > Combat Shaders > Install Stage Readability`。重跑安裝會將此批配色設回上表；若要自行微調，可直接編輯 `Assets/Resources/Combat/StageReadabilityPalette.asset`。

驗證入口：`StageReadabilityValidation.RunBatch`、`StageReadabilityValidation.RunIntegration`；既有角色與特效回歸：`CombatShaderValidation.RunBatch`。結果位於 `CodexLogs/StageReadability-20260916`。
