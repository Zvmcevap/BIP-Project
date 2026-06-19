using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private ImageTrackedChickenCoop collectibles;
    [SerializeField] private PlayerStats playerStats;
    // [SerializeField] private CountdownTimer countdownTimer;

    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text collectedText;
    [SerializeField] private TMP_Text pointsText;

    [SerializeField] private TMP_Text RoostersInHandText;
    [SerializeField] private TMP_Text RoostersInCoopText;

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
            statusText.text = "Click Chickens, find the coop";
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
        // countdownTimer.ResetTimer();
    }

    private void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = $"{status}";
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
            collectedText.text = $"Chickens in Coop {playerStats.ChickensInCoop}";
        }

        if (pointsText != null)
        {
            pointsText.text = $"Chickens in hand: {playerStats.ChickensInHand}";
        }
        if (RoostersInHandText != null)
        {
            RoostersInHandText.text = $"Roosters in hand: {playerStats.RoostersInHand}";
        }
        if (RoostersInCoopText != null)
        {
            RoostersInCoopText.text = $"Roosters in coop: {playerStats.RoostersInCoop}";
        }
    }
}