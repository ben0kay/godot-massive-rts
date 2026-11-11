// Resolves local XZ overlap using a bounded, cell-ordered counting-sort grid.
using System;
using Godot;

namespace MassiveRts;

public enum SeparationMode { Off, Normal, Strong }

public readonly record struct AlliedYieldSettings(float ClearanceFraction, float PairCap, float MeleeShare, float ExitCap);

public sealed class SeparationSystem
{
    private const float Slop = 0.025f;
    // Bound the grid scan even when a few soldiers are far apart.
    private const int MaxGridCells = 1 << 20;

    // Indexed by soldier id.
    private const byte RangedRole = 1, FactionMask = 6, VoluntaryMove = 8;
    private readonly int[] cellOfId;
    private byte[] slotFlags;
    private bool[] rangedByType = Array.Empty<bool>();
    private Vector2[] yieldPush;
    private bool yieldEnabled;
    private TrampleSystem trample;
    private AlliedYieldSettings yieldSettings;
    // Indexed by slot, which is the soldier's position in cell order. Neighbours are adjacent in memory.
    private readonly int[] slotId, slotContacts;
    private readonly float[] slotX, slotZ, slotRadius, slotPushX, slotPushZ;

    private int[] cellStart = Array.Empty<int>();
    private float[] footprintByType = Array.Empty<float>();
    private int gridWidth, gridCells;
    private long candidateTotal;
    private double penetrationSum;

    public int YieldSkipped { get; private set; }
    public int YieldPushed { get; private set; }
    public int ExitCapped { get; private set; }
    public int OverlapPairs { get; private set; }
    public int BudgetHits { get; private set; }
    public float MaxPenetration { get; private set; }
    // Average overlap depth (world units) of pairs found this tick, measured before correction.
    public float MeanPenetration => OverlapPairs > 0 ? (float)(penetrationSum / OverlapPairs) : 0f;
    // Average pair tests per soldier per pass. Compare with the neighbour budget to see headroom.
    public float CandidatesPerSoldier { get; private set; }

    public SeparationSystem(int capacity)
    {
        cellOfId = new int[capacity];
        slotId = new int[capacity];
        slotContacts = new int[capacity];
        slotX = new float[capacity];
        slotZ = new float[capacity];
        slotRadius = new float[capacity];
        slotPushX = new float[capacity];
        slotPushZ = new float[capacity];
    }

    // Each pass gets a share of the tick-wide cap. Their combined displacement stays within the cap.
    public void Step(UnitState units, int passes, float gap, int neighbourBudget, float tickPushCap, int tick, byte[] voluntaryMovement = null, AlliedYieldSettings settings = default, TrampleSystem trample = null, ObstacleField obstacles = null)
    {
        OverlapPairs = BudgetHits = YieldSkipped = YieldPushed = ExitCapped = 0;
        yieldEnabled = voluntaryMovement != null;
        this.trample = trample;
        MaxPenetration = CandidatesPerSoldier = 0f;
        penetrationSum = 0;
        candidateTotal = 0;
        if (units.Count == 0 || passes <= 0) return;

        CacheFootprints(units);
        if (yieldEnabled)
        {
            slotFlags ??= new byte[cellOfId.Length];
            yieldPush ??= new Vector2[cellOfId.Length];
            yieldSettings = settings with { PairCap = settings.PairCap / passes, ExitCap = settings.ExitCap / passes };
        }
        float cellWidth = Mathf.Max(0.1f, units.Catalog.MaxFootprint * 2f + gap);
        float passCap = tickPushCap / passes;
        float gapLimit = gap - Slop;
        int aliveTotal = 0;

        for (int pass = 0; pass < passes; pass++)
        {
            int alive = Build(units, cellWidth, voluntaryMovement);
            aliveTotal += alive;
            if (alive < 2) break;
            Array.Clear(slotPushX, 0, alive);
            Array.Clear(slotPushZ, 0, alive);
            Array.Clear(slotContacts, 0, alive);
            if (yieldEnabled) Array.Clear(yieldPush, 0, alive);
            Scan(gapLimit, neighbourBudget, tick + pass);
            Apply(units, alive, passCap, obstacles);
        }
        if (aliveTotal > 0) CandidatesPerSoldier = (float)candidateTotal / aliveTotal;
    }

    private void CacheFootprints(UnitState units)
    {
        int types = units.Catalog.Count;
        if (footprintByType.Length == types) return;
        footprintByType = new float[types];
        rangedByType = new bool[types];
        for (int t = 0; t < types; t++)
        {
            footprintByType[t] = units.Catalog[t].Footprint;
            rangedByType[t] = units.Catalog[t].Ranged;
        }
    }

