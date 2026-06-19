using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlaceObjectOnPlane : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private GameObject prefabToPlace;
    [SerializeField] private bool alignVisualBottomToPlane = true;
    [SerializeField] private bool addBlobShadow = true;
    [SerializeField] private float shadowSize = 0.65f;
    [SerializeField, Range(0f, 1f)] private float shadowOpacity = 0.68f;

    private static readonly List<ARRaycastHit> Hits = new();
    private static Mesh shadowMesh;
    private static Material shadowMaterial;
    private bool warnedMissingPrefab;
    private string status = "Tap a detected plane to place object.";

    void Update()
    {
        if (!TryGetPressPosition(out var screenPosition)) return;

        if (prefabToPlace == null)
        {
            if (!warnedMissingPrefab)
            {
                warnedMissingPrefab = true;
                status = "Missing prefabToPlace.";
                Debug.LogWarning("PlaceObjectOnPlane: prefabToPlace is empty. Assign Security Keypad in the Inspector.");
            }

            return;
        }

        var trackableTypes = TrackableType.PlaneWithinPolygon
            | TrackableType.PlaneWithinBounds
            | TrackableType.PlaneEstimated;

        if (raycastManager.Raycast(screenPosition, Hits, trackableTypes))
        {
            Pose hitPose = Hits[0].pose;
            var placedObject = Instantiate(prefabToPlace, hitPose.position, hitPose.rotation);

            if (alignVisualBottomToPlane)
            {
                AlignVisualBottomToPose(placedObject, hitPose);
            }

            status = $"Placed {prefabToPlace.name}.";
            Debug.Log($"PlaceObjectOnPlane: {status}");

            if (addBlobShadow)
            {
                CreateBlobShadow(hitPose);
            }
        }
        else
        {
            status = "No plane hit. Move phone around, then tap table/floor.";
            Debug.Log($"PlaceObjectOnPlane: {status}");
        }
    }

    private void AlignVisualBottomToPose(GameObject placedObject, Pose hitPose)
    {
        var renderers = placedObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        var bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        placedObject.transform.position += hitPose.position - bottomCenter;
    }

/*    private void OnGUI()
    {
        if (!showDebugText)
        {
            return;
        }

        var rect = new Rect(24f, 140f, Screen.width - 48f, 90f);
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        GUI.Label(rect, $"PlaceObjectOnPlane: {status}", style);
    }*/

    private void CreateBlobShadow(Pose hitPose)
    {
        var shadow = new GameObject("Blob Shadow");
        shadow.transform.SetPositionAndRotation(
            hitPose.position + Vector3.up * 0.01f,
            hitPose.rotation);
        shadow.transform.localScale = new Vector3(shadowSize, 1f, shadowSize);

        var meshFilter = shadow.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = GetShadowMesh();

        var meshRenderer = shadow.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetShadowMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private Material GetShadowMaterial()
    {
        if (shadowMaterial != null)
        {
            SetShadowColor(shadowMaterial, shadowOpacity);
            return shadowMaterial;
        }

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        shadowMaterial = new Material(shader);
        SetShadowColor(shadowMaterial, shadowOpacity);
        shadowMaterial.renderQueue = 3000;
        shadowMaterial.SetFloat("_Surface", 1f);
        shadowMaterial.SetFloat("_Blend", 0f);
        shadowMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        shadowMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        shadowMaterial.SetFloat("_ZWrite", 0f);
        shadowMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        shadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        return shadowMaterial;
    }

    private static void SetShadowColor(Material material, float opacity)
    {
        var color = new Color(0f, 0f, 0f, opacity);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static Mesh GetShadowMesh()
    {
        if (shadowMesh != null) return shadowMesh;

        const int segments = 48;
        var vertices = new Vector3[segments + 1];
        var triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i == segments - 1 ? 1 : i + 2;
        }

        shadowMesh = new Mesh
        {
            name = "Blob Shadow Mesh",
            vertices = vertices,
            triangles = triangles
        };
        shadowMesh.RecalculateNormals();

        return shadowMesh;
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
