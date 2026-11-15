// Shared health and footprint for any destructible building.
using Godot;

namespace MassiveRts;

internal class StructureHealth
{
    private readonly DamageRules rules;
    private readonly float damageTaken;
    public readonly byte Faction;
    public readonly int ArmourClass;
    public readonly ObstacleRectangle Area;
    public readonly float MaxHealth;
    public float Health { get; private set; }
    public bool Destroyed => Health <= 0f;

    public StructureHealth(ObstacleRectangle area, byte faction, float health,
        DamageRules rules, int armourClass, float armour)
    {
        Area = area;
        Faction = faction;
        this.rules = rules;
        ArmourClass = armourClass;
        damageTaken = 1f - (float.IsFinite(armour) ? Mathf.Clamp(armour, 0f, 1f) : 0f);
        Health = MaxHealth = float.IsFinite(health) && health > 0f ? health : 1000f;
    }

    public void Damage(float amount, int damageType)
    {
        if (Destroyed || !float.IsFinite(amount) || amount <= 0f) return;
        Health = Mathf.Max(0f, Health - rules.Resolve(amount, damageType, ArmourClass, damageTaken));
        if (Destroyed) OnDestroyed();
    }

    protected virtual void OnDestroyed() { }
}
