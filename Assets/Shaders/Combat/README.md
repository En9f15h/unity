# 第一批角色與戰鬥 Shader

適用 Unity 6000.3.7f1、專案內的 URP 17.3 / Renderer2D。已套用到 Prefab，直接從原本的選角與戰鬥流程使用。

美術方向：半寫實中世紀奇幻；騎士偏金白、電藍，先知偏紫青。保留原圖、骨架、動畫與原有敵我 Light2D。手繪皮膚使用較弱的邊緣補光。

| Shader | 使用位置 | 效果 |
| --- | --- | --- |
| Combat/Character Lit | Knight、Oracle 各兩個戰鬥皮膚 | URP 2D 光照、短暫受擊閃色、輕微邊緣補光、角色空間一致的相位溶解 |
| Combat/Energy Sprite | 先知 11 個特效 Prefab、騎士格擋／終極特效中的 Sprite、動態法術圖層與殘影 | 能量流動、透明疊加、護盾局部波紋、符文掃光、殘影消散 |
| Combat/Energy Ribbon and Particle | 雷擊、劍身殘電、攻擊劍光、雙職業粒子及四個選角粒子材質 | 柔化邊緣、亮芯、頂點透明度淡出；關閉深度寫入 |
| Combat/Character UI | 四個選角角色預覽 Prefab | 輕微邊緣補光，支援 UI Stencil、RectMask2D 與透明裁切 |

材質預設放在 `Assets/Resources/Combat/Materials/`，第一批共 17 個。舊有四個選角粒子材質也已改用透明粒子 shader。

第二批 UI、血跡與三張新地圖氣氛層已接入，新增 7 個材質；說明見 [PHASE2.md](PHASE2.md)。本頁以下記錄第一批的實作與當時的驗證範圍。

## 調整位置

- 角色：`CharacterKnight.mat`、`CharacterOracle.mat`、`CharacterSketch.mat` 的 Edge Strength / Edge Light。受擊強度與秒數由角色上的 `CharacterShaderFeedback` 控制。
- 護盾：`OracleWard.mat` 的 Opacity（目前 0.5）、Intensity（0.85）與 Glow Strength（0.16）。擋住攻擊時由控制器傳入角色前方位置啟動波紋；這是戰鬥表現點，沒有新增物理碰撞判定。
- 秘法：`OracleEnergy.mat`、`OracleBurst.mat`、`OracleRune.mat` 的 Glow / Intensity / Energy Flow。保留貼圖本身的 UV，噪聲只改變亮度或覆蓋率。
- 騎士：`KnightLightning.mat`、`KnightSlash.mat` 的 Core / Edge / Intensity。角色的 `KnightSlashShaderVFX` 可調 Trail Width。只有輕攻擊、低位攻擊、重攻擊釋放會出現劍光；期間由既有 `BattleStepPlayer` 的 stepDuration 決定。
- 光暈：`Assets/Settings/CombatBloom.asset`，Bloom intensity 0.16、threshold 1.15。GameScene 已新增 `Combat Presentation Volume`，主相機啟用後製；可調該 Volume 的 Weight 來比較光暈強弱。
- 選角預覽：名稱帶 `_UIPreview.mat` 的四個材質。這一批僅處理角色預覽，Ready、VS、能量條等主題 UI 留在第二批。

## 執行方式與維護

角色以 MaterialPropertyBlock 傳入狀態，共用材質不會因單一角色受擊而一起閃色。先知 Fade / Shift 改用溶解，取消 Shift 或重新啟用角色會恢復顯示。殘影以當下骨架與原 UV 烘焙成短暫 Mesh，保留姿勢，結束時自行釋放。劍光隨攻擊節拍播放，HitStop 時停止生成新軌跡；角色受擊及法術亮度使用 unscaled time。

`Tools > Combat Shaders > Validate Installation` 可檢查基本掛載。`Install First Batch` 是重建初始預設的工具，會重新寫入材質參數及 Prefab 掛載；已手動微調材質時，請先備份再重建。

完整渲染驗證工具為 `CombatShaderValidation.RunBatch`，限獨立 Unity 批次執行。它建立未儲存的空白場景進入 Play Mode，輸出圖片和驗證紀錄，完成後結束 Unity。沒有啟動 Photon 房間。

## 驗證與範圍

- Unity 編譯與 GPU 渲染驗證：四個戰鬥皮膚、四個 UI 預覽、15 個特效 Prefab；包含透明端點、溶解恢復、翻面、骨架殘影、劍光中斷、粒子、波紋、動態能量與 UI 裁切。
- 明亮城堡與暗石場的示範圖使用實際資產、URP 2D 與新 Bloom 設定，在獨立場景中安排展示位置；它們是材質示範，並非連線對戰截圖。
- 原圖與動畫檔、CharacterSelectScene 及地圖定義經備份對照未變更。GameScene 的差異僅為相機後製及新 Volume，保留原本背景／地板 index。
- 尚未進行雙人連線整場對戰、行動裝置 GPU 或 FPS 量測。護盾折射、全角色合成外框，以及第二批 UI／血跡／背景氣氛效果不包含在這批。
- 寫實先知 UI 預覽的原素材已有細矩形線條，使用原本 UI 材質也能看到；本批保留該素材，沒有改動圖像。

備份：`CodexBackups/before-combat-shaders-20260914-210149/`，3,487 個原始檔、562.18 MiB，含複製紀錄與 SHA-256 清單。

驗證圖、紀錄及檔案差異清單：`CodexLogs/CombatShaders-20260914/`。
