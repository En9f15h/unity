# 第二批 Shader：UI、血跡與場景氣氛

第二批延續半寫實中世紀奇幻風格：金色金屬用於 Ready／VS、青藍用於騎士能量、紫青符文用於先知與動作揭示。保留原圖中的材質與筆觸，以短暫掃光和低強度脈衝提示狀態。

## 已接入的效果

| Shader | 實際使用位置 | 表現 |
| --- | --- | --- |
| Combat/Presentation UI | 選角 Ready 按鈕／Ready 圖層／VS、騎士能量條、先知能量眼、敵方行動揭示框 | Ready 掃光與待命微光、能量增加脈衝、滿能量與先知視界符文、揭示時掃光 |
| Combat/Blood | bloodDecalPrefab、bloodSprayPrefab | 血跡逐漸暗沉、淡出期間由噪聲侵蝕；噴血使用透明混合與柔化液滴 |
| Combat/Stage Atmosphere | GameScene 中三張新背景的獨立霧層 | 低位緩慢流動薄霧，保留背景石材細節 |
| Combat/Presentation UI 的 Mist 模式 | MapSelectorController 與 MapSelectionCard 的新地圖預覽 | 降低強度的對應霧層，配合預覽圖片長寬比 |

新增 3 個 shader、7 個材質、1 個三地圖氣氛設定檔。第一批的 4 個 shader 與 17 個材質仍保留。

## 可調整的資產

材質位於 `Assets/Resources/Combat/Materials/`：

- `UI_Gold`、`UI_Cyan`、`UI_Rune`：Accent 控制色調，Highlight Strength 控制強度，目前依序為 0.28、0.20、0.24。
- `BloodDecal`、`BloodSpray`：Dry multiplier 控制乾涸後的色彩倍率。原本血跡顏色、隨機貼圖、生命週期與地板遮罩設定保留。
- `StageMist`、`UI_Mist`：提供霧層 shader。每張背景的顏色和濃度由下面的 palette 控制，會覆蓋材質的對應參數。

`Assets/Resources/Combat/StageAtmospherePalette.asset` 的預設：

| 背景 | 色調 | 濃度 |
| --- | --- | --- |
| arena_1920x1088 | 冷灰 | 0.055 |
| castle_1920x1088 | 暖金灰 | 0.050 |
| 暗石競技場 | 冷藍灰 | 0.090 |

選角預覽使用上述濃度的 65%。Palette 以 Sprite 引用對應背景，不依賴 map index。原本 `background.png` 與其他舊地圖會關閉霧層。

## 狀態與維護

`UIShaderFeedback` 以每張 Image 的快取材質隔離狀態，保留 Stencil、RectMask2D、CanvasGroup 透明度與 Image 填滿比例。掃光使用 unscaled time，可在 HitStop 期間完成。停用時清除暫時狀態並釋放材質；行動揭示框再次顯示時會重新設定亮度。

`BloodDecalFade` 在原本 holdTime 內推進乾涸，再於原 fadeDuration 內同時淡出和侵蝕。重新啟用或呼叫 RestartFadeFromCurrentTransform 時清除侵蝕狀態。噴血保留既有粒子速度、形狀、大小、顏色與生命週期。

`StageAtmosphereController` 依背景建立一個透明霧層，不新增碰撞器。GameScene 的霧層使用背景的 Sorting Layer，Order 為背景 +1；背景擁有者移除時會釋放網格。UI 霧層不攔截點擊。

另修正 `OracleSightEnergyUI` 在 ActiveImage 未指定時的空引用；此欄位在現有 Prefab 中確實未指定。

`Tools > Combat Shaders > Install Second Batch` 可重建初始配置，會寫入材質預設及場景／Prefab 掛載。手動調整後若需要重建，先備份自己的參數。

## 驗證與限制

`PresentationShaderValidation.RunBatch` 在獨立未儲存場景中進入 Play Mode，驗證實際資產的 GPU 渲染、UI 事件與遮罩、能量比例、血跡乾涸／侵蝕／生命週期、三張新背景及實際地圖選擇控制器。測試完成後會結束該 Unity 程序；請勿在工作中的互動式 Editor 執行此入口。

結果與渲染圖位於 `CodexLogs/PresentationShaders-20260915/`。圖片為實際專案資產的獨立材質示範，並非連線對戰截圖。原圖與動畫、地圖定義、兩個選角 maps 陣列、GameScene 背景／地板陣列均以備份 SHA-256 與序列化內容比對。

2026-09-15 最終結果：第二批 73 項檢查通過，第一批 149 項回歸檢查通過。七個自訂 shader 的已渲染 GPU variants 均未回報編譯錯誤。第二批紀錄為 `CodexLogs/phase2-shader-validation-final.log`；第一批回歸紀錄為 `CodexLogs/phase2-combat-regression.log`。

Editor 記錄仍有既有 Visual Scripting 快取找不到 `BattleAnimationController` 類型的序列化訊息；第一批原始最終紀錄也有相同訊息。本次沒有修改該套件或快取。

尚未執行雙人連線整場對戰或行動裝置 GPU／FPS 量測。UI 每個啟用的效果使用獨立材質，場景霧層增加一個透明繪製；行動裝置效能仍需在目標機器確認。

這批沒有加入全畫面扭曲或重繪素材。護盾折射、完整角色合成外框仍不在目前實作範圍。

備份：`CodexBackups/before-phase2-shaders-20260915-132331/`，3,555 個原始檔、562.27 MiB，包含複製紀錄和 SHA-256 清單。
