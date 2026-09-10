using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cozy
{
    public sealed partial class CozyGame
    {
        void CreateCommercialIcons()
        {
            ring=Icon(64,(x,y)=>{float r=x*x+y*y;return r>.87f&&r<1?Color.white:Color.clear;});
            star=Icon(32,(x,y)=>Mathf.Abs(x)+Mathf.Abs(y)<.86f || Mathf.Abs(x)<.17f&&Mathf.Abs(y)<.96f || Mathf.Abs(y)<.17f&&Mathf.Abs(x)<.96f?Color.white:Color.clear);
            letter=Icon(32,(x,y)=>{if(Mathf.Abs(x)>.9f||Mathf.Abs(y)>.63f)return Color.clear;return Mathf.Abs(Mathf.Abs(x)*.65f+y-.3f)<.08f?Ink:Milk;});
        }
        void CreateScene()
        {
            var c=new GameObject("Cozy canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas=c.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            c.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            stage=Root("Safe landscape stage",c.transform,new Vector2(1280,720),Vector2.zero);
            background=Image("Aurora snowfield",stage,Resources.Load<Texture2D>("Cozy/Aurora"),new Vector2(1280,720),Vector2.zero,Color.white);
            terrain=Root("Region layout",stage,new Vector2(1280,720),Vector2.zero);
            var weatherRoot=Root("Decorative snowfall",stage,new Vector2(1280,720),Vector2.zero);
            for(int i=0;i<48;i++)weather.Add(Image("Snow mote",weatherRoot,disc,Vector2.one*(2+i%3),new Vector2(-620+i*26,(i*137)%680-340),new Color(1,1,1,.4f)));
            effects=Root("Combat visuals",stage,new Vector2(1280,720),Vector2.zero);
            actors=Root("Player",stage,new Vector2(1280,720),Vector2.zero);
            shadow=Image("Player shadow",actors,disc,new Vector2(44,13),Vector2.zero,new Color(.22f,.29f,.42f,.3f));
            penguin=Image("Animal",actors,idle[0],new Vector2(118,118),Vector2.zero,Color.white);
            hud=Root("HUD",stage,new Vector2(1280,720),Vector2.zero);
            Image("HUD paper",hud,Texture2D.whiteTexture,new Vector2(1280,47),new Vector2(0,336),new Color(.97f,.97f,1,.95f));
            healthLabel=Label(hud,"",17,new Vector2(-466,335),new Vector2(230,40),Ink);
            statusLabel=Label(hud,"",16,new Vector2(-138,335),new Vector2(390,40),Ink);
            scoreLabel=Label(hud,"",17,new Vector2(175,335),new Vector2(180,40),Pink);
            timeLabel=Label(hud,"",19,new Vector2(393,335),new Vector2(210,40),Ink);
            Button(hud,"II",new Vector2(576,335),new Vector2(62,38),Pause,true);
            Image("XP track",hud,Texture2D.whiteTexture,new Vector2(1184,5),new Vector2(0,308),new Color(.24f,.32f,.45f,.5f));
            xpFill=Image("XP",hud,Texture2D.whiteTexture,new Vector2(1,5),new Vector2(-592,308),new Color(.30f,.70f,.75f));xpFill.rectTransform.pivot=new Vector2(0,.5f);
            Image("Objective paper",hud,Texture2D.whiteTexture,new Vector2(1280,39),new Vector2(0,-339),new Color(.97f,.97f,1,.94f));
            hintLabel=Label(hud,"",17,new Vector2(0,-339),new Vector2(1220,36),Ink);
            joystick=Image("Touch origin",hud,disc,new Vector2(140,140),Vector2.zero,new Color(1,1,1,.25f));
            knob=Image("Touch thumb",hud,disc,new Vector2(50,50),Vector2.zero,new Color(1,1,1,.6f));
            overlay=Root("Menus",stage,new Vector2(1280,720),Vector2.zero);
            if(!FindFirstObjectByType<EventSystem>())new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            Resize();
        }
        void SetupTerrain()
        {
            foreach(Transform child in terrain){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            background.color=run.region==Region.Snowfield?Color.white:run.region==Region.Springs?new Color(.84f,1,.87f):new Color(.71f,.70f,.90f);
            for(int i=0;i<run.blockers.Length;i++)
            {
                Vector2 p=run.blockers[i];float radius=CozyRules.BlockerRadius(run.region);
                Color color=run.region==Region.Snowfield?new Color(.64f,.75f,.9f):run.region==Region.Springs?new Color(.48f,.64f,.61f):new Color(.64f,.47f,.41f);
                Image("Impassable obstacle",terrain,run.region==Region.PostOffice?rounded:disc,Vector2.one*radius*2,p,color);
                Image("Obstacle edge",terrain,ring,Vector2.one*radius*2,p,new Color(.24f,.30f,.44f,.8f));
                Label(terrain,run.region==Region.Snowfield?L("눈더미","Snow"):run.region==Region.Springs?L("온천석","Stone"):L("소포","Parcels"),14,p,new Vector2(90,35),Milk);
            }
            for(int i=0;i<12;i++)if(profile.DecorationAvailable(i))Image("Village decoration",terrain,i%3==0?star:i%3==1?disc:letter,Vector2.one*24,new Vector2(-550+i*100,-300),new Color(.88f,.67f,.45f));
        }
        RawImage Draw(Texture texture,Vector2 p,Vector2 size,Color color,float angle=0)
        {
            RawImage view;
            if(usedViews==entityViews.Count){view=Image("Pooled combat visual",effects,texture,size,p,color);entityViews.Add(view);}else view=entityViews[usedViews];
            usedViews++;view.gameObject.SetActive(true);view.texture=texture;view.material=null;view.color=color;
            view.rectTransform.anchoredPosition=p;view.rectTransform.sizeDelta=size;view.rectTransform.localRotation=Quaternion.Euler(0,0,angle);return view;
        }
        void Line(Vector2 a,Vector2 b,float width,Color color)
        {Vector2 d=b-a;Draw(Texture2D.whiteTexture,(a+b)*.5f,new Vector2(d.magnitude,width),color,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);}
        void RenderRun()
        {
            usedViews=0;
            float textScale=profile.settings.largeText?1.12f:1;
            healthLabel.fontSize=scoreLabel.fontSize=hintLabel.fontSize=Mathf.RoundToInt(17*textScale);
            statusLabel.fontSize=Mathf.RoundToInt(16*textScale);timeLabel.fontSize=Mathf.RoundToInt(19*textScale);
            for(int i=0;i<weather.Count;i++)
            {
                weather[i].gameObject.SetActive(!profile.settings.reducedDecoration);
                weather[i].rectTransform.anchoredPosition=new Vector2(-620+i*26+Mathf.Sin(run.elapsed*.3f+i)*10,Mathf.Repeat(i*137-run.elapsed*9,680)-340);
            }
            if(run.objectiveRemaining>0)
            {
                for(int i=0;i<run.objectivePoints.Length;i++)
                {
                    if(run.region!=Region.Springs&&run.collected[i])continue;
                    Vector2 p=run.objectivePoints[i];
                    if(run.region==Region.Springs){Draw(disc,p,Vector2.one*180,new Color(.36f,.83f,.66f,.17f));Draw(ring,p,Vector2.one*180,new Color(.20f,.61f,.43f));}
                    else{Draw(ring,p,Vector2.one*54,new Color(.71f,.47f,.12f));Draw(run.region==Region.Snowfield?star:letter,p,Vector2.one*32,new Color(1,.82f,.35f));}
                }
                if(run.region==Region.PostOffice){Draw(rounded,Vector2.zero,new Vector2(60,65),new Color(.4f,.49f,.77f));Draw(letter,new Vector2(0,10),new Vector2(40,30),Color.white);Draw(ring,Vector2.zero,Vector2.one*88,new Color(.98f,.82f,.4f));}
            }
            foreach(var p in run.pickups)Draw(p.kind==0?disc:heart,p.p,Vector2.one*(p.kind==0?10:24),p.kind==0?new Color(.22f,.65f,.69f):Pink);
            foreach(var a in run.attacks)
            {
                if(a.kind==1){float radius=a.radius*(a.age<.2f?.12f:Mathf.Clamp01((a.age-.2f)/.35f));Draw(ring,a.p,Vector2.one*radius*2,new Color(.19f,.58f,.70f,.8f));}
                else if(a.kind==3){Draw(a.age<1?ring:disc,a.p,Vector2.one*120,a.age<1?new Color(.85f,.32f,.28f):new Color(.88f,.39f,.22f,.58f));Draw(star,a.p,Vector2.one*25,new Color(.9f,.33f,.22f));}
                else Draw(a.kind==0?letter:disc,a.p,Vector2.one*(a.kind==0?21:18),a.kind==0?Milk:new Color(.73f,.17f,.34f),Mathf.Atan2(a.v.y,a.v.x)*Mathf.Rad2Deg);
            }
            foreach(var e in run.enemies)
            {
                if(e.hp<=0)continue;
                if(e.warmup>0){Vector2 edge=new Vector2(Mathf.Clamp(e.p.x,-608,608),Mathf.Clamp(e.p.y,-297,297));Draw(ring,edge,Vector2.one*42,new Color(.72f,.27f,.32f));Draw(star,edge,Vector2.one*18,Pink);continue;}
                Color color=e.role==EnemyRole.Charger?new Color(1,.66f,.62f):e.role==EnemyRole.Ranged?new Color(.85f,.65f,1):e.role==EnemyRole.Splitter?new Color(.67f,.9f,.65f):e.role==EnemyRole.Support?new Color(1,.86f,.47f):Color.white;
                float diameter=e.Radius*2+12;
                if(e.role==EnemyRole.Support)Draw(ring,e.p,Vector2.one*320,new Color(.59f,.62f,.15f,.35f));
                if(e.action==1&&(e.role==EnemyRole.Charger||e.role==EnemyRole.Elite||e.role==EnemyRole.Ranged||e.role==EnemyRole.Boss))
                {Line(e.p,e.p+e.aim*(e.role==EnemyRole.Ranged?350:220),e.role==EnemyRole.Ranged?3:18,new Color(.82f,.24f,.35f,.38f));Draw(star,e.p+e.aim*100,Vector2.one*25,Pink);}
                Draw(e.role==EnemyRole.Tough?toughSpirit:spirit,e.p+new Vector2(0,4),Vector2.one*diameter,color);
                if(e.role==EnemyRole.Charger)Draw(star,e.p+new Vector2(0,diameter*.45f),Vector2.one*17,Pink);
                if(e.role==EnemyRole.Ranged)Draw(letter,e.p+new Vector2(0,-7),Vector2.one*22,Ink);
                if(e.role==EnemyRole.Splitter){Draw(disc,e.p+new Vector2(-12,-16),Vector2.one*13,new Color(.3f,.55f,.36f));Draw(disc,e.p+new Vector2(12,-16),Vector2.one*13,new Color(.3f,.55f,.36f));}
                if(e.role>=EnemyRole.Elite){Draw(star,e.p+Vector2.up*diameter*.45f,Vector2.one*28,new Color(1,.76f,.18f));Draw(Texture2D.whiteTexture,e.p+new Vector2(0,diameter*.6f),new Vector2(diameter,6),Ink);Draw(Texture2D.whiteTexture,e.p+new Vector2((e.hp/e.maxHP-1)*diameter*.5f,diameter*.6f),new Vector2(diameter*e.hp/e.maxHP,6),Pink);}
            }
            if(run.animal==Animal.Penguin)for(int i=0;i<run.Weapon.count;i++)Draw(snowflake,run.FlakePosition(i),Vector2.one*37*run.RangeScale,Color.white,-run.orbitAngle*Mathf.Rad2Deg);
            if(run.shield)Draw(ring,run.position,Vector2.one*65,new Color(.34f,.68f,.9f));
            if(run.carrying)Draw(letter,run.position+new Vector2(0,69),Vector2.one*24,Color.white);
            for(int i=usedViews;i<entityViews.Count;i++)entityViews[i].gameObject.SetActive(false);
            bool isPenguin=run.animal==Animal.Penguin;
            penguin.material=isPenguin?penguinMaterial:companionMaterial;
            penguin.texture=isPenguin?(run.hp<=0?defeat[7]:run.invincible>.85f?hit[(int)(run.elapsed*12)%8]:run.velocity.sqrMagnitude>1?move[(int)(run.elapsed/.095f)%8]:AnimalIdle(run.animal,run.elapsed)):AnimalIdle(run.animal,run.elapsed);
            penguin.rectTransform.sizeDelta=Vector2.one*118;
            penguin.rectTransform.anchoredPosition=run.position+new Vector2(0,24);
            if(Mathf.Abs(run.velocity.x)>2)penguin.rectTransform.localScale=new Vector3(run.velocity.x<0?-1:1,1,1);
            penguin.color=new Color(1,1,1,run.invincible>0?.62f:1);shadow.rectTransform.anchoredPosition=run.position+new Vector2(0,-6);
            healthLabel.text=L($"체력 {run.hp}/{run.MaxHP}",$"HP {run.hp}/{run.MaxHP}")+(run.shield?L(" · 보호막"," · Shield"):"");
            statusLabel.text=AnimalName(run.animal)+$" Lv.{run.level}  /  "+L("무기 ","Weapon ")+run.weaponLevel+(run.branch>0?(run.branch==1?" A":" B"):"");
            scoreLabel.text=run.score+L(" 점"," pts");
            timeLabel.text=run.bossSpawned?L("보스 ","Boss ")+TimeText(Mathf.Max(0,90-(run.elapsed-run.bossStarted))):TimeText(run.elapsed)+(run.endless?"":" / 12:00");
            xpFill.rectTransform.sizeDelta=new Vector2(1184*(run.level>=15?1:Mathf.Clamp01(run.xp/CozyRules.XPRequired(run.level))),5);
            if(saveFailed)hintLabel.text=L("저장 실패 · 저장 공간과 권한을 확인해 주세요.","Save failed · Check storage space and file permissions.");
            else if(run.objectiveRemaining>0)hintLabel.text=run.region==Region.Snowfield?L($"별조각 {run.objectiveCollected}/5 · 남은 {run.objectiveRemaining:0}초 · 원 안으로 걸어가세요",$"Star fragments {run.objectiveCollected}/5 · {run.objectiveRemaining:0}s · Walk into each circle"):run.region==Region.Springs?L($"온천석 점유 {run.objectiveHeld:0.0}/10초 · 남은 {run.objectiveRemaining:0}초",$"Hold the spring {run.objectiveHeld:0.0}/10s · {run.objectiveRemaining:0}s left"):L($"우편 배달 {run.objectiveCollected}/3 · {(run.carrying?"중앙 우체통으로!":"편지를 주워요")} · {run.objectiveRemaining:0}초",$"Deliveries {run.objectiveCollected}/3 · {(run.carrying?"Return to the center mailbox!":"Pick up a letter")} · {run.objectiveRemaining:0}s");
            else if(run.bossSpawned)hintLabel.text=(english?CozyRules.BossEN:CozyRules.BossKO)[(int)run.region]+L(" · 예고한 방향을 피해 빈틈으로 이동하세요"," · Dodge the marked direction and use the gaps");
            else hintLabel.text=run.elapsed<20?(Application.isMobilePlatform?L("화면을 드래그해 이동하세요 / 공격은 자동, XP는 가까이 가서 모아요","Drag to move / Auto attack · Walk near XP to collect it"):L("WASD · 방향키 · 왼쪽 스틱 · 드래그 / 공격은 자동, XP는 가까이 가서 모아요","WASD · arrows · left stick · drag / Auto attack · Walk near XP to collect it")):run.elapsed>=700&&!run.endless?L("곧 지역 보스가 나타납니다!","The region boss is approaching!"):L($"목표 {run.objectives}/3 · 다음 선택 목표: {(run.nextObjective<3?TimeText(120+run.nextObjective*240):"완료")}",$"Objectives {run.objectives}/3 · Next optional objective: {(run.nextObjective<3?TimeText(120+run.nextObjective*240):"done")}");
        }
    }
}
