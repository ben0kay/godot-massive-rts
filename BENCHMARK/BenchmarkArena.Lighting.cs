// Owns the opt-in Benchmark lighting and shadow comparison.
using Godot;

namespace MassiveRts;

internal enum BenchmarkLighting { Current, Lit, RealShadows }

public partial class BenchmarkArena
{
    private DirectionalLight3D benchmarkSun;
    private Material[] litUnitMaterials;
    private ShaderMaterial shadowReceiver, plateauReceiver;
    private BenchmarkLighting benchmarkLighting;
    private bool RealBenchmarkShadows => benchmarkLighting == BenchmarkLighting.RealShadows;
    private bool FakeBenchmarkShadows => panel.GroundShadows && !RealBenchmarkShadows;

    private void ConfigureBenchmarkLighting(ShaderMaterial slate)
    {
        panel.LightingChanged += mode =>
        {
            benchmarkLighting = mode;
            if (mode != BenchmarkLighting.Current && benchmarkSun == null) CreateBenchmarkSun();
            if (RealBenchmarkShadows && shadowReceiver == null)
            {
                shadowReceiver = new ShaderMaterial
                {
                    Shader = GD.Load<Shader>("res://WORLD/TERRAIN/RENDERING/SteelSlateShadowReceiver.gdshader")
                };
                plateauReceiver = new ShaderMaterial
                {
                    Shader = GD.Load<Shader>("res://WORLD/TERRAIN/RENDERING/PlateauShadowReceiver.gdshader")
                };
                foreach (string parameter in new[] { "stone_noise", "stone_normal", "slate_color",
                    "patch_scale", "grain_scale", "relief", "sheen", "seam_strength", "show_map_boundary" })
                    shadowReceiver.SetShaderParameter(parameter, slate.GetShaderParameter(parameter));
            }
            if (benchmarkSun != null)
            {
                benchmarkSun.Visible = mode != BenchmarkLighting.Current;
                benchmarkSun.ShadowEnabled = RealBenchmarkShadows;
            }
            RefreshBenchmarkGroundLighting();
            ApplyArmyLighting();
        };
        panel.ShadowDistanceChanged += distance =>
        {
            if (benchmarkSun != null) benchmarkSun.DirectionalShadowMaxDistance = distance;
        };
    }

    private CheckButton CreateCircleMeshToggle()
    {
        var toggle = new CheckButton
        {
            Text = "Animated Circle Infantry",
            TooltipText = "Switch Circle Infantry between the robot and classic mesh during battle.",
            ButtonPressed = battleUnitVisuals.UseCircleRobotMesh
        };
        toggle.Toggled += SetBattleCircleMesh;
        return toggle;
    }

    private void SetBattleCircleMesh(bool enabled)
    {
        if (enabled == battleUnitVisuals.UseCircleRobotMesh) return;
        battleUnitVisuals.UseCircleRobotMesh = enabled;
        soldierMeshes = FacetedSoldierMeshes.Create(catalog, battleUnitVisuals);
        if (litUnitMaterials != null)
            litUnitMaterials = BattlefieldLighting.CreateUnitMaterials(catalog, battleUnitVisuals);
        if (armyRenderer == null) return;

        // Batch strides differ between the meshes. Rebuild the renderer against the same live state.
        RemoveChild(armyRenderer);
        armyRenderer.QueueFree();
        for (int id = 0; id < units.Count; id++)
            if (units.Health[id] > 0f) units.Dirty[id] = true;
        CreateArmyRenderer();
    }

    private void CreateArmyRenderer()
    {
        armyRenderer = new ArmyRenderer();
        AddChild(armyRenderer);
        armyRenderer.Initialize(units, soldierMeshes, soldierShadow, FakeBenchmarkShadows, battleUnitVisuals);
        ApplyArmyLighting();
        armyRenderer.SetShieldShellsVisible(panel.ShowShieldShells);
    }

    private void CreateBenchmarkSun()
    {
        benchmarkSun = BattlefieldLighting.AddSun(this);
        benchmarkSun.DirectionalShadowMaxDistance = panel.ShadowDistance;
        litUnitMaterials = BattlefieldLighting.CreateUnitMaterials(catalog, battleUnitVisuals);
    }

    private void RefreshBenchmarkGroundLighting()
    {
        Material receiver = RealBenchmarkShadows ? shadowReceiver : null;
        flatGround.MaterialOverride = receiver;
        flatGround.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        if (terrainMeshes != null)
            foreach (Node child in terrainMeshes.GetChildren())
                if (child is MeshInstance3D chunk)
                {
                    chunk.MaterialOverride = RealBenchmarkShadows ? plateauReceiver : null;
                    chunk.CastShadow = RealBenchmarkShadows ? GeometryInstance3D.ShadowCastingSetting.On :
                        GeometryInstance3D.ShadowCastingSetting.Off;
                }
    }

    private void ApplyArmyLighting()
    {
        battleWalls.CastShadow = RealBenchmarkShadows ? GeometryInstance3D.ShadowCastingSetting.On :
            GeometryInstance3D.ShadowCastingSetting.Off;
        battleObstacleShadows.Visible = FakeBenchmarkShadows;
        armyRenderer?.SetShadowsVisible(FakeBenchmarkShadows);
        armyRenderer?.SetBattlefieldLighting(
            autoBattlerPlaytestMap == null && benchmarkLighting == BenchmarkLighting.Current ? null : litUnitMaterials,
            RealBenchmarkShadows);
    }
}
