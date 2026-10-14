# Grid Caster

Unity 2D로 제작 중인 타일 기반 턴제 웨이브 생존 게임입니다.

## 확정한 개발 방향

PC의 전체 보드 판단과 빌드 실현을 중심으로 개발합니다. 스테이지별 독립 런, 보스 포함 약 30분, 다음 스테이지에서 새 빌드를 구성하는 방향을 확정했습니다. 이는 목표이며 고유 보스·후속 스테이지·클리어 기록 저장이 이미 구현됐다는 뜻은 아닙니다. 기존 모바일 웹은 보조 실험으로 유지합니다. 현재 소스는 데스크톱 Web에 PC HUD/A·S·F 입력을, 모바일 브라우저에 터치 UI를 적용합니다. PC 보드와 좌우·상하 HUD는 공통 배율로 배치합니다. 공개 링크는 이번 소스로 재배포하지 않았습니다. [PC 화면 변경·검증](docs/verification/pc-screen-baseline-2026-10-02.md)을 참고하세요.

- [최종 게임 정체성](docs/design/game-identity-2026-10-02.md)
- [개발 워크플로우와 완료 조건](docs/design/development-workflow-2026-10-02.md)
- [첫 스테이지 규칙 감사·종료 흐름·플레이 기록](docs/verification/stage-one-audit-2026-10-02.md)

## 현재 프로토타입

13×13 내부 보드 전체를 보며 8방향으로 이동·공격합니다. 플레이어가 행동한 뒤 적이 한 번 행동하며, 첫 스테이지의 20웨이브(해금·총 마릿수 유지, 조합·배치 변주) 동안 시작 특성·강화·유물로 빌드를 구성합니다. 10웨이브는 중간 혼합전, 20웨이브는 최종 혼합전입니다. 클리어 후 결과 패널에서 전장을 복기하거나 새 빌드로 다시 시작합니다. 독립 보스 AI·클리어 기록 저장·다음 스테이지 전환은 아직 구현하지 않았습니다.

시작 특성과 전용 강화를 고른 뒤 `A`로 일반 공격 방향, `S`로 기본 이동/습득한 이동술, `F`로 준비한 특성 공격을 선택합니다. 공격 화살표에 마우스를 올리면 경로를 확인하고, 좌클릭으로 실행하며 우클릭으로 취소합니다. 보유 강화·유물·이동술을 클릭하면 상세 설명을 확인할 수 있습니다.

## 변경 검토와 문서

- [2026-10-01 전체 변경 / 브랜치 검토 순서](docs/reviews/2026-10-01-change-series.md)
- [커밋·브랜치·게시 규칙](CONTRIBUTING.md)
- [현재 웨이브 구성](ChessWaves.md), [성장과 유물 규칙](GrowthAndRelics.md)
- [검증 자료 안내와 한계](docs/verification/README.md)
- [공격 경로 확장 실험안](docs/design/attack-opportunities-2026-10-01.md), [기능 유지·개선·통합·보류 검토안](docs/design/feature-pruning-2026-10-01.md)

반사·분열 공격과 기능 덜어내기 문서는 제안/실험입니다. 현재 게임에 구현하거나 삭제한 기능 목록으로 해석하지 않습니다.

## 개발 환경

- Unity `6000.4.0f1`
- Universal Render Pipeline 2D
- Unity Input System
- 기준 씬: `Assets/Scenes/SampleScene.unity`

## 프로젝트 열기

1. Unity Hub에서 **Add project from disk**를 선택합니다.
2. 이 `Grid-Caster` 폴더를 선택합니다.
3. Unity `6000.4.0f1`로 프로젝트를 엽니다.
4. `Assets/Scenes/SampleScene.unity`를 열고 실행합니다.

`Library`, `Temp`, `Logs`, `UserSettings`와 IDE 프로젝트 파일은 Unity가 다시 생성하므로 Git에 포함하지 않습니다. `Assets/_Recovery/` 복구 씬도 개발자 로컬에 보존하고 게시하지 않습니다.
