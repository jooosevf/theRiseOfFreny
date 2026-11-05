using System.Collections.Generic;
using UnityEngine;

// The campaign in play order (GDD: 9 dungeons of 9 stages, then the final boss). Dungeons 1 and 2
// exist for now; a new stage is one line here plus its scene in the Build Settings.
public static class CampaignLevels {

    public struct Stage {
        public string id;      // "1-3": dungeon 1, stage 3; also the scene name
        public int dungeon;
        public bool boss;      // last stage of a dungeon, with the boss
        public bool world;     // a room of the test dungeon (CampaignWorld), outside the stage order

        public string DisplayName => boss ? $"{id} · Chefe" : id;
    }

    public static readonly List<Stage> All = Stages();

    private static List<Stage> Stages() {
        List<Stage> stages = new List<Stage> {
            new Stage { id = "1-1", dungeon = 1 },
            new Stage { id = "1-2", dungeon = 1 },
            new Stage { id = "1-3", dungeon = 1 },
            new Stage { id = "1-4", dungeon = 1 },
            new Stage { id = "1-5", dungeon = 1 },
            new Stage { id = "1-6", dungeon = 1 },
            new Stage { id = "1-7", dungeon = 1 },
            new Stage { id = "1-8", dungeon = 1 },
            new Stage { id = "1-9", dungeon = 1, boss = true },
            new Stage { id = "2-1", dungeon = 2 },
            new Stage { id = "2-2", dungeon = 2 },
            new Stage { id = "2-3", dungeon = 2 },
            new Stage { id = "2-4", dungeon = 2 },
            new Stage { id = "2-5", dungeon = 2 },
            new Stage { id = "2-6", dungeon = 2 },
            new Stage { id = "2-7", dungeon = 2 },
            new Stage { id = "2-8", dungeon = 2 },
            new Stage { id = "2-9", dungeon = 2, boss = true },
        };
        if (Features.CampaignWorld) {
            stages.Add(new Stage { id = "T-1", dungeon = 2, world = true });
            stages.Add(new Stage { id = "T-2", dungeon = 2, world = true });
            stages.Add(new Stage { id = "T-3", dungeon = 2, world = true });
            stages.Add(new Stage { id = "T-4", dungeon = 2, world = true });
        }
        return stages;
    }

    public static int IndexOf(string sceneName) {
        return All.FindIndex(stage => stage.id == sceneName);
    }

    public static bool IsStage(string sceneName) {
        return IndexOf(sceneName) >= 0;
    }

    public static bool IsWorldRoom(string sceneName) {
        int index = IndexOf(sceneName);
        return index >= 0 && All[index].world;
    }

    // null after the last stage; stages whose scene is not in the build yet are skipped
    public static string NextOf(string sceneName) {
        int index = IndexOf(sceneName);
        if (index < 0 || All[index].world) {
            return null;
        }
        for (int i = index + 1; i < All.Count && !All[i].world; i++) {
            if (Application.CanStreamedLevelBeLoaded(All[i].id)) {
                return All[i].id;
            }
        }
        return null;
    }
}
