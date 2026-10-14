# 2026-10-14 변경 묶음 리뷰 안내

브랜치: `feature/build-identity-pruning` · 기준: `docs/design-review` (`55375be`). `main`에는 병합하지 않았습니다.

[전체 비교 보기](https://github.com/Cloian/Grid-Caster/compare/docs/design-review...feature/build-identity-pruning)

2026-10-02 이후 로컬에 누적된 작업을 **기능별 커밋**으로 나눠 게시했습니다. 파일 단위로 나눴기 때문에 `Move.cs`처럼 여러 기능이 섞인 파일은 처음 등장하는 커밋에 함께 들어 있습니다. 각 커밋을 따로 컴파일·Play 검증하지는 않았으니, 순서대로 함께 검토해 주세요.

## 커밋 순서

| # | 커밋 | 기능 | 핵심 파일 |
| --- | --- | --- | --- |
| 1 | `badc5b0` | 캐릭터 모션 에셋 · 사암 맵 타일 | `Assets/Player/Lively`, `Assets/Monsters/*/Lively`, `Assets/TIles` |
| 2 | `7a9ae16` | 아트 원본 보존 | `Art/Sources/SampleScene`, `Art/Backups/Player` |
| 3 | `e7f40d8` | 전투 효과음 | `GameAudio.cs`, `PauseAudioSettings.cs`, `Assets/Resources/CombatSFX` |
| 4 | `338d0f2` | 타격 피드백 · 넉백 충돌 · 위협 미리보기 | `CombatFeedback*.cs`, `DamageBoostArea.cs`, `Move.cs`, `MonsterSpawner.cs` |
| 5 | `9140311` | 네 시작 특성 빌드 · 보상/HUD | `RunProgression*.cs`, `PlayerTraitSystem.cs`, `GameHudController.cs` |
| 6 | `475a348` | PC 보드 레이아웃 · 모바일 Safari 어댑터 | `PcBoardLayout.cs`, `MobilePlaytestController.cs`, `Tools/mobile-playtest` |
| 7 | `53e6cca` | 퀸 보스 그레이박스 | `QueenBoss*.cs`, `QueenSafeZonePlanner.cs`, `QueenBossPlaytest.unity` |
| 8 | `71fe359` | 정체성·워크플로우·스테이지 1 문서 | `docs/design`, `docs/adr/0003`, 루트 문서 |
| 9 | `0026d49` | 모션 연결 · 발밑 그림자 · 공격 시전 타이밍 · 배경색 | `ActorMotion.cs`, `CharacterShadow.cs`, `LivelyMotionSetupTool.cs` |
| 10 | 이 문서 | 저장소 정리 · README · 문서 지도 | `README.md`, `docs/README.md`, `Art/README.md` |

## 플레이어가 느끼는 변화

- **타격감**: 명중 반동·스파크·처치 잔상·잔여 마력 파편·카메라 충격. 넉백된 적이 벽/적과 부딪히면 충돌 피해.
- **캐릭터 모션**: 마법사·박쥐·뱀이 대기·이동·공격·피격·사망 모션을 재생합니다. 공격 판정은 모션이 내지르는 프레임(마법사 0.37초, 박쥐 0.28초, 뱀 0.30초)에 나갑니다.
- **맵**: 사암 신전 스타일 바닥·벽, 벽 그림자, 캐릭터 발밑 그림자, 자두색 배경.
- **소리**: 마법 발사·명중·피격·나이트 착지·화염 효과음과 음량 설정.
- **빌드**: 네 시작 특성 기준의 보상·편향 추첨·HUD 상세.
- **보스**: 퀸 보스는 별도 그레이박스 씬에서만 플레이할 수 있습니다.

## 검증 범위

- 최종 상태와 "모션 변경을 뺀 누적 상태"를 각각 dotnet으로 런타임·에디터 어셈블리 빌드: 오류 0.
- Unity 에디터에서 `LivelyMotionSetupTool` 실행 PASS. 사용자가 Play에서 모션 교체와 그림자를 확인했습니다.
- 공격 시전 타이밍 변경 후 Play 확인, Unity 자동 검증 메뉴 재실행, 사람의 재미·난이도 검증은 **미수행**입니다.

## 저장소에 넣지 않은 것

- `Art/Audio/CombatSFX-v2`의 오디션 음원·원본 소스·zip·html(약 57MB): 용량 때문에 로컬에만 둡니다. README·크레딧·manifest·제작 스크립트·후보는 포함했습니다.
- `Assets/_Recovery/` 복구 씬, Unity 캐시·로그.
