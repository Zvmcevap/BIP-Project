using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    private const string TotalPointsKey = "player_total_points";
    private const string CollectedCountKey = "player_collected_count";
    private const string RoostersInHandKey = "player_roosters_in_hand";
    private const string RoostersInCoopKey = "player_roosters_in_coop";

    [SerializeField] private Camera arCamera;
    [SerializeField] private ChickenSpawner cs;
    AudioSource audioSource;
    [SerializeField] AudioClip putInCoopSound;
    public int ChickensInHand { get; private set; }
    public int ChickensInCoop { get; private set; }

    public int RoostersInHand { get; private set; }
    public int RoostersInCoop { get; private set; }

    private bool isCountingChickens;

    public event Action StatsChanged;

    private void Awake()
    {
        Load();
        if (arCamera == null)
            arCamera = Camera.main;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void Load()
    {
        ChickensInHand = PlayerPrefs.GetInt(TotalPointsKey, 0);
        ChickensInCoop = PlayerPrefs.GetInt(CollectedCountKey, 0);
        RoostersInHand = PlayerPrefs.GetInt(RoostersInHandKey, 0);
        RoostersInCoop = PlayerPrefs.GetInt(RoostersInCoopKey, 0);
        StatsChanged?.Invoke();
    }

    public bool IsCollected(string collectibleId)
    {
        if (string.IsNullOrWhiteSpace(collectibleId))
        {
            return false;
        }

        return PlayerPrefs.GetInt(GetCollectedKey(collectibleId), 0) == 1;
    }

    public bool Collect(string collectibleId, bool isRooster)
    {
        if (string.IsNullOrWhiteSpace(collectibleId))
        {
            Debug.LogWarning("PlayerStats: collectibleId is empty.");
            return false;
        }

        if (IsCollected(collectibleId))
        {
            return false;
        }

        PlayerPrefs.SetInt(GetCollectedKey(collectibleId), 1);

        if (isRooster)
        {
            RoostersInHand++;
            PlayerPrefs.SetInt(RoostersInHandKey, RoostersInHand);
        }
        else
        {
            ChickensInHand++;
            PlayerPrefs.SetInt(TotalPointsKey, ChickensInHand);
        }

        PlayerPrefs.Save();
        StatsChanged?.Invoke();
        return true;
    }

    public void ResetAllProgressInThisApp()
    {
        PlayerPrefs.DeleteAll();
        ChickensInHand = 0;
        ChickensInCoop = 0;
        RoostersInHand = 0;
        RoostersInCoop = 0;
        PlayerPrefs.Save();
        StatsChanged?.Invoke();
        Debug.Log("PlayerStats: progress reset.");
    }

    private static string GetCollectedKey(string collectibleId)
    {
        return $"collected_{collectibleId}";
    }

    public void PutChickensInCoop(Vector3 pos)
    {
        if (isCountingChickens) return;
        StartCoroutine(CountChickensIntoCoop(pos));
    }

    private IEnumerator CountChickensIntoCoop(Vector3 pos)
    {
        isCountingChickens = true;

        int chickensToMove = math.max(ChickensInHand, RoostersInHand);

        if (chickensToMove <= 0)
        {
            isCountingChickens = false;
            yield break;
        }

        float duration = 1f;
        float delay = duration / chickensToMove;

        for (int i = 0; i < chickensToMove; i++)
        {
            if (ChickensInHand > 0)
            {
                ChickensInHand--;
                ChickensInCoop++;
            }
            if (RoostersInHand > 0)
            {
                RoostersInHand--;
                RoostersInCoop++;
            }
            PlayerPrefs.SetInt(TotalPointsKey, ChickensInHand);
            PlayerPrefs.SetInt(CollectedCountKey, ChickensInCoop);
            PlayerPrefs.SetInt(RoostersInHandKey, RoostersInHand);
            PlayerPrefs.SetInt(RoostersInCoopKey, RoostersInCoop);
            PlayerPrefs.Save();
            StatsChanged?.Invoke();
            cs.SpawnFeatherBurst(pos);
            audioSource.PlayOneShot(putInCoopSound);
            yield return new WaitForSeconds(delay);
        }
        isCountingChickens = false;
    }
}
