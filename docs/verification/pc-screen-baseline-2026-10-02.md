# PC 전체 보드 화면 기준 정리

## 가설과 범위

보드·HUD가 하나의 구성으로 확대/축소되고, 목록이 보드 밖에 붙어 있으면 전체 전장과 준비 상태를 함께 읽기 쉬워질 것이다. 자동 검사로 배치·입력의 일치를 확인하되, 실제 읽기 편함과 판단의 재미는 사람 플레이로 확인한다.

워크플로우 1단계만 변경했다. 전투 규칙·적 수·피해·성장 효과·보스·후속 스테이지는 변경하지 않았다. 기존 uGUI/폰트/Canvas를 유지하며 새 UI 패키지를 설치하지 않았다. Unity UI·픽셀 렌더링 스킬 기준에 따라 Sprite의 32 PPU/Point와 URP 설정, 저장 씬·GUID를 보존했다. 임의 창 크기에서 정수 배율의 완전한 픽셀 퍼펙트를 보장하는 변경은 아니다.

## 화면 기준

기준 구성은 1920×1080이다. 창 폭·높이 중 더 작은 배율로 전체 구성을 맞추며 보드는 늘어나거나 찌그러지지 않는다.

| 영역 | 기준 구성 |
| --- | --- |
| 중앙 | 외벽 포함 800×800, 내부 플레이 칸 13×13 유지, 화면 중심보다 40 위 |
| 왼쪽 / 오른쪽 | 강화·이동술 / 유물·제단, 각각 330×820 |
| 패널과 외벽 | 간격 18, 창 크기에 따라 전체와 같은 배율 |
| 상단 | 웨이브·시간을 하나의 묶음으로 중앙 정렬, 윗벽 바로 위 |
| 하단 | 행동 안내·체력·특성·F 준비도·공격·이동을 아랫벽 바로 아래 |

카메라 추적·확대·미니맵은 추가하지 않았다. 플레이어가 이동해도 보드 시점은 변하지 않는다. 울트라와이드·세로 창의 남는 공간은 구성 바깥에 남기며 보드와 패널 사이를 벌리지 않는다. 전체 구성과 글씨의 기준은 1920×1080이며 1280×720부터 축소 플레이를 검사했다. 1280에서도 목록 글씨는 작아지므로 편안한 판독성은 사람 확인이 필요하다. 더 작은 창은 배치 회귀 대상으로 검사하되 작은 글씨의 편안한 판독성을 보장하지 않는다. 후속 스테이지의 다른 맵 크기·비정사각 보드 배치는 해당 단계에서 별도로 결정한다.

데스크톱 Web은 PC HUD와 키보드/마우스를 사용한다. iPhone·Android·iPad(데스크톱 UA 포함)는 기존 터치 UI를 사용한다. 창 폭이나 Windows 터치 모니터만으로 모바일 UI로 바꾸지 않는다. HTML의 시작 안내·회전 안내도 같은 기기 판별을 따른다.

## 수정 파일과 설정

- `Assets/Codes/PcBoardLayout.cs`: 카메라와 Canvas의 공통 기준·배율.
- `CameraController.cs`: 전체 보드 크기·중심 고정. 이전 개별 예약 공간 제거. 기존 mapPadding 직렬화는 보존하되 Inspector에서 숨김.
- `RunProgressionUiController.cs`: 좌우 고정 비율, 중앙 상단·하단 묶음, 카메라 뒤에 실행되는 크기 변경 계산.
- `GameHudController.cs`: PC CanvasScaler Expand, 브라우저 백그라운드 일시정지, 읽기 전용 PC 검사 스냅샷.
- `MobilePlaytestController.cs`, `Assets/Plugins/WebGL/GridCasterBrowser.jslib`: Web 기기 판별. 대응 `.meta` 포함.
- `Assets/Editor/PcBoardLayoutValidation.cs`: 실제 씬의 기하·UI raycast 검사.
- `Assets/Editor/MobileWebBuild.cs`, `Tools/mobile-playtest/index.html`, `serve.mjs`: 공용 Web 실행 이름·PC/모바일 시작 안내.
- `Tools/mobile-playtest/pc-browser-smoke.mjs`, `browser-smoke.mjs`: PC 브라우저 검사, 모바일 검사 UA 명시.
- 상위 지침·README·정체성·워크플로우·모바일 안내: 현재 소스 상태 반영. 공개 링크 상태는 별도 표시.

