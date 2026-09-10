# 선택한 캐릭터 idle · Android 0.3.1

2026-09-10 사용자 선택: 카피바라 1번 **유자 온천**, 고양이 2번 **삼색 편지지기**.
타깃 플랫폼은 **Android 모바일**이며 Windows는 렌더/입력 검증에만 사용한다.

| 캐릭터 | 적용 리소스 | 미리보기 |
|---|---|---|
| 카피바라 · 유자 온천 | `Assets/Cozy/Resources/Cozy/Capybara/Idle0.png` ~ `Idle7.png` | [반복 GIF](AnimationCandidates/Idle-v1/capy_a.gif) |
| 고양이 · 삼색 편지지기 | `Assets/Cozy/Resources/Cozy/Cat/Idle0.png` ~ `Idle7.png` | [반복 GIF](AnimationCandidates/Idle-v1/cat_b.gif) |

- 후보 이미지는 내장 `image_gen`으로 생성했다. 생성 프롬프트와 출처는 [prompts.json](AnimationCandidates/Idle-v1/prompts.json), 여섯 원본 시트와 프레임은 같은 폴더에 보존했다.
- 8개의 서로 다른 포즈를 2초 주기로 반복한다. 각 프레임 유지 시간(ms): `600, 230, 230, 100, 160, 100, 260, 320`.
- 384×384 프레임, 발 기준선 316px, 동물별 공통 배율로 위치를 정렬했다. 픽셀 필터 Point, mipmap 없음, 무압축, NPOT 리사이즈 없음.
- 게임 내 표시 영역은 펭귄과 같은 118×118이다. 펭귄의 기본 재생과 이동/피격/퇴장 프레임은 그대로 사용한다.
- 카피바라·고양이는 기존 도형 아이콘을 교체했다. 플레이 화면과 동물 선택 버튼에서 같은 idle을 표시하고 이동 방향에 따라 좌우 반전한다. 전투가 멈추면 게임 속 idle도 멈춘다.
- 새 동물의 배경은 기존 ReferenceSprite 셰이더의 별도 머티리얼로 제거한다. 원본의 밝은 크림색 털과 수건을 보존하며, 펭귄의 기존 키 임계값은 유지한다.
- 두 동물의 이동/공격/피격/퇴장 전용 시트는 이번 범위에 포함되지 않는다. 이동 중에도 선택한 idle을 기본 표현으로 사용한다.
- Android는 가로 양방향 회전, safe area, 화면 드래그, 포커스/앱 중단 시 일시정지 기능을 유지한다. 모바일 플레이·설정 안내는 터치 드래그를 먼저 표시한다. 모바일 설정에서는 키보드 재매핑 버튼을 숨겨 키 입력 대기 화면에 갇히지 않도록 했다.

## 검증 산출물

- `CharacterAnimationValidation.txt`: 16개 프레임/Unity 임포트 설정 및 2초 루프 검사.
- `Review/Animations/`: 실제 Unity UI 렌더에서 각 프레임과 머티리얼을 확인한 화면.
- `CommercialInputValidation.txt`: Input System 가상 터치·패드 검사. Android 실기기의 성능/발열/물리 터치 검증을 대신하지 않는다.
- 실제 렌더 검사와 입력 검사 8개 통과. 저장 복원 오류를 함께 수정하고 `CommercialValidation.txt`의 129개 검사를 통과했다.
- `AndroidBuild.txt`: Android 빌드 결과. APK는 `Builds/Android/CozySurvivors-v0.3.1.apk`.
- `AndroidApkValidation.txt`: 빌드 오류 0, APK 77.4MB, ARM64/API26+, 패키지·버전·서명·16KiB 정렬·SHA-256 확인 결과.

연결된 Android 기기가 없어 이번 작업에서는 실기기 설치·구동을 확인하지 못했다.
