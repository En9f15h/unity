using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.VisualScripting;

public class DOOM : MonoBehaviour
{
    [Header("DOOM Display")]
    [SerializeField]
    private RawImage doomScreen;


    private Texture2D doomTexture;

    private int width;
    private int height;
    private int bufferSize;

    private bool initialized;

    private bool forwardState;
    private bool backwardState;

    private bool leftState;
    private bool rightState;

    private bool fireState;
    private bool useState;

    private bool runState;

    private bool enterState;
    private bool escapeState;
    private bool tabState;

    private bool weapon1State;
    private bool weapon2State;
    private bool weapon3State;
    private bool weapon4State;
    private bool weapon5State;
    private bool weapon6State;
    private bool weapon7State;

    private const int KEY_RIGHTARROW = 0xAE;
    private const int KEY_LEFTARROW = 0xAC;
    private const int KEY_UPARROW = 0xAD;
    private const int KEY_DOWNARROW = 0xAF;

    private const int KEY_USE = 0xA2;
    private const int KEY_FIRE = 0xA3;

    private const int KEY_ESCAPE = 27;
    private const int KEY_ENTER = 13;
    private const int KEY_TAB = 9;

    private const int KEY_RSHIFT = 0xB6;
    private const int KEY_RCTRL = 0x9D;
    private const int KEY_RALT = 0xB8;
    // ============================================================
    // Native DLL
    // ============================================================

    [DllImport(
        "doomgeneric",
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi)]
    private static extern int Doom_Init(string wadPath);


