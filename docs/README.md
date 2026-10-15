# 문서 지도

Grid-Caster 기획·결정·검증 문서를 **기능별**로 모았습니다. 날짜가 붙은 문서는 그 시점의 제안·검토이며, 현재 규칙은 루트 문서(`AGENTS.md`, `ChessWaves.md`, `GrowthAndRelics.md`)와 ADR이 기준입니다.

| 폴더 | 담는 것 |
| --- | --- |
| [`design/`](design) | 기획·제안·조사 (날짜별) |
| [`adr/`](adr) | 확정한 설계 결정 |
| [`verification/`](verification) | 실행 결과·캡처·검증 한계 ([안내](verification/README.md)) |
| [`experiments/`](experiments) | 재현 가능한 오프라인 실험 스크립트 |
| [`reviews/`](reviews) | 변경 묶음별 리뷰 안내 |
| [`images/`](images) | README 이미지 |

---

## 방향과 원칙

| 문서 | 요약 |
| --- | --- |
| [게임 정체성](design/game-identity-2026-10-02.md) | 2026-10-02 최종 합의. 타겟 플레이어와 확정 방향 |
| [개발 워크플로우](design/development-workflow-2026-10-02.md) | 단계별 범위·진입 조건·완료 기준 |
| [핵심 경험 재검토](design/core-experience-review-2026-10-01.md) | 첫 클리어 피드백 정리 |
| [반복 플레이 경험 평가](design/player-repeat-experience-assessment-2026-10-04.md) | 다시 하고 싶은 이유 점검 |
| [기능 분류와 덜어내기](design/feature-pruning-2026-10-01.md) | 유지·개선·통합·보류 제안 |
| [첫 스테이지 플레이테스트 절차](design/stage-one-playtest-protocol-2026-10-02.md) | 관찰 항목과 진행 방법 |
| [ADR 0003 독립 스테이지 런](adr/0003-independent-stage-runs.md) | 스테이지마다 새 빌드 |
| [도메인 용어](../CONTEXT.md) | 스테이지·런·빌드 용어 |

## 전투와 보드

| 문서 | 요약 |
| --- | --- |
| [웨이브 구성](../ChessWaves.md) | SampleScene 고정 조합 웨이브 (현행 규칙) |
| [체스 몬스터 플레이테스트](../ChessPlaytest.md) | 체스 말 패턴 짧은 테스트 |
| [나이트 착지 후 반격 기회](design/knight-counter-opportunity-2026-10-04.md) | 반격 창 규칙 |
| [넉백 충돌과 충격 확산](design/knockback-collision-2026-10-05.md) | 충돌 피해 규칙 |
| [공격 기회 확장 실험](design/attack-opportunities-2026-10-01.md) | 반사·분열 가설 (미구현 실험) |
| [2스테이지: 침수된 뇌전 수로](design/stage-two-flooded-conduits-proposal-2026-10-15.md) | 신규 검토안. 타일·전격/서리 빌드·적 5종·이동 보스·16웨이브·보상·제작 순서 |

## 타격 피드백

| 문서 | 요약 |
| --- | --- |
| [타격감 가설과 검증](design/impact-feel-hypotheses-2026-10-04.md) | 무엇이 손맛을 만드는가 |
| [타격 요소 단독 비교와 순위](design/impact-feel-ranking-2026-10-04.md) | 요소별 비교 |
| [타격 피드백 통합](design/impact-feedback-integration-2026-10-04.md) | 적용 결과 |
| [잔여 마력 파편과 공격 성과](design/impact-presentation-2026-10-05.md) | 처치 성과 표현 |
| [데미지 강화의 적중 범위 공격](design/damage-boost-splash-2026-10-05.md) | 범위 피해 |
| [범위 공격의 범위 성장](design/damage-boost-range-growth-2026-10-05.md) | 성장에 따른 범위 |

## 성장과 빌드

| 문서 | 요약 |
| --- | --- |
| [성장과 유물 설계](../GrowthAndRelics.md) | 현행 규칙 |
| [ADR 0001 제한적 강화 + 보드 유물](adr/0001-hybrid-run-progression.md) | 결정 |
| [빌드·강화·유물 밸런스](design/build-balance-2026-09-30.md) | 초기 밸런스 검토 |
| [네 빌드 분리 실험](design/four-build-identities-2026-10-01.md) | 시작 특성 4종 비교 |
| [비숍 불길과 빌드 편향 추첨](design/build-variation-and-bishop-fire-2026-10-04.md) | 런마다 다른 빌드 유도 |

## 퀸 보스

| 문서 | 요약 |
| --- | --- |
| [여왕 보스 패턴 제안](design/queen-boss-pattern-proposal-2026-10-11.md) | 패턴 후보 |
| [체크메이트 그레이박스 비교](design/queen-graybox-test-2026-10-12.md) | 단색 그레이박스 비교 |
| [독립 여왕 보스 플레이테스트](design/queen-boss-unity-playtest-2026-10-12.md) | Unity 씬 테스트 |
| [체크메이트 밸런스 제안](design/queen-checkmate-balance-proposal-2026-10-12.md) | 후반 이동술 기준 밸런스 |
| [콘셉트 시안](../Art/Concepts) | 퀸·폰 근위대 시안 v1–v10 |

## 아트와 오디오

| 문서 | 요약 |
| --- | --- |
| [아트 스타일 바이블](../ArtStyleBible.md) | 해상도·팔레트·명암·모션·Gold Standard·검수 기준 |
| [이미지 콘셉트 기준](../IMAGE_CONCEPT_BRIEF.md) | 시안 제작용 게임 콘셉트 원본 |
| [아트 폴더 안내](../Art/README.md) | 원본·시안·백업 위치와 적용 경로 |
| [전투 효과음 조사](design/combat-sfx-inventory-2026-10-03.md) | 필요한 소리 목록 |
| [효과음 제작 방식](design/combat-sfx-production-method-2026-10-03.md) | 제작 방법과 v2 |
| [효과음 청취 선택](design/combat-sfx-selection-2026-10-03.md) | 선정 과정 |
| [선택 효과음 적용](design/combat-audio-integration-2026-10-04.md) | 게임 연결 |

## 플랫폼 (PC · 모바일 웹)

| 문서 | 요약 |
| --- | --- |
| [ADR 0002 PC 우선](adr/0002-pc-first-board-experience.md) | 주력 플랫폼 결정 |
| [PC 화면과 빌드 확인 창](design/pc-build-inventory-2026-10-14.md) | 보드 중심 PC 화면 |
| [모바일 Safari 플레이테스트](design/mobile-safari-playtest.md) | 보조 실험 절차 |

## 변경 묶음 리뷰

| 문서 | 범위 |
| --- | --- |
| [2026-10-01 변경 묶음](reviews/2026-10-01-change-series.md) | 브랜치 1–5 (성장·보드·UI·아트·문서) |
| [2026-10-14 변경 묶음](reviews/2026-10-14-change-series.md) | 브랜치 6 (피드백·오디오·보스·플랫폼·모션·맵) |
