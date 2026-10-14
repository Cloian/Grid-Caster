# Claude 아트·이동 모션·UI 변경 검토 — 2026-10-15

검토 기준: `0a300c3`. 주요 범위는 `0026d49`의 모션 연결 및 `c247929`, `d85805e`, `01435ac`의 카메라·HUD·보상 UI 변경이다. Unity 6000.4.0f1의 실제 Play 모드에서 제어된 재현 상태를 구성했다. 아래 세 항목은 수정 전 재현 기록이며, 후속 요청으로 적용한 수정과 새 검증 결과는 마지막 절에 기록한다.

## 1. [P2] 피격 직후 이동하면 걷기 모션이 나오지 않는다

- 위치: `Assets/Editor/LivelyMotionSetupTool.cs:267–274`. 생성된 플레이어·Bat·Snake Controller에도 같은 전환이 저장돼 있다.
- 원인: `IsMoving`에 따른 전환은 Idle ↔ Move에만 있다. Hit와 Attack에서는 클립 전체가 끝나야 Idle로 돌아간다. 다음 행동을 받아도 Hit → Move 전환은 없다.
- 재현: 시작 특성·시작 강화를 선택하고, 제어된 상태에서 `Move.TakeDamage(1)` 직후 `TryPerformAction(Move, right)`를 실행한다. 이동은 정상 승인되고 `IsMoving=true`가 되지만 Animator는 Hit에 남는다.
- 실측: 약 0.333초에 위치 `(1.39, 0.50)`에서 `IsMoving=true / Hit=true / Walk=false`. 약 0.471초에 목적지 `(1.50, 0.50)`에 도착했는데도 `Hit=true / Walk=false`. `player_hit` 길이는 약 0.60초다. 한 칸 이동 전체에서 걷기가 생략됐다.
- 영향: 맞은 직후 이동하면 피격 자세로 미끄러져 이동한다. 나이트 이동술도 별도 제어 상태에서 재현했다. 이동 중 `IsMoving=true / Hit=true / Walk=false`, 목적지 도착 후에도 `Hit=true / Walk=false`였다. 비숍·룩 이동술은 같은 `SetMoving(true)` 및 Controller를 사용하지만, 이 피격 직후 상황을 각각 재현하지는 않았다.
- 수정 방향: 다음 이동 행동이 시작되면 남은 피격/공격 시각 상태에서 Move로 전환할 수 있도록 한다. 연출 완료를 기다리기 위해 게임의 턴·입력 시간을 늘리지 않는다. 생성 도구와 실제 Controller 모두 반영해야 재적용 때 문제가 돌아오지 않는다.

## 2. [P2] 새 플레이어 사망 모션이 첫 프레임에서 멈추고 묘비가 표시되지 않는다

- 위치: `Assets/Codes/PlayerDeathMarker.cs:56–60`, 연관 위치 `Assets/Codes/GameHudController.cs:1044–1045`.
- 원인: 사망 모션이 있으면 별도 묘비 생성을 생략한다. 동시에 사망 HUD는 `Time.timeScale=0`으로 만들고, 플레이어 Animator는 `Normal` 모드라 사망 모션도 멈춘다.
- 재현: 실제 SampleScene에서 시작 선택을 마친 후 치명적인 피해를 준다. 결과 패널을 유지하며 비배율 시간으로 기다린다.
- 실측: 사망 직후와 약 17.72초 후 모두 `Death normalizedTime=0`, Sprite=`wizard_death_00`, `timeScale=0`, Animator=`Normal`. `ShowsGrave=true`지만 실제 Sprite는 묘비까지 도달하지 않는다.
- 영향: 사망 애니메이션과 묘비 연출이 나오지 않고, 전장 복기에서도 첫 사망 프레임이 남는다. `ShowsGrave` 값만 보는 검사는 이 문제를 놓친다.
- 수정 방향: 전투 규칙의 정지는 유지하고 사망 표현만 비배율 시간으로 끝까지 진행하거나, 사망 시 정적인 묘비 표시를 보장한다. 검증은 bool뿐 아니라 실제 마지막 Sprite/Animator 진행도도 확인해야 한다.

## 3. [P2] 제단 툴팁이 일시정지 메뉴 위로 올라온다

- 위치: `Assets/Codes/RunProgressionUiController.cs:499–512`, `ShowTooltipAtScreen`의 `SetAsLastSibling()`.
- 원인: 보드 제단 hover가 검사하는 `IsModalOpen`에는 일시정지와 런 결과 창이 포함되지 않는다. 메뉴를 열어도 LateUpdate에서 툴팁을 다시 표시하고 마지막 sibling으로 올린다.
- 재현: 활성 제단과 해당 칸의 마우스 위치를 구성하고 `GameHudController.TogglePauseMenu()`를 호출한다.
- 실측: `IsPauseMenuOpen=true / IsModalOpen=false / HoverTooltip active=true`. 같은 Canvas에서 PauseMenuOverlay sibling=9, HoverTooltip sibling=10으로 툴팁이 메뉴 위에 표시됐다.
- 영향: 보드가 가려진 일시정지 상태에서도 제단 설명이 메뉴 위로 떠서 메뉴를 가린다. 코드상 결과 창에도 같은 누락 조건이 있다. 결과 창에서의 별도 재현은 수행하지 않았다.
- 수정 방향: 시작 선택·일시정지·런 결과 등 보드를 덮는 HUD 상태에서는 보드 hover를 중단하고 기존 툴팁도 숨긴다.

