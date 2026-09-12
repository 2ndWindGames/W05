#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Cozy
{
    public static class CozyChecks
    {
        public static string Run()
        {
            var report=new StringBuilder();int count=0;
            void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);report.AppendLine("PASS: "+name);count++;}
            CozyRun New(Animal a=Animal.Penguin,Region region=Region.Snowfield,Difficulty d=Difficulty.Normal)=>new CozyRun(a,region,d,false,12345);
            void Choices(CozyRun r)
            {
                r.ResolveChoices();int guard=0;
                while(r.phase==RunPhase.LevelChoice||r.phase==RunPhase.RelicChoice)
                {if(++guard>40)throw new Exception("Choice queue did not drain");int c=r.choices.Contains(-1)?-1:r.choices[0];r.Choose(c,1);}
            }
            var run=New();run.spawnTimer=999;
            for(int i=0;i<60;i++)run.Tick(CozyRules.Step,Vector2.right);
            Check(run.position.x>205&&run.position.x<225,"movement accelerates to 220 px/s");
            for(int i=0;i<10;i++)run.Tick(CozyRules.Step,Vector2.zero);
            Check(run.velocity.sqrMagnitude<.001f,"release stops movement");
            run.position=Vector2.zero;for(int i=0;i<60;i++)run.Tick(CozyRules.Step,Vector2.one);
            Check(run.velocity.magnitude<220.01f,"diagonal speed normalization");
            Check(run.ResolvePosition(new Vector2(1000,1000))==new Vector2(592,312),"playfield boundary follows 48px margin");
            foreach(Region map in Enum.GetValues(typeof(Region))){var r=New(region:map);foreach(var b in r.blockers)Check(Vector2.Distance(r.ResolvePosition(b),b)>=CozyRules.BlockerRadius(map)+12.99f,"obstacle excludes feet "+map);}
            run.Pause();string paused=JsonUtility.ToJson(run);run.Tick(.05f,Vector2.one);Check(JsonUtility.ToJson(run)==paused,"pause freezes full simulation");run.Resume();
            run=New();run.spawnTimer=999;run.DamagePlayer("first",Vector2.right);run.DamagePlayer("second",Vector2.left);
            Check(run.hp==8&&run.lastDamage=="first","same-frame contacts share invulnerability and damage source");
            run=New(d:Difficulty.Cozy);run.DamagePlayer("cozy",Vector2.left);Check(run.hp==9,"Cozy difficulty uses one HP damage");
            run.shield=true;run.invincible=0;run.DamagePlayer("shield",Vector2.left);Check(run.hp==9&&!run.shield,"crystal consumes exactly once");
            run=New();run.spawnTimer=999;run.Spawn(EnemyRole.Basic,Vector2.right*88).warmup=0;run.Tick(.001f,Vector2.zero);
            Check(run.kills==1&&run.score==10,"snowflake contact scores exactly once");run.Tick(.001f,Vector2.zero);Check(run.score==10,"dead enemies cannot award score twice");
            run=New();run.spawnTimer=999;var inner=run.Spawn(EnemyRole.Tough,Vector2.right*42);inner.warmup=0;run.Tick(.001f,Vector2.zero);Check(inner.hp==30,"orbit interior is not a damage ring");
            run=New(Animal.Cat);run.spawnTimer=999;var far=run.Spawn(EnemyRole.Tough,Vector2.right*110);var near=run.Spawn(EnemyRole.Tough,Vector2.right*60);far.warmup=near.warmup=0;
            for(int i=0;i<24;i++)run.Tick(CozyRules.Step,Vector2.zero);
            Check(near.hp==20&&far.hp==30,"letter stops at first collision");
            run=New(Animal.Capybara);run.spawnTimer=999;var waveTarget=run.Spawn(EnemyRole.Tough,Vector2.right*90);waveTarget.warmup=0;
            for(int i=0;i<36;i++)run.Tick(CozyRules.Step,Vector2.zero);
            Check(waveTarget.hp==20,"wave damages an enemy once per instance");
            run=New();run.AddXP(504);run.ResolveChoices();Check(run.level==15&&run.pendingLevels==14,"504 XP produces exactly fourteen queued choices");
            Check(run.choices.Contains(-1)&&new System.Collections.Generic.HashSet<int>(run.choices).Count==3,"level 2 guarantees weapon and unique offers");
            Check(run.Reroll()&&run.Reroll()&&!run.Reroll(),"two rerolls per run");Choices(run);
            Check(run.weaponLevel==5&&run.branch==1&&run.pendingLevels==0,"max-level choice queue drains with permanent final branch");
            int slots=0;foreach(int p in run.passives){if(p>0)slots++;Check(p<=3,"passive rank cap");}Check(slots<=4,"four passive slots maximum");
            Check(!run.Choose(-1,2)&&run.branch==1,"cannot change final branch outside a valid choice");
            foreach(Animal a in Enum.GetValues(typeof(Animal)))for(int branch=1;branch<=2;branch++){var w=CozyRules.Weapon(a,5,branch);Check(w.count<=5&&w.damage>=16&&w.interval>=.5f,"final weapon data "+a+" "+branch);}
            foreach(Region r in Enum.GetValues(typeof(Region)))
            {
                run=New(region:r);run.spawnTimer=999;run.StartObjective();
                if(r==Region.Springs){run.position=run.objectivePoints[0];for(int i=0;i<601;i++)run.Tick(CozyRules.Step,Vector2.zero);}
                else for(int i=0;i<run.objectivePoints.Length;i++){run.position=run.objectivePoints[i];run.Tick(CozyRules.Step,Vector2.zero);if(r==Region.PostOffice){run.position=Vector2.zero;run.Tick(CozyRules.Step,Vector2.zero);}}
                Check(run.objectives==1&&run.score==300&&run.phase==RunPhase.RelicChoice,"objective success and reward "+r);
                run.hp=5;run.choices.Clear();run.choices.Add(1);run.Choose(1);Check(run.hp==7,"new yuzu relic heals on the objective that awarded it "+r);
            }
            run=New();run.StartObjective();run.objectiveRemaining=.001f;run.Tick(.01f,Vector2.zero);Check(run.objectives==0&&run.phase==RunPhase.Playing,"optional objective timeout does not fail run");
            run=New();run.spawnTimer=999;run.AddXP(10);run.StartObjective();run.CompleteObjective();run.ResolveChoices();Check(run.phase==RunPhase.LevelChoice,"growth precedes simultaneous relic reward");run.Choose(run.choices[0]);Check(run.phase==RunPhase.RelicChoice,"relic reward follows growth queue");
            run=New();for(int i=0;i<190;i++)run.Spawn(EnemyRole.Basic);Check(run.enemies.Count==160,"normal enemy cap drops excess requests");
            run=New();for(int i=0;i<15;i++)run.Spawn(EnemyRole.Ranged);Check(run.enemies.FindAll(e=>e.role==EnemyRole.Ranged).Count==6,"ranged cap six with basic fallback");
            run=New();for(int i=0;i<15;i++)run.Spawn(EnemyRole.Charger);Check(run.enemies.FindAll(e=>e.role==EnemyRole.Charger).Count==8,"charger cap eight");
            run=New();for(int i=0;i<15;i++)run.Spawn(EnemyRole.Support);Check(run.enemies.FindAll(e=>e.role==EnemyRole.Support).Count==3,"support cap three");
            run=New();run.spawnTimer=999;run.elapsed=719.99f;run.Tick(.02f,Vector2.zero);
            Check(run.bossSpawned&&run.enemies.Count==1&&run.enemies[0].role==EnemyRole.Boss,"720 seconds switches to isolated boss encounter");
            Check(run.objectiveRemaining==0&&!run.carrying,"boss encounter clears optional objective markers");
            run.nextObjective=3;run.nextHeal=3;run.elapsed=run.bossStarted+90;run.invincible=1;run.Tick(.001f,Vector2.zero);Check(run.phase==RunPhase.Lost&&run.resultReason=="boss_timeout","boss timeout produces a result");
            for(int a=0;a<3;a++)for(int r=0;r<3;r++)for(int d=0;d<3;d++)
            {
                run=New((Animal)a,(Region)r,(Difficulty)d);run.elapsed=720;run.Tick(CozyRules.Step,Vector2.zero);
                var boss=run.enemies.Find(e=>e.role==EnemyRole.Boss);run.Hit(boss,10000,100);
                var p=new CozyProfile();int reward=p.Settle(run);
                Check(run.phase==RunPhase.Won&&p.cleared[r]&&reward==150,"win/unlock/first-clear rewards "+a+"/"+r+"/"+d);
                Check(p.Settle(run)==0&&p.stars==150,"idempotent settlement "+a+"/"+r+"/"+d);
            }
            run=New();run.elapsed=720;run.objectives=3;run.phase=RunPhase.Won;var profile=new CozyProfile();Check(profile.Settle(run)==195,"full first clear pays 195 stars");
            string snapshot=JsonUtility.ToJson(run);var duplicate=JsonUtility.FromJson<CozyRun>(snapshot);duplicate.rewardClaimed=false;Check(profile.Settle(duplicate)==0,"run ID blocks replaying old unclaimed snapshot");
            Check(profile.cleared[0]&&!profile.cleared[1]&&!profile.cleared[2]&&profile.RegionUnlocked(2),"all regions are available without inventing first-clear records");
            Check(!profile.BuyDecoration(0)&&profile.stars==195&&!profile.BuyDecoration(11)&&Array.TrueForAll(profile.cosmetics,x=>!x),"included decorations never charge stars or invent purchase records");
            run=New();run.endless=true;run.elapsed=1000;run.objectives=3;run.Retire();Check(run.CalculateStars()==105,"endless and retirement rewards cap time at twelve minutes");
            string folder=System.IO.Path.Combine("Temp","CozySaveChecks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            var disk=new CozySave(System.IO.Path.Combine(folder,"profile.json"));profile=new CozyProfile();run=New(Animal.Cat);profile.activeRun=run;
            for(int i=0;i<600;i++){run.invincible=2;run.Tick(CozyRules.Step,new Vector2(Mathf.Sin(i*.02f),Mathf.Cos(i*.02f)));Choices(run);}disk.Write(profile);
            var restored=disk.Load();Check(JsonUtility.ToJson(restored.activeRun)==JsonUtility.ToJson(run),"save restores every simulation field");
            for(int i=0;i<300;i++){Vector2 input=new Vector2(Mathf.Sin(i*.01f),Mathf.Cos(i*.01f));run.invincible=2;restored.activeRun.invincible=2;run.Tick(CozyRules.Step,input);restored.activeRun.Tick(CozyRules.Step,input);Choices(run);Choices(restored.activeRun);}
            Check(JsonUtility.ToJson(restored.activeRun)==JsonUtility.ToJson(run),"resumed simulation preserves RNG, collisions and choices");
            disk.Write(profile);File.WriteAllText(disk.Path,"broken");restored=disk.Load();Check(disk.Notice=="backup"&&restored.activeRun!=null,"corrupt primary recovers from verified backup");
            disk.Write(restored);Check(new CozySave(disk.Path).Load().activeRun!=null,"recovery can atomically save again");
            File.WriteAllText(disk.Path,"broken");File.WriteAllText(disk.Path+".bak","broken");disk.Load();Check(disk.ReadOnly&&disk.Notice=="damaged","both corrupt saves are preserved without overwrite");
            var homeDisk=new CozySave(System.IO.Path.Combine(folder,"home.json"));
            homeDisk.Write(new CozyProfile{stars=37});var homeProfile=homeDisk.Load();
            Check(!homeDisk.ReadOnly&&homeProfile.activeRun==null&&homeProfile.stars==37,"profile without an active run round-trips Unity's inline null sentinel");
            var settledProfile=new CozyProfile{activeRun=New()};settledProfile.activeRun.Retire();settledProfile.Settle(settledProfile.activeRun);
            homeDisk.Write(settledProfile);var settledRestored=homeDisk.Load();
            Check(!homeDisk.ReadOnly&&settledRestored.activeRun==null&&settledRestored.settledRuns.Count==1,"settled run reloads as a home profile without losing rewards or history");
            var badChecksum=JsonUtility.FromJson<CozySaveEnvelope>(File.ReadAllText(homeDisk.Path));badChecksum.sha256="invalid";
            File.WriteAllText(homeDisk.Path,JsonUtility.ToJson(badChecksum));homeProfile=homeDisk.Load();
            Check(homeDisk.Notice=="backup"&&!homeDisk.ReadOnly&&homeProfile.stars==37,"checksum InvalidDataException recovers the known-good backup");
            var malformed=new CozyProfile{activeRun=New()};malformed.activeRun.rng=0;
            string malformedPayload=JsonUtility.ToJson(malformed);string malformedHash;
            using(var hash=System.Security.Cryptography.SHA256.Create())malformedHash=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(malformedPayload)));
            string malformedEnvelope=JsonUtility.ToJson(new CozySaveEnvelope{payload=malformedPayload,sha256=malformedHash});
            File.WriteAllText(homeDisk.Path,malformedEnvelope);homeProfile=homeDisk.Load();
            Check(homeDisk.Notice=="backup"&&!homeDisk.ReadOnly&&homeProfile.stars==37,"invalid real run is rejected instead of treated as an empty run");
            File.WriteAllText(homeDisk.Path+".bak",malformedEnvelope);homeDisk.Load();
            Check(homeDisk.ReadOnly&&homeDisk.Notice=="damaged"&&File.ReadAllText(homeDisk.Path)==malformedEnvelope,"two structurally invalid saves remain intact and cannot crash startup");
            var testProfile=new CozyProfile{stars=500};
            Check(!testProfile.settings.testUnlockAll&&!testProfile.settings.testUnlockConfigured&&testProfile.AnimalUnlocked(2)&&testProfile.RegionUnlocked(2),"fresh profiles have all content without initialization or a test flag");
            for(int i=0;i<3;i++)Check(testProfile.AnimalUnlocked(i)&&testProfile.RegionUnlocked(i)&&testProfile.EndlessUnlocked(i)&&testProfile.LettersUnlocked(i),"all-content edition opens animal, region, endless and letters "+i);
            bool allDecorations=true;for(int i=0;i<12;i++)allDecorations&=testProfile.DecorationAvailable(i);
            Check(allDecorations&&!testProfile.BuyDecoration(0)&&testProfile.stars==500,"all twelve decorations are available without spending stars");
            Check(Array.TrueForAll(testProfile.cleared,x=>!x)&&Array.TrueForAll(testProfile.cosmetics,x=>!x)&&Array.TrueForAll(testProfile.achievements,x=>!x)&&testProfile.records.Count==0,"content access preserves actual progress, purchases, achievements and records");
            Check(!testProfile.AnimalUnlocked(-1)&&!testProfile.AnimalUnlocked(3)&&!testProfile.RegionUnlocked(-1)&&!testProfile.RegionUnlocked(3)&&!testProfile.EndlessUnlocked(-1)&&!testProfile.EndlessUnlocked(3)&&!testProfile.LettersUnlocked(-1)&&!testProfile.LettersUnlocked(3)&&!testProfile.DecorationAvailable(-1)&&!testProfile.DecorationAvailable(12),"all-content edition rejects invalid IDs for every content type");
            var testDisk=new CozySave(System.IO.Path.Combine(folder,"test-unlocks.json"));testDisk.Write(testProfile);var testRestored=testDisk.Load();
            Check(testRestored.RegionUnlocked(2)&&testRestored.DecorationAvailable(11)&&testRestored.stars==500,"all content remains available after a fresh save and reload");
            testRestored.settings.testUnlockAll=false;testRestored.settings.testUnlockConfigured=true;testRestored.settings.testUnlockRevision=1;testDisk.Write(testRestored);testRestored=testDisk.Load();
            Check(testRestored.AnimalUnlocked(2)&&testRestored.RegionUnlocked(2)&&testRestored.EndlessUnlocked(2)&&testRestored.LettersUnlocked(2)&&testRestored.DecorationAvailable(11),"v0.3.3 and v0.3.4 profiles saved with OFF still have every content type");
            testRestored.cleared[0]=true;testRestored.cosmetics[0]=true;
            testDisk.Write(testRestored);testRestored=testDisk.Load();
            Check(testRestored.cleared[0]&&!testRestored.cleared[1]&&testRestored.cosmetics[0]&&!testRestored.cosmetics[1]&&testRestored.RegionUnlocked(2)&&testRestored.DecorationAvailable(11)&&testRestored.stars==500,"existing clears, purchases and stars survive alongside all-content access");
            testRestored.settings.testUnlockRevision=0;testDisk.Write(testRestored);testRestored=testDisk.Load();
            Check(testRestored.AnimalUnlocked(2)&&testRestored.RegionUnlocked(2)&&testRestored.EndlessUnlocked(2)&&testRestored.LettersUnlocked(2)&&testRestored.DecorationAvailable(11),"pre-migration profiles saved with OFF also have every content type");
            testRestored.settings.testUnlockAll=true;testDisk.Write(testRestored);testRestored=testDisk.Load();
            Check(testRestored.AnimalUnlocked(2)&&testRestored.RegionUnlocked(2)&&testRestored.EndlessUnlocked(2)&&testRestored.LettersUnlocked(2)&&testRestored.DecorationAvailable(11),"old ON preferences remain compatible with all-content access");
            // Advance 30 minutes at 60 fixed steps/s. This checks bounds, not hardware frame rate.
            run=New(Animal.Capybara);run.endless=true;run.weaponLevel=5;run.branch=2;
            for(int i=0;i<108000;i++)
            {
                run.invincible=2;run.Tick(CozyRules.Step,new Vector2(Mathf.Sin(i*.003f),Mathf.Cos(i*.004f)));Choices(run);
                if(run.Finished)throw new Exception("Unexpected ending in endless stress simulation");
            }
            Check(run.elapsed>1798&&run.enemies.Count<=161&&run.attacks.Count<=CozyRules.ProjectileCap+CozyRules.AttackCap&&run.pickups.Count<=260,"thirty-minute endless simulation remains bounded");
            report.AppendLine($"TOTAL: {count} checks passed / rules {CozyRules.Version}");return report.ToString();
        }
    }
}
#endif
