using System;
using Godot;
using MonkeyPost.Core;
using static MonkeyPost.Game.Art;

namespace MonkeyPost.Game
{
    /// <summary>Builds the Lagos street cage: turf, lines, low walls, monkey posts, fence and buildings.</summary>
    static class PitchBuilder
    {
        const float WallHeight = 1.1f;
        const float NearWallHeight = 0.5f;
        const float WallThickness = 0.25f;
        const float LineWidth = 0.08f;
        const float LineHeight = 0.012f;

        static readonly Color Wall = new Color(0.78f, 0.74f, 0.66f);
        static readonly Color Fence = new Color(0.35f, 0.37f, 0.38f);
        static readonly Color Post = new Color(0.95f, 0.95f, 0.95f);
        static readonly Color[] Buildings =
        {
            new Color(0.80f, 0.70f, 0.45f), new Color(0.55f, 0.66f, 0.78f), new Color(0.82f, 0.52f, 0.38f),
            new Color(0.90f, 0.88f, 0.80f), new Color(0.60f, 0.75f, 0.60f), new Color(0.85f, 0.78f, 0.55f),
        };
        static readonly Color Roof = new Color(0.45f, 0.42f, 0.40f);

        public static Node3D Build(MatchConfig c, Node3D parent)
        {
            var root = new Node3D { Name = "Pitch" };
            parent.AddChild(root);
            float l = c.PitchLength, w = c.PitchWidth;

            Box("Street", root, P(l / 2f, -0.06f, w / 2f), new Vector3(l + 60f, 0.1f, w + 50f), Dust);
            Box("Turf", root, P(l / 2f, -0.02f, w / 2f), new Vector3(l, 0.04f, w), Turf);

            BuildLines(c, root);
            BuildWalls(c, root);
            BuildGoal(c, root, 0f, -1f);
            BuildGoal(c, root, l, 1f);
            BuildFence(c, root);
            BuildBuildings(c, root);
            return root;
        }

        static void BuildLines(MatchConfig c, Node3D root)
        {
            float l = c.PitchLength, w = c.PitchWidth, inset = 0.3f;
            Line(root, P(l / 2f, 0f, inset), new Vector3(l - 2f * inset, LineHeight, LineWidth));
            Line(root, P(l / 2f, 0f, w - inset), new Vector3(l - 2f * inset, LineHeight, LineWidth));
            Line(root, P(inset, 0f, w / 2f), new Vector3(LineWidth, LineHeight, w - 2f * inset));
            Line(root, P(l - inset, 0f, w / 2f), new Vector3(LineWidth, LineHeight, w - 2f * inset));
            Line(root, P(l / 2f, 0f, w / 2f), new Vector3(LineWidth, LineHeight, w - 2f * inset));

            // Centre circle from short segments.
            const int segments = 28;
            const float radius = 3f;
            float segmentLength = 2f * Mathf.Pi * radius / segments + 0.02f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + 0.5f) / segments * Mathf.Pi * 2f;
                var segment = Line(root, P(l / 2f + Mathf.Cos(angle) * radius, 0f, w / 2f + Mathf.Sin(angle) * radius),
                    new Vector3(LineWidth, LineHeight, segmentLength));
                segment.Rotation = new Vector3(0f, angle, 0f);
            }

            // Keeper boxes.
            foreach (float goalX in new[] { 0f, l })
            {
                float dir = goalX == 0f ? 1f : -1f;
                Line(root, P(goalX + dir * c.KeeperBoxDepth, 0f, w / 2f), new Vector3(LineWidth, LineHeight, c.KeeperBoxHalfWidth * 2f));
                foreach (float side in new[] { -1f, 1f })
                {
                    Line(root, P(goalX + dir * c.KeeperBoxDepth / 2f, 0f, w / 2f + side * c.KeeperBoxHalfWidth),
                        new Vector3(c.KeeperBoxDepth, LineHeight, LineWidth));
                }
            }
        }

        static MeshInstance3D Line(Node3D root, Vector3 centre, Vector3 size) => Box("Line", root, centre, size, Art.Line);

        static void BuildWalls(MatchConfig c, Node3D root)
        {
            float l = c.PitchLength, w = c.PitchWidth, t = WallThickness;
            // The near wall (pitch Y = 0, nearest the camera) is kept low so it doesn't hide play.
            Box("Wall near", root, P(l / 2f, NearWallHeight / 2f, -t / 2f), new Vector3(l + 2f * t, NearWallHeight, t), Wall);
            Box("Wall far", root, P(l / 2f, WallHeight / 2f, w + t / 2f), new Vector3(l + 2f * t, WallHeight, t), Wall);

            float gapHalf = c.GoalWidth / 2f + 0.12f;
            float sideLength = w / 2f - gapHalf;
            foreach (float x in new[] { -t / 2f, l + t / 2f })
            {
                Box("Wall end", root, P(x, WallHeight / 2f, sideLength / 2f), new Vector3(t, WallHeight, sideLength), Wall);
                Box("Wall end", root, P(x, WallHeight / 2f, w - sideLength / 2f), new Vector3(t, WallHeight, sideLength), Wall);
            }
        }

        static void BuildGoal(MatchConfig c, Node3D root, float x, float outward)
        {
            float z = c.PitchWidth / 2f;
            // The monkey posts: two short painted posts, knee height, in a gap in the wall.
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Monkey post", root, P(x, c.GoalHeight / 2f, z + side * (c.GoalWidth / 2f + 0.06f)),
                    new Vector3(0.12f, c.GoalHeight, 0.12f), Post);
            }
            // A dark recess behind the gap so goals read clearly.
            Box("Goal recess", root, P(x + outward * 0.5f, 0.35f, z), new Vector3(0.8f, 0.7f, c.GoalWidth + 0.24f), Shadow);
        }

        static void BuildFence(MatchConfig c, Node3D root)
        {
            float l = c.PitchLength, w = c.PitchWidth;
            const float height = 4f;
            for (float x = 0f; x <= l + 0.01f; x += 3f)
                Box("Fence post", root, P(x, height / 2f, w + 0.35f), new Vector3(0.08f, height, 0.08f), Fence);
            Box("Fence rail", root, P(l / 2f, height, w + 0.35f), new Vector3(l, 0.08f, 0.08f), Fence);
            Box("Fence rail", root, P(l / 2f, height * 0.55f, w + 0.35f), new Vector3(l, 0.05f, 0.05f), Fence);
        }

        static void BuildBuildings(MatchConfig c, Node3D root)
        {
            float w = c.PitchWidth;
            var rng = new Random(7);
            float x = -12f;
            int i = 0;
            while (x < c.PitchLength + 12f)
            {
                float width = 5f + (float)rng.NextDouble() * 5f;
                float height = 4f + (float)rng.NextDouble() * 7f;
                float depth = 6f;
                var colour = Buildings[i++ % Buildings.Length];
                float y = w + 5f + depth / 2f + (float)rng.NextDouble() * 2f;
                Box("Building", root, P(x + width / 2f, height / 2f, y), new Vector3(width, height, depth), colour);
                Box("Roof", root, P(x + width / 2f, height + 0.1f, y), new Vector3(width + 0.4f, 0.2f, depth + 0.4f), Roof);
                x += width + 0.6f;
            }
        }
    }
}
