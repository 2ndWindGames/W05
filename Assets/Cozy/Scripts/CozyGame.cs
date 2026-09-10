using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cozy
{
    // Everything moves in a fixed, readable 1280 x 720 arena. UI and touch share this space.
    public sealed class CozyGame : MonoBehaviour
    {
        public CozyTuning tuning;
        public Shader referenceShader;
        enum Phase { Home, Playing, Paused, Defeat, Result }
        Phase phase;
        RectTransform stage, actors, effects, hud, overlay;
        Canvas canvas;
        Font font;
        Material penguinMaterial;
        RawImage penguin, shadow, joystick, knob, portrait;
        readonly RawImage[] flakes = new RawImage[3];
        readonly Vector2[] flakePositions = new Vector2[3];
        Texture2D[] idle, move, hit, defeat;
        Texture2D disc, snowflake, spirit, toughSpirit, heart, rounded;
        Text scoreLabel, timeLabel, hintLabel;
        readonly List<RawImage> hearts = new();
        readonly List<Enemy> enemies = new();
        readonly List<Mote> motes = new();
        readonly Stack<Enemy> enemyPool = new();
        readonly Stack<Mote> motePool = new();
        readonly List<RawImage> weather = new();
        readonly List<UnityEngine.Object> ownedAssets = new();
        Vector2 position, velocity, stickOrigin, input;
        int health, score, kills, best, previousBest, finger = -1;
        float elapsed, clock, spawnTimer, invincible, defeatTime, footprintTimer, soundGate, scorePulse;
        bool muted, noEnemies, gentle, dragging, faceLeft;
        AudioSource music, sfx;
        AudioClip chime, hurt;
        static readonly Color Ink = new(.22f,.25f,.40f);
        static readonly Color Pink = new(.88f,.40f,.57f);
        static readonly Color Milk = new(.98f,.97f,1f);
        class Enemy { public RawImage view; public Vector2 p; public int hp, value; public float warmup, cooldown, seed; public bool tough; }
        class Mote { public RawImage view; public Vector2 p, v; public float life, maxLife; public Color color; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait=false;Screen.autorotateToPortraitUpsideDown=false;
            Screen.autorotateToLandscapeLeft=true;Screen.autorotateToLandscapeRight=true;
            Screen.orientation=ScreenOrientation.AutoRotation;
            if (!tuning) tuning = ScriptableObject.CreateInstance<CozyTuning>();
            gentle = tuning.gentleMode;
            font = Resources.Load<Font>("Cozy/NotoSansKR");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            best = PlayerPrefs.GetInt("Cozy.Penguin.Best",0);
            muted = PlayerPrefs.GetInt("Cozy.Muted",0) == 1;
            idle = Frames("Idle"); move = Frames("Move"); hit = Frames("Hit"); defeat = Frames("Defeat");
            penguinMaterial = new Material(referenceShader ? referenceShader : Shader.Find("Cozy/ReferenceSprite"));
            ownedAssets.Add(penguinMaterial);
            CreateIcons(); CreateScene(); CreateAudio(); Home();
        }

        Texture2D[] Frames(string name)
        {
            var frames = new Texture2D[8];
            for(int i=0;i<8;i++) frames[i]=Resources.Load<Texture2D>("Cozy/"+name+i);
            return frames;
        }

        Texture2D Icon(int size, Func<float,float,Color> sample)
        {
            var t = new Texture2D(size,size,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            var pixels = new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=sample((x+.5f)/size*2-1,(y+.5f)/size*2-1);
            t.SetPixels(pixels); t.Apply(); ownedAssets.Add(t); return t;
        }

        void CreateIcons()
        {
            disc=Icon(48,(x,y)=>x*x+y*y<1 ? Color.white : Color.clear);
            rounded=Icon(64,(x,y)=>{float a=Mathf.Max(Mathf.Abs(x)-.72f,0), b=Mathf.Max(Mathf.Abs(y)-.72f,0);return a*a+b*b<.0784f?Color.white:Color.clear;});
            heart=Icon(24,(x,y)=>{ y=y*.95f+.1f; float a=x*x+y*y-.55f; return a*a*a-x*x*y*y*y<0?Color.white:Color.clear; });
            snowflake=Icon(40,(x,y)=>{
                float r=Mathf.Sqrt(x*x+y*y); if(r>.95f)return Color.clear;
                float a=Mathf.Atan2(y,x); float branch=Mathf.Abs(Mathf.Sin(a*3));
                bool on=branch*r<.085f || (r>.43f && r<.69f && Mathf.Abs(Mathf.Sin(a*3+r*9))<.28f);
                return on ? (r<.23f?Color.white:new Color(.67f,.91f,1)) : Color.clear;
            });
            spirit=SpiritIcon(false); toughSpirit=SpiritIcon(true);
        }

        Texture2D SpiritIcon(bool tough) => Icon(48,(x,y)=>{
            float r=x*x+(y+.03f)*(y+.03f); if(r>.79f)return Color.clear;
            if(r>.63f)return new Color(.34f,.38f,.63f);
            if(y>.36f && y<.54f && x>-.38f && x<-.17f)return Color.white;
            if(y>-.06f && y<.15f && (Mathf.Abs(x-.26f)<.07f || Mathf.Abs(x+.26f)<.07f))return Ink;
            if(y<-.18f && y>-.27f && Mathf.Abs(x)<.1f)return new Color(.58f,.49f,.67f);
            if(y<-.08f && y>-.24f && Mathf.Abs(x)>.36f && Mathf.Abs(x)<.57f)return new Color(.96f,.66f,.77f);
            if(y<-.32f || (x<-.52f && y<.25f))return tough?new Color(.54f,.59f,.81f):new Color(.77f,.82f,.95f);
            if(r>.44f && y<.36f)return tough?new Color(.61f,.66f,.86f):new Color(.86f,.90f,.99f);
            return tough ? new Color(.72f,.77f,.95f) : new Color(.97f,.98f,1);
        });

        RectTransform Root(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent,false); rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f); rt.sizeDelta=size;rt.anchoredPosition=pos; return rt;
        }
        RawImage Image(string name, Transform parent, Texture texture, Vector2 size, Vector2 pos, Color color)
        {
            var rt=Root(name,parent,size,pos);var view=rt.gameObject.AddComponent<RawImage>(); view.texture=texture;view.color=color;view.raycastTarget=false;return view;
        }
        Text Label(Transform parent,string value,int size,Vector2 pos,Vector2 area,Color color)
        {
            var rt=Root(value,parent,area,pos);var t=rt.gameObject.AddComponent<Text>();t.text=value;t.font=font;t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;return t;
        }
        void Button(Transform parent,string title,Vector2 pos,Vector2 size,Action action,bool primary=false)
        {
            var v=Image(title,parent,rounded,size,pos,primary?Pink:Milk);v.raycastTarget=true;
            var b=v.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=v;
            var colors=b.colors;colors.highlightedColor=new Color(.94f,.96f,1);colors.pressedColor=new Color(.83f,.84f,.93f);b.colors=colors;
            Label(v.transform,title,20,Vector2.zero,size,primary?Color.white:Ink);
            b.onClick.AddListener(()=>{Tone(chime,.28f);action();});
        }

        void CreateScene()
        {
            // Overlay canvases must stay at the scene root; nesting can hide UI in device rendering.
            var c=new GameObject("Cozy canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas=c.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=c.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            stage=Root("Safe landscape stage",c.transform,new Vector2(1280,720),Vector2.zero);
            Image("Aurora snowfield",stage,Resources.Load<Texture2D>("Cozy/Aurora"),new Vector2(1280,720),Vector2.zero,Color.white);
            var atmosphere=Root("Drifting snow",stage,new Vector2(1280,720),Vector2.zero);
            for(int i=0;i<48;i++) weather.Add(Image("Snow",atmosphere,disc,Vector2.one*UnityEngine.Random.Range(2,5),new Vector2(UnityEngine.Random.Range(-635,635),UnityEngine.Random.Range(-355,355)),new Color(1,1,1,UnityEngine.Random.Range(.2f,.55f))));
            effects=Root("Pooled snow effects",stage,new Vector2(1280,720),Vector2.zero);
            actors=Root("Actors",stage,new Vector2(1280,720),Vector2.zero);
            shadow=Image("Penguin shadow",actors,disc,new Vector2(52,15),Vector2.zero,new Color(.32f,.37f,.62f,.22f));
            penguin=Image("Penguin",actors,idle[0],new Vector2(118,118),Vector2.zero,Color.white);penguin.material=penguinMaterial;
            for(int i=0;i<3;i++)
            {
                flakes[i]=Image("Snowflake "+i,actors,snowflake,new Vector2(37,37),Vector2.zero,Color.white);
                var outline=flakes[i].gameObject.AddComponent<Outline>();outline.effectColor=new Color(.30f,.53f,.76f,.65f);outline.effectDistance=Vector2.one;
            }
            hud=Root("HUD",stage,new Vector2(1280,720),Vector2.zero);
            Image("Health panel",hud,rounded,new Vector2(230,68),new Vector2(-472,296),new Color(.97f,.96f,1,.92f));
            for(int i=0;i<5;i++) hearts.Add(Image("Warmth "+i,hud,heart,new Vector2(29,29),new Vector2(-546+i*37,296),Pink));
            Image("Score panel",hud,rounded,new Vector2(182,72),new Vector2(0,294),new Color(.97f,.96f,1,.93f));
            scoreLabel=Label(hud,"0",30,new Vector2(0,301),new Vector2(170,42),Ink);
            Label(hud,"눈꽃 점수",12,new Vector2(0,274),new Vector2(150,22),Ink);
            timeLabel=Label(hud,"00:00",22,new Vector2(407,297),new Vector2(130,48),Milk);
            Button(hud,"II",new Vector2(532,297),new Vector2(68,60),Pause);
            hintLabel=Label(hud,"",18,new Vector2(0,-285),new Vector2(720,44),Ink);
            joystick=Image("Touch origin",hud,disc,new Vector2(150,150),new Vector2(-435,-210),new Color(1,1,1,.2f));
            knob=Image("Touch thumb",hud,disc,new Vector2(57,57),new Vector2(-435,-210),new Color(1,1,1,.52f));
            overlay=Root("Menus",stage,new Vector2(1280,720),Vector2.zero);
            if(!FindFirstObjectByType<EventSystem>())new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            Resize();
        }

        void Resize()
        {
            Rect safe=Screen.safeArea;
            if(safe.width<=0 || safe.height<=0)safe=new Rect(0,0,Screen.width,Screen.height);
            float scale=Mathf.Min(safe.width/1280f,safe.height/720f);
#if UNITY_EDITOR
            if(storeFeature)scale=Mathf.Max(safe.width/1280f,safe.height/720f);
#endif
            stage.localScale=Vector3.one*scale;
            stage.anchoredPosition=safe.center-new Vector2(Screen.width,Screen.height)*.5f;
        }

        void ClearMenu() { foreach(Transform child in overlay)Destroy(child.gameObject); }
        void Panel(string eyebrow,string title,string description)
        {
            ClearMenu();
            Image("Veil",overlay,Texture2D.whiteTexture,new Vector2(1280,720),Vector2.zero,new Color(.20f,.24f,.42f,.25f));
            Image("Paper card",overlay,rounded,new Vector2(510,534),new Vector2(0,-5),new Color(.97f,.96f,1,.96f));
            Label(overlay,eyebrow,14,new Vector2(0,214),new Vector2(460,30),Pink);
            Label(overlay,title,38,new Vector2(0,162),new Vector2(460,66),Ink);
            Label(overlay,description,17,new Vector2(0,102),new Vector2(460,60),Ink);
        }
        public void Home()
        {
            ResetRun();phase=Phase.Home;hud.gameObject.SetActive(false);
            Panel("A LITTLE SNOW  /  01",Application.systemLanguage==SystemLanguage.Korean?"포근한모험":"CozySurvivors","오로라 아래, 작은 펭귄의 발걸음");
            portrait=Image("Penguin portrait",overlay,idle[0],new Vector2(194,194),new Vector2(0,-10),Color.white);portrait.material=penguinMaterial;
            Button(overlay,"눈밭으로 산책 가기",new Vector2(0,-135),new Vector2(334,62),StartRun,true);
            Button(overlay,"소리 · 플레이 설정",new Vector2(0,-206),new Vector2(334,48),Settings);
            Label(overlay,"드래그로 이동  ·  눈꽃은 자동으로 회전해요",16,new Vector2(0,-310),new Vector2(720,36),Milk);
            foreach(var f in flakes)f.gameObject.SetActive(false);penguin.gameObject.SetActive(false);shadow.gameObject.SetActive(false);
        }
        void Settings()
        {
            Panel("MAKE YOURSELF COMFORTABLE","작은 설정","원하는 속도로 눈밭을 느껴 보세요");
            Button(overlay,muted?"소리  꺼짐":"소리  켜짐",new Vector2(0,30),new Vector2(370,54),()=>{muted=!muted;music.mute=muted;PlayerPrefs.SetInt("Cozy.Muted",muted?1:0);PlayerPrefs.Save();Settings();});
            Button(overlay,noEnemies?"눈밭 구경  ·  적 없음":gentle?"산책 모드  ·  일정한 적 밀도":"생존 모드  ·  원본 생성 곡선",new Vector2(0,-40),new Vector2(370,54),()=>{
                if(noEnemies){noEnemies=false;gentle=true;}
                else if(gentle)gentle=false;
                else{noEnemies=true;foreach(var e in enemies){e.view.gameObject.SetActive(false);enemyPool.Push(e);}enemies.Clear();}
                Settings();
            });
            Button(overlay,"돌아가기",new Vector2(0,-150),new Vector2(334,60),()=>{if(phase==Phase.Paused)PauseMenu();else Home();},true);
        }
        public void StartRun()
        {
            ResetRun();phase=Phase.Playing;ClearMenu();hud.gameObject.SetActive(true);
            penguin.gameObject.SetActive(true);shadow.gameObject.SetActive(true);foreach(var f in flakes)f.gameObject.SetActive(true);
            music.mute=muted;music.Play();UpdateHud();
        }
        public void Pause()
        {
            if(phase!=Phase.Playing)return;
            phase=Phase.Paused;ReleaseInput();AudioListener.pause=true;PauseMenu();
        }
        void PauseMenu()
        {
            Panel("TAKE A BREATH","잠깐, 쉬어 가요","눈밭은 여기서 기다리고 있어요");
            Button(overlay,"계속 걷기",new Vector2(0,20),new Vector2(334,62),Resume,true);
            Button(overlay,"소리 · 플레이 설정",new Vector2(0,-55),new Vector2(334,54),Settings);
            Button(overlay,"처음으로",new Vector2(0,-125),new Vector2(334,54),Home);
        }
        public void Resume(){if(phase!=Phase.Paused)return;ClearMenu();ReleaseInput();phase=Phase.Playing;AudioListener.pause=false;}
        void Result()
        {
            phase=Phase.Result;music.Stop();SaveBest();
            Panel(score>previousBest?"A NEW LITTLE MEMORY":"UNTIL NEXT SNOW","따뜻한 쉼표",score>previousBest?"새로운 최고 기록을 만들었어요":"오늘의 작은 모험을 기억해요");
            Label(overlay,score+" 점",42,new Vector2(0,26),new Vector2(400,62),Pink);
            Label(overlay,$"산책 시간  {TimeText(elapsed)}    ·    정령  {kills}마리\n최고 기록  {best}점",18,new Vector2(0,-48),new Vector2(430,76),Ink);
            Button(overlay,"한 번 더 산책하기",new Vector2(0,-138),new Vector2(334,60),StartRun,true);
            Button(overlay,"처음으로",new Vector2(0,-208),new Vector2(334,48),Home);
        }
        void ResetRun()
        {
            AudioListener.pause=false;ReleaseInput();
            foreach(var e in enemies){e.view.gameObject.SetActive(false);enemyPool.Push(e);}enemies.Clear();
            foreach(var m in motes){m.view.gameObject.SetActive(false);motePool.Push(m);}motes.Clear();
            position=new Vector2(0,-15);velocity=Vector2.zero;elapsed=0;clock=0;health=tuning.health;score=kills=0;spawnTimer=1.5f;invincible=defeatTime=footprintTimer=scorePulse=0;previousBest=best;faceLeft=false;
            if(music)music.Stop();
        }
        void ReleaseInput(){finger=-1;dragging=false;input=Vector2.zero;velocity=Vector2.zero;if(joystick){joystick.rectTransform.anchoredPosition=knob.rectTransform.anchoredPosition=new Vector2(-435,-210);}}
        void OnApplicationFocus(bool focus){if(!focus)Pause();}
        void OnApplicationPause(bool paused){if(paused)Pause();}
        void OnApplicationQuit(){SaveBest();AudioListener.pause=false;}
        void OnDestroy(){AudioListener.pause=false;if(canvas)Destroy(canvas.gameObject);foreach(var item in ownedAssets)if(item)Destroy(item);}

        Vector2 ScreenPoint(Vector2 screen){RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,screen,null,out var p);return p;}
        bool CanDrag(Vector2 p)=>p.y<220 && p.y> -350 && Mathf.Abs(p.x)<620;
        void ReadInput()
        {
#if UNITY_EDITOR
            if(storeInput.HasValue){input=storeInput.Value;return;}
#endif
            input=Vector2.zero;
            var k=Keyboard.current;
            if(k!=null){input=new Vector2((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0));}
            var touch=Touchscreen.current;
            if(touch!=null)
            {
                foreach(var t in touch.touches)
                {
                    if(t.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.None)continue;
                    int id=t.touchId.ReadValue();Vector2 p=ScreenPoint(t.position.ReadValue());
                    if(t.press.wasPressedThisFrame && finger<0 && CanDrag(p)){finger=id;stickOrigin=p;dragging=true;}
                    if(id!=finger)continue;
                    if(!t.press.isPressed){ReleaseInput();continue;}SetStick(p);
                }
            }
            if(finger<0 && Mouse.current!=null)
            {
                var m=Mouse.current;Vector2 p=ScreenPoint(m.position.ReadValue());
                if(m.leftButton.wasPressedThisFrame && CanDrag(p)){stickOrigin=p;dragging=true;}
                if(dragging && m.leftButton.isPressed)SetStick(p);
                if(m.leftButton.wasReleasedThisFrame)ReleaseInput();
            }
        }
        void SetStick(Vector2 p)
        {
            Vector2 delta=Vector2.ClampMagnitude(p-stickOrigin,tuning.joystickRadius);
            input=delta/tuning.joystickRadius;
            if(input.magnitude<tuning.deadZone)input=Vector2.zero;
            joystick.rectTransform.anchoredPosition=stickOrigin;knob.rectTransform.anchoredPosition=stickOrigin+delta;
        }

        void Update()
        {
            Resize();
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame){if(phase==Phase.Playing)Pause();else if(phase==Phase.Paused)Resume();}
            float dt=Mathf.Min(Time.deltaTime,.05f);
            if(phase==Phase.Paused || phase==Phase.Result)return;
            clock+=dt;
            foreach(var w in weather){var p=w.rectTransform.anchoredPosition;p+=new Vector2(Mathf.Sin(clock*.6f+p.y)*4,-9)*dt;if(p.y< -360)p.y=360;w.rectTransform.anchoredPosition=p;}
            if(phase==Phase.Home){if(portrait)portrait.texture=idle[(int)(clock/.2075f)%8];return;}
            if(phase==Phase.Defeat){defeatTime+=dt;penguin.texture=defeat[Mathf.Min(7,(int)(defeatTime/.6f*8))];if(defeatTime>=.6f)Result();return;}
            ReadInput();Simulate(dt,input);
        }
        void Simulate(float dt,Vector2 direction)
        {
            elapsed+=dt;invincible=Mathf.Max(0,invincible-dt);soundGate-=dt;scorePulse=Mathf.Max(0,scorePulse-dt);
            Vector2 before=position;
            velocity=Vector2.MoveTowards(velocity,CozyTuning.Movement(direction,tuning.moveSpeed),tuning.moveSpeed/Mathf.Max(.01f,tuning.responseSeconds)*dt);
            position+=velocity*dt;position.x=Mathf.Clamp(position.x,-510,510);position.y=Mathf.Clamp(position.y,-245,140);
            bool moving=(position-before).sqrMagnitude>.001f;
            if(moving && Mathf.Abs(velocity.x)>2)faceLeft=velocity.x<0;
            penguin.texture=invincible>tuning.invulnerability-.15f?hit[Mathf.Clamp((int)((tuning.invulnerability-invincible)*48),0,7)]:(moving?move[(int)(clock/.095f)%8]:idle[(int)(clock/.2075f)%8]);
            penguin.rectTransform.anchoredPosition=position+new Vector2(0,24);
            penguin.rectTransform.localScale=new Vector3(faceLeft?-1:1,1,1);
            penguin.color=new Color(1,1,1,invincible>0?.48f+.30f*Mathf.Abs(Mathf.Sin(clock*25)):1);
            shadow.rectTransform.anchoredPosition=position+new Vector2(0,-9);
            footprintTimer-=dt;
            if(moving && footprintTimer<=0){footprintTimer=.10f;Emit(position+new Vector2(Mathf.Sin(clock*30)*9,-8),Vector2.zero,.65f,new Color(.39f,.45f,.67f,.30f),new Vector2(7,4));}
            for(int i=0;i<3;i++)
            {
                float a=elapsed/tuning.orbitSeconds*Mathf.PI*2+i*Mathf.PI*2/3;
                flakePositions[i]=position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*tuning.orbitRadius;
                flakes[i].rectTransform.anchoredPosition=flakePositions[i];flakes[i].rectTransform.localRotation=Quaternion.Euler(0,0,-elapsed*95);
            }
            if(!noEnemies){spawnTimer-=dt;if(spawnTimer<=0){Spawn();spawnTimer=1/(gentle?tuning.gentleSpawnRate:CozyTuning.SpawnRate(elapsed));}}
            for(int i=enemies.Count-1;i>=0;i--)
            {
                var e=enemies[i];e.cooldown-=dt;e.warmup-=dt;
                if(e.warmup>0){e.view.color=new Color(1,1,1,.25f+(.25f-e.warmup)*3);continue;}
                Vector2 toward=(position-e.p).normalized;
                e.p+=toward*(e.tough?48:65)*(gentle?1:CozyTuning.SpeedMultiplier(elapsed))*dt;
                for(int j=0;j<i;j++){Vector2 away=e.p-enemies[j].p;float d=away.magnitude;if(d>0 && d<35)e.p+=away/d*(35-d)*dt*2;}
                e.view.rectTransform.anchoredPosition=e.p+new Vector2(0,4+Mathf.Sin(clock*3+e.seed)*3);
                e.view.color=e.cooldown>.30f?new Color(1,.76f,.87f):Color.white;
                if(e.cooldown<=0)
                {
                    for(int f=0;f<3;f++)if(Vector2.Distance(flakePositions[f],e.p)<tuning.attackRadius+(e.tough?23:18))
                    {e.hp-=10;e.cooldown=tuning.hitCooldown;e.p+=toward*-18;Burst(e.p,5,new Color(.69f,.88f,1));if(soundGate<=0){Tone(chime,.20f);soundGate=.08f;}break;}
                }
                if(e.hp<=0)
                {
                    score+=e.value;kills++;scorePulse=.12f;best=Mathf.Max(best,score);SaveBest();Burst(e.p,7,new Color(.94f,.78f,.93f));
                    e.view.gameObject.SetActive(false);enemies.RemoveAt(i);enemyPool.Push(e);continue;
                }
                if(invincible<=0 && Vector2.Distance(position,e.p)<13+(e.tough?23:18))
                {
                    health--;invincible=tuning.invulnerability;position-=toward*18;e.p+=toward*-40;Burst(position,6,Pink);Tone(hurt,.35f);
                    position.x=Mathf.Clamp(position.x,-510,510);position.y=Mathf.Clamp(position.y,-245,140);
                    if(health<=0){phase=Phase.Defeat;ReleaseInput();foreach(var f in flakes)f.gameObject.SetActive(false);break;}
                }
            }
            for(int i=motes.Count-1;i>=0;i--){var m=motes[i];m.life-=dt;if(m.life<=0){m.view.gameObject.SetActive(false);motes.RemoveAt(i);motePool.Push(m);continue;}m.p+=m.v*dt;m.view.rectTransform.anchoredPosition=m.p;var c=m.color;c.a*=m.life/m.maxLife;m.view.color=c;}
            UpdateHud();
        }
        void Spawn()
        {
            if(enemies.Count>=tuning.enemyLimit)return;
            Vector2 p=Vector2.zero;
            for(int attempt=0;attempt<8;attempt++)
            {
                int edge=UnityEngine.Random.Range(0,4);
                p=edge<2?new Vector2(edge==0?-620:620,UnityEngine.Random.Range(-260,180)):new Vector2(UnityEngine.Random.Range(-560,560),edge==2?-350:230);
                if(Vector2.Distance(p,position)>=180)break;
            }
            if(Vector2.Distance(p,position)<180)return;
            bool tough=UnityEngine.Random.value<(gentle?(elapsed>35?.15f:0):CozyTuning.ToughChance(elapsed));
            Enemy e=enemyPool.Count>0?enemyPool.Pop():new Enemy{view=Image("Snow spirit",actors,spirit,new Vector2(44,44),p,Color.white)};
            e.tough=tough;e.p=p;e.hp=tough?30:10;e.value=tough?30:10;e.warmup=.25f;e.cooldown=0;e.seed=UnityEngine.Random.value*10;
            e.view.texture=tough?toughSpirit:spirit;e.view.rectTransform.sizeDelta=Vector2.one*(tough?57:44);e.view.rectTransform.anchoredPosition=p;e.view.color=new Color(1,1,1,.3f);e.view.gameObject.SetActive(true);
            enemies.Add(e);Burst(p,4,new Color(.8f,.85f,1));penguin.transform.SetAsLastSibling();foreach(var f in flakes)f.transform.SetAsLastSibling();
        }
        void Emit(Vector2 p,Vector2 v,float life,Color color,Vector2 size)
        {
            if(motes.Count>=200)return;
            var m=motePool.Count>0?motePool.Pop():new Mote{view=Image("Snow mote",effects,disc,size,p,color)};
            m.p=p;m.v=v;m.life=m.maxLife=life;m.color=color;m.view.rectTransform.sizeDelta=size;m.view.rectTransform.anchoredPosition=p;m.view.color=color;m.view.gameObject.SetActive(true);motes.Add(m);
        }
        void Burst(Vector2 p,int count,Color c){for(int i=0;i<count;i++)Emit(p,UnityEngine.Random.insideUnitCircle*75,.32f,c,Vector2.one*UnityEngine.Random.Range(4,9));}
        static string TimeText(float time)=>$"{(int)time/60:00}:{(int)time%60:00}";
        void UpdateHud()
        {
            scoreLabel.text=score.ToString();scoreLabel.rectTransform.localScale=Vector3.one*(1+scorePulse/.12f*.08f);timeLabel.text=TimeText(elapsed);
            for(int i=0;i<hearts.Count;i++)hearts[i].color=i<health?Pink:new Color(.71f,.72f,.80f,.5f);
            hintLabel.text=elapsed<5?"화면을 드래그해 걸어 보세요  ·  자동 회전 눈꽃":noEnemies?"눈밭 구경  ·  천천히 걸어도 좋아요":gentle?"산책 모드  ·  눈꽃으로 정령을 톡톡":"";
        }
        void SaveBest(){if(score>PlayerPrefs.GetInt("Cozy.Penguin.Best",0)){PlayerPrefs.SetInt("Cozy.Penguin.Best",score);PlayerPrefs.Save();}}

        void CreateAudio()
        {
            music=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.17f;music.playOnAwake=false;
            sfx=gameObject.AddComponent<AudioSource>();sfx.playOnAwake=false;
            chime=Sound("Ice chime",.26f,t=>Mathf.Sin(2*Mathf.PI*1174.66f*t)*Mathf.Exp(-t*17)+.35f*Mathf.Sin(2*Mathf.PI*1760*t)*Mathf.Exp(-t*22));
            hurt=Sound("Soft knock",.18f,t=>Mathf.Sin(2*Mathf.PI*(240-340*t)*t)*Mathf.Exp(-t*20));
            float[] notes={587.33f,739.99f,880,1108.73f,880,739.99f,659.25f,880,987.77f,739.99f,659.25f,554.37f,587.33f,739.99f,880,739.99f};
            music.clip=Sound("Original snow music",32,t=>{float local=t%2;float note=notes[Mathf.Min(15,(int)(t/2))];float envelope=(1-Mathf.Exp(-local*25))*Mathf.Exp(-local*2.3f);return .4f*envelope*(Mathf.Sin(2*Mathf.PI*note*t)+.22f*Mathf.Sin(2*Mathf.PI*note*2*t));});
        }
        AudioClip Sound(string name,float duration,Func<float,float> sample)
        {
            const int rate=22050;var data=new float[(int)(duration*rate)];for(int i=0;i<data.Length;i++)data[i]=sample(i/(float)rate)*.45f;
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);ownedAssets.Add(clip);return clip;
        }
        void Tone(AudioClip clip,float volume){if(!muted && sfx && clip)sfx.PlayOneShot(clip,volume);}

