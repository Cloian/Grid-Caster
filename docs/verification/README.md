# 검증 자료 읽는 순서

이 폴더는 2026-09-30~10-01 작업의 원본 결과와 조정 이력을 보존한다. 모든 파일이 동일한 코드 버전의 결과는 아니다. **현재 코드의 회귀 통과, 과거 봇의 승률, 정적 모델, 인간의 재미 검증을 구분한다.**

## 먼저 읽을 자료

| 자료 | 의미와 한계 |
| --- | --- |
| [시작 조작 안내·보상 UI](startup-controls-usability-2026-10-01.txt) | 현재 통합 UI의 한 실행에서 1,010개 조건 검사 통과. 독립 게임 1,010회가 아님. 렌더된 화면의 모든 크기/비율을 사람이 평가한 결과가 아님 |
| [체크무늬·룩 변경 요약](board-rook-update-2026-10-01.md) | 660개 체스 조건, 1,000개 UI 조건, 163개 성장 조건의 이전 단계 결과와 당시 캡처. 이후 시작 안내만 추가됨 |
| [보상 편의성 변경](reward-usability-update-2026-09-30.md) | 상세창·레벨·최대 중첩·위험 제단 보장의 배경과 이전 단계 결과 |
| [전투·이동술·아트 연결](gameplay-and-art-2026-09-30.md) | S 입력 통합, 아이콘·아르마딜로·씬 연결의 작업 이력 |
| [밸런스 런 요약](balance-2026-09-30.md) | 당시 코드의 독립 시드 96런 및 전설 회피 32런. 인간 승률이 아니며 이후 룩/제단 변경의 현재 승률도 아님 |
| [공격 기회 원본](attack-opportunities-2026-10-01.json) | 4,800개 정적 배치 집계. Unity 턴 전투나 인간 선호도를 실행한 검사가 아님 |

## 보존된 중간 이력

`balance-tuning*.json`, `balance-mechanics-initial.json`, `*-before-labels.json`은 조정·진단·설명 변경 이전의 기록이다. 초기 실패나 서로 다른 봇 정책도 포함하며 최종 승률에 합산하지 않는다. 특히 `balance-tuning07-startup-failed.json`은 0런의 시작 오류 기록이다. 원본을 삭제하거나 성공 결과로 재작성하지 않는다.

JSON의 `sourceHash`와 `policyHash`는 해당 검사기가 지정한 파일 목록/정책의 해시다. 전체 저장소 커밋 해시가 아니며 이후 코드가 변하면 과거 결과를 현재 결과로 재사용할 수 없다.

## 재현

Unity `6000.4.0f1`에서 씬을 저장하고 Play를 종료한 뒤 `Tools > Playtest`의 `Validate Reward Usability`, `Validate Chess Playtest`, `Validate Run Balance`를 사용한다. 검사는 전투·성장 상태를 구성하는 픽스처이므로 정상 플레이 중 실행하지 않는다. 클릭 검증에는 그래픽 출력이 필요하다.

시작 안내를 포함한 검증은 네이티브 Unity 배치 실행으로 재현할 수 있다. 이미 해당 프로젝트의 Editor가 열려 있다면 종료/저장을 사용자에게 확인하거나 격리 복사본을 사용한다. 다른 프로젝트의 Editor를 종료하지 않는다.

```sh
"/path/to/Unity" -batchmode -projectPath "/path/to/Grid-Caster" \
  -executeMethod ProgressionUsabilityValidation.RunBatch \
  -startupControlsValidation -logFile /tmp/grid-caster-usability.log
```

비동기 Play 검사이므로 `-quit`을 붙이지 않으며, 실제 uGUI 클릭 검증에 `-nographics`를 쓰지 않는다. 완료 시 검사기가 결과 파일을 쓰고 종료한다. 상세한 밸런스 요청 형식은 [밸런스 기록](balance-2026-09-30.md)을 따른다.

사람이 직접 확인해야 할 항목은 다양한 창 크기의 읽기 편함, 키보드/마우스 조작 감각, 실제 난이도와 유물 선택 이유, 빌드별 재미다.
