# 2026-10-01 누적 변경 리뷰 안내

기준 커밋: `751c070` (`main`, 짝수웨이브 보상). 이 안내는 해당 기준 이후 로컬에 누적된 코드·씬·아트·기획·검증 자료를 기능별로 게시하는 묶음이다. `main`에 직접 병합하지 않는다.

## 브랜치와 검토 순서

브랜치는 **앞 단계에 의존하는 누적 구조**다. 각 행의 기준과 비교하면 해당 파트만 볼 수 있다. 완성된 전체 변경을 받으려면 마지막 `docs/design-review`를 사용한다.

| 순서 | 브랜치 | 기준 | 담당 변경 |
| --- | --- | --- | --- |
| 1 | `feature/progression-movement` | `main` | 성장 판정·위험 제단·레벨 제한·이동술/S 입력·상세 설명 데이터와 입력 보호·기본 회복 |
| 2 | `feature/combat-board` | `feature/progression-movement` | 체크무늬 보드·무작위 룩 사선/재정비·나이트 간격·20웨이브 조합·행동 종료 대기 |
| 3 | `feature/ui-usability` | `feature/combat-board` | HUD 상세 버튼·현재 이동술·시작 조작 안내·아이콘 및 저장 씬·UI 검사 |
| 4 | `feature/art-resources` | `feature/ui-usability` | 아르마딜로 원본/시트·임포트 도구·아이콘 제작 원본/재현 스크립트 |
| 5 | `docs/design-review` | `feature/art-resources` | 현행 규칙·밸런스 검토·반사/분열 정적 실험・기능 분류·원본 검증 자료·게시 규칙 |

증강과 이동술은 같은 `Move`/`RunProgressionSystem` API를 사용한다. 상세창도 같은 입력 잠금에 의존하므로 핵심과 기본 상세창을 첫 파트에 함께 둔다. HUD 연결과 조작 안내는 세 번째 파트다. 분리 브랜치만 독립 적용하거나 임의 순서로 cherry-pick하지 않는다.

