# 검증 자료 읽는 순서

이 폴더는 2026-09-30~10-02 작업의 원본 결과와 조정 이력을 보존한다. 모든 파일이 동일한 코드 버전의 결과는 아니다. **현재 코드의 회귀 통과, 과거 봇의 승률, 정적 모델, 인간의 재미 검증을 구분한다.**

## 먼저 읽을 자료

| 자료 | 의미와 한계 |
| --- | --- |
| [넉백 충돌·충격 확산](../design/knockback-collision-2026-10-05.md) | 기본 벽/적 충돌 피해 1, 기존 긴 충격을 주변 8칸 밀기로 교체, 봉쇄 분리. 실제 입력 158·성장 228·전투 67·피드백 43·연출 52·UI 867조건 통과. 인간 재미·전체 런 밸런스 미측정, 공개 Web 미갱신 |
| [데미지 강화의 적중 범위 공격](../design/damage-boost-splash-2026-10-05.md) | 기본 직격 2·주변 8칸 피해 1, 조준 범위 표시, 전용 전달 성장 교체. 범위 공격 148·성장 228·전투 감사 67·UI 867·체스 670·나이트 198조건 통과. 정상 런 승률·사람 재미 미측정, 공개 Web 미갱신 |
| [나이트 착지 후 반격 기회](../design/knight-counter-opportunity-2026-10-04.md) | 다음 행동의 8방향 인접 첫 직격 +1, 이동·빗나감 소모, 선택·취소 유지, 네 특성 중복 방지. 반격 198조건·기존 전투/UI 670조건·타격 피드백 43조건 통과. 사람의 재미·반복 피로 개선과 구분, 공개 Web 미갱신 |
| [비숍 불길·증강/웨이브 변주](../design/build-variation-and-bishop-fire-2026-10-04.md) | 경로 밖 불길 재현·제거, 시작/일반/상급 가중 추첨, 웨이브 조합·외곽 배치 변주. 추첨 84조건·전투/UI 670조건·타격 피드백 회귀 통과. 인간 승률·재미 검증과 구분 |
| [타격 피드백 여덟 요소 적용](../design/impact-feedback-integration-2026-10-04.md) | 반동·처치 잔상·섬광·넉백 이동·적 턴 간격·발사음 감쇠·비행 상한·카메라 반응. 실제 씬 피드백 43조건·전투/UI 662조건·오디오 38조건 통과. 사람의 체감 평가는 미측정, 공개 Web 미갱신 |
| [첫 스테이지 규칙 감사·자동 런 점검](stage-one-audit-2026-10-02.md) | 불길 적중 칸 정화·클리어 후 입력 재개·시간 불일치 수정, 공통 클리어 결과 패널·새 빌드 재시작, 네 빌드 회귀와 기본 8런·별도 시드 32런. Editor 전용 로컬 기록 도구와 사람 플레이 프로토콜 포함. 인간의 재미·승률·30분 길이 검증이 아님 |
| [PC 전체 보드 화면 기준](pc-screen-baseline-2026-10-02.md) | 데스크톱 Web/모바일 분리, 보드·HUD 공통 배율, 창 크기 변경과 입력 회귀. 공개 배포·사람 판독성 검증과 구분 |
| [모바일 보드 중심 UI](mobile-board-ui-2026-10-02.md) | 하단 한 줄 조작·상단 강화/유물·착지 표시·동일 버튼 확정. 낮은 브라우저 높이와 PC 전투 회귀, 최신 공개 빌드의 결과와 실기기 한계 |
| [인터넷 공개 Web 배포](public-web-2026-10-02.md) | GitHub Pages 공개 주소, 빌드 전용 브랜치, 정적 포장·HTTP·브라우저 검증과 실기기 한계 |
| [Safari 터치 테스트 검증](mobile-safari-2026-10-02.md) | 모바일 16개·PC 전투 45개·Chrome 터치 25개 조건. 잔상 수정과 [로컬 접속 방식](../design/mobile-safari-playtest.md). 실제 아이폰/Safari 검증과 구분 |
| [특수 공격 F키](special-attack-f-2026-10-02.md) | 최신 단축키·안내 변경. F 명령 포함 45개 전투 조건과 867개 UI 조건 통과. OS 키 입력 자체는 수동 확인 대상 |
| [준비한 특성 공격](tactical-core-2026-10-01.md) | 최신 수동 특성 규칙의 전투 41·UI 867·효과 228 조건과 정상 능력치 4런 점검. 인간의 재미는 미검증 |
| [네 빌드 분리 실험](four-build-identities-2026-10-01.md) | 수동 특성 전환 이전 회귀·자동 런 기록. 현재 수동 사용 정책의 승률로 읽지 않음 |
| [시작 조작 안내·보상 UI](startup-controls-usability-2026-10-01.txt) | 성장 재설계 이전 UI의 한 실행에서 1,010개 조건 통과. 독립 게임 1,010회가 아님 |
| [체크무늬·룩 변경 요약](board-rook-update-2026-10-01.md) | 이전 단계의 660개 체스·1,000개 UI·163개 성장 조건과 당시 캡처. 현재 성장 효과의 결과로 읽지 않음 |
| [보상 편의성 변경](reward-usability-update-2026-09-30.md) | 상세창·레벨·최대 중첩·위험 제단 보장의 배경과 이전 단계 결과 |
| [전투·이동술·아트 연결](gameplay-and-art-2026-09-30.md) | S 입력 통합, 아이콘·아르마딜로·씬 연결의 작업 이력 |
| [밸런스 런 요약](balance-2026-09-30.md) | 당시 코드의 독립 시드 96런 및 전설 회피 32런. 인간 승률이 아니며 이후 룩/제단 변경의 현재 승률도 아님 |
| [공격 기회 원본](attack-opportunities-2026-10-01.json) | 4,800개 정적 배치 집계. Unity 턴 전투나 인간 선호도를 실행한 검사가 아님 |