#if UNITY_EDITOR
        Vector2? storeInput;
        bool storeFeature;
        public void CaptureStoreScreens()=>StartCoroutine(StoreScreens());
        void SaveStoreImage(string path)
        {
            var frame=ScreenCapture.CaptureScreenshotAsTexture();
            try{System.IO.File.WriteAllBytes(path,frame.EncodeToJPG(96));}
            finally{Destroy(frame);}
        }
        System.Collections.IEnumerator StoreScreens()
        {
            const string folder="StoreAssets/GooglePlay/Screenshots/";
            System.IO.Directory.CreateDirectory(folder);
            int saved=PlayerPrefs.GetInt("Cozy.Penguin.Best",0);bool oldNo=noEnemies,oldGentle=gentle;
            try
            {
                Home();yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();SaveStoreImage(folder+"01-title.jpg");
                gentle=true;noEnemies=false;StartRun();
                for(int i=0;i<3;i++)
                {
                    float end=Time.time+8;
                    while(Time.time<end){if(phase==Phase.Paused)Resume();storeInput=new Vector2(Mathf.Cos(elapsed*.45f),Mathf.Sin(elapsed*.45f))*.65f;yield return null;}
                    yield return new WaitForEndOfFrame();SaveStoreImage(folder+(i+2).ToString("00")+"-gameplay.jpg");
                }
                if(phase==Phase.Paused)Resume();noEnemies=true;foreach(var e in enemies){e.view.gameObject.SetActive(false);enemyPool.Push(e);}enemies.Clear();
                storeInput=Vector2.left*.6f;yield return new WaitForSeconds(1);yield return new WaitForEndOfFrame();SaveStoreImage(folder+"05-snow-stroll.jpg");
                System.IO.File.WriteAllText("Temp/CozyStoreCaptureResult.txt","OK: 5 real game screenshots at "+Screen.width+"x"+Screen.height);
            }
            finally{storeInput=null;PlayerPrefs.SetInt("Cozy.Penguin.Best",saved);PlayerPrefs.Save();best=saved;noEnemies=oldNo;gentle=oldGentle;Home();}
        }
        public void CaptureStoreFeature()=>StartCoroutine(StoreFeature());
        System.Collections.IEnumerator StoreFeature()
        {
            Home();ClearMenu();storeFeature=true;
            // Marketing artwork composed from the game's own background and original sprite.
            // This output is a feature graphic, never presented as a gameplay screenshot.
            Image("Feature readability",overlay,rounded,new Vector2(560,370),new Vector2(-290,0),new Color(.97f,.96f,1,.90f));
            Label(overlay,"포근한모험",62,new Vector2(-290,50),new Vector2(530,95),Ink);
            Label(overlay,"CozySurvivors",30,new Vector2(-290,-30),new Vector2(530,60),Pink);
            var hero=Image("Feature penguin",overlay,idle[0],new Vector2(420,420),new Vector2(295,-5),Color.white);hero.material=penguinMaterial;
            for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Image("Feature snowflake",overlay,snowflake,new Vector2(68,68),new Vector2(295,-5)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*200,Color.white);}
            yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
            SaveStoreImage("StoreAssets/GooglePlay/FeatureGraphic-1024x500.jpg");
            storeFeature=false;Home();
        }

        public void CheckTouchInput() => StartCoroutine(TouchChecks());
        System.Collections.IEnumerator TouchChecks()
        {
            bool oldNo=noEnemies;var report=new System.Text.StringBuilder();
            var device=InputSystem.AddDevice<Touchscreen>();
            void Send(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 local)
            {
                Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,stage.TransformPoint(local));
                InputSystem.QueueStateEvent(device,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=id,phase=phase,position=screen});
            }
            void Check(bool condition,string label){if(!condition)throw new Exception("FAIL "+label);report.AppendLine("PASS "+label);}
            StartRun();noEnemies=true;
            try
            {
                yield return null;
                Send(71,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(-300,-100));yield return new WaitForSeconds(.06f);
                Send(71,UnityEngine.InputSystem.TouchPhase.Moved,new Vector2(-200,-100));yield return new WaitForSeconds(.3f);
                Check(position.x>30,"Touchscreen drag moves penguin through real input path");
                Send(71,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(-200,-100));yield return new WaitForSeconds(.12f);
                Check(velocity.sqrMagnitude<.01f && finger<0,"touch release clears movement");
                Send(72,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(532,297));yield return new WaitForSeconds(.06f);
                Send(72,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(532,297));yield return new WaitForSeconds(.12f);
                Check(phase==Phase.Paused,"touch UI pauses game");
                Send(73,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(0,20));yield return new WaitForSeconds(.06f);
                Send(73,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(0,20));yield return new WaitForSeconds(.12f);
                Check(phase==Phase.Playing,"touch UI resumes game");
                System.IO.File.WriteAllText("Temp/CozyTouchTests.txt",report.ToString());
            }
            finally{InputSystem.RemoveDevice(device);noEnemies=oldNo;Home();}
        }

        // Runs against the actual simulation, then restores the title and saved record.
        public string RunSmokeChecks()
        {
            int saved=PlayerPrefs.GetInt("Cozy.Penguin.Best",0);bool oldNo=noEnemies,oldGentle=gentle;
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);report.AppendLine("PASS: "+label);}
            try
            {
                StartRun();noEnemies=true;
                Check(Mathf.Abs(CozyTuning.Movement(Vector2.one,220).magnitude-220)<.001f,"normalized diagonal movement");
                for(int i=0;i<60;i++)Simulate(1f/60,Vector2.right);
                Check(position.x>205 && position.x<225,"movement speed and acceleration");
                for(int i=0;i<10;i++)Simulate(1f/60,Vector2.zero);
                Check(velocity.sqrMagnitude<.01f,"responsive stop");
                for(int i=0;i<600;i++)Simulate(1f/60,Vector2.one);
                Check(position.x<=510 && position.y<=140,"arena bounds");
                float frozen=elapsed;Pause();Check(phase==Phase.Paused && elapsed==frozen && input==Vector2.zero,"pause clears controls");Resume();
                noEnemies=false;for(int i=0;i<150;i++)Spawn();Check(enemies.Count==100,"enemy cap 100");
                for(int i=0;i<250;i++)Emit(Vector2.zero,Vector2.one,1,Color.white,Vector2.one);Check(motes.Count==200,"effect cap 200");
                StartRun();noEnemies=true;Spawn();var victim=enemies[0];victim.warmup=0;victim.hp=10;victim.p=position+Vector2.right*tuning.orbitRadius;
                Simulate(.001f,Vector2.zero);Check(kills==1 && score==victim.value,"snowflake contact awards score once");int earned=score;Simulate(.001f,Vector2.zero);Check(score==earned,"dead enemy cannot score twice");
                StartRun();noEnemies=true;for(int i=0;i<3;i++){Spawn();enemies[i].p=position;enemies[i].warmup=0;}
                Simulate(.001f,Vector2.zero);Check(health==tuning.health-1,"simultaneous contact costs one heart");Simulate(.1f,Vector2.zero);Check(health==tuning.health-1,"invulnerability");
                health=1;invincible=0;enemies[0].p=position;Simulate(.001f,Vector2.zero);Check(phase==Phase.Defeat,"zero health ends combat");
                Result();Check(phase==Phase.Result,"result flow");
                for(int i=0;i<20;i++){StartRun();Spawn();Emit(position,Vector2.zero,1,Color.white,Vector2.one);Home();Check(enemies.Count==0 && motes.Count==0 && score==0 && health==tuning.health,"restart "+(i+1));}
                StartRun();gentle=false;noEnemies=false;for(int i=0;i<36000;i++){health=5;invincible=2;Simulate(1f/60,new Vector2(Mathf.Sin(i*.01f),Mathf.Cos(i*.01f)));}
                Check(enemies.Count<=100 && motes.Count<=200 && elapsed>599,"10 minute simulation bounded");
                return report.ToString();
            }
            finally{PlayerPrefs.SetInt("Cozy.Penguin.Best",saved);PlayerPrefs.Save();best=saved;noEnemies=oldNo;gentle=oldGentle;Home();}
        }
#endif
    }
}
