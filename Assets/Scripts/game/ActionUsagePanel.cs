using System.Globalization;
using System.Linq;
using UnityEngine;

// Icons and values belong to the square on the health-bar artwork, not the shrinking fill.
public sealed class ActionUsagePanel : MonoBehaviour
{
    static Font font;
    GameObject root;
    SpriteRenderer frame;
    TextMesh[] lines;
    SpriteRenderer[] icons;
    Bounds[] iconBounds;
    float[] advances;
    bool frozen;
    public string DisplayedText => lines == null ? "" : string.Join("\n", lines.Select(t => t.text));
    public void Freeze(LocalActionUsage.Snapshot snapshot, string characterId)
    {
        if (frozen) return;
        frame = GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name.Trim().ToLowerInvariant().Contains("hpbarbackground"));
        if (frame == null || frame.sprite == null) return;
        frozen = true;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Microsoft YaHei", "Noto Sans CJK TC", "Arial" }, 64);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        font.RequestCharactersInTexture("騎士預言家攻擊防禦說謊率尚無紀錄未提供0123456789.%—", 64, FontStyle.Bold);
        bool chinese = font.HasCharacter('攻') && font.HasCharacter('禦');
        string title = characterId == "knight" ? (chinese ? "騎士" : "KNIGHT") : (chinese ? "預言家" : "ORACLE");
        int[] rates = LocalActionUsage.PercentageTenths(snapshot);
        string[] names = chinese ? new[] { "攻擊", "防禦", "說謊率" } : new[] { "ATK", "DEF", "LIE" };
        root = new GameObject(name + "_ActionUsage"); root.transform.SetParent(transform.parent, false);
        lines = new TextMesh[4];
        icons = new SpriteRenderer[4];
        iconBounds = new Bounds[4];
        advances = new float[4];
        string[] imageNames = { characterId == "knight" ? "騎士" : "預言家", "攻擊率", "防禦率", "說謊率" };
        Color[] colors = { new Color(1f,.86f,.57f), new Color(1f,.72f,.59f), new Color(.60f,.88f,1f), new Color(.85f,.74f,1f) };
        for (int i = 0; i < lines.Length; i++)
        {
            Sprite sprite = Resources.Load<Sprite>("UI/" + imageNames[i]);
            if (sprite != null)
            {
                var iconObject = new GameObject(imageNames[i] + " Icon"); iconObject.transform.SetParent(root.transform, false);
                var icon = iconObject.AddComponent<SpriteRenderer>(); icon.sprite = sprite;
                icon.sortingLayerID = frame.sortingLayerID; icon.sortingOrder = Mathf.Max(40, frame.sortingOrder + 5);
                icons[i] = icon;
                // Fit the imported mesh, so transparent padding does not shrink the visible symbol.
                var vertices = sprite.vertices;
                var visibleBounds = new Bounds(vertices.Length > 0 ? (Vector3)vertices[0] : Vector3.zero, Vector3.zero);
                foreach (var vertex in vertices) visibleBounds.Encapsulate((Vector3)vertex);
                iconBounds[i] = visibleBounds.size.sqrMagnitude > 0 ? visibleBounds : sprite.bounds;
            }
            var obj = new GameObject(i == 0 ? "Class and history" : names[i-1]); obj.transform.SetParent(root.transform, false);
            var text = obj.AddComponent<TextMesh>(); text.font = font; text.fontSize = 64; text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = colors[i];
            if (i == 0) text.text = (icons[i] == null ? title + " " : "") + (snapshot == null ? (chinese ? "未提供" : "N/A") : !snapshot.HasHistory ? (chinese ? "尚無紀錄" : "NEW") : "");
            else text.text = (icons[i] == null ? names[i-1] + "  " : "") + (snapshot == null || (i == 3 ? snapshot.declaredSlots == 0 : snapshot.Total == 0) ? "—" : (rates[i-1] / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "%");
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material;
            renderer.sortingLayerID = frame.sortingLayerID; renderer.sortingOrder = Mathf.Max(40, frame.sortingOrder + 5);
            font.RequestCharactersInTexture(text.text, 64, FontStyle.Bold);
            foreach (char c in text.text) if (font.GetCharacterInfo(c, out CharacterInfo info, 64, FontStyle.Bold)) advances[i] += info.advance;
            lines[i] = text;
        }
        LateUpdate();
    }
    void LateUpdate()
    {
        if (root == null || frame == null || frame.sprite == null) return;
        root.SetActive(frame.enabled && frame.gameObject.activeInHierarchy);
        var bounds = frame.sprite.bounds;
        float x = frame.flipX ? .885f : .115f;
        Vector3 local = new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x), Mathf.Lerp(bounds.min.y,bounds.max.y,.495f), 0);
        var camera = Camera.main; Quaternion rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
        root.transform.SetPositionAndRotation(frame.transform.TransformPoint(local), rotation);
        root.transform.localScale = Vector3.one;
        float width = frame.transform.TransformVector(Vector3.right * bounds.size.x * .135f).magnitude;
        float height = frame.transform.TransformVector(Vector3.up * bounds.size.y * .49f).magnitude;
        for (int i = 0; i < lines.Length; i++)
        {
            var text = lines[i];
            bool hasIcon = icons[i] != null;
            float y = height * (.36f - i * .24f);
            float textX = hasIcon ? width * (i == 0 ? .1f : .15f) : 0;
            text.transform.position = root.transform.position + rotation * new Vector3(textX, y, -.02f);
            text.transform.rotation = rotation;
            text.characterSize = Mathf.Min(height * (i == 0 ? .021f : .029f), width * (hasIcon ? .7f : 1f) * 10f / Mathf.Max(advances[i], 1));
            if (hasIcon)
            {
                float iconX = i == 0 && string.IsNullOrEmpty(text.text) ? 0 : -width * .35f;
                var boundsForIcon = iconBounds[i];
                float scale = Mathf.Min(width * .26f / Mathf.Max(boundsForIcon.size.x, .001f), height * .22f / Mathf.Max(boundsForIcon.size.y, .001f));
                var iconTransform = icons[i].transform;
                iconTransform.localScale = Vector3.one * scale;
                iconTransform.SetPositionAndRotation(root.transform.position + rotation * (new Vector3(iconX, y, -.02f) - boundsForIcon.center * scale), rotation);
            }
        }
    }
    void OnDestroy() { if (root != null) Destroy(root); }
}
