# Shader 場景驗證與桌機基準

`ShaderSceneProbe` 使用 Photon OfflineMode，建立一個本機 actor 與一個模擬的遠端資料記錄，不連線至任何伺服器或其他玩家。模擬器回報 Ready 和轉場準備資料，實際選角、倒數、SceneTransitionManager、GameSceneStartSync、GameManager 與背景同步仍由現有元件執行。

此工具不等於雙人網路或完整對戰測試。桌機效能模式會在進入戰鬥後停止回合規劃，加入一個先知外觀作為對照，使用真實場景、UI、角色動畫與技能 VFX 量測渲染成本。

## 執行入口

- `ShaderSceneIntegrationValidation.RunBatch`：在獨立 Unity 批次程序中執行 7 組選角至戰鬥流程，覆蓋兩職業各兩皮膚。完成後退出該 Editor。
- `ShaderSceneIntegrationValidation.BuildBenchmark`：建立 Windows Development Player，僅該次建置加入 `CODEX_SHADER_BENCHMARK`。暫時啟用 Frame Timing Stats，完成後還原；不修改原本 Build Settings 場景清單。
- 產生的 `ShaderBenchmark.exe` 接受 `-shader-probe-output <目錄>`，其餘使用標準 Unity Player 參數。請以 1920×1080、D3D11 的正常 Player 執行，效能量測不要加 `-batchmode` 或 `-nographics`。測試結束後自行退出，輸出 `validation.txt`、`runtime-errors.txt`、`performance.json` 和截圖。

原始碼以 `UNITY_EDITOR || CODEX_SHADER_BENCHMARK` 包住，普通 Player 不包含測試元件或自動入口。請勿替正式版本加入該測試符號，也不要在使用者工作中的 Unity Editor 執行會退出程序的批次入口。

## 量測解讀

三張新地圖各測待機、停用 Bloom 與薄霧、連續施放先知護盾與裂隙。採樣期間不擷取圖片；初始暖機不列入幀時間。紀錄硬體、品質等級、render scale、實際螢幕解析度、平均／中位／P95 幀時間，以及可用的 GPU／CPU、draw call 和 GC 計數。

數值是這台電腦與測試版的短時基準；不代表正式版本、長時間雙人對戰或行動裝置表現。缺少的 GPU／Profiler 計數以 -1 和 0 samples 表示，不能當作零成本。

若平均 Draw Calls 為 0，該輪只反映沒有正常繪製畫面的更新成本，不能用作桌機渲染或 FPS 結論；即使場景流程檢查通過也必須捨棄該輪效能資料。

Frame Timing Stats 的使用依據：[Unity FrameTimingManager](https://docs.unity3d.com/6000.0/Documentation/Manual/frame-timing-manager-enable.html)；建置限定符號依據：[BuildPlayerOptions.extraScriptingDefines](https://docs.unity3d.com/ScriptReference/BuildPlayerOptions-extraScriptingDefines.html)。

2026-09-15 的整合結果、測量與來源差異紀錄在 `CodexLogs/SceneIntegration-20260915/`。
