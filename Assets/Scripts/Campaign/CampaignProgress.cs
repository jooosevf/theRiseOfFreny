using UnityEngine;

// What the campaign remembers between sessions (PlayerPrefs, like FrenyProgress): which stages
// are open and the best time of each. Coins and upgrades stay in FrenyProgress.
public static class CampaignProgress {

    public const string UnlockedKey = "freny.campaign.unlocked";   // index of the furthest open stage
    public const string BestTimeKeyPrefix = "freny.campaign.best.";

    public static int UnlockedIndex => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 0), 0, CampaignLevels.All.Count - 1);

    public static bool IsUnlocked(string stageId) {
        int index = CampaignLevels.IndexOf(stageId);
        return index >= 0 && index <= UnlockedIndex;
    }

    // 0 when the stage was never finished
    public static float BestTime(string stageId) {
        return PlayerPrefs.GetFloat(BestTimeKeyPrefix + stageId, 0f);
    }

    public static void Complete(string stageId, float seconds) {
        float best = BestTime(stageId);
        if (best <= 0f || seconds < best) {
            PlayerPrefs.SetFloat(BestTimeKeyPrefix + stageId, seconds);
        }
        int next = CampaignLevels.IndexOf(stageId) + 1;
        if (next > UnlockedIndex && next < CampaignLevels.All.Count && !CampaignLevels.All[next].world) {
            PlayerPrefs.SetInt(UnlockedKey, next);
        }
        PlayerPrefs.Save();
    }
}
