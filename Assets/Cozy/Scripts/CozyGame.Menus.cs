using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cozy
{
    public sealed partial class CozyGame
    {
        void ClearMenu()
        {
            blockedFrame = Time.frameCount; ReleaseInput();
            menuPortraits.Clear();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            foreach (Transform child in overlay) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        void Panel(string eyebrow, string title, string description)
        {
            ClearMenu(); inMenus = true;
            Image("Menu veil",overlay,Texture2D.whiteTexture,new Vector2(1280,720),Vector2.zero,new Color(.12f,.17f,.30f,.58f));
            Image("Paper",overlay,rounded,new Vector2(1050,636),Vector2.zero,new Color(.97f,.97f,1,.98f));
            Label(overlay,eyebrow,14,new Vector2(0,268),new Vector2(940,30),Pink);
            Label(overlay,title,34,new Vector2(0,220),new Vector2(940,62),Ink);
            Label(overlay,description,17,new Vector2(0,164),new Vector2(940,48),Ink);
        }
        void FocusMenu()
        {
            if (!EventSystem.current) return;
            var buttons = overlay.GetComponentsInChildren<UnityEngine.UI.Button>();
            if (buttons.Length > 0) EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
        }
        public void Home()
        {
            run = null; shownPhase = (RunPhase)(-1); inMenus = true; hud.gameObject.SetActive(false); actors.gameObject.SetActive(false); effects.gameObject.SetActive(false);
            if (music) music.Stop(); AudioListener.pause = false;
            Panel("COZY SURVIVORS",L("포근한 모험","A cozy little adventure"),L("이동만 조작하세요. 공격은 동물 친구에게 맡겨요.","You move. Your animal friend handles the attacks."));
            AnimalPortrait(overlay,Animal.Penguin,new Vector2(225,225),new Vector2(-310,-8));
            Label(overlay,L("12분의 마을 지키기","Twelve minutes to protect home"),21,new Vector2(-290,-135),new Vector2(370,60),Ink);
            Label(overlay,L($"마을 별  {profile.stars}",$"Village stars  {profile.stars}"),21,new Vector2(-290,-200),new Vector2(370,46),Pink);
            float y = 70;
            if (profile.activeRun != null) { Button(overlay,L("저장한 모험 계속하기","Continue saved adventure"),new Vector2(220,y),new Vector2(440,54),ContinueRun,true); y -= 64; }
            Button(overlay,L("새 모험 준비","Prepare a new adventure"),new Vector2(220,y),new Vector2(440,54),NewAdventure,true); y -= 64;
            Button(overlay,L("마을 꾸미기 · 도감","Village & collection"),new Vector2(220,y),new Vector2(440,54),Village); y -= 64;
            Button(overlay,L("설정 · 조작","Settings & controls"),new Vector2(220,y),new Vector2(440,54),()=>{settingsReturn="home";Settings();});
            if (save.Notice != null) Label(overlay,save.Notice=="backup"?L("저장 백업을 복원했어요.","Recovered your save from backup."):L("기존 저장을 읽지 못했어요. 파일을 보존하며 임시로 플레이합니다.","Unable to read the existing save. Playing without overwriting it."),15,new Vector2(0,-278),new Vector2(940,42),Pink);
            FocusMenu();
        }
        void NewAdventure()
        {
            if (profile.activeRun == null) { Selection(); return; }
            Panel("SAVED ADVENTURE",L("진행 중인 모험이 있어요","An adventure is waiting"),L("새 모험을 시작하려면 저장한 모험을 정산하고 마을로 돌아옵니다.","To start again, settle the saved adventure and return home."));
            Button(overlay,L("저장한 모험 계속","Continue adventure"),new Vector2(0,40),new Vector2(460,60),ContinueRun,true);
            Button(overlay,L("귀환 보상 받고 새 모험","Collect return rewards & start anew"),new Vector2(0,-45),new Vector2(460,60),()=>{var old=profile.activeRun;old.Retire();profile.Settle(old);if(SaveProfile())Selection();});
            Button(overlay,L("돌아가기","Back"),new Vector2(0,-140),new Vector2(460,54),Home); FocusMenu();
        }
        void Selection()
        {
            if(!profile.AnimalUnlocked((int)selectedAnimal))selectedAnimal=Animal.Penguin;
            if(!profile.RegionUnlocked((int)selectedRegion))selectedRegion=Region.Snowfield;
            if(!profile.EndlessUnlocked((int)selectedRegion))selectedEndless=false;
            Panel("YOUR NEXT WALK",L("오늘의 모험","Today's adventure"),profile.TestUnlocksEnabled?L("테스트 전체 해금 ON · 모든 동물·지역·무한 모드를 선택할 수 있어요.","Test unlocks ON · Every animal, region and endless mode is available."):L("동물 · 지역 · 난도를 고르고 출발하세요. 모든 난도에서 해금과 기본 보상이 같아요.","Choose a friend, region and difficulty. All difficulties share unlocks and base rewards."));
            for(int i=0;i<3;i++)
            {
                int index=i;float x=(i-1)*310;
                var animalButton=Button(overlay,AnimalName((Animal)i)+(profile.AnimalUnlocked(i)?"":L(" · 눈밭 완료"," · Clear Snowfield")),new Vector2(x,84),new Vector2(290,60),()=>{if(profile.AnimalUnlocked(index))selectedAnimal=(Animal)index;Selection();},(int)selectedAnimal==i);
                var animalLabel=animalButton.GetComponentInChildren<UnityEngine.UI.Text>();
                animalLabel.rectTransform.anchoredPosition=new Vector2(30,0);animalLabel.rectTransform.sizeDelta=new Vector2(220,60);
                AnimalPortrait(animalButton.transform,(Animal)i,new Vector2(78,78),new Vector2(-110,8));
                Button(overlay,RegionName((Region)i)+(profile.RegionUnlocked(i)?"":L(" · 잠김"," · Locked")),new Vector2(x,0),new Vector2(290,60),()=>{if(profile.RegionUnlocked(index)){selectedRegion=(Region)index;if(!profile.EndlessUnlocked(index))selectedEndless=false;}Selection();},(int)selectedRegion==i);
                Button(overlay,DifficultyName((Difficulty)i),new Vector2(x,-84),new Vector2(290,54),()=>{selectedDifficulty=(Difficulty)index;Selection();},(int)selectedDifficulty==i);
            }
            Label(overlay,L("포근: HP 80%, 속도 85%, 피해 1HP / 보통: 피해 2HP / 도전: HP 120%, 속도 110%","Cozy: HP 80%, speed 85%, damage 1HP / Normal: 2HP / Challenge: HP 120%, speed 110%"),15,new Vector2(0,-130),new Vector2(950,42),Ink);
            Button(overlay,selectedEndless?L("무한 도전","Endless challenge"):L("12분 마을 지키기","12-minute adventure"),new Vector2(-300,-190),new Vector2(300,54),()=>{if(profile.EndlessUnlocked((int)selectedRegion))selectedEndless=!selectedEndless;Selection();});
            Button(overlay,L("출발","Let's go"),new Vector2(180,-190),new Vector2(560,54),StartRun,true);
            Button(overlay,L("마을로","Back home"),new Vector2(0,-266),new Vector2(310,46),Home);FocusMenu();
        }
        void PauseMenu()
        {
            Panel("TAKE A BREATH",L("잠깐 쉬어 가요","Take a little break"),L("모험은 여기서 기다리고 있어요.","Your adventure will be here when you return."));
            Button(overlay,L("계속하기","Continue"),new Vector2(0,60),new Vector2(460,58),Resume,true);
            Button(overlay,L("설정","Settings"),new Vector2(0,-14),new Vector2(460,54),()=>{settingsReturn="pause";Settings();});
            Button(overlay,L("저장 후 마을로","Save & return home"),new Vector2(0,-88),new Vector2(460,54),()=>{if(SaveProfile())Home();});
            Button(overlay,L("이번 모험 귀환 · 보상 정산","End adventure & collect rewards"),new Vector2(0,-166),new Vector2(460,54),()=>{run.Retire();ShowPhase();});FocusMenu();
        }
        void ChoiceMenu()
        {
            bool relic=run.phase==RunPhase.RelicChoice;
            Panel(relic?"A GIFT FOR YOUR JOURNEY":"A LITTLE STRONGER",run.choosingBranch?L("최종 분기를 골라요","Choose your final path"):relic?L("목표 달성! 유물을 골라요","Objective complete! Choose a relic"):L($"레벨 {run.level-run.pendingLevels+1} · 성장 선택",$"Level {run.level-run.pendingLevels+1} · Choose an upgrade"),L("선택하는 동안 모든 전투가 멈춥니다.","All combat is paused while you choose."));
            if(run.choosingBranch)
            {
                for(int i=1;i<=2;i++){int b=i;Button(overlay,BranchDescription(run.animal,b),new Vector2((i==1?-1:1)*235,-5),new Vector2(440,175),()=>{run.Choose(-1,b);AfterChoice();},true);}
            }
            else for(int i=0;i<run.choices.Count;i++)
            {
                int choice=run.choices[i];float x=(i-(run.choices.Count-1)/2f)*310;
                string title=relic?(english?CozyRules.RelicEN:CozyRules.RelicKO)[choice]:choice==-1?L("기본 무기 강화","Upgrade your weapon"):choice==-2?L("따뜻한 차","Warm tea"):(english?CozyRules.PassiveEN:CozyRules.PassiveKO)[choice];
                string effect=relic?(english?CozyRules.RelicEffectEN:CozyRules.RelicEffectKO)[choice]:choice==-1?WeaponDescription():choice==-2?L("HP 2 회복","Restore 2 HP"):(english?CozyRules.PassiveEffectEN:CozyRules.PassiveEffectKO)[choice]+$"\n{run.passives[choice]+1} / 3";
                Button(overlay,title+"\n\n"+effect,new Vector2(x,-10),new Vector2(290,210),()=>{run.Choose(choice);AfterChoice();},true);
            }
            if(!relic&&!run.choosingBranch)Button(overlay,L($"다시 고르기 · {run.rerolls}회 남음",$"Reroll · {run.rerolls} left"),new Vector2(0,-188),new Vector2(410,54),()=>{if(run.Reroll()){SaveProfile();ChoiceMenu();}});
            Button(overlay,L("잠시 쉬기 · 저장","Pause & save"),new Vector2(0,-262),new Vector2(410,44),Pause);FocusMenu();
        }
        string WeaponDescription()
        {
            var next=CozyRules.Weapon(run.animal,run.weaponLevel+1,1);
            return run.weaponLevel==4?L("레벨 5 · 최종 분기 선택","Level 5 · Select a final branch"):L($"레벨 {run.weaponLevel+1}\n피해 {next.damage} · 주기 {next.interval:0.##}초",$"Level {run.weaponLevel+1}\nDamage {next.damage} · Interval {next.interval:0.##}s");
        }
        string BranchDescription(Animal a,int b)
        {
            if(a==Animal.Penguin)return b==1?L("A · 넓은 눈길\n눈꽃 5 · 피해 22\n궤도 108 · 회전 2초","A · Wide snowfall\n5 flakes · 22 damage\n108 orbit · 2s rotation"):L("B · 포근한 눈길\n눈꽃 4 · 피해 22\n궤도 76 · 20% 감속","B · Cozy snowfall\n4 flakes · 22 damage\n76 orbit · 20% slow");
            if(a==Animal.Capybara)return b==1?L("A · 큰 파동\n피해 26 · 범위 195\n주기 1.45초","A · Wide ripples\n26 damage · 195 range\n1.45s interval"):L("B · 잦은 파동\n피해 26 · 범위 150\n주기 1초","B · Quick ripples\n26 damage · 150 range\n1s interval");
            return b==1?L("A · 긴 편지\n피해 22 · 3명 관통\n주기 0.5초","A · Long letter\n22 damage · Pierce 3\n0.5s interval"):L("B · 친구에게\n피해 16 · 3명 발사\n주기 0.5초","B · To our friends\n16 damage · 3 targets\n0.5s interval");
        }
        string BuildSummary()
        {
            string value=AnimalName(run.animal)+L($" 무기 {run.weaponLevel}",$" weapon {run.weaponLevel}")+(run.branch>0?" "+(run.branch==1?"A":"B"):"");
            for(int i=0;i<12;i++)if(run.passives[i]>0)value+=" / "+(english?CozyRules.PassiveEN:CozyRules.PassiveKO)[i]+" "+run.passives[i];
            return value;
        }
        void Result()
        {
            music.Stop();bool win=run.phase==RunPhase.Won;
            Panel(win?"OUR VILLAGE IS SAFE":"UNTIL NEXT TIME",win?L("마을을 지켰어요!","You protected the village!"):L("따뜻한 쉼표","A warm little rest"),win?L("다음 지역과 새로운 모험이 기다립니다.","Another adventure is waiting for you."):L("쌓은 마을 별은 그대로 가져가요.","Keep the village stars you earned."));
            Label(overlay,L($"{run.score:N0}점     마을 별 +{run.villageReward}",$"{run.score:N0} points     +{run.villageReward} village stars"),32,new Vector2(0,90),new Vector2(900,58),Pink);
            Label(overlay,RegionName(run.region)+" / "+DifficultyName(run.difficulty)+L($"\n생존 {TimeText(Mathf.Min(run.elapsed,720))} · 보스 {TimeText(run.bossStarted<0?0:run.elapsed-run.bossStarted)} · 목표 {run.objectives}/3 · 처치 {run.kills}",$"\nSurvival {TimeText(Mathf.Min(run.elapsed,720))} · Boss {TimeText(run.bossStarted<0?0:run.elapsed-run.bossStarted)} · Objectives {run.objectives}/3 · Kills {run.kills}"),19,new Vector2(0,16),new Vector2(930,72),Ink);
            Label(overlay,BuildSummary(),16,new Vector2(0,-68),new Vector2(940,70),Ink);
            Label(overlay,L("결과: ","Result: ")+DamageName(run.resultReason)+(profile.failures>=3?L(" · 다음 모험은 포근 난도도 좋아요."," · Try Cozy difficulty next time."):""),16,new Vector2(0,-130),new Vector2(920,40),Ink);
            Button(overlay,L("같은 설정으로 다시","Try these settings again"),new Vector2(-246,-206),new Vector2(450,58),StartRun,true);
            Button(overlay,L("동물 · 지역 변경","Change friend or region"),new Vector2(246,-206),new Vector2(450,58),Selection);
            Button(overlay,L("마을로","Home"),new Vector2(0,-276),new Vector2(300,42),Home);FocusMenu();
        }
        string DamageName(string id)
        {
            if(id=="victory")return L("지역 보스 처치","Region boss defeated");if(id=="retired")return L("모험에서 귀환","Returned home");if(id=="boss_timeout")return L("보스전 제한 90초 종료","Boss time limit reached");
            string[] names={"눈뭉치 접촉","튼튼 정령 접촉","돌진 정령 접촉","원거리 정령 접촉","분열 정령 접촉","지원 정령 접촉","새끼 정령 접촉","정예 돌진","보스 접촉"};
            if(Enum.TryParse<EnemyRole>(id,out var role))return english?role+" contact":names[(int)role];
            return id=="ranged"?L("정령 투사체","Spirit projectile"):id=="snow_fan"?L("눈구름 부채탄","Snowcloud fan"):id=="spring_eruption"?L("온천 분출","Spring eruption"):id=="letter_fan"?L("밤편지 부채탄","Night letter fan"):id;
        }
        void Settings()
        {
            Panel("MAKE YOURSELF COMFORTABLE",L("작은 설정","Make yourself comfortable"),Application.isMobilePlatform?L("화면을 드래그해 이동하고, 손을 떼면 멈춥니다.","Drag anywhere on the field to move. Lift your finger to stop."):L("WASD / 방향키 / 왼쪽 스틱 / 화면 드래그로 이동합니다.","Move with WASD, arrows, left stick or a screen drag."));
            var s=profile.settings;
            void Change(Action action){action();SaveProfile();Settings();}
            Button(overlay,L("언어: 한국어","Language: English"),new Vector2(-240,90),new Vector2(450,50),()=>Change(()=>s.english=!s.english));
            Button(overlay,L("음소거: ","Mute: ")+(s.muted?L("켜짐","on"):L("꺼짐","off")),new Vector2(240,90),new Vector2(450,50),()=>Change(()=>s.muted=!s.muted));
            Button(overlay,L($"음악 {s.music:P0} · 누르면 조절",$"Music {s.music:P0} · Cycle volume"),new Vector2(-240,25),new Vector2(450,50),()=>Change(()=>s.music=s.music>=.99f?0:Mathf.Min(1,s.music+.1f)));
            Button(overlay,L($"효과음 {s.effects:P0} · 누르면 조절",$"Effects {s.effects:P0} · Cycle volume"),new Vector2(240,25),new Vector2(450,50),()=>Change(()=>s.effects=s.effects>=.99f?0:Mathf.Min(1,s.effects+.1f)));
            Button(overlay,L("큰 글씨: ","Larger text: ")+(s.largeText?"ON":"OFF"),new Vector2(-240,-40),new Vector2(450,50),()=>Change(()=>s.largeText=!s.largeText));
            Button(overlay,L("장식 줄이기: ","Reduce decoration: ")+(s.reducedDecoration?"ON":"OFF"),new Vector2(240,-40),new Vector2(450,50),()=>Change(()=>s.reducedDecoration=!s.reducedDecoration));
            string[] ids={"up","left","down","right"}, names={s.up,s.left,s.down,s.right};
            if(Application.isMobilePlatform)
                Label(overlay,L("전투 화면을 누른 위치에 조이스틱이 나타나요.\n공격은 자동으로 진행됩니다.","The movement stick appears where you touch the field.\nYour animal attacks automatically."),20,new Vector2(0,-116),new Vector2(920,70),Ink);
            else for(int i=0;i<4;i++){string id=ids[i];Button(overlay,id.ToUpper()+"  ["+names[i].ToUpper()+"]",new Vector2((i-1.5f)*230,-116),new Vector2(215,54),()=>{rebind=id;Panel("CONTROLS",L("이동 키를 누르세요","Press a movement key"),L("문자 키를 선택하세요. ESC는 취소합니다.","Choose a letter key. Escape cancels."));});}
            Label(overlay,Application.isMobilePlatform?L("오른쪽 위 II: 일시정지 / 앱을 벗어나면 자동으로 멈춥니다.\n흔들림·화면 플래시·진동은 사용하지 않습니다.","Tap II to pause. Leaving the app pauses automatically.\nNo screen shake, full-screen flashes or vibration."):L("패드 A: 선택 / 방향키: 메뉴 이동 / START: 일시정지\n흔들림·화면 플래시·진동은 사용하지 않습니다.","Gamepad A: confirm / D-pad: navigate / START: pause\nNo screen shake, full-screen flashes or vibration."),16,new Vector2(0,-189),new Vector2(920,65),Ink);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Button(overlay,L("테스트 · 전체 해금: ","Test · Unlock all: ")+(s.testUnlockAll?"ON":"OFF"),new Vector2(-240,-268),new Vector2(450,46),()=>Change(()=>{s.testUnlockAll=!s.testUnlockAll;s.testUnlockConfigured=true;if(run!=null)SetupTerrain();}),s.testUnlockAll);
            Button(overlay,L("돌아가기","Back"),new Vector2(240,-268),new Vector2(450,46),()=>{if(settingsReturn=="pause")PauseMenu();else Home();});
#else
            Button(overlay,L("돌아가기","Back"),new Vector2(0,-268),new Vector2(410,46),()=>{if(settingsReturn=="pause")PauseMenu();else Home();});
#endif
            FocusMenu();
        }
        void ReadRebind()
        {
            if(Time.frameCount<=blockedFrame||Keyboard.current==null)return;
            if(Keyboard.current.escapeKey.wasPressedThisFrame){rebind=null;Settings();return;}
            foreach(var key in Keyboard.current.allKeys)
            {
                if(!key.wasPressedThisFrame||key.name.Length!=1||!char.IsLetter(key.name[0]))continue;
                var s=profile.settings;string value=key.name;
                // Swap a duplicate binding instead of making another direction unreachable.
                string old=rebind=="up"?s.up:rebind=="down"?s.down:rebind=="left"?s.left:s.right;
                if(s.up==value)s.up=old;if(s.down==value)s.down=old;if(s.left==value)s.left=old;if(s.right==value)s.right=old;
                if(rebind=="up")s.up=value;else if(rebind=="down")s.down=value;else if(rebind=="left")s.left=value;else s.right=value;
                rebind=null;SaveProfile();Settings();break;
            }
        }
        void Village()
        {
            Panel("OUR LITTLE HOME",L("마을 꾸미기", "Our little village"),L($"마을 별 {profile.stars} · 장식은 전투 능력에 영향을 주지 않습니다.",$"{profile.stars} village stars · Decorations never change combat stats."));
            string[] namesKO={"별 화분","유자 나무","우편함","눈사람","온천 바구니","편지 가랜드","오로라 등불","찻상","별 벤치","겨울 정자","온천 지붕","달빛 풍향계"};
            string[] namesEN={"Star pot","Yuzu tree","Mailbox","Snow friend","Spa basket","Letter garland","Aurora lamp","Tea table","Star bench","Winter gazebo","Spa roof","Moon vane"};
            for(int i=0;i<12;i++)
            {
                int id=i;bool owned=profile.DecorationAvailable(i);string title=(english?namesEN:namesKO)[i]+"\n"+(owned?(profile.cosmetics[i]?L("마을에 배치됨","Placed in village"):L("테스트 배치","Test preview")):(100*(i/3+1))+L(" 별"," stars"));
                Button(overlay,title,new Vector2((i%3-1)*310,88-(i/3)*77),new Vector2(294,64),()=>{if(profile.BuyDecoration(id))SaveProfile();Village();},owned);
            }
            Button(overlay,L("편지 · 업적 · 기록", "Letters, achievements & records"),new Vector2(-240,-259),new Vector2(450,48),Collection);
            Button(overlay,L("돌아가기","Back"),new Vector2(240,-259),new Vector2(450,48),Home);FocusMenu();
        }
        void Collection()
        {
            int count=0;foreach(bool a in profile.achievements)if(a)count++;
            Panel("MEMORIES OF OUR WALKS",L("모험 도감", "Adventure collection"),L($"업적 {count}/18 · 구 프로토타입 최고 {profile.legacyBest}점",$"Achievements {count}/18 · Legacy prototype best {profile.legacyBest}"));
            for(int region=0;region<3;region++)
            {
                string text=RegionName((Region)region)+"\n";
                for(int a=0;a<3;a++)text+=AnimalName((Animal)a)+": "+(profile.LettersUnlocked(region)?Letter(a,region):L("지역을 완료하면 편지가 도착해요.","Clear this region to receive a letter."))+"\n\n";
                Label(overlay,text,18,new Vector2((region-1)*313,-5),new Vector2(294,252),Ink);
            }
            int high=0;foreach(var r in profile.records)high=Math.Max(high,r.score);
            Label(overlay,L($"지역 첫 승리 · 동물 승리 · 지역 목표 · 무기 6분기 · 장식 수집\n기록 {profile.records.Count}개 · 최고 {high}점 (지역·동물·난도·모드별 저장)",$"First clears · Animal wins · Objectives · 6 weapon branches · Decorations\n{profile.records.Count} record groups · Best {high} points"),16,new Vector2(0,-177),new Vector2(930,70),Ink);
            Button(overlay,L("마을 꾸미기로","Back to village"),new Vector2(0,-268),new Vector2(420,46),Village);FocusMenu();
        }
        string Letter(int a,int r)
        {
            string[] ko={"네 발자국 덕분에 눈길이 덜 외로웠어.","반짝이는 눈을 보니 따뜻한 차가 생각나.","눈밭에서 찾은 별을 편지에 넣었어.","따뜻한 물에 발을 쉬게 해 줘서 고마워.","다음엔 유자를 조금 더 띄워 보자.","수증기 사이로 네가 보이면 안심이 돼.","편지들이 무사히 집을 찾았네.","긴 배달 뒤에는 우리 같이 쉬자.","너에게 보내는 편지가 제일 먼저 도착했으면 해."};
            string[] en={"Your footprints made the snow feel less lonely.","Sparkling snow makes me think of warm tea.","I put a snowfield star in this letter.","Thanks for giving our tired feet a warm rest.","Let's float a little more yuzu next time.","Seeing you through the steam feels like home.","Every letter found its way home.","Let's rest together after that long delivery.","I hope my letter reaches you first."};
            return (english?en:ko)[r*3+a];
        }
    }
}
