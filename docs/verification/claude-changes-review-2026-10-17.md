# Claude 작업분 검증과 정리 경로 수정

검증: 2026-10-16 밤~10-17, Unity 6000.4.0f1, 원본 Grid-Caster 프로젝트의 Editor Play.

현재 변경분의 범위 공격 문양·파편 색상·비숍 이동 시간, 여왕/근위병 외형·모션·상태 HUD, PC 행동 HUD를 검토했다. 기존 작업을 되돌리거나 새 패키지를 설치하지 않았다. 커밋·push·공개 Web 갱신은 하지 않았다.

## 확인하고 수정한 버그

### 1. 공격 취소·씬 종료가 공격 완료로 처리됨

기존 로그에서 `TurnProjectile.OnDisable → 플레이어 공격 완료 → MonsterSpawner.BeginMonsterTurn → 비숍 불길 생성` 경로를 확인했다. 씬 부모가 비활성화되는 도중 새 FireTrail을 붙이면서 SetParent 오류가 발생했고, 종료 후 GameAudio·FireTrail 오브젝트가 남았다는 경고가 기록돼 있었다.

수정 전 실제 Play에서도 비행 중 투사체를 생성하고 `ProjectileManager.ClearProjectiles()`를 호출하면 공격 완료 콜백이 호출됐다(`cancelCalledAttackCompletion=true`).

- `TurnProjectile.Cancel`은 완료 콜백을 먼저 비운다. 정리 중 관통 후속탄·추가 공격·적 턴을 시작하지 않는다.
- `TurnProjectile.OnDisable`은 이동·대기 중 적중·완료 콜백을 취소한다. 정상 도착·적중은 기존 `FinishTurn`에서 완료를 알린다.
- `MonsterMovement.OnDisable`도 이동·착지·턴 완료 콜백과 지연 공격 코루틴을 취소한다. 정상 착지와 사망의 명시적 완료 경로는 유지한다.

수정 후 일반 취소, 부모 비활성화, 몬스터 이동 중 비활성화 검사를 통과했다. 마지막에는 실제 SampleScene에서 일반 공격을 입력하고, 투사체가 비행하는 프레임에 Editor Play를 종료했다. 투사체 발견 플래그를 확인했으며 종료 후 오류·경고가 없었다.

### 2. 재시작 시 새 범위 공격 문양이 남음

`DamageBoostSigilEffect`는 독립 루트 오브젝트와 지연 코루틴을 사용하지만 기존 `CombatFeedback.ClearAll`의 정리 대상에 없었다. 수정 전 재현에서 문양 9개를 생성한 뒤 초기화해도 9개가 남았다. 일시정지 중에는 스케일 시간이 멈추므로 자연 소멸도 진행되지 않는다.

- 문양 인스턴스를 집합으로 관리하고 `ClearAll`에서 즉시 비활성화·제거한다.
- 공통 `CombatFeedback.ClearAll`에 문양 정리를 연결했다. CombatFeedback 인스턴스가 없어도 실행한다.
- 단순 증감 카운터 대신 실제 등록 집합의 크기를 사용해 중복 정리·미등록 컴포넌트 파괴로 음수가 되지 않게 했다.

일시정지 중 재시작 직후 활성 문양 0개, 일시정지 해제 후 지연 문양 재등장 없음이 검증됐다. 피해량·충전·행동 횟수는 변경하지 않았다.

## 검사 결과

| 검사 | 최종 결과 | 결과 파일 |
| --- | --- | --- |
| 범위 공격 연출·파편·문양·취소·비숍 짧은 이동 | PASS, 66개 조건 | [연출/수명](impact-presentation-20261017.txt) |
| 범위 피해·방향·성장·예고·충전 | PASS, 202개 조건 | [범위 공격](damage-boost-range-20261017.txt) |
| 이동술·피격 후 걷기·툴팁·일시정지·사망 모션·재시작 | PASS, 56개 조건 | [이동/UI](motion-ui-regression-20261017-000449.txt) |
| PC 전체 보드·HUD·빌드/상세 창·실제 버튼 레이캐스트 | PASS, 52개 조건 | [PC 배치](pc-board-layout-native-20261017-000239.txt) |
| 사망·20웨이브 클리어·복기·새 런 | PASS, 72개 조건 | [종료/재시작](stage-end-20261017-000255.txt) |
| 여왕·근위병·특성/이동술/단계·승리·사망 | PASS, 341개 조건 | [보스](queen-boss-playtest-20261017.txt) |
| 일반 체스 적·예약·이동·투사체·웨이브·강화 | PASS, 671개 조건 | [체스 전투](chess-playtest-20261017.txt) |

조건 수는 독립 플레이 횟수가 아니다. 모션 검사의 프레임별 검사 수는 실행 환경에 따라 달라진다. 새 비숍 검사는 한 칸 대각선 이동의 목적지가 연출 전에 예약되고, 실제 이동 연출이 최소 0.25초(프레임 허용 범위 포함) 유지되는지 확인한다.

사망 모션 검사는 첫 실행에서 고정 시간 대기 후 마지막 프레임 조건이 실패했지만 동일 게임 코드 재실행에서 통과했다. 검사기는 경과 시간만 기다리는 대신 실제 Death 상태·최종 묘비 Sprite·normalizedTime 완료를 기다리고, 클립 길이+2초의 제한 시간을 적용하도록 보강했다. 게임 사망 코드를 이 결과만으로 변경하지 않았다. 보강 후 위 최종 결과가 통과했다. 초기 실패 기록도 같은 폴더에 보존했다.

새 문양 두 시트는 각각 Sprite 6개, 32 PPU, Point 필터, 밉맵 비활성화임을 실제 TextureImporter로 확인했다. 문양이 나타나기 전·파편 도착 시점도 Play에서 확인했다([직격](captures/residual-magic-core-20261017.png), [파편 도착](captures/residual-magic-arrival-20261017.png)).

## 최종 Editor 상태와 한계

- 실제 공격 비행 중 Play 종료까지 확인한 뒤 `Assets/Scenes/SampleScene.unity`, `isDirty=false`, `isPlaying=false`, `isCompiling=false`.
- 최종 검사 구간 및 마지막 실제 종료 후 Console 오류·경고 0개.
- Mac 재부팅 뒤 일반 Unity MCP 연결로 모든 검사를 실행했다. 앞선 보고서의 임시 포트 고갈로 인한 수동 소스 포트 우회는 이번 실행에 필요하지 않았다.
- 수정 전 소스와 최초 연출 결과는 저장소 밖 `/Users/gimdong-yun/.codex/backups/Grid-Caster/claude-review-20261016-235547/`에 보존했다. 고정된 과거 이름을 덮어쓰는 기존 검사 결과는 새 날짜 파일로 분리하고 과거 파일을 복원했다.
- 자동 검사는 실제 입력과 제어된 전투 상태를 사용한다. 사람의 재미·전체 런 승률·장시간 Editor 안정성을 보장하는 결과는 아니다.

다시 실행할 때는 저장된 SampleScene에서 Play를 종료한 뒤 `Tools > Playtest`의 위 검사 메뉴를 순서대로 실행한다. 여러 Play 검사를 동시에 실행하지 않는다.
