// Creates shared beveled low-poly soldier meshes with baked top, chamfer and side tones.
// All shapes use one unshaded material and remain compatible with MultiMesh batching.
using System;
using Godot;
namespace MassiveRts;

public static class FacetedSoldierMeshes
{
    private const float TopInsetFraction = 0.11f;
    // Extra contrast only for soldier mesh creation; shared building bevels stay unchanged.
    private const float SoldierSideShade = 0.75f;
    private static readonly Color TopTint = Colors.White;
    private static readonly Color BottomTint = new(0.48f, 0.48f, 0.48f);

    // =========================================================
    // Construct one reusable mesh per unit type. No per-soldier geometry or lighting work.
    public static Mesh[] Create(RuntimeUnitCatalog catalog, UnitVisualConfig visuals)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        bool enhanced = visuals.UseEnhancedMeshes;
        var crackShader = visuals.UseUnitFracture
            ? GD.Load<Shader>("res://VISUALS/FEEDBACK/UNIT_HITS/UnitCracks.gdshader") : null;
        var crackMask = crackShader != null
            ? GD.Load<Texture2D>("res://VISUALS/FEEDBACK/UNIT_HITS/UnitCrackMask.svg") : null;
        var meshes = new Mesh[catalog.Count];
        for (int i = 0; i < meshes.Length; i++)
        {
            ref readonly UnitStats type = ref catalog[i];
            UnitOutline outline = type.Outline;
            Material unitMaterial = material;
            if (crackShader != null && crackMask != null)
            {
                var cracked = new ShaderMaterial { Shader = crackShader };
                cracked.SetShaderParameter("crack_mask", crackMask);
                cracked.SetShaderParameter("half_extent", new Vector2(outline.RadiusX, outline.RadiusZ));
                cracked.SetShaderParameter("darkness", Mathf.Clamp(visuals.CrackDarkness, 0f, 1f));
                unitMaterial = cracked;
            }
            meshes[i] = CreateUnitMesh(outline, type.Height, unitMaterial,
                enhanced && type.Id == RuntimeUnitCatalog.CircleId,
                visuals.UseEnhancedHexagonMesh && type.Id == RuntimeUnitCatalog.HexagonId,
                catalog.Definition(i), includeIndependent: false);
        }
        return meshes;
    }

    // =========================================================
    // A narrow inset top ring creates a single, lightly shaded bevel around every silhouette.
    // The shoulders retain the original outer footprint, and the bottoms remain at -height/2.
    internal static Mesh CreatePlacementPreview(UnitDefinition unit, Color tint)
    {
        float size = UnitOutline.EffectiveSize(unit);
        UnitVisualConfig visuals = UnitVisualConfig.Load();
        return CreateUnitMesh(UnitOutline.FromDefinition(unit, size), Mathf.Max(0.01f, unit.Height) * size,
            new StandardMaterial3D { AlbedoColor = tint, VertexColorUseAsAlbedo = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            visuals.UseEnhancedMeshes && unit.Id == RuntimeUnitCatalog.CircleId,
            visuals.UseEnhancedHexagonMesh && unit.Id == RuntimeUnitCatalog.HexagonId,
            unit, includeIndependent: true);
    }

    private static ArrayMesh CreateUnitMesh(UnitOutline outline, float height, Material material,
        bool enhancedCircle, bool enhancedHexagon, UnitDefinition unit, bool includeIndependent)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);

        foreach (var mount in unit.RocketMounts ?? new())
        {
            if (mount == null || (mount.IndependentAim && (!includeIndependent || mount.Projectile == null))) continue;
            AppendRocketBarrel(surface, MountedRocket.LocalPose(mount, UnitOutline.EffectiveSize(unit)));
        }

        if (outline.IsHFrame)
        {
            float x = outline.RadiusX, z = outline.RadiusZ;
            AppendBeveledRectangle(surface, new Vector3(0f, -height * 0.04f, 0f),
                new Vector3(x * 1.38f, height * 0.86f, z * 0.46f), new Color(0.75f, 0.75f, 0.75f), SoldierSideShade);
            for (int side = -1; side <= 1; side += 2)
            {
                AppendBeveledRectangle(surface, new Vector3(side * x * 0.67f, 0f, z * 0.05f),
                    new Vector3(x * 0.66f, height, z * 1.58f), Colors.White, SoldierSideShade);
                AppendBeveledRectangle(surface, new Vector3(side * x * 0.67f, 0f, -z * 0.84f),
                    new Vector3(x * 0.66f, height * 0.90f, z * 0.32f), new Color(0.88f, 0.88f, 0.88f), SoldierSideShade);
            }
            return surface.Commit();
        }

        if (outline.IsDrillRig)
        {
            AppendDrillRig(surface, outline, height);
            return surface.Commit();
        }

        AppendPrism(surface, outline.Sides, outline.RadiusX, outline.RadiusZ, height, outline.Rotation,
            Vector3.Zero, Colors.White, customOutline: outline.IsChevron || outline.IsPike || outline.IsDreadnaught || outline.IsDeepFork ? outline : null,
            sideShade: SoldierSideShade, rimmedCap: enhancedCircle, topCapBrightness: enhancedHexagon ? 0.55f : 1f);
        if (enhancedHexagon)
            AppendHexagonSiegeArmor(surface, outline, height);
        if (unit.Id == RuntimeUnitCatalog.MobileGeneratorId)
        {
            AppendPrism(surface, 6, outline.RadiusX * 0.68f, outline.RadiusZ * 0.68f,
                height * 0.24f, outline.Rotation, Vector3.Up * height * 0.50f, new Color(0.37f, 0.53f, 0.58f));
            AppendPrism(surface, 6, outline.RadiusX * 0.39f, outline.RadiusZ * 0.39f,
                height * 0.68f, outline.Rotation, Vector3.Up * height * 0.69f, new Color(0.72f, 1f, 1f));
        }
        if (unit.Shape == UnitShape.Prism)
        {
            for (int side = -1; side <= 1; side += 2)
                AppendBeveledRectangle(surface, new Vector3(side * outline.RadiusX * 0.44f, height * 0.50f, -outline.RadiusZ * 0.10f),
                    new Vector3(outline.RadiusX * 0.38f, height * 0.34f, outline.RadiusZ * 1.25f), new Color(0.77f, 0.92f, 1f));
            AppendBeveledRectangle(surface, new Vector3(0f, height * 0.35f, -outline.RadiusZ * 0.82f),
                new Vector3(outline.RadiusX * 0.40f, height * 0.30f, outline.RadiusZ * 0.20f), new Color(0.70f, 0.96f, 1f));
        }
        if (outline.IsDreadnaught)
            AppendDreadnaughtDetails(surface, outline, height);
        if (outline.IsChevron)
        {
            float length = outline.BarrelFront - outline.BarrelBack;
            float midpoint = (outline.BarrelFront + outline.BarrelBack) * 0.5f;
            AppendBeveledRectangle(surface, new Vector3(0f, height * 0.32f, -midpoint),
                new Vector3(outline.BarrelWidth, height * 0.24f, length), new Color(0.86f, 0.86f, 0.86f), SoldierSideShade);
        }
        return surface.Commit();
    }

    // =========================================================
    // Six beveled-look deck plates sit over a darkened cap. Built once per type
    // so the existing faction MultiMesh, FX2 cracks and hit colours still work.
    // All detail stays inside the original hexagonal footprint.
    private static void AppendHexagonSiegeArmor(SurfaceTool surface, UnitOutline outline, float height)
    {
        float deck = height * 0.5f;
        float plateBaseY = deck + height * 0.008f;
        float plateTopY = deck + height * 0.050f;
        var sideTint = new Color(0.57f, 0.57f, 0.57f);
        var innerTint = new Color(0.68f, 0.68f, 0.68f);
        var centerTint = new Color(0.66f, 0.66f, 0.66f);

        for (int i = 0; i < 6; i++)
        {
            Vector2 cornerA = outline.Vertex(i);
            Vector2 cornerB = outline.Vertex((i + 1) % 6);
            // Trim each wedge on both spoke edges, leaving six readable dark seams.
            Vector2 innerA = (cornerA * 0.37f).Lerp(cornerB * 0.37f, 0.11f);
            Vector2 innerB = (cornerB * 0.37f).Lerp(cornerA * 0.37f, 0.11f);
            Vector2 outerA = (cornerA * 0.81f).Lerp(cornerB * 0.81f, 0.055f);
            Vector2 outerB = (cornerB * 0.81f).Lerp(cornerA * 0.81f, 0.055f);

            Vector3 topInnerA = new(innerA.X, plateTopY, innerA.Y);
            Vector3 topInnerB = new(innerB.X, plateTopY, innerB.Y);
            Vector3 topOuterA = new(outerA.X, plateTopY, outerA.Y);
            Vector3 topOuterB = new(outerB.X, plateTopY, outerB.Y);
            Vector3 lowInnerA = new(innerA.X, plateBaseY, innerA.Y);
            Vector3 lowInnerB = new(innerB.X, plateBaseY, innerB.Y);
            Vector3 lowOuterA = new(outerA.X, plateBaseY, outerA.Y);
            Vector3 lowOuterB = new(outerB.X, plateBaseY, outerB.Y);

            // The two forward plates catch a little more light to indicate facing.
            Color topTint = i == 0 || i == 5
                ? Colors.White : new Color(0.92f, 0.92f, 0.92f);
            AddQuad(surface, topInnerA, topInnerB, topOuterB, topOuterA, Vector3.Up, topTint);

            Vector3 edge = topOuterB - topOuterA;
            Vector3 outward = new Vector3(edge.Z, 0f, -edge.X).Normalized();
            AddQuad(surface, lowOuterA, topOuterA, topOuterB, lowOuterB, outward, sideTint);
            AddQuad(surface, lowInnerB, topInnerB, topInnerA, lowInnerA, -outward, innerTint);
        }

        // Shallow darker central hexagon, without glow or an extra material.
        Vector3 center = new(0f, deck + height * 0.014f, 0f);
        for (int i = 0; i < 6; i++)
        {
            Vector2 a = outline.Vertex(i) * 0.29f;
            Vector2 b = outline.Vertex((i + 1) % 6) * 0.29f;
            AddTriangle(surface, center,
                new Vector3(b.X, center.Y, b.Y), new Vector3(a.X, center.Y, a.Y),
                Vector3.Up, centerTint);
        }
    }

    // =========================================================
    // Raised deck armor, narrow prow ridge, vents and side insets are baked once
    // into the shared Dreadnaught mesh. They do not change its combat footprint.
    private static void AppendDreadnaughtDetails(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ, deck = height * 0.5f;
        float lower = deck + height * 0.012f, upper = deck + height * 0.065f;

        // Inset convex plate follows the wedge nose without covering the corner turrets.
        ReadOnlySpan<Vector2> plate =
        [
            new(0f, -0.78f), new(0.43f, -0.29f), new(0.43f, 0.39f),
            new(0.31f, 0.53f), new(-0.31f, 0.53f),
            new(-0.43f, 0.39f), new(-0.43f, -0.29f)
        ];
        for (int i = 0; i < plate.Length; i++)
        {
            int next = (i + 1) % plate.Length;
            Vector2 a = plate[i], b = plate[next];
            Vector3 outerA = new(a.X * x, lower, a.Y * z);
            Vector3 outerB = new(b.X * x, lower, b.Y * z);
            Vector3 topA = new(a.X * x * 0.94f, upper, a.Y * z * 0.94f);
            Vector3 topB = new(b.X * x * 0.94f, upper, b.Y * z * 0.94f);
            Vector3 outward = new(outerB.Z - outerA.Z, 0f, outerA.X - outerB.X);
            outward = outward.Normalized();
            AddTriangle(surface, new Vector3(0f, upper, 0f), topB, topA,
                Vector3.Up, new Color(0.91f, 0.96f, 1f));
            AddQuad(surface, outerA, topA, topB, outerB,
                outward, new Color(0.72f, 0.84f, 0.97f));
        }

        // A restrained keel along the pointed front breaks up the otherwise flat prow.
        AppendBeveledRectangle(surface,
            new Vector3(0f, deck + height * 0.038f, -z * 0.76f),
            new Vector3(x * 0.12f, height * 0.075f, z * 0.23f),
            new Color(0.91f, 0.95f, 1f), SoldierSideShade);

        // Two deck rails and short vents give the large hull readable detail from above.
        for (int side = -1; side <= 1; side += 2)
        {
            AppendBeveledRectangle(surface,
                new Vector3(side * x * 0.78f, deck + height * 0.018f, z * 0.14f),
                new Vector3(x * 0.19f, height * 0.036f, z * 0.35f),
                new Color(0.82f, 0.90f, 1f), SoldierSideShade);
            for (int vent = 0; vent < 2; vent++)
            {
                AppendBeveledRectangle(surface,
                    new Vector3(side * x * 0.65f, deck + height * 0.019f, z * (0.39f + vent * 0.12f)),
                    new Vector3(x * 0.28f, height * 0.038f, z * 0.055f),
                    new Color(0.57f, 0.69f, 0.82f), SoldierSideShade);
            }

            // Flat vertical side armor panels add depth without widening the hull.
            float panelX = side * (x + 0.008f);
            for (int panel = 0; panel < 2; panel++)
            {
                float zStart = z * (0.08f + panel * 0.34f);
                float zEnd = zStart + z * 0.23f;
                if (side < 0) (zStart, zEnd) = (zEnd, zStart);
                Vector3 bottomA = new(panelX, -height * 0.24f, zStart);
                Vector3 topA = new(panelX, height * 0.06f, zStart);
                Vector3 topB = new(panelX, height * 0.06f, zEnd);
                Vector3 bottomB = new(panelX, -height * 0.24f, zEnd);
                AddQuad(surface, bottomA, topA, topB, bottomB, new Vector3(side, 0f, 0f),
                    new Color(0.55f, 0.65f, 0.78f));
            }
        }
    }

    private static void AppendDrillRig(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ;
        AppendBeveledRectangle(surface, new Vector3(0f, 0f, z * 0.11f),
            new Vector3(x * 2f, height, z * 1.22f), Colors.White, SoldierSideShade);

        var steel = new Color(0.78f, 0.78f, 0.78f);
        var baseCenter = new Vector3(0f, height * 0.08f, -z * 0.55f);
        AppendBeveledRectangle(surface, baseCenter,
            new Vector3(x * 0.72f, height * 0.62f, z * 0.24f), steel * 0.75f, SoldierSideShade);

        const int facets = 6;
        float radius = x * 0.34f;
        var tip = new Vector3(0f, baseCenter.Y, -outline.DrillLengthFromOrigin);
        for (int i = 0; i < facets; i++)
        {
            float first = Mathf.Tau * i / facets, second = Mathf.Tau * (i + 1) / facets;
            Vector3 a = baseCenter + new Vector3(Mathf.Cos(first), Mathf.Sin(first), 0f) * radius;
            Vector3 b = baseCenter + new Vector3(Mathf.Cos(second), Mathf.Sin(second), 0f) * radius;
            Vector3 normal = (tip - a).Cross(b - a).Normalized();
            AddTriangle(surface, a, tip, b, normal, steel * Mathf.Lerp(0.58f, 1f, (normal.Y + 1f) * 0.5f));
        }
    }

    public static void AppendBeveledRectangle(SurfaceTool surface, Vector3 center, Vector3 size, Color tint, float sideShade = 1f)
    {
        const float inverseSqrtTwo = 0.70710678f;
        AppendPrism(surface, 4, size.X * inverseSqrtTwo, size.Z * inverseSqrtTwo,
            size.Y, Mathf.Pi * 0.25f, center, tint, 0.16f, sideShade: sideShade);
    }

    internal static void AppendRocketBarrel(SurfaceTool surface, Transform3D pose)
    {
        const float inverseSqrtTwo = 0.70710678f;
        Vector3 size = new(pose.Basis.X.Length(), pose.Basis.Y.Length(), pose.Basis.Z.Length());
        float rotation = Mathf.Atan2(pose.Basis.X.Z, pose.Basis.X.X);
        AppendPrism(surface, 4, size.X * inverseSqrtTwo, size.Z * inverseSqrtTwo, size.Y, Mathf.Pi * 0.25f,
            pose * (MountedRocket.Muzzle * 0.5f), new Color(0.77f, 0.77f, 0.77f), sideShade: SoldierSideShade, yaw: rotation);
    }

    private static void AppendPrism(SurfaceTool surface, int sides, float radiusX, float radiusZ,
        float height, float rotation, Vector3 center, Color tint, float rectangleInset = 0f,
        UnitOutline? customOutline = null, float sideShade = 1f, bool rimmedCap = false,
        float yaw = 0f, float topCapBrightness = 1f)
    {
        float half = height * 0.5f;
        float bevelRise = Mathf.Min(height * 0.16f, Mathf.Min(radiusX, radiusZ) * 0.15f);
        float insetDistance = Mathf.Min(radiusX, radiusZ) * TopInsetFraction;
        float insetX = TopInsetFraction, insetZ = TopInsetFraction;
        if (rectangleInset > 0f)
        {
            insetDistance = rectangleInset;
            insetX = Mathf.Min(TopInsetFraction, rectangleInset / (radiusX * 0.70710678f));
            insetZ = Mathf.Min(TopInsetFraction, rectangleInset / (radiusZ * 0.70710678f));
        }
        var top = new Vector3[sides];
        var shoulder = new Vector3[sides];
        var bottom = new Vector3[sides];

        // Each concave outline uses an interior point that sees every edge of its cap.
        Vector3 cap = Vector3.Zero;
        bool concaveFork = customOutline?.IsDeepFork == true;
        if (!concaveFork && customOutline is UnitOutline outline)
            cap.Z = outline.IsDreadnaught ? 0f : outline.IsPike ? radiusZ * 0.32f : -radiusZ * 0.44f;
        for (int i = 0; i < sides; i++)
        {
            float angle = rotation + i * Mathf.Tau / sides;
            float x = Mathf.Cos(angle) * radiusX, z = Mathf.Sin(angle) * radiusZ;
            if (customOutline.HasValue)
            {
                Vector2 vertex = customOutline.Value.Vertex(i);
                x = vertex.X;
                z = vertex.Y;
            }
            if (yaw != 0f)
            {
                Vector2 rotated = new Vector2(x, z).Rotated(yaw);
                x = rotated.X;
                z = rotated.Y;
            }
            top[i] = center + new Vector3(Mathf.Lerp(x, cap.X, insetX), half, Mathf.Lerp(z, cap.Z, insetZ));
            shoulder[i] = center + new Vector3(x, half - bevelRise, z);
            bottom[i] = center + new Vector3(x, -half, z);
        }

        if (concaveFork) AppendForkCaps(surface, top, bottom, tint);
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 edge = shoulder[next] - shoulder[i];
            Vector3 outward = new Vector3(edge.Z, 0f, -edge.X).Normalized();

            if (rimmedCap)
            {
                Vector3 capCenter = center + Vector3.Up * half;
                Vector3 recess = Vector3.Down * (height * 0.035f);
                Vector3 inner = capCenter.Lerp(top[i], 0.84f) + recess;
                Vector3 innerNext = capCenter.Lerp(top[next], 0.84f) + recess;
                AddQuad(surface, inner, innerNext, top[next], top[i], Vector3.Up, Colors.White * tint);
                AddTriangle(surface, capCenter + recess, innerNext, inner, Vector3.Up, tint * 0.68f);
            }
            else if (!concaveFork)
                AddTriangle(surface, center + cap + Vector3.Up * half, top[next], top[i], Vector3.Up, TopTint * tint * topCapBrightness);
            if (!concaveFork) AddTriangle(surface, center + cap + Vector3.Down * half, bottom[i], bottom[next], Vector3.Down, BottomTint * tint);

            AddQuad(surface, bottom[i], shoulder[i], shoulder[next], bottom[next],
                outward, SideTint(outward) * tint * sideShade);
            Vector3 bevelNormal = (outward * bevelRise + Vector3.Up * insetDistance).Normalized();
            AddQuad(surface, shoulder[i], top[i], top[next], shoulder[next],
                bevelNormal, BevelTint(outward) * tint * Mathf.Lerp(1f, sideShade, 0.5f));
        }
    }

    // Triangulate the concave cap once per shared mesh so the notch stays open.
    private static void AppendForkCaps(SurfaceTool surface, Vector3[] top, Vector3[] bottom, Color tint)
    {
        var outline = new Vector2[bottom.Length];
        for (int i = 0; i < outline.Length; i++) outline[i] = new Vector2(bottom[i].X, bottom[i].Z);
        int[] triangles = Geometry2D.TriangulatePolygon(outline);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if ((top[b] - top[a]).Cross(top[c] - top[a]).Y < 0f) (b, c) = (c, b);
            AddTriangle(surface, top[a], top[b], top[c], Vector3.Up, tint);
            AddTriangle(surface, bottom[a], bottom[c], bottom[b], Vector3.Down, BottomTint * tint);
        }
    }

    // =========================================================
    private static float SideBrightness(Vector3 outward)
    {
        // Fixed fake northwest sun, with no run-time lighting or per-instance material cost.
        return Mathf.Clamp(0.73f - 0.10f * outward.X - 0.12f * outward.Z, 0.58f, 0.86f);
    }

    private static Color SideTint(Vector3 outward)
    {
        float brightness = SideBrightness(outward);
        return new Color(brightness, brightness, brightness);
    }

    private static Color BevelTint(Vector3 outward)
    {
        // Extra intermediate tone makes the top chamfer read as a softened edge.
        float brightness = Mathf.Lerp(SideBrightness(outward), 1f, 0.58f);
        return new Color(brightness, brightness, brightness);
    }

    private static void AddQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 normal, Color tint)
    {
        AddTriangle(surface, a, b, c, normal, tint);
        AddTriangle(surface, a, c, d, normal, tint);
    }

    private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c,
        Vector3 normal, Color tint)
    {
        surface.SetNormal(normal);
        surface.SetColor(tint);
        // Preserve the project's existing Godot clockwise front-face convention.
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
    }
}
