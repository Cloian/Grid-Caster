# Unity 복구 반복·AI 동시 연결 점검 — 2026-10-16

## 범위와 현재 결론

원본 `/Users/gimdong-yun/GitHub/Grid-Caster`, Unity `6000.4.0f1`, URP, Input System, 내장 `com.coplaydev.unity-mcp` `10.2.0`을 대상으로 검사했다. 별도 프로젝트·웹 빌드·패키지 업그레이드는 사용하지 않았다.

AI 브리지의 동시 클라이언트 강제 종료, 포트 판정, 시작 실패 반복과 소켓 정리 문제를 재현·수정했다. 게임 코드·아트·씬의 기존 동시 작업 변경을 되돌리지 않았다. 아래 회귀는 실제 Editor Play에서 통과했다. 모든 과거 종료의 원인이 하나였다는 뜻이나, OS·Unity의 모든 크래시를 제거했다는 뜻은 아니다.

## 직접 확인한 증거

1. 작업 시작 시 `Editor-prev.log`에는 `No available ports found in range 6400-6500` 예외가 **1,674회** 남아 있었다. `DiscoverNewPort()`의 일반 예외가 `Start()`의 `SocketException` 처리 밖으로 빠졌고, idle 갱신이 이를 다시 호출했다.
2. 실제 TCP 클라이언트 A를 연결해 ping 성공 후 B를 연결하면, A의 다음 요청은 `EOFError: server closed connection`으로 끝났다. 소스가 새 접속마다 모든 기존 접속을 stale로 간주해 닫았다. Codex·Claude처럼 별도 stdio 서버를 쓰는 클라이언트와 양립하지 않았다.
3. 기존 관리형 `ReuseAddress` 설정에서는 이미 listen 중인 포트도 사용 가능으로 판정했다. 단순 bind 검사뿐 아니라 listen까지 추가한 첫 비교도 실패했다. 독점 설정만 사용하면 이 Mac에서 남아 있는 TIME_WAIT 때문에 기존 포트 범위를 다시 못 여는 문제도 확인했다.
4. 최근 `Editor.log`의 마지막 종료에는 `License returned ... your project will close`가 기록돼 있었다. 이는 네이티브 크래시 스택과 구분되는 라이선스에 의한 종료다. 이번 검사 시작 시와 새 실행 시 라이선스는 활성 상태였다. 계정·라이선스 반환·약관 재수락은 실행하지 않았다.
5. Mac 전체 TCP에 TIME_WAIT 약 **17,800~18,000개**가 있었고 자동 로컬 접속도 `EADDRNOTAVAIL / Can't assign requested address`로 실패했다. 기본 임시 포트 범위는 49152~65535였다. TCP 목적지의 대다수는 외부 HTTPS 443이므로 이를 전부 Unity나 Claude 탓으로 돌릴 근거는 없다. 라이선싱·Hub 로그에도 네트워크 주소 할당 실패가 있었다.

## 수정한 부분

- `Packages/com.coplaydev.unity-mcp/Editor/Services/Transport/Transports/StdioBridgeHost.cs`
  - 새 연결이 기존 연결을 강제 종료하던 동작 제거. 기존 EOF·시간 초과·Stop 정리는 유지한다.
  - 포트 소진을 포함한 시작 실패를 처리하고 1→2→4→8→16→30초로 재시도 간격을 늘린다.
  - delayCall·Play 상태 변경이 기존 대기 시간을 우회하거나 줄이지 못하게 한다.
  - 대체 포트를 선택하기 전 실패한 리스너를 닫는다.
  - `isRunning == false`여도 부분 생성된 리스너·작업이 있으면 Stop이 정리한다.
- `Packages/com.coplaydev.unity-mcp/Editor/Helpers/PortManager.cs`
  - using으로 소켓을 해제하고 bind와 listen을 모두 검사한다.
  - 포트 범위 소진 예외 종류와 실제 마지막 포트 6499 표기를 정리한다.
