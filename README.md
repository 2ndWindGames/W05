# 포근한 모험 · Cozy Survivors

상용 종합 기획서 v1.0을 적용한 **Android 모바일** Unity 게임입니다. 가로 화면과 터치 드래그 조작을 기준으로 개발합니다.

- Unity **6000.3.23f1**에서 `Assets/Cozy/Scenes/AuroraSnowfield.unity`를 열고 Play.
- Android APK: `Builds/Android/CozySurvivors-v0.3.4.apk` · Android 8.0 이상 · ARM64 · 개발용 서명.
- APK 빌드: Unity 메뉴 `Cozy > 2. Build Android APK`. 버전 코드 7.
- [영문 출시 노트 · v0.3.4](Documentation/RELEASE_NOTES_v0.3.4.en.md)
- v0.3.3 첫 실행에서는 기존 OFF 설정도 **전체 해금 ON**으로 한 번 전환합니다. 이후 **설정 · 조작 → 테스트 · 전체 해금: ON/OFF**에서 변경하며 재실행해도 유지됩니다. [테스트 모드 안내](Documentation/TEST_MODE.md)
- 화면 드래그로 이동, 자동 공격, 화면 오른쪽 위 일시정지 버튼. 키보드·패드는 에디터/보조 입력에서도 지원합니다.
- Windows 실행 파일은 개발 검증용 보조 타깃입니다.
- [구현 범위와 검증, 남은 출시 작업](Documentation/COMMERCIAL_IMPLEMENTATION.md)
- [시뮬레이션 검증](Documentation/CommercialValidation.txt)
- [터치·패드 입력 검증](Documentation/CommercialInputValidation.txt)
- [선택한 캐릭터 idle 애니메이션](Documentation/CHARACTER_ANIMATIONS.md)
- [Android APK 검증](Documentation/AndroidDistributionValidation-v0.3.4.txt)
- [기준 기획서](Documentation/Cozy_Animal_Survivors_Commercial_GDD_v1.0.pptx)

전체 해금 ON에서는 모든 동물·지역·무한 모드·장식·편지를 바로 확인할 수 있습니다. OFF인 기본 진행에서는 펭귄·카피바라로 시작하고, 눈밭 보스 처치 시 고양이·온천·눈밭 무한 모드가 열리며, 온천을 완료하면 우체국이 열립니다. 카피바라 1번(유자 온천), 고양이 2번(삼색 편지지기)의 8프레임 idle을 적용했습니다. 다른 행동 애니메이션과 추가 지역 아트·Android 실기기 QA는 별도로 남아 있습니다.
