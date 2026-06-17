using System;
using System.Collections;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    private const string TotalPointsKey = "player_total_points";
    private const string CollectedCountKey = "player_collected_count";

    public int ChickensInHand { get; private set; } = 100;
    public int ChickensInCoop { get; private set; }
    private bool isCounting;

    public event Action StatsChanged;

    private void Awake()
    {
        Load();
    }

    public void Load()
    {
        ChickensInHand = PlayerPrefs.GetInt(TotalPointsKey, 100);
        ChickensInCoop = PlayerPrefs.GetInt(CollectedCountKey, 0);
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

    public bool Collect(string collectibleId, int points)
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

        ChickensInHand++;
        // ChickensInCoop++;

        PlayerPrefs.SetInt(TotalPointsKey, ChickensInHand);
        PlayerPrefs.SetInt(CollectedCountKey, ChickensInCoop);
        PlayerPrefs.Save();

        StatsChanged?.Invoke();
        Debug.Log($"PlayerStats: collected '{collectibleId}'. InHand: {ChickensInHand}, InCoop: {ChickensInCoop}.");
        return true;
    }

    public void ResetAllProgressInThisApp()
    {
        PlayerPrefs.DeleteAll();
        ChickensInHand = 0;
        ChickensInCoop = 0;
        PlayerPrefs.Save();
        StatsChanged?.Invoke();
        Debug.Log("PlayerStats: progress reset.");
    }

    private static string GetCollectedKey(string collectibleId)
    {
        return $"collected_{collectibleId}";
    }

    public void PutChickensInCoop()
    {
        if (isCounting) return;
        StartCoroutine(CountChickensIntoCoop());
    }

    private IEnumerator CountChickensIntoCoop()
    {
        isCounting = true;

        int chickensToMove = ChickensInHand;

        if (chickensToMove <= 0)
        {
            isCounting = false;
            yield break;
        }

        float duration = 1f;
        float delay = duration / chickensToMove;

        for (int i = 0; i < chickensToMove; i++)
        {
            ChickensInHand--;
            ChickensInCoop++;

            yield return new WaitForSeconds(delay);
        }

        isCounting = false;
    }
}