- `Packages/com.coplaydev.unity-mcp/Editor/Helpers/LoopbackSocketOptions.cs` 및 `.meta`
  - 검사와 실제 리스너에 같은 옵션을 사용한다.
  - macOS에서는 네이티브 `SO_REUSEADDR=1`, `SO_REUSEPORT=0`을 구분해 TIME_WAIT 주소 재사용과 살아 있는 리스너 공유 금지를 함께 적용한다.
  - 상수는 설치된 macOS SDK `sys/socket.h`의 값을 확인했다. 다른 플랫폼에서는 독점 옵션을 사용하며 macOS 호출은 컴파일 조건으로 제외한다.
- `Assets/Editor/EditorBridgeRecoveryValidation.cs` 및 `.meta`
  - 실제 6400~6499와 설정 포트를 점유한 실패·복구 검사. 게임 씬은 변경하지 않는다.

이번 수정 전 내장 패키지 원본 대비 [검토용 패치](unity-mcp-stability-2026-10-16.patch)를 함께 남겼다. 내장 패키지 폴더는 작업 시작부터 Git 미추적 상태였고, 잠금 파일은 이미 embedded 소스를 가리켰다. 패키지를 재설치·교체하면 로컬 수정이 사라질 수 있으므로 향후 게시 시 이 패키지와 패치의 보관 방식을 함께 정해야 한다. 이번에는 커밋·push하지 않았다.

## 실행 결과

| 검사 | 결과 | 자료 |
| --- | --- | --- |
| 포트 소진·부분 리스너 정리·재시도·재연결 | PASS, 1,000회 실패 포트 검사에서 FD 누적 없음 | [브리지 복구](editor-bridge-recovery-2026-10-16.txt) |
| 독립 클라이언트 두 개 동시 유지 | PASS, ping 51회와 실제 Unity 씬 명령 2회 | [동시 연결](unity-mcp-multi-client-2026-10-16.json) |
| 이동·피격 후 걷기·이동술·사망·툴팁·일시정지 | PASS | [모션/UI](motion-ui-regression-20261016-203951.txt) |
| PC 전체 보드·빌드 창·상세창·실제 버튼 레이캐스트 | PASS, 52개 assertion | [PC 보드](pc-board-layout-native-20261016-204125.txt) |
| 사망·클리어·복기·새 런 재시작 | PASS, 72개 assertion | [종료 상태](stage-end-20261016-204154.txt) |
| 퀸 보스·특성/이동술/단계·A→B 이동·벽·승리·사망 | PASS, 341개 assertion. 공격 구성 36개 포함 | [퀸 보스](queen-boss-playtest-20261016-204543.txt) |
| 일반 체스 적·이동·공격·예약·투사체·혼합 웨이브·강화 | PASS, 671개 assertion | [체스 전투](chess-playtest-20261016-204739.txt) |

assertion 수는 독립 플레이 횟수가 아니다. 모션 검사는 프레임별 assertion도 포함하므로 독립 게임 횟수처럼 집계하지 않았다. 퀸 검사기의 과거 고정 이름 원본은 HEAD 내용으로 보존하고 이번 결과는 새 날짜 파일로 분리했다.

퀸 새 스프라이트·Animator·상태 아이콘·체력 표시도 실제 보스 Play에서 확인하고 [화면을 저장했다](captures/queen-current-20261016.png). 이 검사는 인간의 재미·런 승률·약 30분 분량 검증이 아니다.

## 복구 파일 보존과 재실행

