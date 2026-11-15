// Compiles authored damage and armour names into a dense, neutral-by-default hit table.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
[GlobalClass]
public partial class DamageMatrix : Resource
{
    public const string DefaultPath = "res://GAMEPLAY/COMBAT/DefaultDamageMatrix.tres";
    [Export] public string[] DamageTypes { get; set; } = new[] { "Standard" };
    [Export] public string[] ArmourClasses { get; set; } = new[] { "Light" };
    // Row-major in DamageTypes order, with one column per ArmourClasses entry.
    [Export] public float[] Effectiveness { get; set; } = Array.Empty<float>();
}

public sealed class DamageRules
{
    public const int NeutralType = -1;
    private readonly string[] damageNames, armourNames;
    private readonly Dictionary<string, int> damageIds, armourIds;
    private readonly float[] multipliers;

    public DamageRules(DamageMatrix matrix)
    {
        damageNames = (string[])matrix.DamageTypes.Clone();
        armourNames = (string[])matrix.ArmourClasses.Clone();
        damageIds = Index(damageNames, "Standard");
        armourIds = Index(armourNames, "Light");
        multipliers = new float[damageNames.Length * armourNames.Length];
        for (int i = 0; i < multipliers.Length; i++)
        {
            float value = i < matrix.Effectiveness.Length ? matrix.Effectiveness[i] : 1f;
            multipliers[i] = float.IsFinite(value) ? Mathf.Clamp(value, 0f, 100f) : 1f;
        }
    }

    private static Dictionary<string, int> Index(string[] names, string neutralDefault)
    {
        if (names.Length == 0 || names[0] != neutralDefault)
            throw new ArgumentException($"Damage matrix must start this list with {neutralDefault}.");
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < names.Length; i++)
            if (string.IsNullOrWhiteSpace(names[i]) || !result.TryAdd(names[i], i))
                throw new ArgumentException("Damage matrix names must be unique and nonempty.");
        return result;
    }

    public int DamageType(string name) => damageIds.TryGetValue(name ?? "", out int index) ? index : 0;
    public int ArmourClass(string name) => armourIds.TryGetValue(name ?? "", out int index) ? index : 0;
    public string ArmourName(int armour) => armourNames[armour];
    public float Resolve(float damage, int type, int armour, float damageTaken) =>
        Mathf.Max(0f, damage * (type == NeutralType ? 1f : Effectiveness(type, armour)) * damageTaken);

    private float Effectiveness(int type, int armour) => multipliers[type * armourNames.Length + armour];

    public string Describe(int type, int armour)
    {
        var summary = new System.Text.StringBuilder(damageNames[type]).Append(" damage · ").Append(armourNames[armour]).Append(" armour");
        for (int i = 0; i < armourNames.Length; i++)
        {
            float multiplier = Effectiveness(type, i);
            if (Mathf.IsEqualApprox(multiplier, 1f)) continue;
            summary.Append($" · {multiplier:0.##}× vs {armourNames[i]}");
        }
        return summary.ToString();
    }
}