새 GameObject/컴포넌트/Inspector 할당·태그·레이어·Collider 설정은 필요 없다. `SampleScene`에서 Play하면 기존 GameHUD가 런타임에 배치한다. 저장 씬·프리팹·스프라이트·패키지 설정은 수정하지 않았다.

## 실행한 검사

원본 Grid-Caster Editor는 열려 있지만 CLI/Unity 연결이 없는 상태였다. 연결된 다른 HiJackPot Editor는 사용하지 않았다. 원본 Editor·미저장 씬을 닫거나 설정을 바꾸지 않고 `/private/tmp/grid-caster-pc-layout.nVhKHi`의 격리 복사본에서 Unity 6000.4.0f1로 실행했다. CLI는 복사본도 기존 Editor의 열린 프로젝트로 판정하여 거부했으므로 해당 복사본을 Unity 실행 파일로 직접 검사했다.

- [PC 화면 30개 조건](pc-board-layout-native-2026-10-02.txt): URP 런타임 판별, 7개 해상도 계산, 실제 640×480 배치·GraphicRaycaster 클릭, 8방향 공격 선택. `-nographics` 검사의 좌표·raycast 결과이며 실제 렌더 이미지 검증은 아니다.
- [전투 45개 조건](pc-screen-tactical-regression-2026-10-02.txt): 기존 추가 시전 중 이동 차단, 무료 이동, F 준비·사용 및 턴 규칙 회귀.
- [모바일 어댑터 26개 조건](pc-screen-mobile-regression-2026-10-02.txt): 터치 선택·확정·취소·화면 밖 조작 배치 유지. 실제 iPhone/Safari 검증이 아니다.
- JS 기기 판별 6가지(PC·Windows 터치·Mac·iPhone·iPad·Android) 및 HTML/브라우저 검사 스크립트 구문 확인 통과.
- Web 빌드 성공: `Builds/PcBoardWeb`, BuildReport 총 크기 13,825,201 bytes. 원본 Editor의 플랫폼을 바꾸지 않고 격리 복사본에서 Web 대상으로 빌드했다.
- [PC Chrome 브라우저 316개 조건](pc-screen-browser-2026-10-02.json): 1회 런에서 실제 키보드·마우스로 시작 선택, 상세창, A/S/F(미준비 F 거부), 취소, 정확히 한 칸 이동·한 적 턴, 일시정지·탭 비활성 훅을 확인했다. 1920×1080, 1280×720, 1024×768, 2560×1080, 3840×2160, 800×1200, 960×540의 7가지 크기로 변경하고 1920으로 돌아왔다. 각 크기의 보드·10개 상시 HUD 분리와 공격/이동 카드 클릭을 확인했다. 316회 독립 게임이나 완주 검사가 아니다. 준비된 F의 전투 효과는 위 전투 픽스처 검사에서 확인했다.
- [Chrome 모바일 에뮬레이션 36개 조건](pc-screen-mobile-browser-2026-10-02.json): 같은 Web 빌드가 iPhone UA에서 기존 터치 UI를 유지하고 선택·확정·취소·서랍·방향/착지 표시·일시정지를 정상 처리했다. 실제 iPhone/Safari 결과가 아니다.
- [PC 공격 경로 화면 캡처](captures/pc-screen-2026-10-02.png): 8방향 화살표와 선택한 대각선의 노란 경로가 보이며 외벽 칸은 제외된다. 1280×720·4K 등 추가 캡처는 로컬 `Builds/PcBoardWeb/verification/pc-verified/`에 있다. 1920·1280 화면을 직접 이미지 확인했다.

브라우저 검사 초안은 프레임보다 짧은 키 입력과 턴 연출 완료 전 조회 때문에 실패했다. 키·클릭 유지 시간을 확보하고 `canAct`/완료 턴을 기다리도록 검사기를 수정한 뒤 최종 실행이 통과했다. 게임 연출 속도·턴 규칙을 검사에 맞춰 변경하지 않았다. 상세창 배경으로 처리되는 클릭은 창을 닫을 수 있지만 보드 행동으로 전달되지 않음을 확인했다. 초기 실패 자료는 무시하거나 최종 통과 횟수에 합산하지 않는다.

