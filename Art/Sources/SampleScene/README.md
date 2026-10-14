# SampleScene 스프라이트 원본 목록

정리일: 2026-10-14. 현재 열린 SampleScene의 저장 파일 참조와 런타임 로드 코드를 기준으로 정리했다. 모든 웨이브·사망·성장 선택에서 사용될 수 있는 에셋을 포함하며, 지금 화면에 보이는 에셋만을 의미하지 않는다.

이 폴더는 **편집용 원본 보관 사본**이다. Unity는 기존 Assets 경로의 파일을 계속 사용한다. 사본을 수정한 뒤 게임에 반영하려면 아래 게임 사용 파일로 내보내야 한다. 기존 파일 이동·삭제, GUID·씬·애니메이션 연결 변경은 하지 않았다.

주인공 최신 원본은 Desktop/aseprite-practice/new_wizard.aseprite이며 이전 backup 버전과 구분했다.

## 원본 및 사용처

| 분류 | 용도 | 정리한 원본 | Unity 사용 파일 | 확인 내용 |
|---|---|---|---|---|
| Player | 주인공 | [new_wizard.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Player/new_wizard.aseprite>) | [new_wizard-Sheet.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Player/new_wizard-Sheet.png>) | 첫 8프레임을 각 50×50에서 (5,5,40,40)로 추출한 320×40 시트. idle 2프레임·walk 6프레임. |
| Player | 사망 묘비 | [grave.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Player/grave.aseprite>) | [grave.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Player/Death/grave.aseprite>) | PlayerDeathMarker 참조. |
| Monsters | 박쥐 / 나이트·룩 공용 외형 | [bat_frame.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Monsters/bat_frame.aseprite>) | [bat_frame.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Monsters/Bat/bat_frame.aseprite>) | 박쥐와 chessMonsterSprite가 공유. 룩 전용 Sprite는 미할당이므로 같은 Sprite를 사용하며 기믹·색상 표현을 더한다. |
| Monsters | 뱀 | [snake_asprite.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Monsters/snake_asprite.aseprite>) | [snake_asprite.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Monsters/Snake/snake_asprite.aseprite>) | 현재 MonsterSpawner의 기존 Snake Prefab을 통해 사용. |
| Monsters | 비숍 / 아르마딜로 | [armadillo.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Monsters/armadillo.aseprite>) | [armadillo_sheet.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Monsters/Armadillo/armadillo_sheet.png>) | bishopMonsterSprite와 독립 ArmadilloIdle.anim / Animator Controller로 사용. |
| Projectiles | 플레이어 공격 투사체 | [attack.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Projectiles/attack.aseprite>) | [attack.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Projectiles/Attack/attack.aseprite>) | PlayerProjectileAttack.anim에서도 참조. |
| Tiles | 타일셋 | [tileset2.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Tiles/tileset2.aseprite>) | [tileset2.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/TIles/tileset2.aseprite>) | 씬과 Tile 에셋이 원본 Aseprite의 sub-asset을 직접 참조. |
| Tiles | 벽 시트 22 | [wall-Sheet22.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Tiles/wall-Sheet22.aseprite>) | [wall-Sheet22.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/TIles/wall-Sheet22.png>) | 첫 프레임 PNG 내보내기와 게임 PNG의 픽셀 전체 일치 확인. |
| Tiles | 벽 시트 | [wall_sheet.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Tiles/wall_sheet.aseprite>) | [wall_sheet.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/TIles/wall_sheet.png>) | 첫 프레임 PNG 내보내기와 게임 PNG의 픽셀 전체 일치 확인. |
| Tiles | 벽 시트 123 | [wall_sheet123.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Tiles/wall_sheet123.aseprite>) | [wall_sheet123.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/TIles/wall_sheet123.png>) | 첫 프레임 PNG 내보내기와 게임 PNG의 픽셀 전체 일치 확인. |
| UI | 행동·시작 특성 공통 시트 | [yosulbong.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/UI/yosulbong.aseprite>) | [BasicAttack.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Actions/BasicAttack.png>)<br>[Move.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Actions/Move.png>)<br>[Teleport.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Traits/Teleport.png>)<br>[DoubleCast.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Traits/DoubleCast.png>)<br>[Pierce.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Traits/Pierce.png>)<br>[DamageBoost.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Traits/DamageBoost.png>)<br>[Knockback.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Traits/Knockback.png>) | 140×20 시트를 20×20으로 자른 7아이콘과 정확히 일치. 순서: 기본공격 / 구 Teleport / 더블캐스트 / 관통 / 범위공격 / 넉백 / 이동. Teleport는 기존 HUD 대체 참조이며 F 순간이동 기능을 뜻하지 않는다. |
| UI | 체력 하트 | [heart.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/UI/heart.aseprite>) | [heart.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Health/heart.aseprite>) | HUD 체력 표시. |
| UI/Movement | 나이트 이동술 아이콘 | [KnightMove.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/UI/Movement/KnightMove.aseprite>) | [KnightMove.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Movement/KnightMove.png>)<br>[KnightMove.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Resources/UI/ProgressionIcons/KnightMove.png>) | HUD와 성장 UI가 같은 PNG 내용 사용. |
| UI/Movement | 비숍 이동술 아이콘 | [BishopMove.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/UI/Movement/BishopMove.aseprite>) | [BishopMove.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Movement/BishopMove.png>)<br>[BishopMove.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Resources/UI/ProgressionIcons/BishopMove.png>) | HUD와 성장 UI가 같은 PNG 내용 사용. |
| UI/Movement | 룩 이동술 아이콘 | [RookMove.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/UI/Movement/RookMove.aseprite>) | [RookMove.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/UI/Icons/Movement/RookMove.png>)<br>[RookMove.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Resources/UI/ProgressionIcons/RookMove.png>) | HUD와 성장 UI가 같은 PNG 내용 사용. |
| UI/Progression | 기어 강화 아이콘 | [ChainBurst.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/UI/Progression/ChainBurst.aseprite>) | [ChainBurst.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Resources/UI/ProgressionIcons/ChainBurst.png>) | alternating_gear에서 런타임 로드. Art/Icons/HandPixel_v1/ChainBurst.png와 파일 내용 동일. |
| UI/Progression | 봉쇄 강화 아이콘 | [BindingLanding.aseprite](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/UI/Progression/BindingLanding.aseprite>) | [BindingLanding.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/Resources/UI/ProgressionIcons/BindingLanding.png>) | movement_breath / impact_recovery에서 런타임 로드. 제작 PNG와 파일 내용 동일. |
| Tiles/PNGOnly | 바닥 타일 PNG 원본 | [Sprite-tile2-Sheet.png](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Tiles/PNGOnly/Sprite-tile2-Sheet.png>) | [Sprite-tile2-Sheet.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/TIles/Sprite-tile2-Sheet.png>) | 대응 편집용 Aseprite를 확정하지 못함. tile_1234 및 tile_1234-Sheet 내보내기와 픽셀이 달라 동일 원본으로 취급하지 않음. |
| Tiles/PNGOnly | 벽 2 PNG 원본 | [wall2_sheet.png](</Users/gimdong-yun/GitHub/Grid-Caster/Art/Sources/SampleScene/Tiles/PNGOnly/wall2_sheet.png>) | [wall2_sheet.png](</Users/gimdong-yun/GitHub/Grid-Caster/Assets/TIles/wall2_sheet.png>) | 대응 편집용 Aseprite를 확정하지 못함. 일부 연결 Tile 에셋은 Assets/trash 아래에 남아 있음. |

