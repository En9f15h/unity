using UnityEditor;
using UnityEngine;

public static class OracleConfigReferenceRepair
{
    private const string OracleConfigPath = "Assets/Scripts/game/classes/OracleConfig.asset";
    private const string DanceIconGuid = "69d363a8e6edeed4ead43f91ba964396";

    [MenuItem("Tools/Oracle/Repair Config References")]
    public static void RepairFromMenu()
    {
        Repair();
    }

    public static void RepairFromCommandLine()
    {
        bool repaired = Repair();
        EditorApplication.Exit(repaired ? 0 : 1);
    }

    private static bool Repair()
    {
        OracleClassConfig config = AssetDatabase.LoadAssetAtPath<OracleClassConfig>(OracleConfigPath);
        if (config == null)
        {
            Debug.LogError("[OracleConfigReferenceRepair] OracleConfig.asset is missing.");
            return false;
        }

        if (config.dance == null)
            config.dance = new DanceActionData();

        Sprite danceIcon = LoadFirstSpriteByGuid(DanceIconGuid);
        if (danceIcon == null)
        {
            Debug.LogError("[OracleConfigReferenceRepair] Dance icon sprite could not be resolved.");
            return false;
        }

        config.dance.iconSprite = danceIcon;
        config.dance.lockedContinuationSprite = danceIcon;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[OracleConfigReferenceRepair] Dance icon assigned from " + AssetDatabase.GetAssetPath(danceIcon) + ".");
        return true;
    }

    private static Sprite LoadFirstSpriteByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return null;

        Sprite mainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (mainSprite != null)
            return mainSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }

        return null;
    }
}
