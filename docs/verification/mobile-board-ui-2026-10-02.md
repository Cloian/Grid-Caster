# 모바일 보드 중심 UI 갱신

## 요청과 판단

사용자가 제공한 실제 iPhone/카카오톡 인앱 브라우저 화면은 하단 8개 버튼과 카메라 여백이 보드에 집중하기 어렵게 만든다는 근거다. 이 사진은 **변경 전 사용자 실기기 관찰**이며 아래 자동 검사 결과와 구분한다.

13×13 플레이 칸과 경계 벽은 한 화면에 유지한다. 전투를 줌/스크롤하거나 적을 잘라내지 않는다. 정사각 보드의 크기는 세로 기기의 폭에도 제한되므로 하단 버튼 제거만으로 타일 크기가 두 배가 되지는 않는다. 모바일 카메라 패딩과 보드 좌우 여백을 줄여 실제 보드를 확대했다.

## 변경 파일과 동작

- `Assets/Codes/MobilePlaytestController.cs`: 하단 이동·공격·특수공격 한 줄. 별도 실행 버튼 제거, 같은 버튼으로 선택과 확정. 강화·유물·메뉴·상태를 보드 위로 이동. 취소는 선택 중에만 표시하며 다른 버튼/보드와 겹치지 않는다. 선택한 이동 칸에 청록 테두리와 반투명 플레이어를 표시한다. 공격 방향은 좌표 대신 방향 이름을 표시한다.
- `Assets/Codes/CameraController.cs`: 모바일 맵 패딩 0.03, PC 기존 설정 유지. 보드 중심/전체 경계·장애물·캐릭터는 그대로 유지.
- `Assets/Editor/MobilePlaytestValidation.cs`: 한 줄 조작, 착지 미리보기, 같은 버튼 확정, 추가 공격 재조준 검사 보강.
- `Tools/mobile-playtest/browser-smoke.mjs`: 실제 진단 좌표로 UI 터치, 보드 밖 배치·취소/조작 겹침·낮은 브라우저 높이 검사 추가. 읽기 전용 상태 조회 외 전투 조작은 UI 터치로 진행.
- `Tools/mobile-playtest/index.html`: 새 조작 안내와 Safari/홈 화면 웹 앱 실행 안내.
- `docs/design/mobile-safari-playtest.md`, 검증 README, 이 문서와 원본 결과: 동작·한계·검증 기록.

씬·프리팹·PlayerSettings·패키지·태그·레이어·Tilemap/Collider 설정을 바꾸지 않았다. 기존 GameHUD가 모바일 어댑터를 자동 연결하므로 수동 Inspector 작업은 없다. 새 Sprite/아이콘은 추가하지 않았고 기존 Point/PPU 설정도 변경하지 않았다. UI/픽셀 렌더링 지침에 따라 기존 uGUI와 URP를 재사용하고 전투 판정과 시각적 착지 표시를 분리했다. 전투 수치, 성장, 적 턴 시점, PC A/S/F 입력은 변경하지 않았다.

## 검증

실제 프로젝트의 Unity 버전 `6000.4.0f1`, Input System `1.19.0`, URP `17.4.0`, uGUI `2.0.0`를 확인했다. 열려 있는 원본 Editor와 다른 프로젝트를 건드리지 않고 격리 복사본에서 실행했다.

- [모바일 네이티브 원본](mobile-board-ui-adapter-2026-10-02.txt): 26개 조건 통과. 375×667, 390×660, 390×844, 430×932 레이아웃 계산, 실제 uGUI raycast, 착지 표시·확정·상세창 입력 차단, 더블캐스트 재조준 완료와 이동/취소 차단 포함. Unity Search의 기존 Editor 시작 예외는 게임 코드/Play 검사 오류와 구분했다.
- [PC 전투 회귀 원본](mobile-board-ui-pc-2026-10-02.txt): 45개 조건 통과, 실행 중 URP 확인. 실제 OS 키 이벤트 자체의 검사나 인간 완주 기록은 아니다.
- Unity Web 빌드: 성공, CLI `editorErrors: []`, `MOBILE_WEB_BUILD_SUCCEEDED`, 압축 산출물 13,782,087 bytes. 생성된 정적 포장 파일 4개는 압축 해제한 원래 바이트와 모두 일치하며 WASM 매직 바이트도 확인했다. 정적 실행 파일 합계는 63,940,814 bytes이며 실제 네트워크 전송량과 같지 않다.
- 로컬 Chrome 터치 검사: 최종 빌드 36개 조건 통과, console/page 오류 없음. 실제 화면 캡처에서 이동 착지점, 한 줄 버튼, 보드 밖 취소 배치와 짧은 브라우저 높이를 확인했다.
- 공개 HTTPS Chrome 터치 검사: `https://cloian.github.io/Grid-Caster/?v=be8b591`에서 최종 빌드 36개 조건 통과, console/page 오류 없음. 공개 전투 화면 캡처도 확인했다. 로컬 서버나 파일을 공개 주소로 대체한 검사가 아니다.

