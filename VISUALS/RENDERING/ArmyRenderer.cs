// Packed MultiMesh batches with dirty-instance updates and constant-time death removal.
using System.Diagnostics;
using Godot;
namespace MassiveRts;

public partial class ArmyRenderer : Node3D
{
    private const int InstanceStride = 16; // Three affine rows, then RGBA.
    private const int InfantryWalkStride = InstanceStride + 4; // Custom phase/weight only for opted-in infantry batches.
    private const int SparseUpdateLimit = 64;
    private float[][] buffers;
    private int[][] changedSlots;
    private int[] changeCounts;
    private MultiMesh[] batches;
    private MultiMeshInstance3D[] shadowBatches;
    private float[][] shadowBuffers;
    private Mesh shadowMesh;
    private Mesh[] batchShapes;
    private Material batchMaterialOverride;
    private Material[] lightingMaterials;
    private MultiMeshInstance3D[] unitViews;
    private bool castUnitShadows;
    private bool shadowsEnabled;
    private const int ShadowStride = 12;
    // Relative to the previous oval footprint; large elites get extra reduction.
    private const float SoldierShadowScale = 0.80f;
    private const float PentagonShadowScale = 0.70f;
    private const float HexagonShadowScale = 0.58f;
    private int[][] members;
    private int[] slots;
    private int[] counts;
    private int typeCount;
    private float[] halfHeights;
    private bool[] facesPositiveX;
    private bool useCircleRobotHeading;
    private bool animateCircleSword;
    private bool animateCircleWalk;
    private bool animateSquareAttack;
    private bool animateSquareWalk;
    private bool animateTriangleFire;
    private bool animateTriangleWalk;
    private bool animatePentagonAttack;
    private bool animatePentagonWalk;
    private bool animateDiamondAttack;
    private bool animateDiamondGallop;
    private bool animateBreacherAttack;
    private bool animateBreacherWalk;
    private bool animateLongMarksmanFire;
    private bool animateLongMarksmanWalk;
    private bool animatePikemanAttack;
    private bool animatePikemanWalk;
    private bool animateObliteratorAttack;
    private bool animateObliteratorWalk;
    private bool animatePrismWeapon;
    private bool animatePrismWalk;
    private bool animateHexagonAttack;
    private bool animateHexagonWalk;
    private int[] batchStrides;
    private Vector2[] infantryWalkPosition, infantryWalkPose;
    internal bool HasInfantryWalkMotion { get; private set; }
    private UnitState units;
    private bool cracksEnabled;
    private int maxShieldShells;
    private bool showShieldShells = true;
    internal double ShieldVisualMilliseconds { get; private set; }
    internal int ShieldVisualCount => shieldShells?.VisibleCount ?? 0;