    [DllImport(
        "doomgeneric",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern void Doom_Tick();

    [DllImport(
    "doomgeneric",
    CallingConvention = CallingConvention.Cdecl)]
    private static extern void Doom_KeyEvent(
    int doomKey,
    int pressed);


    [DllImport(
        "doomgeneric",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Doom_GetFrameBuffer();


    [DllImport(
        "doomgeneric",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int Doom_GetWidth();


    [DllImport(
        "doomgeneric",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int Doom_GetHeight();


    [DllImport(
        "doomgeneric",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int Doom_GetBufferSize();


    [DllImport(
        "doomgeneric",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern int Doom_IsInitialized();


    // ============================================================
    // Unity
    // ============================================================

    private void Start()
    {
        StartDoom();
        doomScreen.GameObject().SetActive(true);
    }


    private void Update()
    {
        if (!initialized)
            return;

        // 1. Unity Input
        HandleInput();

        // 2. DOOM 執行
        Doom_Tick();

        // 3. 顯示最新畫面
        UpdateDoomTexture();
    }

    // ============================================================
    // 初始化
    // ============================================================

    private void StartDoom()
    {
        string wadPath = Path.Combine(
            Application.streamingAssetsPath,
            
            "doom.wad"
        );


        Debug.Log("[DOOM] WAD Path:");
        Debug.Log(wadPath);


        // --------------------------------------------------------
        // 先確認 WAD 存在
        // --------------------------------------------------------

        if (!File.Exists(wadPath))
        {
            Debug.LogError(
                "[DOOM] 找不到 WAD！\n" +
                wadPath
            );

            return;
        }


        // --------------------------------------------------------
        // Native Init
        // --------------------------------------------------------

        int result = Doom_Init(wadPath);


        if (result == 0)
        {
            Debug.LogError("[DOOM] Doom_Init 失敗");
            return;
        }


        // --------------------------------------------------------
        // 取得 framebuffer 資訊
        // --------------------------------------------------------

        width = Doom_GetWidth();
        height = Doom_GetHeight();
        bufferSize = Doom_GetBufferSize();


        Debug.Log(
            $"[DOOM] Resolution = {width} x {height}"
        );

        Debug.Log(
            $"[DOOM] Buffer Size = {bufferSize} bytes"
        );


        // --------------------------------------------------------
        // 建立 Unity Texture
        //
        // DoomGeneric:
        //
        // 整數：
        // 0xAARRGGBB
        //
        // Windows little endian 記憶體：
        // B G R A
        //
        // 所以 Unity 用 BGRA32
        // --------------------------------------------------------

        doomTexture = new Texture2D(
            width,
            height,
            TextureFormat.BGRA32,
            false
        );


        // 保留 DOOM Pixel Art
        doomTexture.filterMode = FilterMode.Point;

        doomTexture.wrapMode = TextureWrapMode.Clamp;


        // --------------------------------------------------------
        // RawImage
        // --------------------------------------------------------

        doomScreen.texture = doomTexture;


        // Doom framebuffer 第一列是畫面上方
        // Unity Texture UV 原點方向不同
        //
        // 用 UV 翻轉 Y
        doomScreen.uvRect = new Rect(
            0,
            1,
            1,
            -1
        );


        initialized = true;


        Debug.Log("[DOOM] 初始化完成");
    }

    private void SyncKey(
ref bool previousState,
bool currentState,
int doomKey)
    {
        // 狀態沒有改變
        if (previousState == currentState)
            return;

        previousState = currentState;

        Doom_KeyEvent(
            doomKey,
            currentState ? 1 : 0
        );
    }
    private void HandleInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;


        // ========================================================
        // Movement
        // ========================================================

        SyncKey(
            ref forwardState,
            keyboard.wKey.isPressed ||
            keyboard.upArrowKey.isPressed,
            KEY_UPARROW
        );


        SyncKey(
            ref backwardState,
            keyboard.sKey.isPressed ||
            keyboard.downArrowKey.isPressed,
            KEY_DOWNARROW
        );


        SyncKey(
            ref leftState,
            keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed,
            KEY_LEFTARROW
        );


        SyncKey(
            ref rightState,
            keyboard.dKey.isPressed ||
            keyboard.rightArrowKey.isPressed,
            KEY_RIGHTARROW
        );


        // ========================================================
        // Fire
        //
        // Ctrl / Mouse Left
        // ========================================================

        bool mouseFire =
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed;

        SyncKey(
            ref fireState,
            keyboard.leftCtrlKey.isPressed ||
            keyboard.rightCtrlKey.isPressed ||
            mouseFire,
            KEY_FIRE
        );


        // ========================================================
        // Use
        //
        // Space / E
        // ========================================================

        SyncKey(
            ref useState,
            keyboard.spaceKey.isPressed ||
            keyboard.eKey.isPressed,
            KEY_USE
        );


        // ========================================================
        // Run
        //
        // Shift
        // ========================================================

        SyncKey(
            ref runState,
            keyboard.leftShiftKey.isPressed ||
            keyboard.rightShiftKey.isPressed,
            KEY_RSHIFT
        );


        // ========================================================
        // Menu
        // ========================================================

        SyncKey(
            ref escapeState,
            keyboard.escapeKey.isPressed,
            KEY_ESCAPE
        );


        SyncKey(
            ref enterState,
            keyboard.enterKey.isPressed ||
            keyboard.numpadEnterKey.isPressed,
            KEY_ENTER
        );


        SyncKey(
            ref tabState,
            keyboard.tabKey.isPressed,
            KEY_TAB
        );


        // ========================================================
        // Weapons
        // ========================================================

        SyncKey(
            ref weapon1State,
            keyboard.digit1Key.isPressed,
            '1'
        );

        SyncKey(
            ref weapon2State,
            keyboard.digit2Key.isPressed,
            '2'
        );

        SyncKey(
            ref weapon3State,
            keyboard.digit3Key.isPressed,
            '3'
        );

        SyncKey(
            ref weapon4State,
            keyboard.digit4Key.isPressed,
            '4'
        );

        SyncKey(
            ref weapon5State,
            keyboard.digit5Key.isPressed,
            '5'
        );

        SyncKey(
            ref weapon6State,
            keyboard.digit6Key.isPressed,
            '6'
        );

        SyncKey(
            ref weapon7State,
            keyboard.digit7Key.isPressed,
            '7'
        );
    }

    // ============================================================
    // framebuffer -> Texture2D
    // ============================================================

    private void UpdateDoomTexture()
    {
        IntPtr framebuffer = Doom_GetFrameBuffer();


        if (framebuffer == IntPtr.Zero)
        {
            Debug.LogError("[DOOM] FrameBuffer = NULL");

            initialized = false;

            return;
        }


        doomTexture.LoadRawTextureData(
            framebuffer,
            bufferSize
        );


        doomTexture.Apply(
            false,
            false
        );
    }
}