using UnityEngine;

public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance;

    public CharacterClassData selectedClass;
    public int selectedAppearanceIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectClass(CharacterClassData classData)
    {
        selectedClass = classData;
        selectedAppearanceIndex = 0;
    }

    public void SelectAppearance(int index)
    {
        if (selectedClass == null) return;
        if (index < 0 || index >= selectedClass.appearances.Count) return;

        selectedAppearanceIndex = index;
    }

    public CharacterAppearanceData GetSelectedAppearance()
    {
        if (selectedClass == null) return null;
        if (selectedAppearanceIndex < 0 || selectedAppearanceIndex >= selectedClass.appearances.Count) return null;

        return selectedClass.appearances[selectedAppearanceIndex];
    }
}