게임 코드 컴파일 오류는 없었고 최종 브라우저 실행의 console error/pageerror는 0이었다. Chrome은 SwiftShader 소프트웨어 렌더링으로 실행했으며 URP의 FSR 업스케일 셰이더 미지원 경고 등이 있었다. 실제 하드웨어 GPU 성능 검사가 아니다. 모바일 배치 실행에서 Unity Editor 자체 `UnityEditor.Search.SearchDatabase` 인덱싱 예외가 1회 있었으나 검사 결과는 통과했다. 이를 원본 Editor Console 오류 없음으로 확대 해석하지 않는다.

## Unity 핸드오프 / 이전 웹 검사 재현

사용자의 추가 지시에 따라 앞으로 별도 링크 대신 Unity 앱에서 결과를 확인한다. 기존 원본 Grid-Caster Unity 6000.4.0f1 창의 `SampleScene`을 메뉴 `Edit > Play Mode > Play`로 실행했고, 최신 시작 안내·강화/유물 패널·상하 HUD가 반영된 시작 특성 선택 화면을 직접 확인한 뒤 그대로 열어두었다. CLI 연결용 패키지는 설치하지 않았으며 다른 HiJackPot 창은 조작하지 않았다. Inspector에 새로 연결할 항목은 없다.

웹 검증 당시 주소는 `http://127.0.0.1:8770`이었다. 제가 띄운 해당 로컬 서버만 종료했고 포트에 리스너가 없음을 확인했다. 저장된 웹 빌드와 검증 자료는 삭제하지 않았다. 아래 명령은 사용자가 추후 웹 검사를 별도로 요청할 때의 재현용이며 현재 테스트 안내가 아니다. 서버는 빌드 폴더만 제공하고 인터넷 공개 서버가 아니다.

```sh
node Tools/mobile-playtest/serve.mjs Builds/PcBoardWeb 127.0.0.1 8770
```

검사기 재현은 `pc-browser-smoke.mjs <URL> <playwright 패키지 경로> <결과 폴더>`와 기존 모바일 `browser-smoke.mjs`를 사용한다. Unity 검사 메뉴는 `Tools > Playtest > Validate PC Board Layout`이다. 씬을 저장하고 Play를 종료한 뒤 실행한다.

## 사람이 확인할 항목 / 남은 작업

1. `SampleScene` Play → 시작 특성·증강 선택. 전체 보드와 위험 칸이 보이고 좌우 목록이 보드를 가리지 않는지 확인한다.
2. A 일반 공격 → 마우스 방향으로 경로 확인 → 좌클릭 실행 / 우클릭 취소.
3. S 기본 이동·이동술 → 목적지 확인·좌클릭. 이동해도 보드 시점은 고정돼야 한다.
4. 일반 공격으로 준비한 뒤 F 특성 공격을 선택한다. 하단 준비도·특성 설명을 함께 읽을 수 있어야 한다.
5. 좌우 목록 상세창·보상창·ESC 일시정지 중 입력이 전투에 전달되지 않는지 확인한다.
6. 작은 창·큰 창에서 글씨와 위험 표시의 판독성을 사용자 플레이로 확인한다. 자동 검사 통과만으로 워크플로우 1단계의 경험 완료로 표시하지 않는다.

공개 GitHub Pages 링크는 이전 배포 그대로다. 이번 변경의 공개 게시·커밋·푸시는 하지 않았다. 웹 새로고침 뒤 런 복원, 보스·스테이지 확장도 이번 범위가 아니다.

## References

- [개발 워크플로우](../design/development-workflow-2026-10-02.md), [게임 정체성](../design/game-identity-2026-10-02.md).
- `Assets/Codes/PcBoardLayout.cs`, `CameraController.cs`, `GameHudController.cs`, `RunProgressionUiController.cs`.
- `Assets/Plugins/WebGL/GridCasterBrowser.jslib`, `Assets/Editor/PcBoardLayoutValidation.cs`.
