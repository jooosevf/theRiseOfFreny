using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

// The "Mesh" variant of GolemRig: the 2D Animation package's SpriteSkin bending the Golem's idle
// frame over the same bones. Everything the Skinning Editor would store in the texture's .meta
// (bones, bind poses, a vertex grid with bone weights) is built here at runtime, from the same
// part rectangles golem_rig.py cuts, so the prototype needs no PSB and no hand-made mesh. Each
// vertex belongs to one part and blends into the parent bone within a few pixels of the joint.
public static class GolemRigMesh {
    private const int Art = 40;            // art pixels per side
    private const int Step = 2;            // vertex grid step, in art pixels
    private const float Blend = 3f;        // art pixels of blending at the joints

    // part rectangles (y0, y1, x0, x1 inclusive, y down) as in golem_rig.py, with the bone and the
    // joint row the weights blend towards the parent from
    private struct Region {
        public GolemRig.Bone bone;
        public int y0, y1, x0, x1;
        public float jointY;
        public Region(GolemRig.Bone bone, int y0, int y1, int x0, int x1, float jointY) {
            this.bone = bone; this.y0 = y0; this.y1 = y1; this.x0 = x0; this.x1 = x1; this.jointY = jointY;
        }
    }
    private static readonly Region[] Regions = {
        new Region(GolemRig.Bone.Neck, 9, 13, 15, 23, 14f),
        new Region(GolemRig.Bone.ShoulderBack, 16, 19, 9, 14, 18f),
        new Region(GolemRig.Bone.ShoulderBack, 20, 23, 9, 13, 18f),
        new Region(GolemRig.Bone.ShoulderBack, 24, 33, 9, 12, 18f),
        new Region(GolemRig.Bone.ShoulderFront, 16, 19, 25, 30, 18f),
        new Region(GolemRig.Bone.ShoulderFront, 20, 29, 26, 30, 18f),
        new Region(GolemRig.Bone.ShoulderFront, 30, 33, 24, 28, 18f),
        new Region(GolemRig.Bone.HipBack, 33, 39, 15, 18, 33f),
        new Region(GolemRig.Bone.HipFront, 33, 39, 20, 23, 33f),
        new Region(GolemRig.Bone.Torso, 14, 32, 13, 25, 32f),   // last: whatever is left of the body
    };

    // gives `renderer` a copy of `source` skinned to `bones` (GolemRig's order) and a SpriteSkin
    public static bool Skin(SpriteRenderer renderer, Sprite source, Transform[] bones) {
        if (renderer == null || source == null || bones == null) {
            return false;
        }
        Texture2D tex = source.texture;
        Sprite sprite = Sprite.Create(tex, source.rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = "GolemRig skinned";

        int n = Art / Step + 1;
        int count = n * n;
        NativeArray<Vector3> positions = new NativeArray<Vector3>(count, Allocator.Temp);
        NativeArray<Vector2> uvs = new NativeArray<Vector2>(count, Allocator.Temp);
        NativeArray<BoneWeight> weights = new NativeArray<BoneWeight>(count, Allocator.Temp);
        float px = GolemRig.ArtPixel;
        for (int gy = 0; gy < n; gy++) {
            for (int gx = 0; gx < n; gx++) {
                int i = gy * n + gx;
                float ax = gx * Step, ay = gy * Step;   // art coordinates, y down
                positions[i] = new Vector3((ax - Art * 0.5f) * px, (Art * 0.5f - ay) * px, 0f);
                uvs[i] = new Vector2(ax / Art, 1f - ay / Art);
                weights[i] = WeightAt(ax, ay);
            }
        }
        int cells = n - 1;
        NativeArray<ushort> indices = new NativeArray<ushort>(cells * cells * 6, Allocator.Temp);
        int k = 0;
        for (int gy = 0; gy < cells; gy++) {
            for (int gx = 0; gx < cells; gx++) {
                ushort a = (ushort)(gy * n + gx), b = (ushort)(a + 1), c = (ushort)(a + n), d = (ushort)(c + 1);
                indices[k++] = a; indices[k++] = b; indices[k++] = c;
                indices[k++] = b; indices[k++] = d; indices[k++] = c;
            }
        }

        // bind poses: the bones as they stand now (the rest pose), in the renderer's space
        Matrix4x4 toSprite = renderer.transform.localToWorldMatrix;
        NativeArray<Matrix4x4> bindPoses = new NativeArray<Matrix4x4>(bones.Length, Allocator.Temp);
        SpriteBone[] spriteBones = new SpriteBone[bones.Length];
        for (int i = 0; i < bones.Length; i++) {
            bindPoses[i] = bones[i].worldToLocalMatrix * toSprite;
            spriteBones[i] = new SpriteBone {
                name = bones[i].name,
                position = bones[i].localPosition * source.pixelsPerUnit,
                rotation = bones[i].localRotation,
                length = 10f,
                parentId = bones[i].parent == renderer.transform ? -1 : System.Array.IndexOf(bones, bones[i].parent),
            };
        }

        sprite.SetVertexCount(count);
        sprite.SetVertexAttribute(VertexAttribute.Position, positions);
        sprite.SetVertexAttribute(VertexAttribute.TexCoord0, uvs);
        sprite.SetVertexAttribute(VertexAttribute.BlendWeight, weights);
        sprite.SetIndices(indices);
        sprite.SetBones(spriteBones);
        sprite.SetBindPoses(bindPoses);
        positions.Dispose(); uvs.Dispose(); weights.Dispose(); indices.Dispose(); bindPoses.Dispose();

        renderer.sprite = sprite;
        // fully qualified: the project has its own SpriteSkin (the recoloured skins)
        UnityEngine.U2D.Animation.SpriteSkin skin = renderer.gameObject.AddComponent<UnityEngine.U2D.Animation.SpriteSkin>();
        skin.alwaysUpdate = true;
        skin.SetRootBone(bones[0]);
        UnityEngine.U2D.Animation.SpriteSkinState state = skin.SetBoneTransforms(bones);
        if (state != UnityEngine.U2D.Animation.SpriteSkinState.Ready) {
            Debug.LogWarning("GolemRigMesh: SpriteSkin state " + state);
            Object.Destroy(skin);
            return false;
        }
        return true;
    }

    // the vertex's part, blended towards the parent bone near the joint
    private static BoneWeight WeightAt(float ax, float ay) {
        foreach (Region r in Regions) {
            if (ax < r.x0 || ax > r.x1 + 1 || ay < r.y0 || ay > r.y1 + 1) {
                continue;
            }
            int parent = r.bone == GolemRig.Bone.Torso ? (int)GolemRig.Bone.Pelvis
                : r.bone == GolemRig.Bone.HipBack || r.bone == GolemRig.Bone.HipFront ? (int)GolemRig.Bone.Pelvis
                : (int)GolemRig.Bone.Torso;
            float away = Mathf.Abs(ay - r.jointY);
            float own = r.bone == GolemRig.Bone.Torso ? 1f : Mathf.Clamp01(0.5f + 0.5f * away / Blend);
            return new BoneWeight { boneIndex0 = (int)r.bone, weight0 = own, boneIndex1 = parent, weight1 = 1f - own };
        }
        return new BoneWeight { boneIndex0 = (int)GolemRig.Bone.Pelvis, weight0 = 1f };   // empty corners
    }
}
