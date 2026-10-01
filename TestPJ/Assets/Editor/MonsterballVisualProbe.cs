#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yeolha.BeltScroll;

[InitializeOnLoad]
internal static class MonsterballVisualProbe
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(Application.dataPath, "../Temp/MonsterballProbe"));
    private static readonly string Request = Path.Combine(Root, "run.request");
    private static readonly float[] Samples = { 0.4f, 1f, 2f, 3f, 4f, 5f, 6f, 8f, 10f, 12f };
    private static readonly StringBuilder Log = new StringBuilder();
    private static bool started;
    private static float startTime;
    private static int sampleIndex;
    private static MonsterballRedController monster;
    private static ParticleSystem hornFire;

    static MonsterballVisualProbe()
    {
        EditorApplication.delayCall += BeginIfRequested;
        EditorApplication.update += BeginIfRequested;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void BeginIfRequested()
    {
        if (!File.Exists(Request)) return;
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
        {
            File.WriteAllText(Path.Combine(Root, "result.txt"),
                "SampleScene is not the active scene; probe did not enter Play Mode.");
            File.Delete(Request);
            return;
        }
        EditorApplication.update -= BeginIfRequested;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.update -= Tick;
    }

    private static void Tick()
    {
        if (!File.Exists(Request) || !EditorApplication.isPlaying) return;
        if (!started)
        {
            monster = Object.FindFirstObjectByType<MonsterballRedController>();
            Character player = null;
            foreach (Character character in Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
                if (character.Faction == FactionType.Player) { player = character; break; }
            if (monster == null || player == null)
            {
                Finish("Monsterball or player was not found in Play Mode.");
                return;
            }
            Transform fireTransform = monster.transform.Find("VFX/HornFire/Flame");
            if (fireTransform == null) fireTransform = monster.transform.Find("Horn Flames");
            hornFire = fireTransform != null ? fireTransform.GetComponent<ParticleSystem>() : null;
            Log.AppendLine("Play Mode probe; player moved 5.5 m left of Monsterball at runtime.");
            started = true;
            startTime = Time.realtimeSinceStartup;
        }

        float elapsed = Time.realtimeSinceStartup - startTime;
        if (sampleIndex < Samples.Length && elapsed >= Samples[sampleIndex])
        {
            string imagePath = Path.Combine(Root, $"frame_{sampleIndex:00}.png");
            ScreenCapture.CaptureScreenshot(imagePath);
            int particleCount = hornFire != null ? hornFire.particleCount : -1;
            Log.AppendLine($"{elapsed:0.00}s state={monster.CurrentState} " +
                $"root={monster.transform.position} hornParticles={particleCount} " +
                $"camera={(Camera.main != null ? Camera.main.transform.position.ToString() : "missing")}");
            File.WriteAllText(Path.Combine(Root, "result.txt"), Log.ToString());
            sampleIndex++;
        }
        if (sampleIndex >= Samples.Length || elapsed > 16f)
            Finish(elapsed > 16f ? "Timed out." : "Completed.");
    }

    private static void Finish(string message)
    {
        Log.AppendLine(message);
        File.WriteAllText(Path.Combine(Root, "result.txt"), Log.ToString());
        File.Delete(Request);
        EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(0);
        else EditorApplication.isPlaying = false;
    }
}
#endif