GitHub의 [전체 비교](https://github.com/Cloian/Grid-Caster/compare/main...docs/design-review)에서 최종 변경을 확인할 수 있다. 파트 비교에서는 위 표의 기준을 base, 담당 브랜치를 compare로 선택한다.

## 주요 동작 변화

- 성장: 직접 적중/처치와 범위 피해를 구분하고 행동당 중복 효과·재연쇄를 제한한다. 빈도 강화는 소비 중인 공격 묶음을 재추첨하지 않는다. 실제 조건과 카드 설명을 연결한다.
- 보상: 위험 제단은 가능한 상위 등급을 우선하고 안전보다 낮아지지 않는다. 최대 중첩·동일 이동술·최대 연마를 제시/선택하지 못한다. 교체 시 이동술 레벨을 유지한다.
- 이동: S에서 기본 8방향과 보유 이동술을 함께 선택한다. 기존 F/충전 게이지는 현재 플레이에서 사용하지 않는다. 상세 확인은 행동을 소비하지 않고 닫는 클릭의 보드 전달을 막는다.
- 전투: 13×13 내부 보드는 교차 색으로 보인다. 상단 성벽 룩은 플레이어 열을 추적하지 않고 빈 열을 무작위 예고한 뒤 다음 행동에 즉시 발사하고 한 행동 재정비한다.
- 진행: 초반 조합, 나이트 행동 간격, 기본 회복 30%와 정수 추가 회복을 조정했다. 추가 시전/무료 이동이 끝나기 전에 웨이브가 넘어가지 않도록 기다린다.
- UI/아트: 카드에는 핵심 효과·평균 확률·현재/선택 후 레벨을, 상세창에는 정확한 조건을 표시한다. 시작 화면 A/S/마우스 안내, 이동술 아이콘, 아르마딜로 원본/시트와 32 PPU·Point 메타를 포함한다.

## 핵심 파일

- `Assets/Codes/RunProgressionSystem.cs`, `RunProgressionCatalog.cs`, `RunProgressionDescriptions.cs`: 판정·데이터·설명.
- `Assets/Codes/Move.cs`, `RunProgressionUiController.cs`: 행동과 모달 입력 보호.
- `Assets/Codes/ChessMonsterBehaviour.cs`, `MonsterSpawner.cs`, `GridManager.cs`, `WaveTemplate.cs`: 적·보드·조합.
- `Assets/Codes/GameHudController.cs`, `Assets/Editor/GameHudSetupTool.cs`, `Assets/Scenes/SampleScene.unity`: HUD 생성과 저장 참조.
- `Assets/Editor/ChessPlaytestValidation.cs`, `ProgressionUsabilityValidation.cs`, `RunBalanceValidation.cs`: 재현 가능한 검사.

`SampleScene.unity`는 기존 Editor 도구로 저장된 HUD 연결/배치 스냅샷이다. UI 직렬화 ID 재생성과 블록 순서 변경 때문에 텍스트 diff가 크다. 전체 YAML 줄 수를 기능 변경량으로 해석하지 않는다. 기존 외부 에셋 GUID는 유지하며 이 게시 작업에서는 씬 YAML을 재작성하지 않는다.

## 검증과 남은 범위

검증 종류·단계·한계는 [검증 자료 안내](../verification/README.md)를 따른다. 누적 전체 상태의 실제 Unity 검사 자료를 함께 게시한다. **각 중간 커밋을 따로 Play 검증했다는 뜻은 아니다.** 과거 밸런스 시드 결과는 현재 룩/제단 변경 후 인간 난이도의 근거로 사용하지 않는다.

게시 전에는 파일/에셋 메타·기존 GUID 보존·중복 GUID·큰 파일·비밀 정보 패턴을 확인하고 텍스트 diff 공백 검사를 수행한다. Unity가 직렬화한 씬·`.meta` 빈 필드의 공백은 무관한 재포맷을 피하기 위해 제외한다. 복구 씬/캐시/로그는 업로드하지 않고 로컬에 보존한다. 조정 JSON과 아트 초안은 이력 안내와 함께 보존한다.

이 게시 작업 중 Unity `6000.4.0f1` 배치로 시작 안내/보상 UI를 다시 검사했다. 종료 코드 0, `REWARD_USABILITY_PASS`, 1,010개 조건 통과였으며 원본 결과를 [시작 안내 검증 파일](../verification/startup-controls-usability-2026-10-01.txt)에 보존했다. `unity-cli` 스킬의 배치 실행 지침을 적용하되, 비동기 Play 완료를 기다리기 위해 `-quit` 없는 네이티브 Editor 실행을 사용했다. 이 작업에서는 게임 소스·씬·패키지를 새로 수정하지 않았으며 각 중간 브랜치의 별도 Play나 사람의 조작/재미 검증은 수행하지 않았다.

반사/분열 오프라인 실험도 `--samples 600`으로 임시 출력 경로에 재실행했다. 4,800개 배치·3,221개 기하 조건 검사를 통과했고 기존 게시 JSON과 내용이 완전히 일치했다. Markdown 로컬 링크의 대상 파일도 존재하는지 확인했다.

반사·두 갈래·세 갈래 공격은 오프라인 가설 검토이며 런타임 기능을 추가하지 않았다. 기능 분류는 제안으로 게임 요소를 삭제하지 않았다. 고유 중간/최종 보스, 추가 스테이지, 사람의 재미·선호도·현재 승률 검증은 남은 작업이다.

## 직접 확인

Unity `6000.4.0f1`에서 `Assets/Scenes/SampleScene.unity`를 열어 Play한다. 추가 GameObject/태그/레이어 설정 없이 저장된 연결을 사용한다. 시작 안내를 읽고 특성/증강을 선택한 뒤 A/S·마우스, 보유 항목 상세 확인, 이동술 연마, 체크무늬와 비숍 대각선, 12웨이브 이후 룩의 예고·발사·재정비 반격을 확인한다.

앞으로의 커밋은 [CONTRIBUTING.md](../../CONTRIBUTING.md)의 제목·배경·변경·검증·주의사항 형식을 사용한다. `AGENTS.md`에도 같은 지침을 등록했다. AGENTS의 과거 12×12 목표·추적 카메라·7칸 순간이동·10% 회복 문구는 확인된 현재 코드의 13×13 전체 보드·고정 시점·S 이동술·30% 회복과 일치하도록 정리했다. 게임 코드를 바꾼 것이 아니라 문서의 이전 상태 설명을 갱신한 것이다.