    // Counting sort of living soldiers into a dense, padded row-major grid.
    // The one-cell padding ring means the forward stencil never needs bounds checks.
    private int Build(UnitState units, float cellWidth, byte[] voluntaryMovement)
    {
        int count = units.Count;
        Vector3[] position = units.Position;
        float[] health = units.Health;
        byte[] type = units.Type;

        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        int alive = 0;
        for (int id = 0; id < count; id++)
        {
            if (health[id] <= 0) continue;
            Vector3 p = position[id];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Z < minZ) minZ = p.Z;
            if (p.Z > maxZ) maxZ = p.Z;
            alive++;
        }
        if (alive < 2) return alive;

        double inverse, originX, originZ;
        int width, height;
        int gridBudget = Math.Min(MaxGridCells, Math.Max(4096, alive * 8));
        while (true)
        {
            inverse = 1.0 / cellWidth;
            // Rebase the array without sliding the cell boundaries as armies move.
            originX = Math.Floor(minX * inverse);
            originZ = Math.Floor(minZ * inverse);
            double requiredWidth = Math.Floor(maxX * inverse) - originX + 3;
            double requiredHeight = Math.Floor(maxZ * inverse) - originZ + 3;
            if (requiredWidth * requiredHeight <= gridBudget)
            {
                width = (int)requiredWidth;
                height = (int)requiredHeight;
                break;
            }
            cellWidth *= 1.5f;
        }
        int cells = width * height;
        if (cellStart.Length < cells + 1) cellStart = new int[Math.Min(MaxGridCells + 1, cells + 1 + (cells >> 2))];
        else Array.Clear(cellStart, 0, cells + 1);

        for (int id = 0; id < count; id++)
        {
            if (health[id] <= 0) continue;
            Vector3 p = position[id];
            int cx = (int)(Math.Floor(p.X * inverse) - originX) + 1;
            int cz = (int)(Math.Floor(p.Z * inverse) - originZ) + 1;
            int cell = cz * width + cx;
            cellOfId[id] = cell;
            cellStart[cell]++;
        }

        // Inclusive prefix sum gives each cell's END. Scattering in reverse with a pre-decrement
        // leaves cellStart[c] as the cell's START, and cellStart[c + 1] as its end.
        int run = 0;
        for (int c = 0; c < cells; c++) { run += cellStart[c]; cellStart[c] = run; }
        cellStart[cells] = run;