## 보존된 중간 이력

`balance-tuning*.json`, `balance-mechanics-initial.json`, `*-before-labels.json`은 조정·진단·설명 변경 이전의 기록이다. 초기 실패나 서로 다른 봇 정책도 포함하며 최종 승률에 합산하지 않는다. 특히 `balance-tuning07-startup-failed.json`은 0런의 시작 오류 기록이다. 원본을 삭제하거나 성공 결과로 재작성하지 않는다.

JSON의 `sourceHash`와 `policyHash`는 해당 검사기가 지정한 파일 목록/정책의 해시다. 전체 저장소 커밋 해시가 아니며 이후 코드가 변하면 과거 결과를 현재 결과로 재사용할 수 없다.

## 재현

Unity `6000.4.0f1`에서 씬을 저장하고 Play를 종료한 뒤 `Tools > Playtest`의 `Validate Stage One Tactical Audit`, `Validate Stage One Run Smoke`, `Validate Stage End and Restart`, `Validate PC Board Layout`, `Validate Reward Usability`, `Validate Chess Playtest`를 사용한다. 검사는 전투·성장 상태를 구성하는 픽스처이므로 정상 플레이 중 실행하지 않는다. 클릭 검증에는 그래픽 출력이 필요하다. 현재 단계는 기존 Unity 앱에서 실행하며 새 테스트 프로젝트·웹 링크를 만들지 않는다. `Record Local Play Sessions`는 별도 선택 기능이고 기본 꺼짐이다.

시작 안내를 포함한 검증은 네이티브 Unity 배치 실행으로 재현할 수 있다. 이미 해당 프로젝트의 Editor가 열려 있다면 종료/저장을 사용자에게 확인하거나 격리 복사본을 사용한다. 다른 프로젝트의 Editor를 종료하지 않는다.

```sh
"/path/to/Unity" -batchmode -projectPath "/path/to/Grid-Caster" \
  -executeMethod ProgressionUsabilityValidation.RunBatch \
  -startupControlsValidation -logFile /tmp/grid-caster-usability.log
```

비동기 Play 검사이므로 `-quit`을 붙이지 않으며, 실제 uGUI 클릭 검증에 `-nographics`를 쓰지 않는다. 완료 시 검사기가 결과 파일을 쓰고 종료한다. 상세한 밸런스 요청 형식은 [밸런스 기록](balance-2026-09-30.md)을 따른다.

사람이 직접 확인해야 할 항목은 다양한 창 크기의 읽기 편함, 키보드/마우스 조작 감각, 실제 난이도와 유물 선택 이유, 빌드별 재미다.
