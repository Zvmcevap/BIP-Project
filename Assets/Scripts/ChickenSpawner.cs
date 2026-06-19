using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.XR.ARFoundation;

public class ChickenSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Assign chicken prefab here. A yellow capsule placeholder is used until the real asset arrives.")]
    [SerializeField] private GameObject chickenPrefab;
    [SerializeField] private GameObject roosterPrefab;

    [Header("AR References")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private PlayerStats playerStats;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private int maxActiveChickens = 5;
    [SerializeField] private int maxActiveRoosters = 1;

    [SerializeField] private int chickenPoints = 10;
    [SerializeField, Range(0.0001f, 0.01f)] private float chickenScale = 0.1f;

    [Header("Speed Scaling")]
    [Tooltip("Speed boost added per catch (0.15 = +15% per catch, reaches 5x at ~27 catches).")]
    [SerializeField, Range(0.05f, 0.5f)] private float speedIncrementPerCatch = 0.15f;
    [Tooltip("Maximum allowed speed multiplier.")]
    [SerializeField] private float maxSpeedMultiplier = 5f;

    [Header("Catch Effects")]
    [SerializeField] private AudioClip chickenCatchSound;
    [SerializeField] private AudioClip roosterCatchSound;

    [Header("Smoke Effect")]
    [SerializeField, Range(1f, 30f)] private float smokeSize = 12f;
    [SerializeField, Range(10f, 100f)] private float smokeSpeed = 50f;
    [SerializeField, Range(4, 24)] private int smokeCount = 12;
    [SerializeField, Range(0.2f, 2f)] private float smokeLifetime = 0.6f;
    [SerializeField, Range(0f, 3f)] private float smokeCloudRadius = 1f;

    [Header("Stars Effect")]
    [SerializeField, Range(1f, 15f)] private float starsSize = 4f;
    [SerializeField, Range(20f, 200f)] private float starsSpeed = 90f;
    [SerializeField, Range(2, 20)] private int starsCount = 8;
    [SerializeField, Range(0.1f, 1f)] private float starsLifetime = 0.4f;

    // Raised after each successful catch; arg is the total catch count this session.
    public event Action<int> ChickenCaught;
    public int TotalCatches { get; set; }
    private float spawnTimer;
    private readonly List<ChickenBehaviour> activeChickens = new();
    private readonly List<ChickenBehaviour> activeRoosters = new();
    private string debugStatus = "Initialising...";
    private AudioSource audioSource;

    private float CurrentSpeedMultiplier =>
        Mathf.Min(1f + TotalCatches * speedIncrementPerCatch, maxSpeedMultiplier);

    private void Awake()
    {
        if (arCamera == null)
            arCamera = Camera.main;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        HandleInput();
        HandleSpawning();
    }

    private void HandleSpawning()
    {
        activeChickens.RemoveAll(c => c == null);

        if (activeChickens.Count >= maxActiveChickens)
            return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
            return;

        spawnTimer = spawnInterval;
        TrySpawnChicken();
    }
    
    private void TrySpawnChicken()
    {
        if (planeManager == null)
        {
            debugStatus = "ERROR: Plane Manager not assigned in Inspector!";
            return;
        }

        var planes = new List<ARPlane>();
        foreach (var p in planeManager.trackables)
            planes.Add(p);

        if (planes.Count == 0)
        {
            debugStatus = "Waiting for AR to detect a flat surface...\nPoint camera at a table or floor and move slowly.";
            return;
        }

        bool spawnRooster = activeRoosters.Count < maxActiveRoosters;

        var plane = planes[UnityEngine.Random.Range(0, planes.Count)];
        var position = GetRandomPositionOnPlane(plane);
        GameObject chickenObj;
        if (spawnRooster)
        {
            chickenObj = roosterPrefab != null
                ? Instantiate(roosterPrefab, position, Quaternion.identity)
                : CreatePlaceholderChicken(position);
        }
        else
        {
            chickenObj = chickenPrefab != null
                ? Instantiate(chickenPrefab, position, Quaternion.identity)
                : CreatePlaceholderChicken(position);
        }

        chickenObj.transform.localScale = Vector3.one * chickenScale;
        if (!spawnRooster)
        {
            chickenObj.transform.localScale *= 0.4f;
        }

        var chicken = chickenObj.GetComponent<ChickenBehaviour>();
        if (chicken == null)
            chicken = chickenObj.AddComponent<ChickenBehaviour>();

        chicken.IsRooster = spawnRooster;
        if (chickenObj.GetComponentInChildren<Collider>() == null)
        {
            var col = chickenObj.AddComponent<SphereCollider>();
            col.radius = 0.5f / chickenScale;
        }

        var id = $"chicken_{Guid.NewGuid():N}"[..16];
        if (spawnRooster)
        {
            chicken.Configure(id, chickenPoints, true, plane.normal);
            activeRoosters.Add(chicken);
        }
        else
        {
            chicken.Configure(id, chickenPoints, false, plane.normal);
            activeChickens.Add(chicken);
        }
        chicken.SpeedMultiplier = CurrentSpeedMultiplier;

        debugStatus = $"Spawned! Planes: {planes.Count} | Chickens: {activeChickens.Count} | Speed: {CurrentSpeedMultiplier:F1}x";
    }

    private void HandleInput()
    {
        if (!TryGetPressPosition(out var screenPos))
            return;

        if (arCamera == null)
            return;

        var ray = arCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, ~0, QueryTriggerInteraction.Collide))
            return;

        var chicken = hit.collider.GetComponentInParent<ChickenBehaviour>();
        if (chicken == null)
            return;

        CatchChicken(chicken);
    }

    private void CatchChicken(ChickenBehaviour chicken)
    {
        if (chicken.IsRooster)
        {
            if (!activeRoosters.Contains(chicken))
                return;
            activeRoosters.Remove(chicken);
        }
        else
        {
            if (!activeChickens.Contains(chicken))
                return;
            activeChickens.Remove(chicken);
        }

        TotalCatches++;

        var multiplier = CurrentSpeedMultiplier;
        foreach (var c in activeChickens)
        {
            if (c != null)
                c.SpeedMultiplier = multiplier;
        }

        if (playerStats != null)
            playerStats.Collect(chicken.ChickenId, chicken.IsRooster);

        SpawnFeatherBurst(chicken.transform.position);

        if (audioSource != null)
        {
            if (chicken.IsRooster && roosterCatchSound != null)
            {
                audioSource.PlayOneShot(roosterCatchSound);
            }
            else if (!chicken.IsRooster && chickenCatchSound != null)
            {
                audioSource.PlayOneShot(chickenCatchSound);
            }
        }

        Destroy(chicken.gameObject);
        ChickenCaught?.Invoke(TotalCatches);

        Debug.Log($"ChickenSpawner: catch #{TotalCatches}, speed now {multiplier:F2}x.");
    }

    public void SpawnFeatherBurst(Vector3 position)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default"));

        var root = new GameObject("CartoonFightBurst");
        root.transform.position = position;

        // Layer 1: big white puffy smoke clouds
        AddSmokeLayer(root, mat);

        // Layer 2: yellow stars shooting outward
        AddStarsLayer(root, mat);

        Destroy(root, 1.5f);
    }

    private void AddSmokeLayer(GameObject root, Material mat)
    {
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(smokeLifetime * 0.6f, smokeLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(chickenScale * smokeSpeed * 0.6f, chickenScale * smokeSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(chickenScale * smokeSize * 0.6f, chickenScale * smokeSize);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.85f, 0.85f, 0.85f));
        main.gravityModifier = -0.1f;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, smokeCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = chickenScale * smokeCloudRadius;

        // Puffs grow as they move outward
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var growCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 1.4f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, growCurve);

        // Fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.sortingOrder = 10;

        ps.Play();
    }

    private void AddStarsLayer(GameObject root, Material mat)
    {
        var starsGO = new GameObject("Stars");
        starsGO.transform.SetParent(root.transform, false);

        var ps = starsGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(starsLifetime * 0.6f, starsLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(chickenScale * starsSpeed * 0.6f, chickenScale * starsSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(chickenScale * starsSize * 0.6f, chickenScale * starsSize);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.2f), new Color(1f, 0.6f, 0.1f));
        main.gravityModifier = 0.2f;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, starsCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = chickenScale * 0.2f;

        // Stars shrink as they fly out
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var shrinkCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 0.8f),
            new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, shrinkCurve);

        // Fade out at the end
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 1f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = starsGO.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.sortingOrder = 11;

        ps.Play();
    }

    private static Vector3 GetRandomPositionOnPlane(ARPlane plane)
    {
        var halfX = plane.size.x / 2f;
        var halfZ = plane.size.y / 2f;
        var local = new Vector3(
            UnityEngine.Random.Range(-halfX, halfX),
            0f,
            UnityEngine.Random.Range(-halfZ, halfZ));
        return plane.transform.TransformPoint(local);
    }

    private static GameObject CreatePlaceholderChicken(Vector3 position)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        obj.name = "Chicken (Placeholder)";
        obj.transform.position = position;

        var renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            var color = new Color(1f, 0.85f, 0.1f);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            renderer.sharedMaterial = mat;
        }

        return obj;
    }

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        GUI.Box(new Rect(16, Screen.height - 160, Screen.width - 32, 148), $"ChickenSpawner:\n{debugStatus}", style);
    }

    private static bool TryGetPressPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                screenPosition = touch.position.ReadValue();
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }
}