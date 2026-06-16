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

    [Header("AR References")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private PlayerStats playerStats;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private int maxActiveChickens = 5;
    [SerializeField] private int chickenPoints = 10;
    [SerializeField] private Vector3 chickenScale = new Vector3(0.15f, 0.15f, 0.15f);

    [Header("Speed Scaling")]
    [Tooltip("Speed boost added per catch (0.15 = +15% per catch, reaches 5x at ~27 catches).")]
    [SerializeField, Range(0.05f, 0.5f)] private float speedIncrementPerCatch = 0.15f;
    [Tooltip("Maximum allowed speed multiplier.")]
    [SerializeField] private float maxSpeedMultiplier = 5f;

    // Raised after each successful catch; arg is the total catch count this session.
    public event Action<int> ChickenCaught;

    private int totalCatches;
    private float spawnTimer;
    private readonly List<ChickenBehaviour> activeChickens = new();
    private string debugStatus = "Initialising...";

    private float CurrentSpeedMultiplier =>
        Mathf.Min(1f + totalCatches * speedIncrementPerCatch, maxSpeedMultiplier);

    private void Awake()
    {
        if (arCamera == null)
            arCamera = Camera.main;
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

        var plane = planes[UnityEngine.Random.Range(0, planes.Count)];
        var position = GetRandomPositionOnPlane(plane);

        var chickenObj = chickenPrefab != null
            ? Instantiate(chickenPrefab, position, Quaternion.identity)
            : CreatePlaceholderChicken(position);

        chickenObj.transform.localScale = chickenScale;

        var chicken = chickenObj.GetComponent<ChickenBehaviour>();
        if (chicken == null)
            chicken = chickenObj.AddComponent<ChickenBehaviour>();

        if (chickenObj.GetComponentInChildren<Collider>() == null)
        {
            var col = chickenObj.AddComponent<SphereCollider>();
            col.radius = 0.5f;
        }

        var id = $"chicken_{Guid.NewGuid():N}".Substring(0, 16);
        chicken.Configure(id, chickenPoints, plane.normal);
        chicken.SpeedMultiplier = CurrentSpeedMultiplier;

        activeChickens.Add(chicken);
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
        if (!activeChickens.Contains(chicken))
            return;

        activeChickens.Remove(chicken);
        totalCatches++;

        var multiplier = CurrentSpeedMultiplier;
        foreach (var c in activeChickens)
        {
            if (c != null)
                c.SpeedMultiplier = multiplier;
        }

        if (playerStats != null)
            playerStats.Collect(chicken.ChickenId, chicken.Points);

        Destroy(chicken.gameObject);
        ChickenCaught?.Invoke(totalCatches);

        Debug.Log($"ChickenSpawner: catch #{totalCatches}, speed now {multiplier:F2}x.");
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
