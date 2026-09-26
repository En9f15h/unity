using UnityEditor;
using UnityEngine;

public static class CharacterSelectionParticleGenerator
{
    public const string PrefabFolder = "Assets/Generated/CharacterSelection/Prefabs";
    public const string MaterialFolder = "Assets/Generated/CharacterSelection/Materials";

    public static void GenerateParticlePrefabs()
    {
        CharacterSelectionTextureGenerator.EnsureFolder(PrefabFolder);
        CharacterSelectionTextureGenerator.EnsureFolder(MaterialFolder);

        Material readyMaterial = CreateParticleMaterial("M_ReadyAura", new Color(0.65f, 0.45f, 1f, 0.65f));
        Material revealMaterial = CreateParticleMaterial("M_RevealBurst", new Color(1f, 0.82f, 0.28f, 0.85f));
        Material lightningMaterial = CreateParticleMaterial("M_LightningBurst", new Color(0.6f, 0.9f, 1f, 0.9f));
        Material dustMaterial = CreateParticleMaterial("M_PurpleDust", new Color(0.45f, 0.25f, 0.75f, 0.35f));

        CreateParticlePrefab("ReadyAura", true, 1.6f, 0.22f, 18, new Vector3(0f, 0.35f, 0f), readyMaterial, new Color(0.65f, 0.45f, 1f, 0.65f));
        CreateParticlePrefab("RevealBurst", false, 0.5f, 1.4f, 35, Vector3.zero, revealMaterial, new Color(1f, 0.82f, 0.28f, 0.85f));
        CreateParticlePrefab("LightningBurst", false, 0.2f, 2.2f, 18, Vector3.zero, lightningMaterial, new Color(0.6f, 0.9f, 1f, 0.95f));
        CreateParticlePrefab("PurpleDust", true, 2.4f, 0.18f, 22, new Vector3(-0.15f, 0.08f, 0f), dustMaterial, new Color(0.45f, 0.25f, 0.75f, 0.35f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Material CreateParticleMaterial(string name, Color tint)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.name = name;
        material.color = tint;

        string path = MaterialFolder + "/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(material, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void CreateParticlePrefab(string name, bool loop, float lifetime, float speed, int burstCount, Vector3 drift, Material material, Color color)
    {
        GameObject root = new GameObject(name);
        ParticleSystem particleSystem = root.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = loop;
        main.duration = Mathf.Max(0.1f, lifetime);
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = loop ? 0.12f : 0.22f;
        main.startColor = color;
        main.maxParticles = Mathf.Max(16, burstCount * 2);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = loop ? 8f : 0f;
        if (!loop)
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = loop ? 0.6f : 0.15f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = drift.x;
        velocity.y = drift.y;
        velocity.z = drift.z;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        string path = PrefabFolder + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            AssetDatabase.DeleteAsset(path);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
}