    internal void SetShieldShellsVisible(bool visible)
    {
        showShieldShells = visible;
        SyncShieldShells();
    }
    private ShieldShellRenderer shieldShells;
    private float firstCrackHealth, middleCrackHealth, heavyCrackHealth;
    private Vector3[] previousPositions;
    private Vector2[] previousFacing;
    private bool[] unsettled;
    private bool hasPreviousPose;
    private float poseBlend = 1f;
    public bool HasInterpolatedMotion { get; private set; }
    public int DenseUploads { get; private set; }
    public int SparseUploads { get; private set; }
    public int ChangedWrites { get; private set; }
    public double UploadMilliseconds { get; private set; }
    public int InstanceCount { get; private set; }
    // =========================================================
    public void Initialize(UnitState state, Mesh[] shapes, Mesh sharedShadow, bool showShadows, UnitVisualConfig visuals, Material materialOverride = null)
    {
        maxShieldShells = Mathf.Clamp(visuals.MaxShieldShells, 0, 2048);
        if (!visuals.ShowShieldShells) maxShieldShells = 0;
        cracksEnabled = visuals.UseUnitFracture;
        useCircleRobotHeading = visuals.UseCircleRobotMesh;
        animateCircleSword = visuals.UseCircleRobotMesh && visuals.AnimateCircleSword;
        animateCircleWalk = visuals.UseCircleRobotMesh && visuals.AnimateCircleWalk;
        animateSquareAttack = visuals.UseSquareHeavyMesh && visuals.AnimateSquareAttack;
        animateSquareWalk = visuals.UseSquareHeavyMesh && visuals.AnimateSquareWalk;
        animateHexagonAttack = visuals.UseHexagonSiegeRobotMesh && visuals.AnimateHexagonAttack;
        animateHexagonWalk = visuals.UseHexagonSiegeRobotMesh && visuals.AnimateHexagonWalk;
        animateTriangleFire = visuals.UseTriangleArcherMesh && visuals.AnimateTriangleFire;
        animateTriangleWalk = visuals.UseTriangleArcherMesh && visuals.AnimateTriangleWalk;
        animatePentagonAttack = visuals.UsePentagonEliteMesh && visuals.AnimatePentagonAttack;
        animatePentagonWalk = visuals.UsePentagonEliteMesh && visuals.AnimatePentagonWalk;
        animateDiamondAttack = visuals.UseDiamondCavalryMesh && visuals.AnimateDiamondAttack;
        animateDiamondGallop = visuals.UseDiamondCavalryMesh && visuals.AnimateDiamondGallop;
        animateBreacherAttack = visuals.UseHBreacherMesh && visuals.AnimateHBreacherAttack;
        animateBreacherWalk = visuals.UseHBreacherMesh && visuals.AnimateHBreacherWalk;
        animateLongMarksmanFire = visuals.UseLongMarksmanMesh && visuals.AnimateLongMarksmanFire;
        animateLongMarksmanWalk = visuals.UseLongMarksmanMesh && visuals.AnimateLongMarksmanWalk;
        animatePikemanAttack = visuals.UsePikemanMesh && visuals.AnimatePikemanAttack;
        animatePikemanWalk = visuals.UsePikemanMesh && visuals.AnimatePikemanWalk;
        animateObliteratorAttack = visuals.UseObliteratorAssaultMesh && visuals.AnimateObliteratorAttack;
        animateObliteratorWalk = visuals.UseObliteratorAssaultMesh && visuals.AnimateObliteratorWalk;
        animatePrismWeapon = visuals.UsePrismLanceMesh && visuals.AnimatePrismWeapon;
        animatePrismWalk = visuals.UsePrismLanceMesh && visuals.AnimatePrismWalk;
        bool anyWalk = animateCircleWalk || animateSquareWalk || animateHexagonWalk || animateTriangleWalk ||
            animatePentagonWalk || animateDiamondGallop || animateBreacherWalk || animateLongMarksmanWalk || animatePikemanWalk ||
            animateObliteratorWalk || animatePrismWalk;
        infantryWalkPosition = anyWalk ? new Vector2[state.Position.Length] : null;
        infantryWalkPose = anyWalk ? new Vector2[state.Position.Length] : null;
        if (state.HitFeedback != null)
        {
            state.HitFeedback.ExtendCircleSword = animateCircleSword;
            state.HitFeedback.ExtendSquareAttack = animateSquareAttack;
            state.HitFeedback.ExtendTriangleFire = animateTriangleFire;
            state.HitFeedback.ExtendPentagonAttack = animatePentagonAttack;
            state.HitFeedback.ExtendDiamondAttack = animateDiamondAttack;
            state.HitFeedback.ExtendBreacherAttack = animateBreacherAttack;
            state.HitFeedback.ExtendLongMarksmanFire = animateLongMarksmanFire;
            state.HitFeedback.ExtendPikemanAttack = animatePikemanAttack;
            state.HitFeedback.ExtendObliteratorAttack = animateObliteratorAttack;
            state.HitFeedback.ExtendPrismFire = animatePrismWeapon;
            state.HitFeedback.EnableHexagonAttack(animateHexagonAttack);
        }
        firstCrackHealth = Mathf.Clamp(visuals.FirstCrackHealth, 0f, 1f);
        heavyCrackHealth = Mathf.Clamp(visuals.HeavyCrackHealth, 0f, firstCrackHealth);
        middleCrackHealth = (firstCrackHealth + heavyCrackHealth) * 0.5f;
        units = state; slots = new int[state.Position.Length]; System.Array.Fill(slots, -1);
        typeCount = state.Catalog.Count;
        int batchCount = typeCount * Factions.Count;
        batchStrides = new int[batchCount];
        for (int b = 0; b < batchCount; b++)
            batchStrides[b] = WalkEnabled(b % typeCount) || (animateHexagonAttack && b % typeCount == RuntimeUnitCatalog.Hexagon) ||
                (animatePrismWeapon && b % typeCount == RuntimeUnitCatalog.PrismLance)
                ? InfantryWalkStride : InstanceStride;
        members = new int[batchCount][];
        buffers = new float[batchCount][];
        changedSlots = new int[batchCount][];
        changeCounts = new int[batchCount];
        batches = new MultiMesh[batchCount];
        counts = new int[batchCount];
        unitViews = new MultiMeshInstance3D[batchCount];
        halfHeights = new float[typeCount];
        facesPositiveX = new bool[typeCount];
        for (int i = 0; i < typeCount; i++)
        {
            halfHeights[i] = state.Catalog[i].Height * 0.5f;
            facesPositiveX[i] = state.Catalog[i].Shape == UnitShape.Diamond;
        }
        var capacities = new int[batchCount];
        for (int i = 0; i < state.Count; i++) capacities[Batch(i)]++;
        batchShapes = shapes;
        batchMaterialOverride = materialOverride;
        for (int b = 0; b < batchCount; b++)
            if (capacities[b] > 0) AllocateBatch(b, capacities[b]);
        shadowMesh = sharedShadow;
        SetShadowsVisible(showShadows);
        Sync();
    }
    // =========================================================
    private void AllocateBatch(int batch, int capacity)
    {
        var mesh = batches[batch];
        if (mesh == null)
        {
            mesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true, UseCustomData = batchStrides[batch] == InfantryWalkStride,
                Mesh = batchShapes[batch % typeCount]
            };
            batches[batch] = mesh;
            changedSlots[batch] = new int[SparseUpdateLimit];
            unitViews[batch] = new MultiMeshInstance3D { Multimesh = mesh };
            ApplyBatchLighting(batch);
            AddChild(unitViews[batch]);
        }
        int previousCapacity = members[batch]?.Length ?? 0;
        System.Array.Resize(ref members[batch], capacity);
        int stride = batchStrides[batch];
        System.Array.Resize(ref buffers[batch], capacity * stride);
        Color faction = FactionColor(batch / typeCount);
        for (int offset = previousCapacity * stride; offset < buffers[batch].Length; offset += stride)
        {
            buffers[batch][offset] = buffers[batch][offset + 5] = buffers[batch][offset + 10] = 1f;
            StoreColor(buffers[batch], offset, faction);
        }
        mesh.InstanceCount = capacity;
        mesh.VisibleInstanceCount = counts[batch];
        if (counts[batch] > 0) mesh.Buffer = buffers[batch];
        if (shadowsEnabled || shadowBatches?[batch] != null)
        {
            EnsureShadowBatch(batch);
            var shadow = shadowBatches[batch].Multimesh;
            System.Array.Resize(ref shadowBuffers[batch], capacity * ShadowStride);
            shadow.InstanceCount = capacity;
            shadow.VisibleInstanceCount = counts[batch];
            if (counts[batch] > 0) shadow.Buffer = shadowBuffers[batch];
        }
    }

    internal void SetBattlefieldLighting(Material[] materials, bool castShadows)
    {
        lightingMaterials = materials;
        castUnitShadows = castShadows;
        for (int batch = 0; batch < unitViews.Length; batch++)
            if (unitViews[batch] != null) ApplyBatchLighting(batch);
    }

    private void ApplyBatchLighting(int batch)
    {
        var view = unitViews[batch];
        view.MaterialOverride = lightingMaterials?[batch % typeCount] ?? batchMaterialOverride;
        view.CastShadow = castUnitShadows ? GeometryInstance3D.ShadowCastingSetting.On :
            GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private void EnsureShadowBatch(int batch)
    {
        if (shadowBatches[batch] != null) return;
        var mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = shadowMesh };
        shadowBatches[batch] = new MultiMeshInstance3D
        {
            Multimesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(shadowBatches[batch]);
    }

    public void SetShadowsVisible(bool visible)
    {
        if (shadowsEnabled == visible) return;
        shadowsEnabled = visible;
        shadowBatches ??= new MultiMeshInstance3D[batches.Length];
        shadowBuffers ??= new float[batches.Length][];
        for (int b = 0; b < batches.Length; b++)
        {
            if (batches[b] == null) continue;
            if (visible)
            {
                if (shadowBatches[b] == null)
                {
                    EnsureShadowBatch(b);
                    int capacity = batches[b].InstanceCount;
                    shadowBuffers[b] = new float[capacity * ShadowStride];
                    var mesh = shadowBatches[b].Multimesh;
                    mesh.InstanceCount = capacity;
                    mesh.VisibleInstanceCount = 0;
                }
                for (int slot = 0; slot < counts[b]; slot++)
                    WriteShadow(b, slot, GroundPose(members[b][slot]));
                UploadShadows(b);
            }
            if (shadowBatches[b] != null) shadowBatches[b].Visible = visible;
        }
    }

    private Vector3 GroundPose(int id) => hasPreviousPose && poseBlend < 1f
        ? previousPositions[id].Lerp(units.Position[id], poseBlend) : units.Position[id];

    private void WriteShadow(int batch, int slot, Vector3 position)
    {
        float[] buffer = shadowBuffers[batch];
        int offset = slot * ShadowStride;
        int type = batch % typeCount;
        UnitOutline outline = units.Catalog[type].Outline;
        float size = type == RuntimeUnitCatalog.Hexagon ? HexagonShadowScale
            : type == RuntimeUnitCatalog.Pentagon ? PentagonShadowScale : SoldierShadowScale;
        buffer[offset] = outline.RadiusX * 1.45f * size;
        buffer[offset + 5] = 1f;
        buffer[offset + 10] = outline.RadiusZ * 1.15f * size;
        // World-space sun direction: down-right on the default 45-degree camera.
        buffer[offset + 3] = position.X + outline.Footprint * 0.40f;
        buffer[offset + 7] = position.Y + 0.02f;
        buffer[offset + 11] = position.Z - outline.Footprint * 0.16f;
    }

    private void UploadShadows(int batch, int changed = SparseUpdateLimit + 1)
    {
        MultiMesh mesh = shadowBatches[batch].Multimesh;
        int visible = counts[batch];
        float[] buffer = shadowBuffers[batch];
        if (changed > SparseUpdateLimit)
        {
            if (visible > 0) mesh.Buffer = buffer;
        }
        else
        {
            for (int i = 0; i < changed; i++)
            {
                int slot = changedSlots[batch][i];
                if (slot >= visible) continue;
                int offset = slot * ShadowStride;
                Basis basis = Basis.Identity.Scaled(new Vector3(buffer[offset], 1f, buffer[offset + 10]));
                Vector3 position = new(buffer[offset + 3], buffer[offset + 7], buffer[offset + 11]);
                mesh.SetInstanceTransform(slot, new Transform3D(basis, position));
            }
        }
        if (mesh.VisibleInstanceCount != visible) mesh.VisibleInstanceCount = visible;
    }

    public void CapturePose()
    {
        previousPositions ??= new Vector3[units.Position.Length];
        previousFacing ??= new Vector2[units.Position.Length];
        unsettled ??= new bool[units.Position.Length];
        System.Array.Copy(units.Position, previousPositions, units.Count);
        System.Array.Copy(units.Facing, previousFacing, units.Count);
        hasPreviousPose = true;
    }

    public void ClearPoseHistory() => hasPreviousPose = false;

    private int Batch(int id) => units.Faction[id] * typeCount + units.Type[id];
    internal static Color FactionColor(int faction) => Factions.Color(faction);

    private bool WalkEnabled(int type) => (animateCircleWalk && type == RuntimeUnitCatalog.Circle) ||
        (animateSquareWalk && type == RuntimeUnitCatalog.Square) ||
        (animateTriangleWalk && type == RuntimeUnitCatalog.Triangle) ||
        (animatePentagonWalk && type == RuntimeUnitCatalog.Pentagon) ||
        (animateDiamondGallop && type == RuntimeUnitCatalog.Diamond) ||
        (animateBreacherWalk && type == RuntimeUnitCatalog.Breacher) ||
        (animateLongMarksmanWalk && type == RuntimeUnitCatalog.LongTriangle) ||
        (animatePikemanWalk && type == RuntimeUnitCatalog.Pikeman) ||
        (animateObliteratorWalk && type == RuntimeUnitCatalog.Obliterator) ||
        (animatePrismWalk && type == RuntimeUnitCatalog.PrismLance) ||
        (animateHexagonWalk && type == RuntimeUnitCatalog.Hexagon);

    private bool AttackEnabled(int type) => (animateCircleSword && type == RuntimeUnitCatalog.Circle) ||
        (animateSquareAttack && type == RuntimeUnitCatalog.Square) ||
        (animateTriangleFire && type == RuntimeUnitCatalog.Triangle) ||
        (animatePentagonAttack && type == RuntimeUnitCatalog.Pentagon) ||
        (animateDiamondAttack && type == RuntimeUnitCatalog.Diamond) ||
        type == RuntimeUnitCatalog.SiegeRam ||
        (animateBreacherAttack && type == RuntimeUnitCatalog.Breacher) ||
        (animateLongMarksmanFire && type == RuntimeUnitCatalog.LongTriangle) ||
        (animatePikemanAttack && type == RuntimeUnitCatalog.Pikeman) ||
        (animateObliteratorAttack && type == RuntimeUnitCatalog.Obliterator) ||
        (animatePrismWeapon && type == RuntimeUnitCatalog.PrismLance) ||
        (animateHexagonAttack && type == RuntimeUnitCatalog.Hexagon);

    private bool UpdateInfantryWalk(int id)
    {
        Vector3 ground = GroundPose(id);
        Vector2 point = new(ground.X, ground.Z);
        if (slots[id] < 0)
        {
            infantryWalkPosition[id] = point;
            uint seed = unchecked((uint)id * 2654435761u);
            infantryWalkPose[id] = new Vector2((seed >> 16) / 65536f, 0f);
            return false;
        }
        Vector2 pose = infantryWalkPose[id];
        float radius = Mathf.Max(0.01f, units.Catalog[units.Type[id]].Footprint);
        float distanceSquared = point.DistanceSquaredTo(infantryWalkPosition[id]);
        infantryWalkPosition[id] = point;
        float targetWeight = 0f;
        float phase = pose.X;
        // Ignore tiny separation jitter and large teleports rather than stepping in place.
        if (distanceSquared > radius * radius * 0.0004f && distanceSquared < radius * radius * 16f)
        {
            float distance = Mathf.Sqrt(distanceSquared);
            float strideLength = units.Type[id] == RuntimeUnitCatalog.Hexagon ? 3.0f :
                units.Type[id] == RuntimeUnitCatalog.Square ? 4.4f :
                units.Type[id] == RuntimeUnitCatalog.Triangle ? 3.4f :
                units.Type[id] == RuntimeUnitCatalog.Pentagon ? 4.0f :
                units.Type[id] == RuntimeUnitCatalog.Diamond ? 4.8f :
                units.Type[id] == RuntimeUnitCatalog.Breacher ? 3.2f :
                units.Type[id] == RuntimeUnitCatalog.LongTriangle ? 2.6f :
                units.Type[id] == RuntimeUnitCatalog.Pikeman ? 3.5f :
                units.Type[id] == RuntimeUnitCatalog.Obliterator ? 3.2f :
                units.Type[id] == RuntimeUnitCatalog.PrismLance ? 2.6f : 3.8f;
            phase += distance / (radius * strideLength);
            phase -= Mathf.Floor(phase);
            targetWeight = Mathf.Clamp(distance / (radius * 0.10f), 0f, 1f);
        }
        float weight = Mathf.MoveToward(pose.Y, targetWeight, 0.25f);
        var next = new Vector2(phase, weight);
        infantryWalkPose[id] = next;
        HasInfantryWalkMotion |= weight > 0f;
        return pose != next;
    }

    private static void StoreColor(float[] buffer, int offset, Color color)
    {
        buffer[offset + 12] = color.R;
        buffer[offset + 13] = color.G;
        buffer[offset + 14] = color.B;
        buffer[offset + 15] = color.A;
    }

    // Stage instance changes in reusable arrays. Only sparse batches need a slot list.
    private void Write(int id, int batch, int slot)
    {
        Vector3 position = GroundPose(id);
        if (shadowsEnabled) WriteShadow(batch, slot, position);
        int stride = batchStrides[batch];
        int offset = slot * stride;
        float[] buffer = buffers[batch];
        byte type = units.Type[id];
        var (flash, scale) = units.HitFeedback?.Evaluate(id, type) ?? (0f, 1f);
        bool white = flash > 0f;
        if (stride == InfantryWalkStride)
        {
            Vector2 walk = WalkEnabled(type) ? infantryWalkPose[id] : Vector2.Zero;
            buffer[offset + 16] = walk.X;
            buffer[offset + 17] = walk.Y;
            // Custom Z is type-local: Hexagon stomp or Prism's real charge fraction.
            buffer[offset + 18] = animatePrismWeapon && type == RuntimeUnitCatalog.PrismLance
                ? Mathf.Clamp(units.PrismCharge(id) / Mathf.Max(0.001f, units.Catalog[type].PrismBeam.ChargeSeconds), 0f, 1f)
                : animateHexagonAttack && type == RuntimeUnitCatalog.Hexagon &&
                    units.HitFeedback?.IsHexagonStomp(id) == true ? 1f : 0f;
        }
        // Visual mesh only; the authoritative position and projectile spawn stay unchanged.
        if (units.RangedRecoil != null && units.Catalog[type].RangedRecoil.Distance > 0f)
        {
            Vector2 kick = units.RangedRecoil.VisualOffset(id, units.Catalog[type].RangedRecoil);
            position.X += kick.X;
            position.Z += kick.Y;
        }
        ref readonly var stats = ref units.Catalog[type];
        float cooldown = units.Cooldown[id];
        if (stats.MeleeLungeDistance > 0f && cooldown > 0f)
        {
            float progress = (stats.Interval - cooldown) / CombatSystem.MeleeLungeSeconds;
            if (progress < 1f)
            {
                Vector2 stroke = units.Facing[id] * (stats.MeleeLungeDistance * (1f - Mathf.Max(0f, progress)));
                position.X += stroke.X;
                position.Z += stroke.Y;
            }
        }
        // The optional robot uses the same turn-limited facing as other directional meshes.
        float cosine = 1f, sine = 0f;
        if (type != RuntimeUnitCatalog.Circle || useCircleRobotHeading)
        {
            Vector2 facing = units.Facing[id];
            if (hasPreviousPose && poseBlend < 1f)
            {
                Vector2 before = previousFacing[id];
                if (before != facing)
                {
                    if (before == Vector2.Zero)
                        before = facesPositiveX[type] ? Vector2.Right : Vector2.Up;
                    facing = before.Lerp(facing, poseBlend).Normalized();
                }
            }
            if (facing != Vector2.Zero)
            {
                bool positiveX = facesPositiveX[type];
                cosine = positiveX ? facing.X : -facing.Y;
                sine = positiveX ? -facing.Y : -facing.X;
            }
        }
        buffer[offset] = buffer[offset + 10] = cosine * scale;
        buffer[offset + 2] = sine * scale;
        buffer[offset + 8] = -buffer[offset + 2];
        buffer[offset + 5] = scale;
        buffer[offset + 3] = position.X;
        buffer[offset + 7] = position.Y + halfHeights[units.Type[id]] * scale;
        buffer[offset + 11] = position.Z;
        Color color = FactionColor(units.Faction[id]);
        if (white) color = color.Lerp(Colors.White, flash);
        if (!white && stats.PrismBeam.Enabled)
            color = color.Lerp(new Color(0.70f, 0.94f, 1f), units.PrismCharge(id) / stats.PrismBeam.ChargeSeconds * 0.25f);
        if (cracksEnabled && !white)
        {
            float health = units.Health[id];
            int stage = health >= stats.Health * firstCrackHealth ? 0
                : health >= stats.Health * middleCrackHealth ? 1
                : health >= stats.Health * heavyCrackHealth ? 2 : 3;
            // Opaque unit shader reads the existing alpha byte as stage and stable orientation.
            uint variant = unchecked((uint)id * 2654435761u) >> 30;
            color.A = stage == 0 ? 1f : (stage * 4 + variant) / 255f;
        }
        if (AttackEnabled(type))
        {
            // Keep the existing RGBA instance stride: low nibble is fracture,
            // high nibble is attack phase (0 idle, 1..15 animated).
            int fractureCode = color.A == 1f ? 0 : Mathf.RoundToInt(color.A * 255f);
            float phase = units.HitFeedback?.AttackPhase(id, type) ?? -1f;
            int swingCode = phase < 0f ? 0 : 1 + Mathf.RoundToInt(phase * 14f);
            color.A = (fractureCode + swingCode * 16) / 255f;
        }
        if (buffer[offset + 12] != color.R || buffer[offset + 13] != color.G ||
            buffer[offset + 14] != color.B || buffer[offset + 15] != color.A)
            StoreColor(buffer, offset, color);

        int changeCount = changeCounts[batch]++;
        if (changeCount < SparseUpdateLimit) changedSlots[batch][changeCount] = slot;
    }

    private void Upload(int batch)
    {
        MultiMesh mesh = batches[batch];
        if (mesh == null) return;
        int changed = changeCounts[batch];
        int visible = counts[batch];
        ChangedWrites += changed;
        if (changed > SparseUpdateLimit && visible > 0)
        {
            DenseUploads++;
            mesh.Buffer = buffers[batch];
        }
        else if (changed > 0 && visible > 0)
        {
            SparseUploads++;
            float[] buffer = buffers[batch];
            int stride = batchStrides[batch];
            for (int i = 0; i < changed; i++)
            {
                int slot = changedSlots[batch][i];
                if (slot >= visible) continue;
                int offset = slot * stride;
                var position = new Vector3(buffer[offset + 3], buffer[offset + 7], buffer[offset + 11]);
                // Packed transforms are affine rows, while Basis takes columns.
                Basis basis = new(
                    new Vector3(buffer[offset], 0f, buffer[offset + 8]),
                    new Vector3(0f, buffer[offset + 5], 0f),
                    new Vector3(buffer[offset + 2], 0f, buffer[offset + 10]));
                mesh.SetInstanceTransform(slot, new Transform3D(basis, position));
                mesh.SetInstanceColor(slot, new Color(buffer[offset + 12], buffer[offset + 13], buffer[offset + 14], buffer[offset + 15]));
                if (stride == InfantryWalkStride)
                    mesh.SetInstanceCustomData(slot, new Color(buffer[offset + 16], buffer[offset + 17], buffer[offset + 18], 0f));
            }
        }
        if (mesh.VisibleInstanceCount != visible) mesh.VisibleInstanceCount = visible;
        if (shadowsEnabled && (changed > 0 || shadowBatches[batch].Multimesh.VisibleInstanceCount != visible))
            UploadShadows(batch, changed);
        changeCounts[batch] = 0;
    }

    // =========================================================
    private MultiMesh prismChargeLights;

    // Uses the packed charging list, so settled and non-Prism soldiers add no work.
    private void SyncPrismCharge()
    {
        const int capacity = 256;
        int count = Mathf.Min(units.ChargingCount, capacity);
        if (prismChargeLights == null)
        {
            if (count == 0) return;
            var material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            prismChargeLights = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
                Mesh = new SphereMesh { RadialSegments = 8, Rings = 4, Material = material },
                InstanceCount = capacity
            };
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = prismChargeLights, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        if (count == 0 && prismChargeLights.VisibleInstanceCount == 0) return;
        for (int i = 0; i < count; i++)
        {
            int id = units.ChargingId(i);
            ref readonly var type = ref units.Catalog[units.Type[id]];
            float charge = units.PrismCharge(id) / type.PrismBeam.ChargeSeconds;
            Vector2 facing = units.Facing[id];
            if (type.Shape == UnitShape.Circle || facing == Vector2.Zero) facing = Vector2.Up;
            float scale = Mathf.Lerp(0.12f, 0.65f, charge);
            float forward = type.Outline.RadiusZ * 0.86f;
            Vector3 point = GroundPose(id) + new Vector3(facing.X * forward, type.Height * 0.90f, facing.Y * forward);
            prismChargeLights.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), point));
            prismChargeLights.SetInstanceColor(i, new Color(0.68f, 0.94f, 1f, 0.30f + 0.70f * charge));
        }
        prismChargeLights.VisibleInstanceCount = count;
    }

    private void SyncShieldShells()
    {
        ShieldVisualMilliseconds = 0;
        if (maxShieldShells == 0 || units.Shields is not { } shields) return;
        if (!showShieldShells)
        {
            shieldShells?.Hide();
            return;
        }
        if (shieldShells == null && (shields.ShieldedCount > 0 || shields.HasFlashes))
            shieldShells = new ShieldShellRenderer(this, units, batchShapes, maxShieldShells);
        if (shieldShells == null) return;
        long start = Stopwatch.GetTimestamp();
        shieldShells.Sync(shields, slots, buffers, batchStrides);
        ShieldVisualMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    public void Sync(float blend = 1f)
    {
        poseBlend = blend;
        HasInterpolatedMotion = false;
        HasInfantryWalkMotion = false;
        for (int id = 0; id < units.Count; id++)
        {
            bool moving = hasPreviousPose && units.Health[id] > 0f &&
                (previousPositions[id] != units.Position[id] ||
                 ((units.Type[id] != RuntimeUnitCatalog.Circle || useCircleRobotHeading) && previousFacing[id] != units.Facing[id]));
            bool wasUnsettled = unsettled != null && unsettled[id];
            if (unsettled != null) unsettled[id] = moving && blend < 1f;
            HasInterpolatedMotion |= moving && blend < 1f;
            bool gaitChanged = WalkEnabled(units.Type[id]) &&
                units.Health[id] > 0f && UpdateInfantryWalk(id);
            if (!units.Dirty[id] && !moving && !wasUnsettled && !gaitChanged) continue;
            units.Dirty[id] = false;
            int batch = Batch(id), slot = slots[id];
            if (units.Health[id] <= 0)
            {
                if (slot < 0) continue;
                int last = --counts[batch], moved = members[batch][last];
                if (slot != last) { members[batch][slot] = moved; slots[moved] = slot; Write(moved, batch, slot); }
                slots[id] = -1; InstanceCount--;
            }
            else
            {
                if (slot < 0)
                {
                    int capacity = members[batch]?.Length ?? 0;
                    if (counts[batch] == capacity)
                        AllocateBatch(batch, System.Math.Min(slots.Length, System.Math.Max(32, capacity * 2)));
                    slot = counts[batch]++;
                    members[batch][slot] = id;
                    slots[id] = slot;
                    InstanceCount++;
                }
                Write(id, batch, slot);
            }
        }
        SyncPrismCharge();
        SyncShieldShells();
        DenseUploads = SparseUploads = ChangedWrites = 0;
        long uploadStart = Stopwatch.GetTimestamp();
        for (int b = 0; b < batches.Length; b++) Upload(b);
        UploadMilliseconds = Stopwatch.GetElapsedTime(uploadStart).TotalMilliseconds;
    }
}
