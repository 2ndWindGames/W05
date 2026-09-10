#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cozy
{
    public sealed partial class CozyGame
    {
        void CaptureReviewFrame(string path)
        {
            var camera=FindFirstObjectByType<Camera>();
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            var previousTarget=camera.targetTexture;var previousMode=canvas.renderMode;var previousCamera=canvas.worldCamera;
            var previousActive=RenderTexture.active;var previousScale=stage.localScale;var previousPosition=stage.anchoredPosition;
            Texture2D frame=null;
            try
            {
                target.Create();camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                stage.localScale=Vector3.one;stage.anchoredPosition=Vector2.zero;Canvas.ForceUpdateCanvases();
                if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
                else camera.Render();
                RenderTexture.active=target;frame=new Texture2D(1280,720,TextureFormat.RGB24,false);
                frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();File.WriteAllBytes(path,frame.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previousActive;camera.targetTexture=previousTarget;canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;
                stage.localScale=previousScale;stage.anchoredPosition=previousPosition;if(frame)Destroy(frame);target.Release();Destroy(target);Canvas.ForceUpdateCanvases();
            }
        }
        IEnumerator InputChecks()
        {
            var originalProfile=profile;var originalSave=save;
            bool originalAutomation=automatedReview;automatedReview=true;
            var originalBackground=InputSystem.settings.backgroundBehavior;
            // Virtual test devices must keep receiving queued events even when this hidden window is unfocused.
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            profile=new CozyProfile();save=new CozySave(Path.Combine("Temp","cozy-input-check.json"));
            var touch=InputSystem.AddDevice<Touchscreen>();var pad=InputSystem.AddDevice<Gamepad>();
            var report=new System.Text.StringBuilder();
            void Check(bool ok,string message){if(!ok)throw new Exception($"FAIL {message}; phase={run?.phase}; position={run?.position}; input={input}; elapsed={run?.elapsed}; screen={Screen.width}x{Screen.height}");report.AppendLine("PASS "+message);}
            IEnumerator Until(Func<bool> condition)
            {
                float deadline=Time.realtimeSinceStartup+8;
                do{yield return null;}while(!condition()&&Time.realtimeSinceStartup<deadline);
            }
            void Send(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 local)
            {
                Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,stage.TransformPoint(local));
                InputSystem.QueueStateEvent(touch,new UnityEngine.InputSystem.LowLevel.TouchState{touchId=id,phase=phase,position=screen});
            }
            try
            {
                selectedAnimal=Animal.Penguin;selectedRegion=Region.Snowfield;selectedDifficulty=Difficulty.Normal;selectedEndless=false;StartRun();run.spawnTimer=999;
                yield return null;yield return null;
                Send(71,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(-300,-100));yield return new WaitForSecondsRealtime(.1f);
                Send(71,UnityEngine.InputSystem.TouchPhase.Moved,new Vector2(-200,-100));yield return Until(()=>run.position.x>30);
                Check(run.position.x>30,"touch drag moves through device input path");
                Send(71,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(-200,-100));yield return Until(()=>run.velocity.sqrMagnitude<.01f&&finger<0);
                Check(run.velocity.sqrMagnitude<.01f&&finger<0,"touch release clears movement");
                Send(72,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(576,335));yield return new WaitForSecondsRealtime(.1f);
                Send(72,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(576,335));yield return Until(()=>run.phase==RunPhase.Paused);
                Check(run.phase==RunPhase.Paused,"touch HUD button pauses");
                Send(73,UnityEngine.InputSystem.TouchPhase.Began,new Vector2(0,60));yield return new WaitForSecondsRealtime(.1f);
                Send(73,UnityEngine.InputSystem.TouchPhase.Ended,new Vector2(0,60));yield return Until(()=>run.phase==RunPhase.Playing);
                Check(run.phase==RunPhase.Playing,"touch menu resumes");
                float before=run.position.x;
                InputSystem.QueueStateEvent(pad,new UnityEngine.InputSystem.LowLevel.GamepadState{leftStick=Vector2.right});yield return Until(()=>run.position.x>before+30);
                Check(run.position.x>before+30,"gamepad left stick moves through real input");
                InputSystem.QueueStateEvent(pad,new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.Start));yield return Until(()=>run.phase==RunPhase.Paused);
                Check(run.phase==RunPhase.Paused,"gamepad START opens pause menu");
                InputSystem.QueueStateEvent(pad,new UnityEngine.InputSystem.LowLevel.GamepadState());yield return new WaitForSecondsRealtime(.1f);
                InputSystem.QueueStateEvent(pad,new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.South));yield return new WaitForSecondsRealtime(.1f);
                InputSystem.QueueStateEvent(pad,new UnityEngine.InputSystem.LowLevel.GamepadState());yield return Until(()=>run.phase==RunPhase.Playing);
                Check(run.phase==RunPhase.Playing,"gamepad A submits the focused menu button");
                automatedReview=false;OnApplicationFocus(false);Check(run.phase==RunPhase.Paused,"normal play pauses on focus loss");automatedReview=true;
                Directory.CreateDirectory("Temp");File.WriteAllText("Temp/CozyTouchTests.txt",report.ToString());
            }
            finally{InputSystem.RemoveDevice(touch);InputSystem.RemoveDevice(pad);InputSystem.settings.backgroundBehavior=originalBackground;profile=originalProfile;save=originalSave;automatedReview=originalAutomation;Home();}
        }
        IEnumerator ReviewBuild()
        {
            string folder=Path.GetFullPath("Documentation/Review");Directory.CreateDirectory(folder);
            save=new CozySave(Path.Combine(folder,"review-profile.json"));profile=new CozyProfile();
            Home();yield return new WaitForSecondsRealtime(.7f);
            CaptureReviewFrame(Path.Combine(folder,"01-home.png"));yield return new WaitForSecondsRealtime(.4f);
            Selection();yield return new WaitForSecondsRealtime(.4f);CaptureReviewFrame(Path.Combine(folder,"02-selection.png"));yield return new WaitForSecondsRealtime(.3f);
            profile.cleared=new[]{true,true,true};
            for(int a=0;a<3;a++)
            {
                selectedAnimal=(Animal)a;selectedRegion=(Region)a;StartRun();run.invincible=.4f;run.weaponLevel=5;run.branch=a%2+1;run.StartObjective();
                for(int i=0;i<14;i++){var e=run.Spawn((EnemyRole)(i%6),new Vector2(-460+(i%7)*145,-160+(i/7)*330));if(e!=null)e.warmup=0;}
                yield return new WaitForSecondsRealtime(.8f);CaptureReviewFrame(Path.Combine(folder,$"0{a+3}-gameplay.png"));yield return new WaitForSecondsRealtime(.3f);
            }
            run.AddXP(15);run.ResolveChoices();shownPhase=(RunPhase)(-1);ShowPhase();yield return new WaitForSecondsRealtime(.4f);CaptureReviewFrame(Path.Combine(folder,"06-growth.png"));yield return new WaitForSecondsRealtime(.3f);
            while(run.phase==RunPhase.LevelChoice)run.Choose(run.choices[0],1);
            run.elapsed=720;run.Tick(CozyRules.Step,Vector2.zero);shownPhase=(RunPhase)(-1);ShowPhase();
            yield return new WaitForSecondsRealtime(1);CaptureReviewFrame(Path.Combine(folder,"07-boss.png"));
            var boss=run.enemies.Find(e=>e.role==EnemyRole.Boss);run.Hit(boss,10000,0);ShowPhase();
            yield return new WaitForSecondsRealtime(.4f);CaptureReviewFrame(Path.Combine(folder,"08-result.png"));
            profile.settings.english=true;profile.settings.largeText=true;settingsReturn="home";Settings();
            yield return new WaitForSecondsRealtime(.4f);CaptureReviewFrame(Path.Combine(folder,"09-settings-en.png"));
            string animationFolder=Path.Combine(folder,"Animations");Directory.CreateDirectory(animationFolder);
            profile.settings.english=false;profile.settings.largeText=false;
            float[] idleTimes={.3f,.7f,.95f,1.10f,1.24f,1.36f,1.5f,1.84f};
            foreach(var animal in new[]{Animal.Capybara,Animal.Cat})
            {
                selectedAnimal=animal;selectedRegion=Region.Snowfield;StartRun();
                run.Pause();ClearMenu();inMenus=true;
                for(int frame=0;frame<8;frame++)
                {
                    run.elapsed=idleTimes[frame];run.velocity=Vector2.zero;RenderRun();yield return null;
                    var expected=(animal==Animal.Capybara?capybaraIdle:catIdle)[frame];
                    if(penguin.texture!=expected || penguin.material!=companionMaterial)throw new Exception("Character idle render mismatch: "+animal+" frame "+frame);
                    CaptureReviewFrame(Path.Combine(animationFolder,$"{animal}-{frame}.png"));
                }
                run.elapsed+=2;RenderRun();
                if(penguin.texture!=(animal==Animal.Capybara?capybaraIdle:catIdle)[7])throw new Exception("Character animation does not loop: "+animal);
            }
            File.WriteAllText(Path.Combine(animationFolder,"validation.txt"),"PASS 16 real UI frame renders, selected textures/material and 2-second loop for both animals.\n");
            yield return InputChecks();
            File.WriteAllText(Path.Combine(folder,"complete.txt"),"Review captures and device input checks complete.");Application.Quit();
        }
        void Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-cozy-review")>=0){automatedReview=true;Application.runInBackground=true;StartCoroutine(ReviewBuild());}
        }
    }
}
#endif
