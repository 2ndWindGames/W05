# 포근한 모험 · Cozy Survivors

상용 종합 기획서 v1.0을 적용한 **Android 모바일** Unity 게임입니다. 가로 화면과 터치 드래그 조작을 기준으로 개발합니다.

- Unity **6000.3.23f1**에서 `Assets/Cozy/Scenes/AuroraSnowfield.unity`를 열고 Play.
- Android APK: `Builds/Android/CozySurvivors-v0.3.5.apk` · Android 8.0 이상 · ARM64 · 직접 설치용 개발 서명 · Development Build 꺼짐.
- APK 빌드: Unity 메뉴 `Cozy > 2. Build Android APK`. 버전 코드 8.
- AAB 빌드: 기존 업로드 키와 비밀번호를 Android Publishing Settings에 설정한 뒤 `Cozy > 5. Build Android AAB (configured signing)`. 출력은 `Builds/Android/CozySurvivors-v0.3.5.aab`.
- [영문 출시 노트 · v0.3.5](Documentation/RELEASE_NOTES_v0.3.5.en.md)
- **모든 콘텐츠가 항상 해금됩니다.** 일반 APK·AAB에서도 적용되며 기존 OFF 설정은 무시합니다. 설정 화면에는 전환 버튼 대신 ‘모든 콘텐츠 해금됨’을 표시합니다. [전체 해금 안내](Documentation/TEST_MODE.md)
- 화면 드래그로 이동, 자동 공격, 화면 오른쪽 위 일시정지 버튼. 키보드·패드는 에디터/보조 입력에서도 지원합니다.
- Windows 실행 파일은 개발 검증용 보조 타깃입니다.
- [구현 범위와 검증, 남은 출시 작업](Documentation/COMMERCIAL_IMPLEMENTATION.md)
- [시뮬레이션 검증](Documentation/CommercialValidation.txt)
- [터치·패드 입력 검증](Documentation/CommercialInputValidation.txt)
- [선택한 캐릭터 idle 애니메이션](Documentation/CHARACTER_ANIMATIONS.md)
- [Android 전체 해금 검증](Documentation/AndroidAllUnlockedValidation-v0.3.5.txt)
- [기준 기획서](Documentation/Cozy_Animal_Survivors_Commercial_GDD_v1.0.pptx)

동물 3종·지역 3곳·모든 무한 모드·장식 12종·편지 9개를 처음부터 이용할 수 있습니다. 실제 클리어·업적·구매 기록과 보유 별은 보존하고, 전투 중 성장과 보상은 기존 규칙을 따릅니다. 카피바라 1번(유자 온천), 고양이 2번(삼색 편지지기)의 8프레임 idle을 적용했습니다. 다른 행동 애니메이션과 추가 지역 아트·Android 실기기 QA는 별도로 남아 있습니다.
