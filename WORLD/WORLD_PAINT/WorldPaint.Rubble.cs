// Compiles authored rubble into a static visual batch, slow cells and neutral blockers.
using System;
using System.Collections.Generic;
using Godot;
using Rubble = Godot.Collections.Dictionary<Godot.Vector2I, int>;

namespace MassiveRts;

public partial class WorldPaint
{
    internal enum RubbleKind { Small = 1, Heavy, Ruin }
    private const int MaxRuins = 48;
    private MultiMeshInstance3D rubbleView;
    internal HashSet<Vector2I> HeavyRubbleCells { get; private set; }
    internal float HeavyRubbleSpeed => FiniteRubbleSetting(data?.HeavyRubbleSpeed ?? 0.75f, 0.1f, 1f, 0.75f);

    internal static bool ValidRubble(Rubble cells)
    {
        if (cells.Count > MaxPaintedCells) return false;
        int ruins = 0;
        foreach (var entry in cells)
        {
            if (entry.Value < (int)RubbleKind.Small || entry.Value > (int)RubbleKind.Ruin) return false;
            if (entry.Value == (int)RubbleKind.Ruin && ++ruins > MaxRuins) return false;
        }
        return true;
    }

    internal static uint RubbleSeed(Vector2I cell)
    {
        uint hash = unchecked((uint)cell.X * 0x9E3779B9u ^ (uint)cell.Y * 0x85EBCA6Bu);
        return unchecked((hash ^ (hash >> 16)) * 0x7FEB352Du) ^ (hash >> 9);
    }

    private static float FiniteRubbleSetting(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;

    private float RuinWidth => CellSize * FiniteRubbleSetting(data.RuinWidthFraction, 0.35f, 1f, 0.9f);
    private float RuinHeight => FiniteRubbleSetting(data.RuinHeight, 0.5f, 8f, 1.8f);
    private static Vector2 RubbleCenter(Vector2I cell) => ((Vector2)cell + Vector2.One * 0.5f) * CellSize;

    // =========================================================
    private void RebuildRubble()
    {
        HeavyRubbleCells = null;
        if (data == null || data.RubbleCells.Count == 0 || !ValidRubble(data.RubbleCells))
        {
            if (rubbleView != null) rubbleView.Multimesh = null;
            return;
        }

        var mesh = WreckField.SharedHuskMesh();
        var batch = new MultiMesh
        {
            Mesh = mesh, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = data.RubbleCells.Count, VisibleInstanceCount = 0
        };
        float meshHeight = mesh.GetAabb().Size.Y;
        int slot = 0;
        foreach (var entry in data.RubbleCells)
        {
            if (!Contains(entry.Key)) continue;
            var kind = (RubbleKind)entry.Value;
            if (kind == RubbleKind.Heavy) (HeavyRubbleCells ??= new()).Add(entry.Key);

            uint hash = RubbleSeed(entry.Key);
            float variation = (hash & 65535u) / 65535f;
            Vector2 center = RubbleCenter(entry.Key);
            float yaw = kind == RubbleKind.Ruin ? (hash & 3u) * Mathf.Pi * 0.5f : variation * Mathf.Tau;
            float width = kind == RubbleKind.Ruin ? RuinWidth
                : CellSize * (kind == RubbleKind.Heavy ? 0.58f : 0.31f) * Mathf.Lerp(0.75f, 1.15f, variation);
            float height = kind == RubbleKind.Ruin ? RuinHeight : width * (kind == RubbleKind.Heavy ? 0.52f : 0.34f);
            var basis = new Basis(Vector3.Up, yaw) * Basis.FromScale(new Vector3(width, height / meshHeight, width));
            float y = Surface?.At(center.X, center.Y) ?? 0f;
            batch.SetInstanceTransform(slot++, new Transform3D(basis, new Vector3(center.X, y, center.Y)));
        }
        batch.VisibleInstanceCount = slot;
        if (rubbleView == null)
        {
            rubbleView = new MultiMeshInstance3D { Name = "AuthoredRubble", TopLevel = true,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(rubbleView, false, InternalMode.Back);
            rubbleView.GlobalTransform = Transform3D.Identity;
        }
        rubbleView.Multimesh = batch;
    }

    private ObstacleRectangle[] AppendRuins(ObstacleRectangle[] cover)
    {
        if (data == null || data.RubbleCells.Count == 0 || !ValidRubble(data.RubbleCells)) return cover;
        var ruins = new List<ObstacleRectangle>();
        Vector2 size = Vector2.One * RuinWidth;
        foreach (var entry in data.RubbleCells)
        {
            if (entry.Value != (int)RubbleKind.Ruin || !Contains(entry.Key)) continue;
            Vector2 center = RubbleCenter(entry.Key);
            float y = Surface?.At(center.X, center.Y) ?? 0f;
            ruins.Add(new ObstacleRectangle { WorldBounds = new Rect2(center - size * 0.5f, size),
                AirborneHeightRange = new Vector2(y, y + RuinHeight) });
        }
        if (ruins.Count == 0) return cover;
        var result = new ObstacleRectangle[cover.Length + ruins.Count];
        Array.Copy(cover, result, cover.Length);
        ruins.CopyTo(result, cover.Length);
        return result;
    }
}
