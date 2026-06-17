using UnityEngine;

public class ChickenBehaviour : MonoBehaviour
{
    [SerializeField] private float baseSpeed = 0.4f;
    [SerializeField] private float wanderRadius = 0.6f;
    [SerializeField] private float directionChangeInterval = 2.5f;

    public bool IsRooster { get; set; }

    public string ChickenId { get; private set; }
    public int Points { get; private set; } = 10;
    public float SpeedMultiplier { get; set; } = 1f;

    private Vector3 planeNormal;
    private Vector3 spawnPosition;
    private Vector3 wanderTarget;
    private float directionTimer;

    public void Configure(string id, int points, bool isRooster, Vector3 normal)
    {
        ChickenId = id;
        Points = points;
        IsRooster = isRooster;
        planeNormal = normal.normalized;
        spawnPosition = transform.position;
        PickNewWanderTarget();
    }

    private void Update()
    {
        directionTimer -= Time.deltaTime;

        if (directionTimer <= 0f || Vector3.Distance(transform.position, wanderTarget) < 0.05f)
            PickNewWanderTarget();

        var moveDir = (wanderTarget - transform.position).normalized;
        transform.position += moveDir * (baseSpeed * SpeedMultiplier) * Time.deltaTime;

        var flatDir = Vector3.ProjectOnPlane(moveDir, planeNormal);
        if (flatDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatDir, planeNormal);
    }

    private void PickNewWanderTarget()
    {
        directionTimer = Random.Range(directionChangeInterval * 0.5f, directionChangeInterval * 1.5f);

        var angle = Random.Range(0f, Mathf.PI * 2f);
        var radius = Random.Range(0.05f, wanderRadius);

        var tangent = GetPlaneTangent();
        var bitangent = Vector3.Cross(planeNormal, tangent).normalized;

        wanderTarget = spawnPosition
            + tangent * (Mathf.Cos(angle) * radius)
            + bitangent * (Mathf.Sin(angle) * radius);
    }

    private Vector3 GetPlaneTangent()
    {
        var t = Vector3.Cross(planeNormal, Vector3.forward);
        if (t.sqrMagnitude < 0.01f)
            t = Vector3.Cross(planeNormal, Vector3.right);
        return t.normalized;
    }
}