## 검증 및 한계

- `PcBoardLayoutValidation`, 실제 1920×1080: PASS. HUD와 보드의 분리, 행동 버튼 레이캐스트, 빌드 창 및 중첩 상세 창의 입력 차단/복원을 확인했다. 로그: `docs/verification/pc-board-layout-native-20261015-015414.txt`.
- `ProgressionUsabilityValidation`: PASS. 강화·이동술 선택, 효과 문구, 상세 클릭, 카드/설명 높이와 제단 후보 규칙을 확인했다. 로그: `docs/verification/reward-usability-20261015-015525.txt`.
- Play 중 1920×1080 → 1960×1960으로 변경해 측면/상하 HUD 배치 전환과 실제 RectTransform 범위를 확인했다. 해당 범위에서 화면 밖 배치나 내부 보드 겹침은 찾지 못했다.
- 1960×1960 자동 HUD 검사에서는 빌드 버튼 레이캐스트가 실패했다. 게임 View는 1960×1960인데 Editor 콜백의 `Screen`은 1948×1688이며 버튼 중심이 이 콜백 범위 밖에 있다. 런타임 레이캐스트 버그로 확정하지 않았다. 로그: `docs/verification/pc-board-layout-native-20261015-015342.txt`.
- 기존 회귀 검사는 위 세 항목의 Animator 진행도와 일시정지 툴팁 표시를 검사하지 않는다. 통과를 근거로 해당 버그를 부정할 수 없다.
- `ChessPlaytestValidation`: PASS, 669개 검사. 일반·대각선 이동, 적 BFS/예약/점유, 이동술 착지, 공격과 혼합 웨이브 진행을 확인했다. 로그: `docs/verification/chess-movement-ui-review-20261015.txt`.
- 실제 브라우저·iPhone 기기 검사는 수행하지 않았다. 검토 후 SampleScene, 원래 Game View 크기, Play 정지 상태로 복원했다. 씬은 dirty 상태가 아니다.

## 후속 수정 — 2026-10-15

세 항목을 수정했다. 게임 규칙·이동 속도·턴 순서는 유지했다.

- 플레이어·Bat·Snake Controller에 Hit/Attack → Move 즉시 전환을 추가했다. `LivelyMotionSetupTool`에도 같은 전환을 추가해 모션 재적용 시 유지된다. Controller는 Unity Editor API로 수정했고 GUID·기존 상태 참조는 보존했다.
- 플레이어 사망 때만 Animator를 `UnscaledTime`으로 전환해 결과 화면에서 묘비까지 재생한다. 사망 시 피해 색상 깜빡임도 종료해 붉은 묘비로 굳지 않게 했다. 플레이테스트 초기화 때 살아 있을 때의 Animator 시간 모드를 복원한다.
- 시작 선택·상세 창·일시정지·결과 창이 표시되는 동안 기존 툴팁을 숨기고 새 hover 표시도 차단한다. 결과 창을 숨긴 전장 복기에서는 유물 설명을 다시 볼 수 있고 전투 입력은 계속 차단된다.
- `Assets/Editor/MotionUiRegressionValidation.cs`를 추가했다. 메뉴: `Tools/Playtest/Validate Motion and UI Regression`.

수정 후 실제 Unity 검사:

- 새 모션/UI 회귀 검사: PASS. 피격 직후 일반 이동과 나이트·비숍·룩 이동술의 실제 걷기 상태, 도착 전 이동 연출, 행동당 적 턴 한 번, 묘비의 최종 Sprite, 색상 복원, 일시정지·결과 창·복기의 툴팁, 재시작 초기화를 확인했다. 로그: `docs/verification/motion-ui-regression-20261015-020817.txt`.
- 1920×1080 PC HUD/빌드 창 검사: PASS. 로그: `docs/verification/pc-board-layout-native-20261015-020920.txt`.
- 스테이지 종료·복기·재시작 검사: PASS, 72개. 로그: `docs/verification/stage-end-20261015-020951.txt`.
- 수정 후 이동·공격·적 점유 종합 Chess 검사: PASS, 669개. 로그: `docs/verification/chess-movement-ui-fixed-20261015.txt`.

씬·스프라이트·렌더 설정은 변경하지 않았다. 테스트 중 Unity가 자동으로 재직렬화한 TimeManager 설정도 원상복구했다. 실제 브라우저·iPhone 검사는 이번 수정에서도 수행하지 않았다. 핸드오프는 기존 Editor의 SampleScene Play를 사용한다.
