using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// The rooms are generated (Tools/LevelBuilder) and only hold named empty objects for what is special
// about them; this turns each into the real thing when the room loads, the way CampaignBootstrap
// turns the old Savepoint sprites into checkpoints. The names are "World|kind|arguments...":
//   World|door|id|scene|door there · World|save · World|lever|id
//   World|gate|id|lever+lever · World|vase|id · World|item|id|Kind|amount
public static class WorldMarkers {

    private const string Prefix = "World|";
    private const float DoorWidth = 1.2f;
    private const float DoorHeight = 2.4f;
    private const float GateHeight = 3f;

    public static void Build(Scene scene) {
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name.StartsWith(Prefix)) {
                Make(root, root.name.Split('|'));
            }
        }
    }

    private static void Make(GameObject obj, string[] args) {
        switch (args[1]) {
            case "door":
                Sprite(obj, "World/Door", 2);
                Trigger(obj, DoorWidth, DoorHeight);
                LevelDoor door = obj.AddComponent<LevelDoor>();
                door.doorId = args[2];
                door.kind = LevelDoor.Kind.Link;
                door.targetScene = args[3];
                door.targetDoor = args[4];
                door.pressUp = true;
                break;
            case "save":
                Sprite(obj, "World/Save Point", 2);
                Trigger(obj, 1.2f, 2f);
                obj.AddComponent<SavePoint>();
                break;
            case "lever":
                Sprite(obj, "World/Lever Off", 3);
                Trigger(obj, 1.2f, 1.6f);
                obj.AddComponent<WorldLever>().Init(args[2]);
                break;
            case "gate":
                Sprite(obj, "World/Gate", 3);
                BoxCollider2D wall = obj.AddComponent<BoxCollider2D>();
                wall.size = new Vector2(1f, GateHeight);
                obj.layer = LayerMask.NameToLayer("Ground");
                obj.AddComponent<WorldGate>().Init(args[3].Split('+'));
                break;
            case "vase":
                string vaseKey = CampaignWorld.KeyFor("prop", args[2]);
                if (CampaignWorld.State.Has(vaseKey)) {
                    Object.Destroy(obj);
                } else {
                    Breakable.PlaceVase(obj.transform.position, Layer(), vaseKey);
                }
                break;
            case "item":
                string itemKey = CampaignWorld.KeyFor("item", args[2]);
                if (!CampaignWorld.State.Has(itemKey)) {
                    ItemPickup.Kind kind = (ItemPickup.Kind)System.Enum.Parse(typeof(ItemPickup.Kind), args[3]);
                    ItemPickup.Spawn(kind, int.Parse(args[4]), obj.transform.position, Vector2.zero).StayForever(itemKey);
                }
                break;
            default:
                Debug.LogWarning("Unknown world marker: " + obj.name);
                break;
        }
    }

    private static void Sprite(GameObject obj, string path, int order) {
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = Resources.Load<Sprite>(path);
        TilemapRenderer layer = Layer();
        if (layer != null) {
            renderer.sharedMaterial = layer.sharedMaterial;   // lit by the torches like the room
            renderer.sortingLayerID = layer.sortingLayerID;
            renderer.sortingOrder = layer.sortingOrder + order;
        }
    }

    private static void Trigger(GameObject obj, float width, float height) {
        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(width, height);
    }

    private static TilemapRenderer Layer() {
        foreach (TilemapRenderer renderer in FindCompat.All<TilemapRenderer>()) {
            if (renderer.name == "Foreground_Tilemap") {
                return renderer;
            }
        }
        return null;
    }
}
