# 전투·이동술·아르마딜로 적용 완료 기록

기준: Unity 6000.4.0f1, URP 2D, Unity Input System, `Assets/Scenes/SampleScene.unity`.

## 적용한 동작

- 수정된 64×64 아르마딜로 원본에서 256×64 시트(4프레임)를 다시 추출했다. 비숍은 독립 `ArmadilloIdle.anim`과 `ArmadilloBishop.controller`를 사용해 5 FPS로 순환한다. 런타임 스포너가 Sprite와 Animator를 연결하며 새 몬스터 Prefab은 만들지 않았다.
- S 이동 선택에 기본 8방향과 보유 이동술의 착지 지점을 함께 표시한다. 모든 이동 후보는 노란 타일 테두리다. F 입력과 충전 게이지는 게임 플레이에서 사용하지 않는다.
- 처치 폭발·관통 파동과 착지·추가 시전·강화 적중·넉백의 행동 봉쇄를 연결했다. 폭발 처치는 다른 폭발을 재귀적으로 만들지 않고, 봉쇄는 다음 적 행동 한 번만 소모한다.
- 1~4웨이브 적 수를 3/3/4/5로 조정했다. 새 적 유형과 적 수가 동시에 크게 증가하는 초반 압박을 낮춘다.
- 시작 특성 직후 전용 강화 하나를 선택한다. 2웨이브에는 첫 이동술인 나이트 도약을 보장한다. 2~18의 모든 짝수 웨이브에 강화 선택을 제공하며, 4·8·12·16에서 유물을 획득하면 상급 강화로 바뀐다.
- 이동술 HUD에 체스 말 아이콘·이름·레벨을 표시한다. 강화 카드 설명은 선택 후 중첩 수치에 맞춘다. 이동술 습득·범위 공격·봉쇄 강화에 공통 계열의 픽셀 아이콘을 연결했다.
- Aseprite 아이콘은 기존 사용자의 아이콘 테두리를 그대로 보존한 20×20 원본이다. PNG와 Aseprite 원본에 32 PPU·Point를 적용하고 정적 원본의 Model Prefab/AnimationClip 자동 생성을 끈다.

## 파일

수정한 런타임 스크립트:

- `Assets/Codes/DirectionalActionIndicator.cs`
- `Assets/Codes/GameHudController.cs`
- `Assets/Codes/Move.cs`
- `Assets/Codes/RunProgressionCatalog.cs`
- `Assets/Codes/RunProgressionSystem.cs`
- `Assets/Codes/RunProgressionUiController.cs`
- `Assets/Codes/WaveTemplate.cs`

수정한 Editor 도구와 에셋:

- `Assets/Editor/ArmadilloBishopSetupTool.cs`
- `Assets/Editor/ChessPlaytestValidation.cs`
- `Assets/Editor/GameHudSetupTool.cs`
- `Assets/Monsters/Armadillo/armadillo.aseprite`와 대응 메타
- `Assets/Monsters/Armadillo/armadillo_sheet.png`와 대응 메타
- `Assets/Scenes/SampleScene.unity`: HUD Sprite 참조와 UI 배치를 Editor 도구로 적용

신규 에셋과 문서:

- `Assets/UI/Icons/Movement/`: KnightMove, BishopMove, RookMove의 `.aseprite`·`.png`·대응 `.meta`
- `Assets/Resources/UI/ProgressionIcons/`: 세 이동술과 ChainBurst, BindingLanding, EchoPressure의 `.png`·대응 `.meta`
- `Art/Icons/draw_movement_pieces.lua`: Aseprite 제작 재현 스크립트
- 이 검증 기록. `GrowthAndRelics.md`와 `ChessWaves.md`의 현재 효과·입력·적 수 안내도 갱신했다.

기존 `Art/Icons/HandPixel_v1/` 초안은 보존한다. Unity가 생성한 `Assets/_Recovery/`의 복구 씬도 삭제하지 않는다. `RunValidationOnce.cs`는 이번 검증을 위한 임시 도구로 최종 프로젝트에서는 제거한다.

## 실제 실행한 검증

- 실제 Editor Play 모드의 `CHESS_PLAYTEST_VALIDATION_PASS`.
- 독립 패턴 테스트와 SampleScene의 이동·공격·턴·점유·투사체·불길·시작 강화·웨이브 보상·HUD 배치 검사.
- 뱀이 같은 최단 경로에서 앞 적이 죽어 비운 칸으로 전진하는지 확인.
- S의 기본 이동/이동술 통합, 노란 테두리, 무충전 사용, 나이트/비숍/룩 아이콘 전환 확인.
- 1~13웨이브 연결과 보상 분기, 1~20웨이브 구성표 확인.
- 별도 실제 Play 시나리오의 `GRID_CASTER_FINAL_SCENARIO_PASS` 및 종료 코드 0: 처치 폭발 범위·재연쇄 방지, 관통 파동 2중첩 피해, 반향/잔광/넉백/착지 봉쇄, 봉쇄 한 턴 소모, 아르마딜로 런타임 4프레임 순환과 타일 안 표시.
- `unity projects verify --expect-editor 6000.4.0f1`: 메타 누락·중복 GUID·충돌 마커·패키지 JSON 검사 오류/경고 0.

웨이브 연결 검사는 적을 강제로 처치해 진행한다. 실제 난이도와 승률을 측정한 테스트가 아니며, 사람의 조작으로 20웨이브를 클리어한 결과를 의미하지 않는다.

## Inspector와 직접 테스트

SampleScene의 연결은 이미 적용했다. 추가 GameObject 생성, 컴포넌트 연결, 태그·레이어·Tilemap·Collider 설정은 필요 없다. 스포너의 기존 비숍 Sprite/Controller 참조와 HUD의 세 이동술 Sprite 참조를 사용한다.

1. SampleScene을 열고 Play를 누른다.
2. 시작 특성과 전용 강화 하나를 선택한다.
3. A를 누르고 방향 타일을 클릭하면 공격과 적 턴이 한 번 진행된다.
4. S를 누르고 노란 테두리 타일을 클릭하면 이동한다. 우클릭 또는 S 재입력으로 선택을 취소한다.
5. 2웨이브 보상에서 나이트 도약을 선택하면 S에 L자 착지 지점이 추가되고 HUD 아이콘이 나이트 말로 바뀐다.
6. 범위 공격·봉쇄 강화를 선택해 적 무리의 피해와 다음 행동 정지를 확인한다. 비숍 해금 이후 아르마딜로 애니메이션과 불길을 확인한다.

아직 다루지 않은 범위: 모든 강화·유물의 최종 아이콘, 사람의 플레이 결과를 기준으로 한 중후반 최종 밸런스, 고유 보스·직업·동료 등 추후 콘텐츠.
