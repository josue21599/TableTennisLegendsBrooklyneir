using System; using System.Collections.Generic;
using Brooklyneir.Config; using Brooklyneir.Core; using Brooklyneir.Gameplay; using Brooklyneir.Physics; using Brooklyneir.Rackets; using UnityEngine;
public static class WallReturnSweep {
  static bool AIM=false; static bool LAT=System.Environment.GetEnvironmentVariable("LAT")!=null; static float VAR=0f; public static void Main(string[] a){ if(a.Length>0) VAR=float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture);
    var cfg=new BalanceConfig(); cfg.Validate(); var pw=cfg.practiceWall;
    var stats=new RacketStatModel(new RacketStats(5,5,5,5,5), cfg.racket);
    var rng=new GameRandom(3); var model=new ShotModel(cfg,rng);
    var wall=new WallCollision(TableGeometry.HalfLength+pw.wallOffset,pw.wallHalfWidth,pw.wallHeight,pw.wallRestitution,pw.wallFriction,cfg.physics.spinDecayOnBounce){MaxReturnSpeed=pw.wallMaxReturnSpeed, Physics=cfg.physics, ReturnBounceDepth=pw.returnBounceDepth, ReturnNetClearance=pw.returnNetClearance, ReturnVariation=VAR, Seed=77};
    var sim=new BallSimulation(cfg.physics){Wall=wall}; var buf=new List<BallContactEvent>(8);
    int[] reasons=new int[7]; int totalHits=0, rallies=300; int[] hist=new int[6];
    for(int n=0;n<rallies;n++){
      float racketZ=-rng.Range(1.5f,2.3f);
      var refe=new PracticeWallReferee();
      var c=new ShotContext(); c.Hitter=0; c.Stats=stats; c.SwingMultiplier=1; c.NoiseMultiplier=cfg.shots.humanNoiseMultiplier; c.IsServe=true;
      c.ContactPoint=new Vector3(rng.Range(-0.4f,0.4f),TableGeometry.TopY+0.26f,racketZ+0.12f); var r=model.Compute(c);
      sim.Launch(c.ContactPoint,r.Velocity,r.SpinAccel); refe.OnServe(); int hits=0;
      for(int k=0;k<20000&&sim.Active;k++){
        Vector3 before=sim.Position; buf.Clear(); sim.Substep(cfg.physics.substep,buf); bool lost=false;
        foreach(var e in buf){ if(AIM && e.Type==BallContactType.Wall){ Vector3 p0=sim.Position; float hs=Mathf.Min(Mathf.Abs(e.VelocityBefore.z)*pw.wallRestitution, pw.wallMaxReturnSpeed); Vector3 tgt=new Vector3(p0.x*0.6f, TableGeometry.TopY+cfg.physics.ballRadius, -0.75f); Vector3 flat=tgt-p0; flat.y=0; var sol=ShotSolver.SolveFlattened(p0,tgt,flat.magnitude/Mathf.Max(1f,hs),new Vector3(0,-cfg.physics.gravity,0),cfg.physics,0.3f,0.75f); sim.Launch(p0,sol.Velocity,Vector3.zero);} var o=refe.OnContact(e.Type,e.Side); if(o.Lost){reasons[(int)o.Reason]++; lost=true;break;} }
        if(lost)break;
        if(refe.CanHit && (before.z-racketZ)*(sim.Position.z-racketZ)<=0){
          float f=(before.z-racketZ)/(before.z-sim.Position.z); Vector3 cp=Vector3.Lerp(before,sim.Position,f); float h=cp.y-TableGeometry.TopY;
          if(h>=cfg.racket.minHittableHeight && h<=cfg.racket.maxHittableHeight){
            var hc=new ShotContext(); hc.Hitter=0; hc.Stats=stats; hc.SwingMultiplier=1; hc.NoiseMultiplier=cfg.shots.humanNoiseMultiplier; hc.ContactPoint=cp; hc.RacketVelocity=new Vector3(LAT? rng.Range(-3f,3f):0f,0,LAT? rng.Range(0f,2f):0f);
            var hr=model.Compute(hc); refe.OnRacketHit(); sim.Launch(cp,hr.Velocity,hr.SpinAccel); hits++; if(hits>=20)break;
          }
        }
        var po=refe.OnBallPosition(sim.Position.x,sim.Position.y,sim.Position.z,4,5); if(po.Lost){reasons[(int)po.Reason]++; break;}
      }
      totalHits+=hits; hist[Math.Min(hits,5)]++;
    }
    Console.WriteLine($"returnVariation={VAR}  clamps={wall.ClampActivations}  Pala quieta bien colocada, {rallies} saques: devoluciones medias por saque {(float)totalHits/rallies:F2}");
    Console.WriteLine($"  rachas: 0={hist[0]} 1={hist[1]} 2={hist[2]} 3={hist[3]} 4={hist[4]} 5+={hist[5]}");
    Console.WriteLine($"  pérdidas: propio {reasons[1]} red {reasons[2]} fuera {reasons[3]} sinBote {reasons[4]} doble {reasons[5]} noDev {reasons[6]}");
  }
}
