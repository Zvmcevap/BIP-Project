using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private ImageTrackedChickenCoop collectibles;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private CountdownTimer countdownTimer;

    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text collectedText;
    [SerializeField] private TMP_Text pointsText;


    private void OnEnable()
    {
        if (collectibles != null)
        {
            collectibles.StatusChanged += UpdateStatus;
        }

        if (playerStats != null)
        {
            playerStats.StatsChanged += UpdateStats;
        }
    }

    private void Start()
    {
        UpdateStats();

        if (statusText != null)
        {
            statusText.text = "Find Chickens, find the coop";
        }
    }

    private void OnDisable()
    {
        if (collectibles != null)
        {
            collectibles.StatusChanged -= UpdateStatus;
        }

        if (playerStats != null)
        {
            playerStats.StatsChanged -= UpdateStats;
        }
    }

    public void ResetStats()
    {
        playerStats.ResetAllProgressInThisApp();
        countdownTimer.ResetTimer();
    }

    private void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = $"Coop in sight: {status}";
        }
    }

    private void UpdateStats()
    {
        if (playerStats == null)
        {
            return;
        }

        if (collectedText != null)
        {
            collectedText.text = $"Chickens in coop: {playerStats.ChickensInCoop}";
        }

        if (pointsText != null)
        {
            pointsText.text = $"Chickens in hand: {playerStats.ChickensInHand}";
        }
    }
}