        for (int id = count - 1; id >= 0; id--)
        {
            if (health[id] <= 0) continue;
            int slot = --cellStart[cellOfId[id]];
            Vector3 p = position[id];
            slotId[slot] = id;
            slotX[slot] = p.X;
            slotZ[slot] = p.Z;
            slotRadius[slot] = footprintByType[type[id]];
            if (voluntaryMovement != null)
            {
                slotFlags[slot] = (byte)((units.Faction[id] << 1) |
                    (rangedByType[type[id]] ? RangedRole : 0) |
                    (voluntaryMovement[id] != 0 ? VoluntaryMove : 0));
            }
        }
        gridWidth = width;
        gridCells = cells;
        return alive;
    }

    // Forward half-stencil: rest of own cell, cell (+1,0), and the three cells above ((-1,+1),(0,+1),(+1,+1)).
    // In row-major order the first two are contiguous in slot order, and so are the three above,
    // so each soldier scans exactly two linear ranges and every cross-cell pair is seen once.
    private void Scan(float gapLimit, int budget, int salt)
    {
        int w = gridWidth;
        int[] start = cellStart;
        float[] sx = slotX, sz = slotZ, sr = slotRadius;

        for (int c = 0; c < gridCells; c++)
        {
            int cellBegin = start[c], cellEnd = start[c + 1];
            if (cellBegin == cellEnd) continue;
            int endA = start[c + 2];
            int startB = start[c + w - 1], endB = start[c + w + 2];
            int lenB = endB - startB;

            for (int i = cellBegin; i < cellEnd; i++)
            {
                float xi = sx[i], zi = sz[i], reach = sr[i] + gapLimit;
                int firstA = i + 1;
                int lenA = endA - firstA;
                int total = lenA + lenB;

                if (total <= budget)
                {
                    candidateTotal += total;
                    for (int j = firstA; j < endA; j++)
                    {
                        float dx = xi - sx[j], dz = zi - sz[j];
                        float limit = reach + sr[j];
                        float d2 = dx * dx + dz * dz;
                        if (limit > 0f && d2 < limit * limit) Resolve(i, j, dx, dz, d2, limit);
                    }
                    for (int j = startB; j < endB; j++)
                    {
                        float dx = xi - sx[j], dz = zi - sz[j];
                        float limit = reach + sr[j];
                        float d2 = dx * dx + dz * dz;
                        if (limit > 0f && d2 < limit * limit) Resolve(i, j, dx, dz, d2, limit);
                    }
                }
                else
                {
                    // Over budget: test a cyclic window of `budget` candidates from a pseudo-random
                    // start (deterministic per soldier and tick). The starting slot rotates within the
                    // candidates owned by this soldier.
                    BudgetHits++;
                    candidateTotal += budget;
                    uint hash = unchecked((uint)slotId[i] * 2654435761u + (uint)salt * 40503u);
                    int index = (int)(hash % (uint)total);
                    for (int k = 0; k < budget; k++)
                    {
                        int j = index < lenA ? firstA + index : startB + (index - lenA);
                        float dx = xi - sx[j], dz = zi - sz[j];
                        float limit = reach + sr[j];
                        float d2 = dx * dx + dz * dz;
                        if (limit > 0f && d2 < limit * limit) Resolve(i, j, dx, dz, d2, limit);
                        if (++index == total) index = 0;
                    }
                }
            }
        }
    }

    private void Resolve(int i, int j, float dx, float dz, float d2, float limit)
    {
        if (trample?.IgnoreSeparation(slotId[i], slotId[j]) == true) return;
        if (yieldEnabled)
        {
            byte a = slotFlags[i], b = slotFlags[j];
            int difference = a ^ b;
            // Gate the exception on faction before considering the two roles.
            if ((difference & FactionMask) == 0 && (difference & RangedRole) != 0)
            {
                ResolveAlliedPair(i, j, dx, dz, d2, limit, a, b);
                return;
            }
        }

        float distance = MathF.Sqrt(d2);
        float nx, nz;
        if (distance > 0.00001f)
        {
            float inverse = 1f / distance;
            nx = dx * inverse;
            nz = dz * inverse;
        }
        else
        {
            Vector2 d = CoincidentDirection(slotId[i], slotId[j]);
            nx = d.X;
            nz = d.Y;
        }
        float penetration = limit - distance;
        float half = penetration * 0.5f;
        float px = nx * half, pz = nz * half;
        slotPushX[i] += px; slotPushZ[i] += pz; slotContacts[i]++;
        slotPushX[j] -= px; slotPushZ[j] -= pz; slotContacts[j]++;

        OverlapPairs++;
        penetrationSum += penetration;
        if (penetration > MaxPenetration) MaxPenetration = penetration;
    }

    private void ResolveAlliedPair(int i, int j, float dx, float dz, float d2, float fullLimit, byte a, byte b)
    {
        bool firstIsMelee = (a & RangedRole) == 0;
        byte melee = firstIsMelee ? a : b;
        byte archer = firstIsMelee ? b : a;
        bool yielding = (melee & VoluntaryMove) != 0 && (archer & VoluntaryMove) == 0;
        float limit = yielding ? Mathf.Min(fullLimit, (slotRadius[i] + slotRadius[j]) * yieldSettings.ClearanceFraction) : fullLimit;
        if (d2 >= limit * limit)
        {
            YieldSkipped++;
            return;
        }

        float distance = MathF.Sqrt(d2);
        Vector2 direction = distance > 0.00001f
            ? new Vector2(dx / distance, dz / distance)
            : CoincidentDirection(slotId[i], slotId[j]);
        float penetration = limit - distance;
        float pairCap = yielding ? yieldSettings.PairCap : yieldSettings.ExitCap;
        float share = yielding ? (firstIsMelee ? yieldSettings.MeleeShare : 1f - yieldSettings.MeleeShare) : 0.5f;
        Vector2 correction = direction * Mathf.Min(penetration, pairCap);
        yieldPush[i] += correction * share;
        yieldPush[j] -= correction * (1f - share);
        if (yielding) YieldPushed++;
        else if (penetration > pairCap) ExitCapped++;
    }

    private static Vector2 CoincidentDirection(int id, int other)
    {
        uint low = (uint)Math.Min(id, other), high = (uint)Math.Max(id, other);
        uint hash = unchecked((low * 73856093u) ^ (high * 19349663u));
        float angle = (hash & 255u) * (Mathf.Tau / 256f);
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return id < other ? direction : -direction;
    }

    private void Apply(UnitState units, int alive, float cap, ObstacleField obstacles)
    {
        float capSquared = cap * cap;
        for (int s = 0; s < alive; s++)
        {
            float px = slotPushX[s], pz = slotPushZ[s];
            int contacts = slotContacts[s];
            // Same contact damping as before: full response for isolated pairs, averaged when packed.
            if (contacts > 2)
            {
                float damp = 2f / contacts;
                px *= damp;
                pz *= damp;
            }
            if (yieldEnabled)
            {
                px += yieldPush[s].X;
                pz += yieldPush[s].Y;
            }
            float lengthSquared = px * px + pz * pz;
            if (lengthSquared == 0f) continue;
            if (lengthSquared > capSquared)
            {
                float scale = cap / MathF.Sqrt(lengthSquared);
                px *= scale;
                pz *= scale;
            }
            int id = slotId[s];
            Vector3 position = units.Position[id];
            position.X += px;
            position.Z += pz;
            if (obstacles != null)
            {
                Vector2 safe = obstacles.ClipMove(ObstacleField.Xz(units.Position[id]), ObstacleField.Xz(position),
                    footprintByType[units.Type[id]]);
                position.X = safe.X;
                position.Z = safe.Y;
            }
            position.Y = BattlefieldHeight.At(position.X, position.Z);
            units.Position[id] = position;
            units.Dirty[id] = true;
        }
    }
}
