# 특수 공격 F키 변경

사용자 요청에 따라 준비된 특성 공격의 단축키를 Shift+A에서 F로 교체했다. A 일반 공격, S 이동·이동술, 아이콘 클릭, 마우스 방향 선택·우클릭 취소는 유지했다. F를 눌러 조준을 여는 것만으로는 턴이나 충전을 소모하지 않는다. 방향을 확정하면 기존 규칙대로 소모한다. 과거 F 순간이동은 복원하지 않았다.

수정 파일: `Move.cs`, `GameHudController.cs`, `RunProgressionUiController.cs`, 기존 검증기 세 개, `AGENTS.md`, `GrowthAndRelics.md`, 핵심 경험 확정 문서와 검증 색인. 신규 런타임 스크립트·씬·에셋·패키지는 없다. Unity UI 스킬에 따라 기존 uGUI만 수정했으며 픽셀아트 렌더 설정은 유지했다.

- [핵심 검사](special-attack-f-2026-10-02.txt): Unity 6000.4.0f1 SampleScene Play에서 45개 조건 통과. 미충전 F 거부, 준비된 F 선택, 취소 보존, A 복귀, 확정 소모, 무료 이동 중 F 금지를 포함한다.
- [UI 회귀](special-attack-f-ui-2026-10-02.txt): 867개 조건 통과. 시작 안내의 A/F/S·마우스 표기, 네 줄 배치 및 기존 보상 UI를 검사했다.

자동 F 검사는 `ProcessInputFrame`의 F 명령 경로를 실행한 것으로 OS 키 입력 자체를 누른 검사가 아니다. 실제 키 바인딩은 `Move.Update`에서 `keyboard.fKey.wasPressedThisFrame`을 그 명령에 연결했는지 코드로 확인했다. 초기 가상 키보드 시도는 배치 Editor에서 pressed-frame 상태를 구성하지 못해 검증기 실패로 중단했다. 그 결과를 게임 입력 버그나 통과로 취급하지 않고 명령 검사로 대체했다. 기존 Unity Search 인덱서 예외는 배치 시작 진단으로 남았지만 이번 컴파일·게임 검사 실패는 없었다.

추가 Inspector 설정, GameObject, 컴포넌트, 태그·레이어·Collider 설정은 필요 없다. 원래 Unity 창에서 Assets > Refresh 후 Play하고 일반 공격으로 충전을 채운 뒤 F를 누르면 특수 공격의 방향 선택이 나와야 한다. HUD는 `F 충전 n/N` 또는 `F 준비 1/1`로 표시된다. 우클릭하면 준비를 유지한다. Shift+A는 더 이상 특수 공격 단축키가 아니며 A 일반 공격으로 처리된다.

충전량·피해·웨이브·턴·이동술 규칙은 바꾸지 않았다. 실제 OS 키 입력과 다양한 크기의 글자 가독성은 위 수동 절차로 확인할 항목이다.
