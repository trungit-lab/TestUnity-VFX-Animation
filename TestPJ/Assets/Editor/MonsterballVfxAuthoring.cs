#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yeolha.BeltScroll;

[InitializeOnLoad]
public static class MonsterballVfxAuthoring
{
    private const string MaterialsDir = "Assets/Resources/Monsterball/Materials";
    private const string PrefabDir = "Assets/Resources/Monsterball";
    private const string PrefabPath = "Assets/Resources/Monsterball/Monsterball_VFX.prefab";
    private const string ResultPath = "Temp/MonsterballVfxAuthoring_Result.txt";

    static MonsterballVfxAuthoring()
    {
        EditorApplication.delayCall += RunAuthoringIfRequested;
        EditorApplication.update += RunAuthoringIfRequested;
    }

    [MenuItem("Monsterball/Author VFX Hierarchy and Materials")]
    public static void RunAuthoring()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("=== Monsterball VFX Authoring Started ===");

        try
        {
            if (!Directory.Exists(MaterialsDir))
            {
                Directory.CreateDirectory(MaterialsDir);
                AssetDatabase.Refresh();
            }

            // 1. Shaders & Textures
            Shader softShader = Shader.Find("Monsterball/SoftParticle");
            Shader flameShader = Shader.Find("Monsterball/AdditiveFlame");

            Texture2D smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/VFX Assets_Monster ball/UNI VFX/Common/Textures/WispySmoke03b_8x8.png");
            Texture2D flameTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/VFX Assets_Monster ball/UNI VFX/Characters & Artifacts/Textures/MediumFlame03.tif");
            Texture2D glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/VFX Assets_Monster ball/UNI VFX/Common/Textures/uni_glow.png");
            Texture2D sparkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/VFX Assets_Monster ball/UNI VFX/Common/Textures/uni_spark.png");
            Texture2D streakTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/VFX Assets_Monster ball/UNI VFX/Characters & Artifacts/Textures/uni_streak.png");

            log.AppendLine($"Shaders: soft={softShader != null}, flame={flameShader != null}");
            log.AppendLine($"Textures: smoke={smokeTex != null}, flame={flameTex != null}, glow={glowTex != null}, spark={sparkTex != null}, streak={streakTex != null}");

            // 2. Author Materials
            Material matDarkSmoke = GetOrCreateMaterial($"{MaterialsDir}/M_Monsterball_DarkSmoke.mat", softShader, smokeTex);
            Material matWhiteSmoke = GetOrCreateMaterial($"{MaterialsDir}/M_Monsterball_WhiteSmoke.mat", softShader, smokeTex);
            Material matFlame = GetOrCreateMaterial($"{MaterialsDir}/M_Monsterball_Flame.mat", flameShader != null ? flameShader : softShader, flameTex);
            Material matGlow = GetOrCreateMaterial($"{MaterialsDir}/M_Monsterball_Glow.mat", softShader, glowTex);
            Material matSpark = GetOrCreateMaterial($"{MaterialsDir}/M_Monsterball_Spark.mat", softShader, sparkTex);
            Material matScuff = GetOrCreateMaterial($"{MaterialsDir}/M_Monsterball_Scuff.mat", softShader, streakTex);
            Material matDebris = GetOrCreateMaterial($"{MaterialsDir}/M_Monsterball_Debris.mat", softShader, null);

            AssetDatabase.SaveAssets();
            log.AppendLine("Materials authored and saved.");

            // 3. Build VFX Hierarchy
            GameObject vfxRoot = new GameObject("VFX");

            // HornFire
            GameObject hornFireGroup = new GameObject("HornFire");
            hornFireGroup.transform.SetParent(vfxRoot.transform, false);
            ParticleSystem psHornFlame = SetupParticleSystem(hornFireGroup, "Flame", matFlame, 12, 6, 400);
            ConfigureHornFlame(psHornFlame);
            ParticleSystem psHornEmbers = SetupParticleSystem(hornFireGroup, "Embers", matSpark, 1, 4, 100);
            ConfigureHornEmbers(psHornEmbers);

            // MoveTrail
            GameObject moveTrailGroup = new GameObject("MoveTrail");
            moveTrailGroup.transform.SetParent(vfxRoot.transform, false);
            ParticleSystem psDarkSmoke = SetupParticleSystem(moveTrailGroup, "DarkSmoke", matDarkSmoke, 8, 8, 250);
            ConfigureDarkSmoke(psDarkSmoke);
            ParticleSystem psWhiteSmoke = SetupParticleSystem(moveTrailGroup, "WhiteSmoke", matWhiteSmoke, 8, 8, 150);
            ConfigureWhiteSmoke(psWhiteSmoke);
            ParticleSystem psMoveSparks = SetupParticleSystem(moveTrailGroup, "Sparks", matSpark, 1, 4, 100);
            ConfigureTrailSparks(psMoveSparks);

            // Impact
            GameObject impactGroup = new GameObject("Impact");
            impactGroup.transform.SetParent(vfxRoot.transform, false);
            ParticleSystem psImpactFlash = SetupParticleSystem(impactGroup, "Flash", matGlow, 1, 1, 60);
            ConfigureImpactFlash(psImpactFlash);
            ParticleSystem psImpactFlame = SetupParticleSystem(impactGroup, "Flame", matFlame, 12, 6, 100);
            ConfigureImpactFlame(psImpactFlame);
            ParticleSystem psImpactSparks = SetupParticleSystem(impactGroup, "Sparks", matSpark, 1, 4, 150);
            ConfigureImpactSparks(psImpactSparks);
            ParticleSystem psImpactSmoke = SetupParticleSystem(impactGroup, "Smoke", matDarkSmoke, 8, 8, 60);
            ConfigureImpactSmoke(psImpactSmoke);

            // GroundBurst
            GameObject groundBurstGroup = new GameObject("GroundBurst");
            groundBurstGroup.transform.SetParent(vfxRoot.transform, false);
            ParticleSystem psGroundDust = SetupParticleSystem(groundBurstGroup, "Dust", matDarkSmoke, 8, 8, 120);
            ConfigureGroundDust(psGroundDust);
            ParticleSystem psGroundDebris = SetupParticleSystem(groundBurstGroup, "Debris", matDebris, 1, 1, 100);
            ConfigureGroundDebris(psGroundDebris);
            ParticleSystem psGroundScuff = SetupParticleSystem(groundBurstGroup, "GroundScuff", matScuff, 1, 1, 100, ParticleSystemRenderMode.HorizontalBillboard);
            ConfigureGroundScuff(psGroundScuff);

            // Death
            GameObject deathGroup = new GameObject("Death");
            deathGroup.transform.SetParent(vfxRoot.transform, false);
            ParticleSystem psDeathSmoke = SetupParticleSystem(deathGroup, "Smoke", matDarkSmoke, 8, 8, 120);
            ConfigureDeathSmoke(psDeathSmoke);
            ParticleSystem psDeathFlame = SetupParticleSystem(deathGroup, "Flame", matFlame, 12, 6, 100);
            ConfigureDeathFlame(psDeathFlame);
            ParticleSystem psDeathGlow = SetupParticleSystem(deathGroup, "Glow", matGlow, 1, 1, 60);
            ConfigureDeathGlow(psDeathGlow);
            ParticleSystem psDeathSparks = SetupParticleSystem(deathGroup, "Sparks", matSpark, 1, 4, 150);
            ConfigureDeathSparks(psDeathSparks);

            log.AppendLine("VFX hierarchy and ParticleSystems constructed.");

            // 4. Save Prefab
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(vfxRoot, PrefabPath);
            log.AppendLine($"Saved prefab to {PrefabPath}");

            // 5. Update SampleScene
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity")
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            }

