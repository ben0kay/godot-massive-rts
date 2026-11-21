using Godot;

namespace MassiveRts;

// Shows the hovered soldier's attack range using simulation positions and facing.
internal sealed partial class AttackRangeOverlay : MeshInstance3D
{
    private static readonly Color ShieldColor = new("#78e7f9");
    private const float HoverRadiusSquared = 32f * 32f;
    private const int ArcSegments = 64;
    private static readonly Color RangeColor = new("#ffd166");
    private static readonly Color EligibleColor = new("#61e8a8");
    private static readonly Color OutsideColor = new("#ff7979");
    private static readonly Color ShockwaveColor = new("#89caff");
    private readonly ImmediateMesh lines = new();

    public override void _Ready()
    {
        Mesh = lines;
        MaterialOverride = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            RenderPriority = 10
        };
        Hide();
    }

    public void Refresh(UnitState units, Camera3D camera, float separationGap, bool enabled)
    {
        int hovered = enabled && units != null ? PickUnit(units, camera) : -1;
        if (hovered < 0 || !units.Catalog[units.Type[hovered]].TargetsUnits)
        {
            Hide();
            return;
        }

        ref readonly var type = ref units.Catalog[units.Type[hovered]];
        Vector3 position = units.Position[hovered];
        Vector3 origin = position + Vector3.Up * (type.Height + 0.12f);
        int target = units.Target[hovered];
        bool hasTarget = target >= 0 && units.Health[target] > 0f;
        float defenderRadius = units.Catalog[hasTarget ? units.Type[target] : RuntimeUnitCatalog.Circle].Footprint;
        float radius = type.Ranged ? units.RangedReach(hovered, type) : ContactRange(type.MeleeAttackRadius, defenderRadius, separationGap);
        Vector2 facing = units.Facing[hovered];
        float cosineSquared = type.CleaveEnabled ? type.CleaveCosineSquared : type.MeleeFacingCosineSquared;
        bool directional = !type.Ranged && (type.Shape != UnitShape.Circle || type.CleaveEnabled) && facing != Vector2.Zero;
        float halfAngle = directional ? Mathf.Acos(Mathf.Sqrt(cosineSquared)) : Mathf.Pi;

        lines.ClearSurfaces();
        lines.SurfaceBegin(Godot.Mesh.PrimitiveType.Lines);
        Arc(origin, radius, facing.Angle(), halfAngle, RangeColor);
        if (type.MinimumFireRange > 0f)
            Arc(origin, type.MinimumFireRange, 0f, Mathf.Pi, OutsideColor);
        if (type.Shockwave.EveryAttacks > 0)
            Arc(origin, type.Shockwave.Radius, 0f, Mathf.Pi, ShockwaveColor);

        if (!type.Ranged)
        {
            for (int id = 0; id < units.Count; id++)
            {
                if (units.Health[id] <= 0f || units.Faction[id] == units.Faction[hovered]) continue;
                Vector3 offset = units.Position[id] - position;
                float reach = ContactRange(type.MeleeAttackRadius, units.Catalog[units.Type[id]].Footprint, separationGap);
                float distanceSquared = offset.LengthSquared();
                if (id != target && distanceSquared > (reach + 0.75f) * (reach + 0.75f)) continue;
                float forward = facing.X * offset.X + facing.Y * offset.Z;
                float planarSquared = offset.X * offset.X + offset.Z * offset.Z;
                bool inArc = !directional || forward >= 0f && forward * forward >= planarSquared * cosineSquared;
                Color color = distanceSquared <= reach * reach && inArc ? EligibleColor : OutsideColor;
                Vector3 marker = new(origin.X + offset.X, origin.Y, origin.Z + offset.Z);
                if (id == target) Segment(origin, marker, color);
                Segment(marker - Vector3.Right * 0.15f, marker + Vector3.Right * 0.15f, color);
                Segment(marker - Vector3.Back * 0.15f, marker + Vector3.Back * 0.15f, color);
            }
        }

        lines.SurfaceEnd();
        Show();
    }

    internal void RefreshShields(UnitState units, bool showAll, System.Collections.Generic.List<int> selected = null,
        StructureHealth selectedTower = null)
    {
        var shields = units?.Shields;
        if (shields == null || (!showAll && (selected == null || selected.Count == 0) && selectedTower == null))
        {
            Hide();
            return;
        }

        lines.ClearSurfaces();
        int drawn = 0;
        for (int i = 0; i < shields.SourceCount && drawn < 64; i++)
        {
            if (!shields.TryGetSource(i, units, out Vector3 center, out float radius, out int soldier, out var tower)) continue;
            if (!showAll && (soldier >= 0 ? selected?.Contains(soldier) != true : tower != selectedTower)) continue;
            if (drawn++ == 0) lines.SurfaceBegin(Godot.Mesh.PrimitiveType.Lines);
            Arc(center + Vector3.Up * 0.14f, radius, 0f, Mathf.Pi, ShieldColor);
        }
        if (drawn > 0) lines.SurfaceEnd();
        Visible = drawn > 0;
    }

    private static float ContactRange(float attacker, float defender, float separationGap) =>
        CombatSystem.MeleeContact(attacker, defender, separationGap) + CombatSystem.MeleeContactTolerance;

    private static int PickUnit(UnitState units, Camera3D camera)
    {
        Vector2 pointer = camera.GetViewport().GetMousePosition();
        float best = HoverRadiusSquared;
        int picked = -1;
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0f) continue;
            Vector3 center = units.Position[id] + Vector3.Up * (units.Catalog[units.Type[id]].Height * 0.5f);
            if (camera.IsPositionBehind(center)) continue;
            float distance = pointer.DistanceSquaredTo(camera.UnprojectPosition(center));
            if (distance >= best) continue;
            best = distance;
            picked = id;
        }
        return picked;
    }

    private void Arc(Vector3 origin, float radius, float direction, float halfAngle, Color color)
    {
        Vector3 first = Point(direction - halfAngle);
        Vector3 previous = first;
        for (int segment = 1; segment <= ArcSegments; segment++)
        {
            Vector3 next = Point(direction - halfAngle + 2f * halfAngle * segment / ArcSegments);
            Segment(previous, next, color);
            previous = next;
        }
        if (halfAngle < Mathf.Pi)
        {
            Segment(origin, first, color);
            Segment(origin, previous, color);
        }

        Vector3 Point(float angle) => origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    private void Segment(Vector3 from, Vector3 to, Color color)
    {
        lines.SurfaceSetColor(color);
        lines.SurfaceAddVertex(from);
        lines.SurfaceAddVertex(to);
    }
}
