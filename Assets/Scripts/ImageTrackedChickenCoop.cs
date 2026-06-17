using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTrackedChickenCoop : MonoBehaviour
{
    [Serializable]
    public class ChickenCoopConfig
    {
        private static readonly Vector3 DefaultWorldScale = new(0.15f, 0.15f, 0.15f);

        [Tooltip("Must match the image name from the XR Reference Image Library.")]
        public string imageName;

        [Tooltip("Prefab that will appear on top of the tracked image.")]
        public GameObject prefab;

        public Vector3 localPosition = new(0f, 0.05f, 0f);
        public Vector3 localEulerAngles = new(90f, 0f, 0f);
        public Vector3 worldScale = new(0.15f, 0.15f, 0.15f);

        public void Normalize()
        {
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

    [Header("References")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private Camera arCamera;

    [Header("Chicken Coop Settings")]
    [SerializeField] private List<ChickenCoopConfig> chickenCoops = new();

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [SerializeField] PlayerStats playerStats;
    private readonly Dictionary<string, ChickenCoopConfig> configsByImageName = new();
    private readonly Dictionary<TrackableId, GameObject> spawnedObjectsByTrackableId = new();

    private string status = "Point camera at the chicken coop marker.";

    private bool _chickenCoopInSight = false;
    private Vector3 coopPosition;
    public bool ChickenCoopInSight
    {
        get { return _chickenCoopInSight; }
        private set
        {
            _chickenCoopInSight = value;
            if (_chickenCoopInSight)
            {
                playerStats.PutChickensInCoop(coopPosition);
            }
        }
    }
    public bool ChickenCoopWasClicked { get; private set; }

    public event Action<string> StatusChanged;
    public event Action GameObjectClicked;

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
        SetStatus(status);
    }

    private void OnValidate()
    {
        for (int i = 0; i < chickenCoops.Count; i++)
        {
            var config = chickenCoops[i];

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
            Debug.LogError("ImageTrackedChickenCoop: Missing ARTrackedImageManager.");
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
        if (TryGetPressPosition(out var screenPosition))
        {
            // TryClickAtScreenPosition(screenPosition);
        }
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
            HideObject(removed.Key);

            ChickenCoopInSight = false;
            SetStatus("Chicken Coop not in sight.");
        }
    }

    private void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        var imageName = trackedImage.referenceImage.name;

        if (!configsByImageName.TryGetValue(imageName, out var config))
        {
            ChickenCoopInSight = false;
            SetStatus($"Saw '{imageName}', but no chicken coop config exists.");
            return;
        }

        if (trackedImage.trackingState != TrackingState.Tracking)
        {
            HideObject(trackedImage.trackableId);

            ChickenCoopInSight = false;
            SetStatus($"Chicken Coop not in sight. State: {trackedImage.trackingState}.");
            return;
        }

        ChickenCoopInSight = true;
        SetStatus("Chicken Coop in sight!");

        var coopObject = GetOrCreateObject(
            trackedImage.trackableId,
            config,
            trackedImage.transform
        );

        coopObject.transform.SetParent(trackedImage.transform, false);
        coopObject.transform.localPosition = config.localPosition;
        coopObject.transform.localRotation = Quaternion.Euler(config.localEulerAngles);

        SetWorldScale(coopObject.transform, config.worldScale);

        coopObject.SetActive(true);
    }

    private GameObject GetOrCreateObject(
        TrackableId trackableId,
        ChickenCoopConfig config,
        Transform parent
    )
    {
        if (spawnedObjectsByTrackableId.TryGetValue(trackableId, out var existingObject))
        {
            return existingObject;
        }

        GameObject coopObject;

        if (config.prefab != null)
        {
            coopObject = Instantiate(config.prefab, parent);
        }
        else
        {
            coopObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coopObject.transform.SetParent(parent, false);
            ApplyFallbackMaterial(coopObject);
        }

        if (coopObject.GetComponentInChildren<Collider>() == null)
        {
            var collider = coopObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
        }

        coopObject.name = $"AR Chicken Coop - {config.imageName}";

        var instance = coopObject.GetComponent<ImageTrackedChickenCoopInstance>();

        if (instance == null)
        {
            instance = coopObject.AddComponent<ImageTrackedChickenCoopInstance>();
        }

        instance.Configure(config.imageName);

        if (coopObject.GetComponentInChildren<Collider>() == null)
        {
            var collider = coopObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
        }

        spawnedObjectsByTrackableId.Add(trackableId, coopObject);
        coopPosition = coopObject.transform.position;

        return coopObject;
    }


    public void ResetClickedState()
    {
        ChickenCoopWasClicked = false;
    }

    private void HideObject(TrackableId trackableId)
    {
        if (spawnedObjectsByTrackableId.TryGetValue(trackableId, out var coopObject))
        {
            coopObject.SetActive(false);
        }
    }

    private void RebuildConfigLookup()
    {
        configsByImageName.Clear();

        foreach (var config in chickenCoops)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.imageName))
            {
                continue;
            }

            config.Normalize();

            configsByImageName[config.imageName] = config;
        }
    }

    private void SetStatus(string newStatus)
    {
        status = newStatus;
        StatusChanged?.Invoke(status);

        if (showDebugLogs)
        {
            Debug.Log(status);
        }
    }

    private static void SetWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        var parentScale = target.parent != null
            ? target.parent.lossyScale
            : Vector3.one;

        target.localScale = new Vector3(
            SafeDivide(desiredWorldScale.x, parentScale.x),
            SafeDivide(desiredWorldScale.y, parentScale.y),
            SafeDivide(desiredWorldScale.z, parentScale.z)
        );
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.0001f
            ? value
            : value / divisor;
    }

    private static void ApplyFallbackMaterial(GameObject coopObject)
    {
        var renderer = coopObject.GetComponent<MeshRenderer>();

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

public class ImageTrackedChickenCoopInstance : MonoBehaviour
{
    public string ImageName { get; private set; }

    public void Configure(string imageName)
    {
        ImageName = imageName;
    }
}