## 별도 이미지 원본이 없는 표현

- 이동·공격 화살표와 타일 테두리: Assets/Codes/DirectionalActionIndicator.cs에서 Texture2D / Sprite를 생성.
- 적 위험 예고: Assets/Codes/EnemyThreatPreview.cs에서 픽셀 테두리를 생성.
- 타격 불꽃: Assets/Codes/CombatFeedback.cs에서 1×1 픽셀 Sprite를 생성. 사망 잔상은 원본 캐릭터 Sprite 복사.
- 비숍 불길 / 일부 체스 예고: BishopFireTrail.cs와 ChessMonsterBehaviour.cs의 GUI 텍스트·단색 표시.
- UI 패널·유물/강화의 미제작 아이콘: 단색 패널과 글자 대체 표시가 사용되므로 각각의 Aseprite 원본은 없음.

## 확인되지 않은 원본과 사용하지 않는 시안

- PNGOnly의 2파일은 게임 PNG를 그대로 보관했다. 별도 Aseprite 원본의 동일성을 확정하지 못했다.
- Assets/Resources/UI/ProgressionIcons/EchoPressure.png는 존재하지만 현재 카탈로그 ID·아이콘 매핑에서 사용을 확인하지 못해 사용 원본 묶음에서 제외했다.
- Queen 관련 시안 및 QueenGraybox는 다른 씬/검증용이므로 이 목록에서 제외했다.
- Art/Icons/draw_icons.lua와 draw_movement_pieces.lua는 아이콘 제작 스크립트다.

## 재확인 방법

- Unity Search: `t:sprite`. 프로젝트 전체 Sprite 검색이므로 위 사용 목록과 대조한다.
- 씬 연결: SampleScene → player(SpriteRenderer/Animator), MonsterSpawner, GameHUD, Grid의 Tilemap.
- 성장 UI는 Resources.Load로 읽으므로 씬 직접 참조만 검색하면 일부 아이콘이 누락된다.
- 원본 사본과 출처·해시·사용 파일은 manifest.json에 기록했다.