            GameObject monsterBall = GameObject.Find("MonsterBall_red");
            if (monsterBall == null)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name.Contains("MonsterBall")) { monsterBall = root; break; }
                }
            }

            if (monsterBall != null)
            {
                // Remove existing VFX or runtime particle children
                Transform oldVfx = monsterBall.transform.Find("VFX");
                if (oldVfx != null) Object.DestroyImmediate(oldVfx.gameObject);

                string[] obsoleteNames = { "Dark Smoke Plume", "White Motion Smoke", "Embers and Impact Glow",
                    "Hit Sparks", "Earth Fragments", "Ground Scuffs", "Horn Flames", "Impact Flames" };
                foreach (string obs in obsoleteNames)
                {
                    Transform t = monsterBall.transform.Find(obs);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }

                // Instantiate prefab under monsterBall
                GameObject instanceVfx = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, monsterBall.transform);
                instanceVfx.name = "VFX";
                instanceVfx.transform.localPosition = Vector3.zero;
                instanceVfx.transform.localRotation = Quaternion.identity;
                instanceVfx.transform.localScale = Vector3.one;

                MonsterballEffects effects = monsterBall.GetComponent<MonsterballEffects>();
                if (effects == null) effects = monsterBall.AddComponent<MonsterballEffects>();

                // Bind serialized properties
                SerializedObject so = new SerializedObject(effects);
                so.Update();
                so.FindProperty("hornFire").objectReferenceValue = instanceVfx.transform.Find("HornFire/Flame")?.GetComponent<ParticleSystem>();
                so.FindProperty("hornEmbers").objectReferenceValue = instanceVfx.transform.Find("HornFire/Embers")?.GetComponent<ParticleSystem>();
                so.FindProperty("darkSmoke").objectReferenceValue = instanceVfx.transform.Find("MoveTrail/DarkSmoke")?.GetComponent<ParticleSystem>();
                so.FindProperty("whiteSmoke").objectReferenceValue = instanceVfx.transform.Find("MoveTrail/WhiteSmoke")?.GetComponent<ParticleSystem>();
                so.FindProperty("moveTrailSparks").objectReferenceValue = instanceVfx.transform.Find("MoveTrail/Sparks")?.GetComponent<ParticleSystem>();
                so.FindProperty("impactFlash").objectReferenceValue = instanceVfx.transform.Find("Impact/Flash")?.GetComponent<ParticleSystem>();
                so.FindProperty("impactFlame").objectReferenceValue = instanceVfx.transform.Find("Impact/Flame")?.GetComponent<ParticleSystem>();
                so.FindProperty("impactSparks").objectReferenceValue = instanceVfx.transform.Find("Impact/Sparks")?.GetComponent<ParticleSystem>();
                so.FindProperty("impactSmoke").objectReferenceValue = instanceVfx.transform.Find("Impact/Smoke")?.GetComponent<ParticleSystem>();
                so.FindProperty("groundBurstDust").objectReferenceValue = instanceVfx.transform.Find("GroundBurst/Dust")?.GetComponent<ParticleSystem>();
                so.FindProperty("groundBurstDebris").objectReferenceValue = instanceVfx.transform.Find("GroundBurst/Debris")?.GetComponent<ParticleSystem>();
                so.FindProperty("groundBurstScuff").objectReferenceValue = instanceVfx.transform.Find("GroundBurst/GroundScuff")?.GetComponent<ParticleSystem>();
                so.FindProperty("deathSmoke").objectReferenceValue = instanceVfx.transform.Find("Death/Smoke")?.GetComponent<ParticleSystem>();
                so.FindProperty("deathFlame").objectReferenceValue = instanceVfx.transform.Find("Death/Flame")?.GetComponent<ParticleSystem>();
                so.FindProperty("deathGlow").objectReferenceValue = instanceVfx.transform.Find("Death/Glow")?.GetComponent<ParticleSystem>();
                so.FindProperty("deathSparks").objectReferenceValue = instanceVfx.transform.Find("Death/Sparks")?.GetComponent<ParticleSystem>();
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(monsterBall);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("SampleScene MonsterBall_red updated and saved successfully.");
            }
            else
            {
                log.AppendLine("WARNING: MonsterBall_red not found in active scene.");
            }

            Object.DestroyImmediate(vfxRoot);
            log.AppendLine("=== Authoring Finished Successfully ===");
        }
        catch (System.Exception ex)
        {
            log.AppendLine($"ERROR during authoring: {ex}");
        }

        File.WriteAllText(ResultPath, log.ToString());
        Debug.Log(log.ToString());
    }

    [MenuItem("Monsterball/Tune Existing VFX to Guide")]
    public static void TuneExistingPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ConfigureHornFlame(root.transform.Find("HornFire/Flame").GetComponent<ParticleSystem>());
            ConfigureHornEmbers(root.transform.Find("HornFire/Embers").GetComponent<ParticleSystem>());
            ConfigureDarkSmoke(root.transform.Find("MoveTrail/DarkSmoke").GetComponent<ParticleSystem>());
            ConfigureWhiteSmoke(root.transform.Find("MoveTrail/WhiteSmoke").GetComponent<ParticleSystem>());
            ConfigureTrailSparks(root.transform.Find("MoveTrail/Sparks").GetComponent<ParticleSystem>());
            ConfigureImpactFlash(root.transform.Find("Impact/Flash").GetComponent<ParticleSystem>());
            ConfigureImpactFlame(root.transform.Find("Impact/Flame").GetComponent<ParticleSystem>());
            ConfigureImpactSparks(root.transform.Find("Impact/Sparks").GetComponent<ParticleSystem>());
            ConfigureImpactSmoke(root.transform.Find("Impact/Smoke").GetComponent<ParticleSystem>());
            ConfigureGroundDust(root.transform.Find("GroundBurst/Dust").GetComponent<ParticleSystem>());
            ConfigureGroundDebris(root.transform.Find("GroundBurst/Debris").GetComponent<ParticleSystem>());
            ConfigureGroundScuff(root.transform.Find("GroundBurst/GroundScuff").GetComponent<ParticleSystem>());
            ConfigureDeathSmoke(root.transform.Find("Death/Smoke").GetComponent<ParticleSystem>());
            ConfigureDeathFlame(root.transform.Find("Death/Flame").GetComponent<ParticleSystem>());
            ConfigureDeathGlow(root.transform.Find("Death/Glow").GetComponent<ParticleSystem>());
            ConfigureDeathSparks(root.transform.Find("Death/Sparks").GetComponent<ParticleSystem>());
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>())
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Material material = renderer.sharedMaterial;
                if (material != null && material.HasProperty("_LuminanceAlpha"))
                {
                    material.SetFloat("_LuminanceAlpha", material.name.Contains("Smoke") ? 0f : 1f);
                    EditorUtility.SetDirty(material);
                }
                if (material != null && material.name.Contains("Spark"))
                {
                    var sheet = ps.textureSheetAnimation;
                    sheet.numTilesX = 1;
                    sheet.numTilesY = 4;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Monsterball guide tuning saved to existing prefab; scene bindings preserved.");
    }

    private static void RunAuthoringIfRequested()
    {
        string requestPath = "Temp/MonsterballVfxAuthoring.request";
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
            RunAuthoring();
        }
    }

    private static Material GetOrCreateMaterial(string path, Shader shader, Texture texture)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }
        mat.mainTexture = texture;
        if (mat.HasProperty("_LuminanceAlpha"))
            mat.SetFloat("_LuminanceAlpha", path.Contains("Smoke") ? 0f : 1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static ParticleSystem SetupParticleSystem(GameObject parent, string name, Material mat,
        int tilesX, int tilesY, int maxParticles,
        ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        if (tilesX > 1 || tilesY > 1)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = tilesX;
            sheet.numTilesY = tilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        renderer.sharedMaterial = mat;

        return ps;
    }

    private static void ConfigureHornFlame(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.72f, 0.4f, 0.95f),
            new Color(1f, 0.92f, 0.72f, 1f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(0.4f, 1.15f), new Keyframe(1f, 0.8f)));
    }

    private static void ConfigureHornEmbers(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startColor = new Color(1f, 0.75f, 0.3f, 0.85f);
        main.gravityModifier = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureDarkSmoke(ParticleSystem ps)
    {
        // Dark Smoke Plume / Trail:
        // Simulation Space = World
        // Expands gradually, fades smoothly, irregular/noisy silhouette (Noise module), alpha-blended transparent material
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startColor = new Color(0.09f, 0.08f, 0.11f, 0.75f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(1f, 2.0f)));

        var noise = ps.noise;
        noise.enabled = true;
        noise.frequency = 0.5f;
        noise.strength = 0.22f;
        noise.scrollSpeed = 0.2f;
        noise.octaveCount = 2;
    }

    private static void ConfigureWhiteSmoke(ParticleSystem ps)
    {
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.05f);
        main.startColor = new Color(0.9f, 0.87f, 0.88f, 0.32f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(1f, 1.35f)));

        var noise = ps.noise;
        noise.enabled = true;
        noise.frequency = 0.5f;
        noise.strength = 0.2f;
    }

    private static void ConfigureTrailSparks(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
        main.startColor = new Color(1f, 0.65f, 0.2f, 0.85f);
        main.gravityModifier = 0.6f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureImpactFlash(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.1f);
        main.startColor = new Color(1f, 0.8f, 0.5f, 0.9f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureImpactFlame(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.15f, 1.85f);
        main.startColor = new Color(1f, 0.78f, 0.45f, 0.95f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(1f, 1.25f)));
    }

    private static void ConfigureImpactSparks(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = new Color(1f, 0.85f, 0.55f, 1f);
        main.gravityModifier = 0.7f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureImpactSmoke(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startColor = new Color(0.18f, 0.16f, 0.18f, 0.55f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));
    }

    private static void ConfigureGroundDust(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.85f, 1.4f);
        main.startColor = new Color(0.11f, 0.09f, 0.12f, 0.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(1f, 1.4f)));
    }

    private static void ConfigureGroundDebris(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new Color(0.16f, 0.13f, 0.14f, 0.95f);
        main.gravityModifier = 0.9f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureGroundScuff(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.85f, 1.5f);
        main.startColor = new Color(0.1f, 0.09f, 0.11f, 0.55f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureDeathSmoke(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1f, 1.65f);
        main.startColor = new Color(0.2f, 0.18f, 0.2f, 0.75f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.8f), new Keyframe(1f, 1.8f)));

        var noise = ps.noise;
        noise.enabled = true;
        noise.frequency = 0.5f;
        noise.strength = 0.4f;
    }

    private static void ConfigureDeathFlame(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
        main.startColor = new Color(1f, 0.45f, 0.1f, 0.9f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.8f), new Keyframe(1f, 1.3f)));
    }

    private static void ConfigureDeathGlow(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.3f);
        main.startColor = new Color(1f, 0.35f, 0.05f, 0.85f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void ConfigureDeathSparks(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.18f);
        main.startColor = new Color(1f, 0.8f, 0.3f, 1f);
        main.gravityModifier = 0.6f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }
}
#endif