- `Assets/_Recovery`의 11개 씬과 `.meta`, SampleScene·QueenBossPlaytest 씬과 `.meta`, UserSettings, 이전 Editor 로그와 수정 전 소스를 저장소 밖 `.codex/backups/Grid-Caster/editor-recovery-20261016-203237/`에 복사했다.
- 복구 씬·캐시·Library를 삭제하거나 손상 파일로 단정해 덮어쓰지 않았다.
- 테스트 후 Play를 종료하고 저장된 SampleScene으로 복귀했으며 `isDirty=false`를 확인했다.
- 20:50 정상 종료 후 Hub에서 원본 프로젝트를 다시 열었다. 새 로그 시작 `2026-10-16T11:50:27Z`에서 SampleScene 로드·브리지 시작을 확인했고, 20:51 이후 실제 명령으로 원본 경로·Unity 버전·SampleScene·`isDirty=false`·`isPlaying=false`·`isCompiling=false`·브리지 실행을 재확인했다.
- 이번 재실행에서 복구 씬 열기·라이선스 강제 종료·컴파일 오류·포트 소진 예외는 없었다. 기존 복구 씬 11개는 그대로 남아 있다. 장기간 안정성을 보장하는 자료는 아니다.

## 남아 있는 외부 조건과 한계

Mac 전체의 자동 임시 포트 할당 실패는 아직 남아 있다. 이번 진단 연결은 42100~42499의 별도 로컬 소스 포트를 사용해 에디터를 검증했다. 이는 정상적인 MCP 클라이언트의 자동 접속이 복구됐다는 증거가 아니다. 방화벽·네트워크 보안·커널 설정은 변경하지 않았고 다른 앱을 종료하지 않았다.

따라서 이 Mac의 일반 네트워크·라이선싱 실패가 계속되면 다른 작업을 저장한 뒤 OS 재시작으로 정리하고, Hub 로그인·라이선스·Codex/Claude 연결을 다시 확인하는 후속 단계가 필요하다. 사용자 승인 없이 Mac을 재시작하지 않았다. 반복된다면 포트 할당 실패의 시스템 원인을 별도로 추적해야 한다.

재시작 전 Console에는 고장 재현 중 실패한 이전 브리지 검사 3건과 Unity 자체 프로파일러 리스닝 포트 오류가 남아 있었다. 이를 임의로 지워 ‘Console 오류 0’이라고 보고하지 않았다. 최종 브리지 검사는 수정 후 PASS이며 게임 회귀 검사 중 새 컴파일·게임 예외는 없었다.

정상 재실행 후 Console을 다시 읽었을 때는 `Failed to start primary listening socket at (0.0.0.0:55504-55512)` 한 건이 남았다. MCP는 6400에서 정상 시작했으며, 이 항목은 위 OS 포트 고갈과 함께 남아 있는 Unity 자체 리스닝 문제다. 시스템 문제까지 해소됐다고 보고하지 않는다.

Git 공백 검사는 이번 C#·문서 변경에서 확인했고, 검토용 패치는 `git apply --reverse --check`로 현재 수정 내용과 일치함을 확인했다. 작업 시작부터 비어 있던 `ProjectSettings.asset`의 cloudProjectId/organizationId 값 뒤 공백은 기존 변경으로 남겼고 무관하게 재포맷하지 않았다.

## 다시 검사하는 방법

1. 원본 프로젝트를 Unity 6000.4.0f1로 열고 SampleScene이 열리는지 확인한다.
2. 씬을 저장하고 Play를 종료한 상태에서 `Tools > Playtest > Validate Editor Bridge Recovery`를 실행한다. 브리지 접속은 검사 중 잠시 끊겼다가 다시 연결된다.
3. 게임 기능은 `Validate Motion and UI Regression`, `Validate PC Board Layout`, `Validate Stage End and Restart`, `Validate Queen Boss Playtest`, `Validate Chess Playtest` 메뉴로 확인한다.
4. 최종 사용자 확인은 SampleScene Play에서 시작 특성·첫 강화 선택 후 A/S/F, 이동술, Esc 일시정지·상세창·재시작을 확인한다. 이 작업에 새 GameObject·Inspector 할당·레이어 설정은 필요 없다.
