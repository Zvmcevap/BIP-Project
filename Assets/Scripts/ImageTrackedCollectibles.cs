using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTrackedCollectibles : MonoBehaviour
{
    [Serializable]
    public class CollectibleConfig
    {
        private static readonly Vector3 DefaultWorldScale = new(0.15f, 0.15f, 0.15f);

        [Tooltip("Must match the image name from XR Reference Image Library.")]
        public string imageName;

        [Tooltip("Unique id used in PlayerPrefs, for example coffee_mug_coin.")]
        public string collectibleId;

        public GameObject prefab;
        public int points = 10;
        public Vector3 localPosition = new(0f, 0.05f, 0f);
        public Vector3 localEulerAngles = new(90f, 0f, 0f);
        public Vector3 worldScale = new(0.15f, 0.15f, 0.15f);

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(collectibleId))
            {
                collectibleId = imageName;
            }

            if (IsZeroScale(worldScale))
            {
                worldScale = DefaultWorldScale;
            }
        }

        private static bool IsZeroScale(Vector3 scale)
        {
            return Mathf.Approximately(scale.x, 0f)
                && Mathf.Approximately(scale.y, 0f)
                && Mathf.Approximately(scale.z, 0f);
        }
    }

    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private bool showDebugText = false;
    [SerializeField] private List<CollectibleConfig> collectibles = new();

    private readonly Dictionary<string, CollectibleConfig> configsByImageName = new();
    private readonly Dictionary<TrackableId, GameObject> spawnedObjectsByTrackableId = new();

    private string status = "Point camera at a collectible marker.";

    // Event raised whenever status text changes.
    // UIManager can subscribe and update TMP_Text.
    public event Action<string> StatusChanged;

    // Event raised when a collectible is successfully collected.
    // AudioManager can subscribe to play sounds.
    // Other systems, such as quests or achievements, can also react.
    public event Action<string> CollectibleCollected;

    private void Awake()
    {
        if (trackedImageManager == null)
        {
            trackedImageManager = GetComponent<ARTrackedImageManager>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
        }

        RebuildConfigLookup();
    }

    private void Start()
    {
        // Send initial status to UI after scene starts.
        SetStatus(status);
    }

    private void OnValidate()
    {
        for (int i = 0; i < collectibles.Count; i++)
        {
            var config = collectibles[i];

            if (config != null)
            {
                config.Normalize();
            }
        }
    }

    private void OnEnable()
    {
        if (trackedImageManager == null)
        {
            SetStatus("Missing ARTrackedImageManager.");
            Debug.LogError($"ImageTrackedCollectibles: {status}");
            return;
        }

        trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
    }

    private void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
        }
    }

    private void Update()
    {
    }

    private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var trackedImage in args.added)
        {
            UpdateTrackedImage(trackedImage);
        }

        foreach (var trackedImage in args.updated)
        {
            UpdateTrackedImage(trackedImage);
        }

        foreach (var removed in args.removed)
        {
            if (spawnedObjectsByTrackableId.TryGetValue(removed.Key, out var spawnedObject))
            {
                Destroy(spawnedObject);
                spawnedObjectsByTrackableId.Remove(removed.Key);
            }
        }
    }

    private void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        var imageName = trackedImage.referenceImage.name;

        if (!configsByImageName.TryGetValue(imageName, out var config))
        {
            SetStatus($"Saw '{imageName}', but no collectible config exists.");
            return;
        }

        if (playerStats != null && playerStats.IsCollected(config.collectibleId))
        {
            HideObject(trackedImage.trackableId);
            SetStatus($"'{imageName}' already collected.");
            return;
        }

        if (trackedImage.trackingState != TrackingState.Tracking)
        {
            HideObject(trackedImage.trackableId);
            SetStatus($"Saw '{imageName}', state: {trackedImage.trackingState}.");
            return;
        }

        var collectibleObject = GetOrCreateObject(trackedImage.trackableId, config, trackedImage.transform);

        collectibleObject.transform.SetParent(trackedImage.transform, false);
        collectibleObject.transform.localPosition = config.localPosition;
        collectibleObject.transform.localRotation = Quaternion.Euler(config.localEulerAngles);

        SetWorldScale(collectibleObject.transform, config.worldScale);

        collectibleObject.SetActive(true);

        SetStatus($"Tracking '{imageName}'. Tap collectible.");
    }

    private GameObject GetOrCreateObject(TrackableId trackableId, CollectibleConfig config, Transform parent)
    {
        if (spawnedObjectsByTrackableId.TryGetValue(trackableId, out var existingObject))
        {
            return existingObject;
        }

        var collectibleObject = config.prefab != null
            ? Instantiate(config.prefab, parent)
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        collectibleObject.name = $"AR Collectible - {config.collectibleId}";
        collectibleObject.transform.SetParent(parent, false);

        var instance = collectibleObject.GetComponent<ImageTrackedCollectibleInstance>();

        if (instance == null)
        {
            instance = collectibleObject.AddComponent<ImageTrackedCollectibleInstance>();
        }

        instance.Configure(config.collectibleId, config.points);

        if (collectibleObject.GetComponentInChildren<Collider>() == null)
        {
            var collider = collectibleObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
        }

        if (config.prefab == null)
        {
            ApplyFallbackMaterial(collectibleObject);
        }

        spawnedObjectsByTrackableId.Add(trackableId, collectibleObject);

        return collectibleObject;
    }

    private void HideObject(TrackableId trackableId)
    {
        if (spawnedObjectsByTrackableId.TryGetValue(trackableId, out var collectibleObject))
        {
            collectibleObject.SetActive(false);
        }
    }

    private void RebuildConfigLookup()
    {
        configsByImageName.Clear();

        foreach (var config in collectibles)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.imageName))
            {
                continue;
            }

            config.Normalize();

            configsByImageName[config.imageName] = config;
        }
    }

    /// <summary>
    /// Updates internal status and notifies all listeners.
    /// UIManager can subscribe to this event and refresh TMP_Text.
    /// </summary>
    private void SetStatus(string newStatus)
    {
        status = newStatus;
        StatusChanged?.Invoke(status);
    }

    private static void SetWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        var parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;

        target.localScale = new Vector3(
            SafeDivide(desiredWorldScale.x, parentScale.x),
            SafeDivide(desiredWorldScale.y, parentScale.y),
            SafeDivide(desiredWorldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
    }

    private static void ApplyFallbackMaterial(GameObject collectibleObject)
    {
        var renderer = collectibleObject.GetComponent<MeshRenderer>();

        if (renderer == null)
        {
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        var material = new Material(shader);
        var color = new Color(1f, 0.68f, 0.12f);

        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        renderer.sharedMaterial = material;
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

public class ImageTrackedCollectibleInstance : MonoBehaviour
{
    public string CollectibleId { get; private set; }
    public int Points { get; private set; }

    public void Configure(string collectibleId, int points)
    {
        CollectibleId = collectibleId;
        Points = points;
    }
}