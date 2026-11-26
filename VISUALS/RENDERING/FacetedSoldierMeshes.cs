// Creates shared beveled low-poly soldier meshes with baked top, chamfer and side tones.
// Shapes remain compatible with the shared MultiMesh color and fracture data.
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
        var crackShader = visuals.UseUnitFracture || enhanced || visuals.UseCircleRobotMesh || visuals.UseSquareHeavyMesh || visuals.UseHexagonSiegeRobotMesh || visuals.UseTriangleArcherMesh || visuals.UsePentagonEliteMesh || visuals.UseDiamondCavalryMesh || visuals.UseHBreacherMesh
            ? GD.Load<Shader>("res://VISUALS/FEEDBACK/UNIT_HITS/UnitCracks.gdshader") : null;
        var crackMask = visuals.UseUnitFracture
            ? GD.Load<Texture2D>("res://VISUALS/FEEDBACK/UNIT_HITS/UnitCrackMask.svg") : null;
        var meshes = new Mesh[catalog.Count];
        for (int i = 0; i < meshes.Length; i++)
        {
            ref readonly UnitStats type = ref catalog[i];
            UnitOutline outline = type.Outline;
            Material unitMaterial = material;
            bool twoToneArmor = UsesTwoToneArmor(type.Id, outline, visuals);
            if (crackShader != null && (crackMask != null || twoToneArmor))
            {
                var cracked = new ShaderMaterial { Shader = crackShader };
                cracked.SetShaderParameter("show_fracture", crackMask != null);
                cracked.SetShaderParameter("use_two_tone_armor", twoToneArmor);
                ConfigureInfantryAnimation(cracked, visuals, type.Id, type.Height);
                if (crackMask != null) cracked.SetShaderParameter("crack_mask", crackMask);
                cracked.SetShaderParameter("half_extent", new Vector2(outline.RadiusX, outline.RadiusZ));
                cracked.SetShaderParameter("darkness", Mathf.Clamp(visuals.CrackDarkness, 0f, 1f));
                unitMaterial = cracked;
            }
            meshes[i] = CreateUnitMesh(outline, type.Height, unitMaterial,
                enhanced && type.Id == RuntimeUnitCatalog.CircleId,
                visuals.UseEnhancedHexagonMesh && type.Id == RuntimeUnitCatalog.HexagonId,
                catalog.Definition(i), includeIndependent: false, twoToneArmor: twoToneArmor);
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
        UnitOutline outline = UnitOutline.FromDefinition(unit, size);
        bool twoToneArmor = UsesTwoToneArmor(unit.Id, outline, visuals);
        Material material = new StandardMaterial3D { AlbedoColor = tint, VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        if (twoToneArmor)
        {
            var armor = new ShaderMaterial { Shader = GD.Load<Shader>("res://VISUALS/FEEDBACK/UNIT_HITS/UnitCracks.gdshader") };
            armor.SetShaderParameter("show_fracture", false);
            armor.SetShaderParameter("use_two_tone_armor", true);
            armor.SetShaderParameter("armor_tint", tint);
            material = armor;
        }
        return CreateUnitMesh(outline, Mathf.Max(0.01f, unit.Height) * size, material,
            visuals.UseEnhancedMeshes && unit.Id == RuntimeUnitCatalog.CircleId,
            visuals.UseEnhancedHexagonMesh && unit.Id == RuntimeUnitCatalog.HexagonId,
            unit, includeIndependent: true, twoToneArmor: twoToneArmor);
    }

    // Shared by the lit and unlit battle materials. Placement previews remain at rest.
    internal static void ConfigureInfantryAnimation(ShaderMaterial material, UnitVisualConfig visuals, string id, float height)
    {
        bool circle = visuals.UseCircleRobotMesh && id == RuntimeUnitCatalog.CircleId;
        bool square = visuals.UseSquareHeavyMesh && id == RuntimeUnitCatalog.SquareId;
        bool siege = visuals.UseHexagonSiegeRobotMesh && id == RuntimeUnitCatalog.HexagonId;
        bool archer = visuals.UseTriangleArcherMesh && id == RuntimeUnitCatalog.TriangleId;
        bool elite = visuals.UsePentagonEliteMesh && id == RuntimeUnitCatalog.PentagonId;
        bool cavalry = visuals.UseDiamondCavalryMesh && id == RuntimeUnitCatalog.DiamondId;
        bool breacher = visuals.UseHBreacherMesh && id == RuntimeUnitCatalog.BreacherId;
        material.SetShaderParameter("infantry_attack", (circle && visuals.AnimateCircleSword) ||
            (square && visuals.AnimateSquareAttack) || (siege && visuals.AnimateHexagonAttack) ||
            (archer && visuals.AnimateTriangleFire) || (elite && visuals.AnimatePentagonAttack) ||
            (cavalry && visuals.AnimateDiamondAttack) || (breacher && visuals.AnimateHBreacherAttack));
        material.SetShaderParameter("infantry_walk", (circle && visuals.AnimateCircleWalk) ||
            (square && visuals.AnimateSquareWalk) || (siege && visuals.AnimateHexagonWalk) ||
            (archer && visuals.AnimateTriangleWalk) || (elite && visuals.AnimatePentagonWalk) ||
            (cavalry && visuals.AnimateDiamondGallop) || (breacher && visuals.AnimateHBreacherWalk));
        material.SetShaderParameter("h_breacher", breacher);
        material.SetShaderParameter("diamond_cavalry", cavalry);
        material.SetShaderParameter("pentagon_elite", elite);
        material.SetShaderParameter("triangle_archer", archer);
        material.SetShaderParameter("square_heavy", square);
        material.SetShaderParameter("hexagon_siege", siege);
        material.SetShaderParameter("infantry_height", height);
    }

    internal static bool UsesTwoToneArmor(string id, UnitOutline outline, UnitVisualConfig visuals) =>
        (visuals.UseHBreacherMesh && id == RuntimeUnitCatalog.BreacherId) ||
        (visuals.UseDiamondCavalryMesh && id == RuntimeUnitCatalog.DiamondId) ||
        (visuals.UsePentagonEliteMesh && id == RuntimeUnitCatalog.PentagonId) ||
        (visuals.UseTriangleArcherMesh && id == RuntimeUnitCatalog.TriangleId) ||
        (visuals.UseHexagonSiegeRobotMesh && id == RuntimeUnitCatalog.HexagonId) ||
        (visuals.UseSquareHeavyMesh && id == RuntimeUnitCatalog.SquareId) ||
        (visuals.UseCircleRobotMesh && id == RuntimeUnitCatalog.CircleId) ||
        (visuals.UseEnhancedMeshes && ((id == RuntimeUnitCatalog.ChevronId && outline.IsChevron) ||
            (id == RuntimeUnitCatalog.TwinRocketLauncherId && outline.IsDeepFork)));

    private static ArrayMesh CreateUnitMesh(UnitOutline outline, float height, Material material,
        bool enhancedCircle, bool enhancedHexagon, UnitDefinition unit, bool includeIndependent, bool twoToneArmor)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);

        bool enhancedRocket = twoToneArmor && unit.Id == RuntimeUnitCatalog.TwinRocketLauncherId;
        // Masked surfaces use UVs for material kind/brightness. Optional legacy mounts
        // are emitted first so their zero UV remains intact on a mixed surface.
        if (twoToneArmor) surface.SetUV(Vector2.Zero);
        foreach (var mount in unit.RocketMounts ?? new())
        {
            if (mount == null || (mount.IndependentAim && (!includeIndependent || mount.Projectile == null))) continue;
            Transform3D pose = MountedRocket.LocalPose(mount, UnitOutline.EffectiveSize(unit));
            if (enhancedRocket && !mount.IndependentAim && !mount.TwinMinigunModel)
                AppendEnhancedRocketBarrel(surface, pose);
            else
            {
                if (twoToneArmor) surface.SetUV(Vector2.Zero);
                AppendRocketBarrel(surface, pose);
            }
        }

        if (twoToneArmor)
        {
            if (unit.Id == RuntimeUnitCatalog.CircleId) AppendCircleRobot(surface, outline, height);
            else if (unit.Id == RuntimeUnitCatalog.SquareId) AppendSquareHeavy(surface, outline, height);
            else if (unit.Id == RuntimeUnitCatalog.TriangleId) AppendTriangleArcher(surface, outline, height);
            else if (unit.Id == RuntimeUnitCatalog.PentagonId) AppendPentagonElite(surface, outline, height);
            else if (unit.Id == RuntimeUnitCatalog.DiamondId) AppendDiamondCavalry(surface, outline, height);
            else if (unit.Id == RuntimeUnitCatalog.HexagonId) AppendHexagonSiegeRobot(surface, outline, height);
            else if (unit.Id == RuntimeUnitCatalog.BreacherId) AppendHBreacher(surface, outline, height);
            else if (enhancedRocket) AppendTwinRocketArmor(surface, outline, height);
            else AppendLightArtilleryArmor(surface, outline, height);
            return surface.Commit();
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
        if (unit.Shape == UnitShape.Obliterator)
        {
            // A bright raised keel follows the tapered nose of the hull.
            AppendPrism(surface, 4, outline.RadiusX * 0.34f, outline.RadiusZ * 0.72f,
                height * 0.28f, 0f, new Vector3(0f, height * 0.57f, -outline.RadiusZ * 0.10f),
                new Color(0.65f, 0.91f, 1f));
        }
        if (outline.IsDreadnaught)
            AppendDreadnaughtDetails(surface, outline, height);
        if (outline.IsChevron)
        {
            Vector3 center = (outline.ArtilleryBarrelMount(height) + outline.ArtilleryBarrelTip(height)) * 0.5f;
            AppendBeveledRectangle(surface, center,
                new Vector3(outline.BarrelWidth, height * 0.24f, outline.BarrelLength),
                new Color(0.86f, 0.86f, 0.86f), SoldierSideShade, pitch: outline.ChevronBarrelElevation);
        }
        return surface.Commit();
    }

    // =========================================================
    // Experimental rigid infantry robot. All pieces share the same masked surface.
    // Preserve the old radius and ground-anchored height; -Z is forward.
    private static void AppendCircleRobot(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ;
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Block(float px, float py, float pz, float w, float h, float d, Color mask, bool swing = false, bool foot = false)
        {
            const float diagonal = 0.70710678f;
            AppendPrism(surface, 4, w * x * diagonal, d * z * diagonal, h * height,
                Mathf.Pi * 0.25f, new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, armorMask: true, swingPart: swing, walkPart: foot);
        }
        void Ring(float rx, float rz, float y, float h, Color mask)
        {
            AppendPrism(surface, 10, x * rx, z * rz, height * h, -Mathf.Pi * 0.5f,
                Vector3.Up * (height * y), mask, sideShade: SoldierSideShade, armorMask: true);
        }

        // Low round armored torso and oversized cylindrical helmet.
        Ring(0.53f, 0.56f, -0.07f, 0.56f, Paint(0.85f));
        Ring(0.60f, 0.60f, 0.24f, 0.15f, Trim(0.95f));
        Ring(0.62f, 0.62f, 0.40f, 0.20f, Paint(1f));

        // Chunky shoulders and boots; keep every corner within the old footprint.
        for (int side = -1; side <= 1; side += 2)
        {
            Block(side * 0.69f, 0.04f, 0.02f, 0.34f, 0.37f, 0.48f, Paint(0.92f));
            if (side > 0)
                Block(0.65f, -0.18f, -0.13f, 0.25f, 0.22f, 0.48f, Steel(0.95f), swing: true);
            else
                Block(-0.65f, -0.18f, -0.05f, 0.25f, 0.22f, 0.32f, Steel(0.95f));
            Block(side * 0.27f, -0.38f, -0.10f, 0.36f, 0.24f, 0.50f, Steel(1f), foot: true);
        }
        // Right forearm and blade share a vertex-shader pivot; chest stays rigid.
        Block(0f, -0.09f, -0.51f, 0.35f, 0.15f, 0.13f, Trim(0.90f));
        Block(0.59f, -0.21f, -0.48f, 0.14f, 0.15f, 0.33f, Steel(1f), swing: true);
        Block(0.53f, -0.20f, -0.68f, 0.14f, 0.14f, 0.20f, Trim(1f), swing: true);
    }

    // =========================================================
    // Light ranged infantry: tapered triangular armor with a central laser carbine.
    // -Z is forward. Keep the muzzle on the existing shot's center-height axis.
    // All rest geometry fits inside the original triangle; no gameplay muzzle change.
    private static void AppendTriangleArcher(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ;
        const float diagonal = 0.70710678f;
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Block(float px, float py, float pz, float w, float h, float d, Color mask,
            bool recoil = false, bool foot = false)
        {
            AppendPrism(surface, 4, w * x * diagonal, d * z * diagonal, h * height,
                Mathf.Pi * 0.25f, new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, armorMask: true, swingPart: recoil, walkPart: foot);
        }
        void Wedge(float py, float pz, float radius, float h, Color mask)
        {
            AppendPrism(surface, 3, x * radius, z * radius, h * height, outline.Rotation,
                new Vector3(0f, py * height, pz * z), mask,
                sideShade: SoldierSideShade, armorMask: true);
        }

        // Narrow underframe, swept faction shell, and pointed helmet.
        Wedge(-0.14f, 0f, 0.70f, 0.30f, Steel(0.95f));
        Wedge(0.08f, 0f, 0.88f, 0.32f, Paint(0.93f));
        Wedge(0.36f, 0.13f, 0.49f, 0.28f, Paint(1f));
        Block(0f, 0.30f, -0.21f, 0.14f, 0.09f, 0.14f, Trim(0.95f));

        // Slim articulated boots and compact rear shoulder plates.
        for (int side = -1; side <= 1; side += 2)
        {
            Block(side * 0.27f, -0.36f, 0.17f, 0.22f, 0.28f, 0.34f, Steel(0.95f), foot: true);
            Block(side * 0.43f, 0.03f, 0.23f, 0.25f, 0.25f, 0.29f, Paint(0.88f));
        }
        Block(0f, 0.02f, 0.34f, 0.28f, 0.24f, 0.20f, Steel(0.94f));
        Block(0f, 0.16f, 0.35f, 0.17f, 0.05f, 0.14f, Trim(0.86f));

        // Centered gun: fixed receiver and moving barrel/breech, all on one surface.
        // The tagged barrel slides backward on fire, without rotating.
        Block(0f, 0f, -0.30f, 0.28f, 0.24f, 0.42f, Steel(1f));
        Block(0f, 0f, -0.50f, 0.23f, 0.16f, 0.42f, Paint(0.96f), recoil: true);
        Block(0f, 0f, -0.75f, 0.12f, 0.12f, 0.28f, Steel(1f), recoil: true);
        Block(0f, 0f, -0.86f, 0.10f, 0.10f, 0.06f, Trim(1f), recoil: true);
    }

    // =========================================================
    // Compact demolition walker. Keep both ends of the H open: two side
    // housings connected by a central crossbar, with twin sliding rams (-Z).
    private static void AppendHBreacher(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ;
        const float diagonal = 0.70710678f;
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Block(float px, float py, float pz, float w, float h, float d, Color mask,
            bool ram = false, bool foot = false)
        {
            AppendPrism(surface, 4, w * x * diagonal, d * z * diagonal, h * height,
                Mathf.Pi * 0.25f, new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, armorMask: true, swingPart: ram, walkPart: foot);
        }

        // Low crossbeam and angular command housing; no bridge across the open ends.
        Block(0f, -0.07f, 0f, 1.48f, 0.46f, 0.44f, Steel(0.96f));
        Block(0f, 0.21f, 0f, 0.86f, 0.38f, 0.42f, Paint(0.94f));
        Block(0f, 0.435f, 0.01f, 0.54f, 0.13f, 0.33f, Paint(1f));
        Block(0f, 0.28f, -0.205f, 0.48f, 0.075f, 0.025f, Trim(0.96f));

        for (int side = -1; side <= 1; side += 2)
        {
            float px = side * 0.67f;
            // Separate armored drive pods and rear power cells.
            Block(px, -0.09f, 0.09f, 0.64f, 0.48f, 1.46f, Steel(0.93f));
            Block(px, 0.22f, 0.15f, 0.62f, 0.46f, 1.34f, Paint(0.94f));
            Block(px, 0.405f, 0.45f, 0.40f, 0.14f, 0.48f, Paint(0.99f));
            Block(px, 0.18f, 0.815f, 0.38f, 0.12f, 0.03f, Trim(0.83f));

            // Fixed hydraulic casing and moving shaft. The shaft overlaps
            // the casing even at full extension; ram faces never close the notch.
            Block(px, 0.08f, -0.42f, 0.52f, 0.43f, 0.50f, Steel(0.97f));
            Block(px, 0.08f, -0.57f, 0.29f, 0.23f, 0.55f, Steel(1f), ram: true);
            Block(px, 0.08f, -0.74f, 0.59f, 0.46f, 0.20f, Paint(0.98f), ram: true);
            Block(px, 0.08f, -0.84f, 0.46f, 0.30f, 0.06f, Steel(1f), ram: true);
            Block(px, 0.23f, -0.79f, 0.35f, 0.055f, 0.13f, Trim(0.93f), ram: true);

            // Four low ground-contact pads. Fore/hind and side signs select
            // diagonal pairs in the shared shader without extra mesh attributes.
            for (int end = -1; end <= 1; end += 2)
            {
                Block(px, -0.31f, end * 0.38f, 0.34f, 0.28f, 0.30f, Steel(0.94f), foot: true);
                Block(px, -0.42f, end * 0.38f, 0.55f, 0.16f, 0.38f, Steel(1f), foot: true);
            }
        }
    }

    // =========================================================
    // Fast mechanical lancer. Diamond's long axis and forward direction are +X.
    // Low, four-legged mount with a pointed rider pod and centerline energy lance.
    // Coordinates are normalized to the existing elongated rhombus and height.
    private static void AppendDiamondCavalry(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ;
        const float diagonal = 0.70710678f;
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Block(float px, float py, float pz, float length, float h, float width, Color mask,
            bool thrust = false, bool foot = false)
        {
            AppendPrism(surface, 4, length * x * diagonal, width * z * diagonal, h * height,
                Mathf.Pi * 0.25f, new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, armorMask: true, swingPart: thrust, walkPart: foot);
        }
        void Diamond(float px, float py, float rx, float rz, float h, Color mask)
        {
            AppendPrism(surface, 4, x * rx, z * rz, height * h, 0f,
                new Vector3(px * x, py * height, 0f), mask,
                sideShade: SoldierSideShade, armorMask: true);
        }

        // Narrow undercarriage and long, swept faction armor with a tapered rear.
        Diamond(-0.10f, -0.08f, 0.69f, 0.72f, 0.26f, Steel(0.95f));
        Diamond(-0.09f, 0.09f, 0.77f, 0.88f, 0.30f, Paint(0.94f));
        Diamond(-0.13f, 0.27f, 0.48f, 0.52f, 0.16f, Paint(0.98f));
        Diamond(-0.06f, 0.40f, 0.23f, 0.28f, 0.20f, Paint(1f));
        Block(0.13f, 0.39f, 0f, 0.055f, 0.065f, 0.23f, Trim(0.98f));

        // Four independent-looking piston legs. All four use the existing foot tag;
        // the shader distinguishes fore/hind and left/right by the local vertex signs.
        for (int front = -1; front <= 1; front += 2)
        for (int side = -1; side <= 1; side += 2)
        {
            Block(front * 0.30f, -0.09f, side * 0.37f, 0.22f, 0.26f, 0.22f, Steel(0.95f));
            Block(front * 0.30f, -0.28f, side * 0.37f, 0.12f, 0.34f, 0.15f, Steel(0.99f), foot: true);
            Block(front * 0.30f, -0.42f, side * 0.37f, 0.23f, 0.16f, 0.23f, Paint(0.93f), foot: true);
        }
        // Rear propulsion pack and faction energy vents (no extra particles).
        Block(-0.43f, 0.12f, 0f, 0.25f, 0.20f, 0.34f, Steel(0.91f));
        Block(-0.54f, 0.21f, 0f, 0.08f, 0.045f, 0.24f, Trim(0.85f));

        // A central lance slides along +X on actual melee/trample contact.
        // The receiver overlaps the tagged moving haft throughout its travel.
        Block(0.29f, 0.15f, 0f, 0.30f, 0.21f, 0.22f, Steel(1f));
        Block(0.46f, 0.15f, 0f, 0.40f, 0.11f, 0.10f, Paint(0.97f), thrust: true);
        AppendPrism(surface, 4, x * 0.18f, z * 0.085f, height * 0.10f, 0f,
            new Vector3(x * 0.72f, height * 0.15f, 0f), Trim(1f),
            sideShade: SoldierSideShade, armorMask: true, swingPart: true);
    }

    // =========================================================
    // Elite assault infantry. Five-sided chest, angular pauldrons and
    // a broad energy axe; retain the original radius, height and -Z facing.
    private static void AppendPentagonElite(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ;
        const float diagonal = 0.70710678f;
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Block(float px, float py, float pz, float w, float h, float d, Color mask,
            bool swing = false, bool foot = false, float yaw = 0f)
        {
            AppendPrism(surface, 4, w * x * diagonal, d * z * diagonal, h * height,
                Mathf.Pi * 0.25f, new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, yaw: yaw, armorMask: true, swingPart: swing, walkPart: foot);
        }
        void Plate(float px, float py, float pz, float radius, float h, Color mask, float yaw = 0f)
        {
            AppendPrism(surface, 5, x * radius, z * radius, height * h, outline.Rotation,
                new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, yaw: yaw, armorMask: true);
        }

        // Layered pentagonal cuirass and compact pointed command helmet.
        Plate(0f, -0.17f, 0.08f, 0.61f, 0.30f, Steel(0.95f));
        Plate(0f, 0.06f, 0.06f, 0.73f, 0.38f, Paint(0.91f));
        Plate(0f, 0.25f, 0.10f, 0.49f, 0.12f, Paint(0.98f));
        Plate(0f, 0.38f, 0.13f, 0.31f, 0.24f, Paint(1f));
        Block(0f, 0.36f, -0.105f, 0.16f, 0.07f, 0.09f, Trim(0.98f));
        Block(0f, 0.09f, -0.56f, 0.16f, 0.12f, 0.10f, Trim(0.86f));

        // Swept angular shoulders and plated mechanical legs.
        for (int side = -1; side <= 1; side += 2)
        {
            Plate(side * 0.49f, 0.11f, 0.08f, 0.29f, 0.32f, Paint(0.97f), side * 0.30f);
            Block(side * 0.32f, -0.31f, 0.06f, 0.31f, 0.38f, 0.42f, Steel(0.94f), foot: true);
            Block(side * 0.32f, -0.40f, -0.01f, 0.40f, 0.20f, 0.58f, Paint(0.92f), foot: true);
        }
        Block(-0.54f, -0.08f, -0.06f, 0.28f, 0.26f, 0.34f, Steel(0.96f));
        Plate(0f, 0.12f, 0.43f, 0.26f, 0.24f, Steel(0.91f));
        Block(0f, 0.26f, 0.43f, 0.25f, 0.05f, 0.16f, Trim(0.85f));

        // Tagged axe and mechanical arm share a shoulder pivot at (0.48, 0.04).
        // A short diagonal haft overlaps the arm and keeps the axe assembled.
        Block(0.46f, 0.16f, -0.09f, 0.22f, 0.20f, 0.34f, Steel(1f), swing: true, yaw: -0.25f);
        Block(0.34f, 0.24f, -0.29f, 0.12f, 0.12f, 0.59f, Steel(0.98f), swing: true, yaw: -0.42f);
        // Faceted axe head and raised cutting edge, visibly different from Square's hammer.
        Vector2[] axe = { new(-0.85f, -0.70f), new(0.76f, -0.90f), new(1f, -0.22f),
            new(0.86f, 0.58f), new(-0.25f, 0.82f), new(-0.70f, 0.26f) };
        AppendPrism(surface, axe.Length, x * 0.23f, z * 0.18f, height * 0.15f, 0f,
            new Vector3(x * 0.22f, height * 0.24f, -z * 0.55f), Paint(0.97f),
            sideShade: SoldierSideShade, yaw: -0.20f, armorMask: true, visualOutline: axe, swingPart: true);
        Block(0.37f, 0.24f, -0.58f, 0.075f, 0.13f, 0.25f, Trim(1f), swing: true, yaw: -0.20f);
    }

    // =========================================================
    // Strong infantry: broad squared armor, a shield and a short impact hammer.
    // The outline stores the square's corner radius, not its half-width.
    // All rest-pose detail fits inside the original square and height.
    private static void AppendSquareHeavy(SurfaceTool surface, UnitOutline outline, float height)
    {
        const float diagonal = 0.70710678f;
        float x = outline.RadiusX * diagonal, z = outline.RadiusZ * diagonal;
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Block(float px, float py, float pz, float w, float h, float d, Color mask,
            bool swing = false, bool foot = false)
        {
            AppendPrism(surface, 4, w * x * diagonal, d * z * diagonal, h * height,
                Mathf.Pi * 0.25f, new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, armorMask: true, swingPart: swing, walkPart: foot);
        }

        // A compact square helmet and broad layered faction-colored torso.
        Block(0f, -0.15f, 0.10f, 1.04f, 0.36f, 1.05f, Steel(0.93f));
        Block(0f, 0.04f, 0.08f, 1.22f, 0.43f, 1.20f, Paint(0.87f));
        Block(0f, 0.36f, -0.08f, 0.82f, 0.28f, 0.86f, Paint(1f));
        Block(0f, 0.30f, -0.52f, 0.67f, 0.09f, 0.07f, Trim(0.96f));
        Block(0f, 0.08f, 0.70f, 0.76f, 0.24f, 0.23f, Paint(0.77f));

        for (int side = -1; side <= 1; side += 2)
        {
            Block(side * 0.75f, 0.07f, 0.08f, 0.46f, 0.38f, 0.78f, Paint(0.96f));
            Block(side * 0.33f, -0.34f, 0.02f, 0.52f, 0.32f, 0.70f, Steel(0.95f), foot: true);
            Block(side * 0.33f, -0.24f, -0.02f, 0.48f, 0.12f, 0.66f, Paint(0.81f), foot: true);
        }

        // A tall, beveled left shield, with a recessed-looking dark rim.
        Block(-0.62f, -0.11f, -0.40f, 0.35f, 0.28f, 0.57f, Steel(0.98f));
        Block(-0.49f, -0.02f, -0.80f, 0.90f, 0.66f, 0.27f, Steel(0.98f));
        Block(-0.49f, 0.01f, -0.93f, 0.73f, 0.53f, 0.12f, Paint(0.91f));
        Block(-0.49f, 0.15f, -0.986f, 0.42f, 0.07f, 0.028f, Trim(0.90f));

        // Right arm, handle and impact head share a single shader pivot.
        Block(0.70f, -0.17f, -0.17f, 0.32f, 0.25f, 0.56f, Steel(1f), swing: true);
        Block(0.69f, -0.16f, -0.54f, 0.14f, 0.15f, 0.64f, Steel(0.95f), swing: true);
        Block(0.69f, -0.14f, -0.78f, 0.53f, 0.31f, 0.34f, Paint(0.95f), swing: true);
        Block(0.69f, -0.14f, -0.94f, 0.35f, 0.18f, 0.10f, Trim(0.97f), swing: true);
    }

    // =========================================================
    // Fast mobile rocket platform: keep the open fork silhouette and unchanged
    // circular gameplay footprint. All armor is baked into the existing body mesh.
    private static void AppendTwinRocketArmor(SurfaceTool surface, UnitOutline outline, float height)
    {
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        float x = outline.RadiusX, z = outline.RadiusZ;
        // Render-only outline: slimmer forward tips, swept shoulders and a
        // deeper central notch. Keep the compiled UnitOutline/collision intact.
        Vector2[] silhouette =
        {
            new(0.46f, -1f), new(0.78f, -1f), new(0.96f, -0.62f), new(1f, 0.40f),
            new(0.70f, 1f), new(-0.70f, 1f), new(-1f, 0.40f), new(-0.96f, -0.62f),
            new(-0.78f, -1f), new(-0.46f, -1f), new(-0.31f, 0.28f), new(0.31f, 0.28f)
        };
        // Low gunmetal underframe, with a larger faction-colored upper hull.
        AppendPrism(surface, outline.Sides, x, z, height * 0.30f, outline.Rotation,
            Vector3.Down * height * 0.35f, Steel(0.78f), customOutline: outline,
            sideShade: SoldierSideShade, armorMask: true, visualOutline: silhouette);
        AppendPrism(surface, outline.Sides, x, z, height * 0.70f, outline.Rotation,
            Vector3.Up * height * 0.15f, Paint(0.91f), customOutline: outline,
            sideShade: SoldierSideShade, armorMask: true, visualOutline: silhouette);

        void Box(Vector3 center, Vector3 size, Color mask, float yaw = 0f)
        {
            const float k = 0.70710678f;
            AppendPrism(surface, 4, size.X * k, size.Z * k, size.Y, Mathf.Pi * 0.25f,
                center, mask, 0.16f, sideShade: SoldierSideShade, yaw: yaw, armorMask: true);
        }

        // Angular raised rear deck behind the notch; no crossbar over the opening.
        Box(new Vector3(0f, height * 0.53f, z * 0.65f),
            new Vector3(x * 1.22f, height * 0.20f, z * 0.54f), Steel(0.87f));
        Box(new Vector3(0f, height * 0.67f, z * 0.68f),
            new Vector3(x * 0.76f, height * 0.12f, z * 0.38f), Paint(1f));
        Box(new Vector3(0f, height * 0.743f, z * 0.68f),
            new Vector3(x * 0.40f, height * 0.035f, z * 0.11f), Trim(0.85f));

        for (int side = -1; side <= 1; side += 2)
        {
            // Mirrored swept chassis rails echo the tapered outline; actual
            // launchers remain aligned with their independent authored poses.
            float sweep = -side * Mathf.DegToRad(7f);
            Box(new Vector3(side * x * 0.69f, height * 0.47f, z * 0.03f),
                new Vector3(x * 0.34f, height * 0.18f, z * 1.38f), Steel(0.90f), sweep);
            Box(new Vector3(side * x * 0.70f, height * 0.59f, z * 0.37f),
                new Vector3(x * 0.34f, height * 0.15f, z * 0.57f), Paint(0.98f), sweep);
            // One readable dark vent per shoulder, rather than many tiny details.
            Box(new Vector3(side * x * 0.70f, height * 0.674f, z * 0.45f),
                new Vector3(x * 0.20f, height * 0.025f, z * 0.13f), Steel(0.26f), sweep);
        }
    }

    // =========================================================
    // Fixed rocket mount geometry follows the authored pose (including yaw/scale).
    // The rectangular muzzle collar is open at local -Z; the real shot origin
    // stays exactly at MountedRocket.Muzzle, not at a new decorative offset.
    private static void AppendEnhancedRocketBarrel(SurfaceTool surface, Transform3D pose)
    {
        const float k = 0.70710678f;
        Vector3 scale = new(pose.Basis.X.Length(), pose.Basis.Y.Length(), pose.Basis.Z.Length());
        float yaw = Mathf.Atan2(pose.Basis.X.Z, pose.Basis.X.X);
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Box(Vector3 center, Vector3 size, Color mask)
        {
            Vector3 dimensions = size * scale;
            AppendPrism(surface, 4, dimensions.X * k, dimensions.Z * k,
                dimensions.Y, Mathf.Pi * 0.25f, pose * center, mask,
                0.16f, sideShade: SoldierSideShade, yaw: yaw, armorMask: true);
        }

        Box(new Vector3(0f, 0f, -0.44f), new Vector3(0.83f, 0.90f, 0.88f), Steel(0.90f));
        Box(new Vector3(0f, 0f, -0.19f), new Vector3(1.12f, 1.16f, 0.38f), Paint(0.96f));
        Box(new Vector3(0f, 0.49f, -0.57f), new Vector3(0.31f, 0.09f, 0.31f), Trim(0.90f));
        // Four tapered rim faces and inner walls: one small open muzzle, no extra
        // nodes, and no geometry beyond the original local z = -1 endpoint.
        Vector3 Point(float x, float y, float z) => pose * new Vector3(x, y, z);
        Vector3 Normal(Vector3 normal) => (pose.Basis.Inverse().Transposed() * normal).Normalized();
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color mask) =>
            AddQuad(surface, a, b, c, d, Normal(n), mask, armorMask: true);
        const float outerX = 0.55f, outerY = 0.60f, innerX = 0.30f, innerY = 0.33f;
        // Clockwise as viewed from outside, after AddQuad's winding reversal.
        Vector2[] outer = { new(-outerX, -outerY), new(-outerX, outerY), new(outerX, outerY), new(outerX, -outerY) };
        Vector2[] inner = { new(-innerX, -innerY), new(-innerX, innerY), new(innerX, innerY), new(innerX, -innerY) };
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            Vector2 a = outer[i], b = outer[next], c = inner[i], d = inner[next];
            Vector3 frontA = Point(a.X * 0.89f, a.Y * 0.87f, -1f),
                frontB = Point(b.X * 0.89f, b.Y * 0.87f, -1f);
            Vector3 backA = Point(a.X, a.Y, -0.78f), backB = Point(b.X, b.Y, -0.78f);
            Vector3 holeA = Point(c.X, c.Y, -1f), holeB = Point(d.X, d.Y, -1f);
            Vector3 deepA = Point(c.X, c.Y, -0.87f), deepB = Point(d.X, d.Y, -0.87f);
            Vector2 outward = (a + b).Normalized();
            Vector3 n = new(outward.X, outward.Y, 0f);
            Face(frontA, frontB, holeB, holeA, Vector3.Forward, Paint(0.91f));
            // Derive the sloped outer-wall normal in mount-local space.
            Vector3 slopedNormal = new(n.X, n.Y, -0.28f);
            Face(backA, backB, frontB, frontA, slopedNormal.Normalized(), Steel(0.75f));
            Face(holeA, holeB, deepB, deepA, -n, Steel(0.27f));
            Face(backB, backA, Point(c.X, c.Y, -0.78f), Point(d.X, d.Y, -0.78f),
                Vector3.Back, Steel(0.53f));
        }
    }

    // =========================================================
    // Compact light artillery: retain the original chevron silhouette and exact
    // authored barrel axis, but add a shallow layered hull and selective armor.
    // One shared triangle surface; no independently animated parts or nodes.
    private static void AppendLightArtilleryArmor(SurfaceTool surface, UnitOutline outline, float height)
    {
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Box(Vector3 center, Vector3 size, Color mask, float pitch = 0f, float yaw = 0f)
        {
            const float inverseSqrtTwo = 0.70710678f;
            AppendPrism(surface, 4, size.X * inverseSqrtTwo, size.Z * inverseSqrtTwo,
                size.Y, Mathf.Pi * 0.25f, center, mask, 0.16f, sideShade: SoldierSideShade,
                yaw: yaw, pitch: pitch, armorMask: true);
        }

        float x = outline.RadiusX;
        // Faction-painted main hull distinguishes mobile units from neutral-metal buildings.
        // Keep the original ground contact and all existing dimensions.
        AppendPrism(surface, outline.Sides, x, outline.RadiusZ, height * 0.78f, outline.Rotation,
            Vector3.Down * height * 0.11f, Paint(0.86f), customOutline: outline,
            sideShade: SoldierSideShade, armorMask: true);
        float deck = height * 0.28f;
        // Swept shoulder pods run along the two original chevron arms.
        // Scale from the authored width so this respects size multiplier changes.
        for (int side = -1; side <= 1; side += 2)
        {
            float yaw = -side * Mathf.Pi * 0.25f;
            Box(new Vector3(side * x * 0.49f, deck + height * 0.06f, -x * 0.10f),
                new Vector3(x * 0.29f, height * 0.18f, x * 0.87f), Steel(1f), yaw: yaw);
            Box(new Vector3(side * x * 0.49f, deck + height * 0.18f, -x * 0.10f),
                new Vector3(x * 0.23f, height * 0.12f, x * 0.68f), Paint(0.94f), yaw: yaw);
            Box(new Vector3(side * x * 0.70f, deck + height * 0.12f, x * 0.13f),
                new Vector3(x * 0.17f, height * 0.09f, x * 0.13f), Trim(0.85f));
        }
        // Low central gun saddle, rather than a tall heavy turret.
        Vector3 mount = outline.ArtilleryBarrelMount(height);
        Vector3 tip = outline.ArtilleryBarrelTip(height);
        Vector3 axis = (tip - mount).Normalized();
        float length = outline.BarrelLength, width = outline.BarrelWidth;
        float pitch = outline.ChevronBarrelElevation;
        Box(new Vector3(0f, deck + height * 0.03f, mount.Z + x * 0.06f),
            new Vector3(x * 0.53f, height * 0.23f, x * 0.48f), Steel(0.98f));
        Box(mount + axis * (length * 0.49f),
            new Vector3(width, height * 0.24f, length * 0.98f), Steel(1f), pitch);
        Box(mount + axis * (length * 0.16f),
            new Vector3(width * 1.48f, height * 0.35f, length * 0.30f), Paint(0.77f), pitch);
        Vector3 top = new Basis(Vector3.Right, pitch) * Vector3.Up;
        Box(mount + axis * (length * 0.48f) + top * (height * 0.132f),
            new Vector3(width * 0.46f, height * 0.036f, length * 0.34f), Trim(0.90f), pitch);
        Box(mount + axis * (length * 0.86f),
            new Vector3(width * 1.35f, height * 0.32f, length * 0.20f), Paint(0.92f), pitch);
        // End face ends exactly at the existing muzzle; no launch-origin change.
        Box(tip - axis * (length * 0.012f),
            new Vector3(width * 0.74f, height * 0.18f, length * 0.024f), Steel(0.20f), pitch);
    }

    // =========================================================
    // Six beveled-look deck plates sit over a darkened cap. Built once per type
    // so the existing faction MultiMesh, FX2 cracks and hit colours still work.
    // All detail stays inside the original hexagonal footprint.
    // =========================================================
    // Tier-3 siege infantry: six-sided armored torso, two planted piston legs,
    // a recessed power core and a broad blade on a central rotary bearing.
    // Rest pose and blade orbit stay inside the original circular clearance.
    private static void AppendHexagonSiegeRobot(SurfaceTool surface, UnitOutline outline, float height)
    {
        float x = outline.RadiusX, z = outline.RadiusZ;
        const float diagonal = 0.70710678f;
        Color Steel(float shade) => new(shade, 0f, 0f);
        Color Paint(float shade) => new(0f, shade, 0f);
        Color Trim(float shade) => new(0f, 0f, shade);
        void Block(float px, float py, float pz, float w, float h, float d, Color mask,
            bool swing = false, bool foot = false, float yaw = 0f)
        {
            AppendPrism(surface, 4, w * x * diagonal, d * z * diagonal, h * height,
                Mathf.Pi * 0.25f, new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, yaw: yaw, armorMask: true, swingPart: swing, walkPart: foot);
        }
        void Hex(float px, float py, float pz, float radius, float h, Color mask, bool swing = false)
        {
            AppendPrism(surface, 6, x * radius, z * radius, height * h, outline.Rotation,
                new Vector3(px * x, py * height, pz * z), mask,
                sideShade: SoldierSideShade, armorMask: true, swingPart: swing);
        }

        Hex(0f, -0.19f, 0.08f, 0.60f, 0.30f, Steel(0.95f));
        Hex(0f, 0.02f, 0.07f, 0.66f, 0.34f, Paint(0.89f));
        Hex(0f, 0.20f, 0.07f, 0.56f, 0.16f, Paint(0.98f));
        // Broad shoulders preserve the hexagonal silhouette, with dark mechanical gaps.
        for (int side = -1; side <= 1; side += 2)
        {
            Hex(side * 0.54f, 0.04f, 0.10f, 0.29f, 0.37f, Paint(0.96f));
            Block(side * 0.39f, -0.30f, 0.05f, 0.30f, 0.40f, 0.49f, Steel(0.92f), foot: true);
            Block(side * 0.39f, -0.40f, -0.03f, 0.46f, 0.20f, 0.66f, Paint(0.90f), foot: true);
            Block(side * 0.39f, -0.29f, -0.24f, 0.25f, 0.06f, 0.19f, Trim(0.78f), foot: true);
            Block(side * 0.27f, 0.29f, 0.43f, 0.22f, 0.09f, 0.20f, Steel(0.91f));
        }
        // Low hexagonal command head with recessed visor and a rear power pack.
        Hex(0f, 0.38f, 0.15f, 0.33f, 0.24f, Paint(1f));
        Block(0f, 0.37f, -0.17f, 0.40f, 0.07f, 0.06f, Trim(0.97f));
        Block(0f, 0.17f, 0.63f, 0.35f, 0.16f, 0.20f, Steel(0.90f));
        Block(0f, 0.24f, 0.63f, 0.22f, 0.06f, 0.13f, Trim(0.85f));

        // Central bearing and angled arm. All marked parts rotate around the
        // origin in XZ; the weapon stays connected throughout the cleave.
        Hex(0f, 0.26f, 0f, 0.19f, 0.14f, Steel(1f));
        Block(0.18f, 0.27f, -0.29f, 0.18f, 0.12f, 0.66f, Steel(1f), swing: true, yaw: 0.55f);
        // Broad faceted blade with a luminous cutting edge, not a second FX emitter.
        Vector2[] blade = { new(-0.92f, -0.25f), new(-0.35f, -0.85f), new(0.82f, -0.70f),
            new(1f, 0.15f), new(0.45f, 0.76f), new(-0.80f, 0.48f) };
        AppendPrism(surface, blade.Length, x * 0.31f, z * 0.22f, height * 0.14f, 0f,
            new Vector3(x * 0.35f, height * 0.27f, -z * 0.59f), Paint(0.96f),
            sideShade: SoldierSideShade, yaw: 0.30f, armorMask: true, visualOutline: blade, swingPart: true);
        AppendPrism(surface, blade.Length, x * 0.25f, z * 0.17f, height * 0.045f, 0f,
            new Vector3(x * 0.35f, height * 0.35f, -z * 0.61f), Trim(0.92f),
            sideShade: SoldierSideShade, yaw: 0.30f, armorMask: true, visualOutline: blade, swingPart: true);
    }

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

    public static void AppendBeveledRectangle(SurfaceTool surface, Vector3 center, Vector3 size, Color tint,
        float sideShade = 1f, float pitch = 0f)
    {
        const float inverseSqrtTwo = 0.70710678f;
        AppendPrism(surface, 4, size.X * inverseSqrtTwo, size.Z * inverseSqrtTwo,
            size.Y, Mathf.Pi * 0.25f, center, tint, 0.16f, sideShade: sideShade, pitch: pitch);
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
        float yaw = 0f, float topCapBrightness = 1f, float pitch = 0f, bool armorMask = false,
        Vector2[] visualOutline = null, bool swingPart = false, bool walkPart = false)
    {
        // The artillery barrel is tilted once during mesh construction, never per soldier.
        Basis tilt = pitch == 0f ? Basis.Identity : new Basis(Vector3.Right, pitch);
        Vector3 Point(Vector3 p) => pitch == 0f ? p : center + tilt * (p - center);
        Vector3 Normal(Vector3 n) => pitch == 0f ? n : tilt * n;
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color) =>
            AddTriangle(surface, Point(a), Point(b), Point(c), Normal(normal), color, armorMask, swingPart, walkPart);
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color) =>
            AddQuad(surface, Point(a), Point(b), Point(c), Point(d), Normal(normal), color, armorMask, swingPart, walkPart);
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
            if (visualOutline != null)
            {
                x = visualOutline[i].X * radiusX;
                z = visualOutline[i].Y * radiusZ;
            }
            else if (customOutline.HasValue)
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

        if (concaveFork) AppendForkCaps(surface, top, bottom, tint, armorMask);
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
                Quad(inner, innerNext, top[next], top[i], Vector3.Up, Colors.White * tint);
                Triangle(capCenter + recess, innerNext, inner, Vector3.Up, tint * 0.68f);
            }
            else if (!concaveFork)
                Triangle(center + cap + Vector3.Up * half, top[next], top[i], Vector3.Up, TopTint * tint * topCapBrightness);
            if (!concaveFork) Triangle(center + cap + Vector3.Down * half, bottom[i], bottom[next], Vector3.Down, BottomTint * tint);

            Quad(bottom[i], shoulder[i], shoulder[next], bottom[next],
                outward, SideTint(outward) * tint * sideShade);
            Vector3 bevelNormal = (outward * bevelRise + Vector3.Up * insetDistance).Normalized();
            Quad(shoulder[i], top[i], top[next], shoulder[next],
                bevelNormal, BevelTint(outward) * tint * Mathf.Lerp(1f, sideShade, 0.5f));
        }
    }

    // Triangulate the concave cap once per shared mesh so the notch stays open.
    private static void AppendForkCaps(SurfaceTool surface, Vector3[] top, Vector3[] bottom, Color tint, bool armorMask)
    {
        var outline = new Vector2[bottom.Length];
        for (int i = 0; i < outline.Length; i++) outline[i] = new Vector2(bottom[i].X, bottom[i].Z);
        int[] triangles = Geometry2D.TriangulatePolygon(outline);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if ((top[b] - top[a]).Cross(top[c] - top[a]).Y < 0f) (b, c) = (c, b);
            AddTriangle(surface, top[a], top[b], top[c], Vector3.Up, tint, armorMask);
            AddTriangle(surface, bottom[a], bottom[c], bottom[b], Vector3.Down, BottomTint * tint, armorMask);
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
        Vector3 normal, Color tint, bool armorMask = false, bool swingPart = false, bool walkPart = false)
    {
        AddTriangle(surface, a, b, c, normal, tint, armorMask, swingPart, walkPart);
        AddTriangle(surface, a, c, d, normal, tint, armorMask, swingPart, walkPart);
    }

    private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c,
        Vector3 normal, Color tint, bool armorMask = false, bool swingPart = false, bool walkPart = false)
    {
        surface.SetNormal(normal);
        if (armorMask)
        {
            // Store material kind + baked face brightness in UV; COLOR remains
            // white so the existing instance RGB and fracture alpha survive.
            float kind = tint.R > 0f ? 1f : tint.G > 0f ? 2f : 3f;
            // Negative kinds mark the arm; +4 marks boots. Material stays the same.
            float taggedKind = swingPart ? -kind : walkPart ? kind + 4f : kind;
            surface.SetUV(new Vector2(taggedKind, Mathf.Max(tint.R, Mathf.Max(tint.G, tint.B))));
            surface.SetColor(Colors.White);
        }
        else surface.SetColor(tint);
        // Preserve the project's existing Godot clockwise front-face convention.
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
    }
}
