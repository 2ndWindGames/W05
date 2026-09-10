# 포근한모험 · CozySurvivors · 펭귄 안드로이드 프로토타입

> 이 문서는 v0.2의 과거 구현 기록입니다. 현재 상용 기획서 기반 개발 빌드는 `COMMERCIAL_IMPLEMENTATION.md`를 참조하세요.

기준: `Cozy_Animal_Survivors_Prototype_v0.2.pptx`.
목적은 색감, 분위기, 이동과 자동 공격의 감각 확인입니다. 레벨 설계, 성장, 수익화는 포함하지 않습니다.

## 실행

- Unity **6000.3.23f1**, Android Build Support / SDK / NDK / OpenJDK 사용.
- `Assets/Cozy/Scenes/AuroraSnowfield.unity`를 열고 Play.
- 첫 준비는 메뉴 **Cozy → 1. Create or open penguin scene**.
- APK 생성은 **Cozy → 2. Build Android APK**.
- 출력: `Builds/Android/CozySurvivors-v0.2.1.apk` (버전 코드 3).
- Android 8.0 이상, ARM64, 가로 양방향 회전, 개발용 디버그 서명 APK.
- USB 디버깅으로 연결한 기기에서는 Android SDK의 `adb install -r Builds/Android/CozySurvivors-v0.2.1.apk`로 설치할 수 있습니다.
- 기기 언어가 한국어이면 앱 이름은 `포근한모험`, 그 외에는 `CozySurvivors`입니다. 게임 내 전체 UI의 영문 번역은 포함하지 않습니다.
- 아이콘 후보 3개는 `Assets/Cozy/Branding/Icons`에 보관하며, 1번 별과 목도리를 적용합니다. 시작 스플래시는 W01의 SecondWind Games 원본과 동일한 흰 배경·2초·정적 표시입니다.

## 조작과 모드

화면 상단 HUD 아래 어디든 터치하고 드래그합니다. 터치한 곳이 조이스틱 중심이며, 손을 떼면 빠르게 정지합니다. 다른 손가락으로 정지 버튼을 누를 수 있습니다. 에디터에서는 WASD, 방향키, 마우스 드래그를 지원합니다.

눈꽃 세 개가 2.4초마다 회전하며 실제 눈꽃 위치에서만 피해를 줍니다. 고리 전체에 판정이 있는 방식은 아닙니다. 눈뭉치 정령은 한 번, 우박 정령은 세 번 맞으면 사라집니다.

- **산책 모드**: 기본값. 적 밀도를 일정하게 유지하며 외형과 조작감 확인.
- **생존 모드**: 기획서의 시간별 생성 곡선. 이번 작업에서 난이도 균형을 조정하지 않음.
- **눈밭 구경**: 적 없이 이동, 배경과 소리 확인.

체력 5칸, 피격 후 1초 무적, 처치 점수와 최고 기록, 결과와 재시작을 제공합니다. 앱이 백그라운드로 이동하면 정지하고, 돌아와서 계속하기를 눌러 재개합니다. 소리 설정과 최고 기록은 기기 내부에 저장합니다.

## 조작감 조정

`Assets/Cozy/CozyFeel.asset`을 선택합니다.

| 설정 | 기본값 | 의미 |
| --- | --- | --- |
| Move Speed | 220 | 논리 화면 기준 초당 이동 거리 |
| Response Seconds | 0.08 | 가속 및 정지 반응 시간 |
| Joystick Radius | 68 | 최대 속도에 도달하는 드래그 거리 |
| Dead Zone | 0.08 | 작은 손 떨림 무시 |
| Orbit Radius | 88 | 눈꽃과 몸 사이 거리 |
| Orbit Seconds | 2.4 | 눈꽃 한 바퀴 시간 |
| Gentle Spawn Rate | 0.8 | 산책 모드 초당 생성량 |

화면은 안전 영역 안에 16:9 구도로 맞춥니다. 긴 휴대폰에서는 양옆에 짙은 남보라 여백이 생기며 UI나 경기장을 잘라내지 않습니다. 60fps는 목표값이고 실기기 성능 보장은 아닙니다.

## 에셋과 구현

- 펭귄: 제공된 PPTX의 대기/이동/피격/탈진 GIF에서 8프레임씩 추출. 원본 배경색은 렌더링 셰이더에서 키잉하며 원본 픽셀을 다시 그리지 않습니다.
- 배경: 내장 ImageGen으로 제작한 별도 오로라 눈밭. `Assets/Cozy/Resources/Cozy/Aurora.png`.
- 정령, 눈꽃, 하트, 눈가루: 게임 코드에서 생성한 작은 픽셀 도형.
- 음악과 효과음: 코드로 합성한 짧은 자체 루프와 차임. 외부 음원 없음.
- 한글 글꼴: 공식 notofonts/noto-cjk의 Noto Sans CJK KR Regular. SIL Open Font License는 글꼴과 같은 폴더의 `Font-LICENSE.txt`에 포함.

배경 생성 프롬프트:

> Create a production background asset for a cozy penguin mobile survival game, landscape 1536x1024. Elegant pastel pixel art, detailed handcrafted snow covered enchanted forest clearing at twilight. Large completely empty pale icy lavender snow arena covering central 75% of image, with very subtle snow texture. Decorations only narrow outer edges: layered snow drifts, tiny blue crystal flowers, snow capped fir trees, one tiny warmly glowing wooden cabin in upper left, little wooden fence in bottom right, distant lilac mountains and luminous mint green and blush pink aurora across the top 15%. Rich periwinkle shadows, powder blue and cream snow, warm peach light, dreamy charming premium indie game atmosphere. Top down slightly elevated 2D game view. No characters, no creatures, no penguins, no snowmen, no enemies, no footprints, no attack circles, no UI, no words or text. Keep central playable ground exceptionally uncluttered and readable. Full bleed image.

## 검증

열린 에디터의 `Temp/CozyCommand.txt`에 `play`, `test`, `stop`, `build`를 차례대로 기록하면 개발용 명령을 수행합니다. 이전 명령은 `Temp/CozyCommandResult.txt`에서 완료를 확인한 후 다음 명령을 실행합니다. 테스트는 Play 모드에서 실제 시뮬레이션을 사용하며 저장 기록을 복원합니다. 결과는 `Temp/CozyTests.txt`입니다. `start`는 플레이 시작, `capture`는 Game 뷰 캡처입니다.

`touchtest`는 가상 Touchscreen 장치를 통해 실제 입력 경로로 드래그, 손 떼기, 정지 버튼, 계속하기 버튼을 확인합니다. 비동기 결과는 `Temp/CozyTouchTests.txt`에 기록됩니다. `refresh`는 에셋 갱신, `review`는 Game 뷰 검토용입니다.

2026-09-10 검증: 시뮬레이션 검증 34개와 입력 검증 4개 통과. 결과 원문은 `Validation.txt`, `TouchValidation.txt`에 포함합니다. 10분 분량 검증은 시간을 빠르게 진행시킨 시뮬레이션이며 실기기 10분 구동이나 60fps 측정 결과는 아닙니다. 타이틀과 플레이 화면을 직접 렌더링해 확인했습니다. USB로 연결된 Android 기기가 없어 실기기 설치, 발열, 프레임 및 물리 터치 감각 검증은 남아 있습니다.

실기기 검토에서는 이동 시작/정지, 방향 전환, 두 손가락으로 일시정지, 홈 버튼 후 복귀, 5분 발열과 프레임, 작은 화면의 적과 눈꽃 구분을 확인하면 됩니다.
