# 모든 콘텐츠 해금 · v0.3.5

이 버전은 **모든 콘텐츠가 항상 해금된 빌드**입니다. Unity 에디터·개발용 빌드뿐 아니라 **Development Build를 끈 APK와 AAB에도 동일하게 적용**됩니다.

이전 버전은 UNITY_EDITOR 또는 DEVELOPMENT_BUILD 조건 때문에 일반 AAB에서 잠금이 다시 적용됐습니다. v0.3.5는 그 조건과 저장 설정에 따른 해금 분기를 제거했습니다. 기존 저장의 testUnlockAll=false나 초기화 여부와 관계없이 콘텐츠를 이용할 수 있습니다. 저장 파일을 삭제하거나 설정을 켤 필요가 없습니다.

## 적용 범위

- 펭귄·카피바라·고양이 3종과 난도 3개.
- 오로라 눈밭·온천 마을·달빛 우체국 및 각 지역의 무한 모드.
- 마을 장식 12종의 배치와 편지 9개 열람.

설정 화면에는 **모든 콘텐츠 해금됨**을 표시하며 OFF 전환 버튼은 없습니다. 장식은 별을 사용하지 않고 배치된 상태로 표시됩니다.

실제 클리어·업적·기존 구매 내역·보유 별·진행 중인 모험은 보존합니다. 클리어나 업적을 달성한 것처럼 기록을 바꾸지는 않습니다. 레벨·무기 성장과 전투 난도는 기존 규칙을 사용하며 실제 플레이에서 얻는 보상과 클리어도 정상 기록됩니다.

## 빌드

- 표시 버전 **0.3.5**, Android 버전 코드 **8**.
- 직접 설치 APK: Cozy > 2. Build Android APK → Builds/Android/CozySurvivors-v0.3.5.apk. Development Build 꺼짐, 기존 테스트 APK와 같은 개발 서명.
- Google Play용 AAB: Android Publishing Settings에서 기존 업로드 키와 비밀번호를 설정한 뒤 Cozy > 5. Build Android AAB (configured signing) → Builds/Android/CozySurvivors-v0.3.5.aab. Development Build 꺼짐, 설정된 업로드 키 사용.
- 빌드 명령은 작업 후 원래의 APK/AAB 선택 및 사용자 키 사용 설정을 복원합니다.

검사 결과는 CommercialValidation.txt와 AndroidAllUnlockedValidation-v0.3.5.txt에 기록합니다.
