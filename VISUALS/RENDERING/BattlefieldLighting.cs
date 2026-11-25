// Shared sun direction and lit materials for benchmark and campaign battles.
using Godot;

namespace MassiveRts;

internal static class BattlefieldLighting
{
    internal const int DefaultSunPercent = 350;
    internal const int DefaultFillPercent = 40;
    private const string AmbientFillParameter = "battlefield_ambient_fill";

    internal static void SetAmbientFill(float fill) =>
        RenderingServer.GlobalShaderParameterSet(AmbientFillParameter, fill);

    internal static DirectionalLight3D AddSun(Node3D battlefield)
    {
        var sun = new DirectionalLight3D
        {
            Name = "BattlefieldSun",
            LightEnergy = DefaultSunPercent / 100f,
            Basis = Basis.LookingAt(new Vector3(0.48f, -0.70f, -0.53f), Vector3.Up),
            ShadowEnabled = false
        };
        battlefield.AddChild(sun);
        return sun;
    }

    internal static Material[] CreateUnitMaterials(RuntimeUnitCatalog catalog, UnitVisualConfig unitVisuals)
    {
        var shader = GD.Load<Shader>("res://VISUALS/RENDERING/BattlefieldUnitLit.gdshader");
        var crackMask = unitVisuals.UseUnitFracture
            ? GD.Load<Texture2D>("res://VISUALS/FEEDBACK/UNIT_HITS/UnitCrackMask.svg") : null;
        var materials = new Material[catalog.Count];
        for (int i = 0; i < catalog.Count; i++)
        {
            var outline = catalog[i].Outline;
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("show_fracture", crackMask != null);
            material.SetShaderParameter("use_two_tone_armor",
                FacetedSoldierMeshes.UsesTwoToneArmor(catalog[i].Id, outline, unitVisuals));
            if (crackMask != null) material.SetShaderParameter("crack_mask", crackMask);
            material.SetShaderParameter("half_extent", new Vector2(outline.RadiusX, outline.RadiusZ));
            material.SetShaderParameter("darkness", Mathf.Clamp(unitVisuals.CrackDarkness, 0f, 1f));
            materials[i] = material;
        }
        return materials;
    }
}