브라우저 검사는 **Chrome 모바일 터치 에뮬레이션**이며 실제 iPhone/Safari가 아니다. 390×844 기본 화면과 인앱 도구 막대를 고려한 390×660, 375×667 및 가로 안내를 검사한다. 원본 로그/캡처는 Git 제외 경로 `Builds/MobileBoardWeb/verification/`, `live-verification/`에 보존한다.

## 배포

공개 주소: https://cloian.github.io/Grid-Caster/

게임 빌드 ID는 `8cc07f5fd37231c5`, `gh-pages` 배포 커밋은 [`be8b59128822aa7b56f7a9b9637875b40737c444`](https://github.com/Cloian/Grid-Caster/commit/be8b59128822aa7b56f7a9b9637875b40737c444)이다. Pages API `built`, 오류 없음과 원격 브랜치 SHA 일치를 확인했다. 공개 HTML은 새 조작 안내를, manifest는 새 실행 파일 경로를 제공한다. WASM/data HTTP 200과 각각 `application/wasm` / `application/octet-stream`을 확인했다.

gzip 요청의 공개 응답은 두 파일 모두 `Content-Encoding: gzip`이다. 관측 크기는 WASM 12,891,243 bytes, data 5,958,082 bytes다. 모든 지역 CDN이나 전체 초기 로딩 시간의 보장은 아니다.

`gh-pages`에 실행 HTML/manifest와 내용 해시 경로의 4개 Unity 실행 파일만 갱신했다. 기존 빌드 파일은 캐시 사용자와 복구를 위해 유지했다. 개발 브랜치/`main` push·PR·병합·force push는 하지 않았다. Unity 생성 framework의 기존 줄 끝 공백은 원본 바이트 보존을 위해 재포맷하지 않았다. 관리하는 HTML/manifest의 `git diff --check`와 검사 스크립트 Node 구문 검사는 통과했다.

새 HTML 캐시를 구분하는 테스트 링크: https://cloian.github.io/Grid-Caster/?v=be8b591 . Pages 캐시 때문에 갱신 직후 이전 화면이 보이면 새 링크를 열어 확인하고, 같은 세션을 계속 플레이하려면 중간에 새로고침하지 않는다.

## 실기기에서 확인할 것

1. 링크를 Safari에서 열고 시작 특성/증강을 선택한다. 전투 화면에서 13×13 칸과 경계 벽이 모두 보이며 강화·유물·상태가 보드를 가리지 않아야 한다.
2. `이동` → 노란 인접 칸 탭: 해당 칸에 청록색 선택과 반투명 플레이어가 보여야 한다. `이동 실행`으로 그 칸으로 이동하고 적이 한 번 행동해야 한다.
3. `공격` → 원하는 방향의 보드 탭: 노란 경로와 방향 이름 확인 후 `공격 실행`. 미리보기/취소 자체는 턴이 진행되지 않아야 한다.
4. 충전된 특수공격과 더블캐스트 추가 방향 선택을 확인한다. 추가 공격 중 이동/취소가 막히고 공격 버튼으로 완료되어야 한다.
5. 상단 강화·유물 목록에서 상세 확인 후 닫아도 뒤의 보드에 행동이 발생하지 않아야 한다. 일시정지/재개와 회전·브라우저 도구 막대 변화 후 잘림도 확인한다.

카카오톡 인앱 브라우저 도구 막대는 게임에서 제거하지 못한다. Safari 공유 → 홈 화면에 추가 → 표시되는 경우 웹 앱으로 열기를 활성화하고 홈 화면 아이콘으로 실행하는 방식은 [Apple 공식 안내](https://support.apple.com/guide/iphone/iphea86e5236/ios)를 따른다. 이것은 네이티브 앱/오프라인 저장/런 복원 기능이 아니다. 실제 기기 터치 감각·후반 성능·발열·재미·20웨이브 완주는 아직 검증하지 않았다.
