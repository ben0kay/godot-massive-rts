// Shared bomber-only engine plumes and bounded world-space smoke.
using Godot;

namespace MassiveRts;

internal sealed class BomberExhaust
{
    private const int SmokeCapacity=1024;
    private const float SampleInterval=0.085f, Lifetime=1.45f;
    private readonly record struct Puff(Vector3 Origin,float Born,float Seed);
    private readonly MultiMesh flames, smoke;
    private readonly Puff[] puffs;
    private readonly bool enhanced;
    private int head,count;
    private uint sequence;
    private float clock,sampleTimer;
    private bool smokeDirty;
    private static Mesh flameMesh;

    internal static bool Supports(UnitDefinition unit) =>
        unit.Id == "heavy_bomber" && unit.CanBombGround && unit.AircraftMesh == null;

    internal BomberExhaust(Node3D parent,int bomberCount,UnitVisualConfig visuals)
    {
        enhanced=visuals.UseEnhancedBomberMesh;
        if (visuals.UseBomberEngineFlames)
            flames=Batch(parent,flameMesh ??= CreateFlame(),bomberCount*2,false);
        if (visuals.UseBomberSmokeTrails)
        {
            puffs=new Puff[SmokeCapacity];
            var material=new ShaderMaterial
            {
                Shader=GD.Load<Shader>("res://GAMEPLAY/COMBAT/AIR/BomberExhaustSmoke.gdshader")
            };
            smoke=Batch(parent,new QuadMesh { Material=material },SmokeCapacity,true);
        }
    }

    private static MultiMesh Batch(Node3D parent,Mesh mesh,int capacity,bool custom)
    {
        var batch=new MultiMesh
        {
            TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData=custom,Mesh=mesh,InstanceCount=capacity,VisibleInstanceCount=0
        };
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh=batch,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin=3f
        });
        return batch;
    }

    // Enhanced rear panels end at z=-0.8975; the airframe points +Z.
    private Vector3 Port(int side) => enhanced
        ? new Vector3(side*0.94f,0.0775f,-0.90f)
        : new Vector3(side*0.62f,0f,-0.70f);

    internal void Advance(AirSquadron.Fighter[] fighters,float delta)
    {
        clock+=delta;
        if (smoke==null) return;
        sampleTimer+=delta;
        if (sampleTimer<SampleInterval) return;
        sampleTimer%=SampleInterval;
        smokeDirty=true;
        for (int i=0;i<fighters.Length;i++)
        {
            ref readonly var fighter=ref fighters[i];
            if (fighter.Health<=0f || !Supports(fighter.Unit)) continue;
            Basis basis=AirSquadronVisuals.Pose(in fighter);
            for (int side=-1;side<=1;side+=2)
            {
                float seed=(unchecked(++sequence*2654435761u)>>16)/65535f;
                Vector3 origin=fighter.Position+basis*(Port(side)+new Vector3(0,0,-0.72f));
                puffs[head]=new Puff(origin,clock,seed);
                head=(head+1)%SmokeCapacity;
                count=Mathf.Min(count+1,SmokeCapacity);
            }
        }
    }

    internal void Sync(AirSquadron.Fighter[] fighters)
    {
        if (flames!=null)
        {
            int slot=0;
            for (int i=0;i<fighters.Length;i++)
            {
                ref readonly var fighter=ref fighters[i];
                if (fighter.Health<=0f || !Supports(fighter.Unit)) continue;
                Basis basis=AirSquadronVisuals.Pose(in fighter);
                for (int side=-1;side<=1;side+=2)
                {
                    float flicker=Mathf.Sin(clock*29f+i*2.37f+side*0.83f);
                    var scale=Basis.Identity.Scaled(new Vector3(1f,1f,1f+flicker*0.13f));
                    flames.SetInstanceTransform(slot++,new Transform3D(basis*scale,fighter.Position+basis*Port(side)));
                }
            }
            flames.VisibleInstanceCount=slot;
        }
        // Only upload smoke at the sampling cadence. Old puffs survive aircraft deaths.
        if (smoke==null || !smokeDirty) return;
        smokeDirty=false;
        while (count>0)
        {
            int oldest=(head-count+SmokeCapacity)%SmokeCapacity;
            if (clock-puffs[oldest].Born<Lifetime) break;
            count--;
        }
        for (int slot=0;slot<count;slot++)
        {
            var puff=puffs[(head-count+slot+SmokeCapacity)%SmokeCapacity];
            float age=Mathf.Max(0f,clock-puff.Born);
            Vector3 drift=new((puff.Seed-0.5f)*age*0.42f,age*0.38f,0);
            smoke.SetInstanceTransform(slot,new Transform3D(Basis.Identity,puff.Origin+drift));
            smoke.SetInstanceCustomData(slot,new Color(Mathf.Clamp(age/Lifetime,0f,1f),puff.Seed,0f,0f));
        }
        smoke.VisibleInstanceCount=count;
    }

    private static Mesh CreateFlame()
    {
        using var surface=new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new ShaderMaterial
        {
            Shader=GD.Load<Shader>("res://GAMEPLAY/COMBAT/AIR/BomberEngineFlame.gdshader")
        });
        const int sides=8;
        Color root=new("#fff4b6"),middle=new("#ffb12e"),tip=new("#ed4911");
        Vector3 Ring(int i,float radius,float z)
        {
            float angle=i*Mathf.Tau/sides;
            return new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*0.8f,z);
        }
        void Vertex(Vector3 point,Color color)
        {
            surface.SetColor(color);
            surface.AddVertex(point);
        }
        for (int i=0;i<sides;i++)
        {
            Vector3 a=Ring(i,0.12f,0),b=Ring(i+1,0.12f,0);
            Vector3 c=Ring(i+1,0.17f,-0.27f),d=Ring(i,0.17f,-0.27f);
            Vertex(a,root); Vertex(b,root); Vertex(c,middle);
            Vertex(a,root); Vertex(c,middle); Vertex(d,middle);
            Vertex(d,middle); Vertex(c,middle); Vertex(new Vector3(0,0,-1.20f),tip);
        }
        return surface.Commit();
    }
}
