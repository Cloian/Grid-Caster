# 아트 제작 자료

이 폴더는 **제작 원본·시안·백업**을 보존합니다. Unity 런타임은 여기 파일을 직접 로드하지 않습니다. 게임이 쓰는 파일은 `Assets/` 아래에 있습니다.

| 폴더 | 내용 | 게임 적용 위치 |
| --- | --- | --- |
| [`Sources/SampleScene/`](Sources/SampleScene) | 플레이어·몬스터·투사체·타일·UI의 Aseprite 원본 | `Assets/Player`, `Assets/Monsters`, `Assets/TIles`, `Assets/UI` |
| [`Sources/Lively/`](Sources/Lively) | 마법사·박쥐·뱀 대기·이동·공격·피격·사망 모션 원본 (프레임별 ms 포함) | `Assets/Player/Lively`, `Assets/Monsters/{Bat,Snake}/Lively` |
| [`Concepts/`](Concepts) | 퀸 보스·폰 근위대 콘셉트 시안 (`QueenEarly` → v10) | 미적용 시안 |
| [`Icons/`](Icons) | 이동술·성장 아이콘 제작 스크립트와 시안 ([안내](Icons/README.md)) | `Assets/UI/Icons`, `Assets/Resources/UI` |
| [`Audio/`](Audio) | 전투 효과음 제작·선정 자료와 크레딧 | `Assets/Resources/CombatSFX` |
| [`Backups/`](Backups) | 교체 전 시트·타일 원본 | — |

## 기준

- 32 PPU, Point 필터, 압축 없음. 박쥐·뱀 모션 시트는 기존 화면 크기를 유지하려고 40 PPU입니다.
- 캐릭터 프레임 기준점은 프레임 중심입니다. 마법사 50px, 박쥐·뱀 40px 프레임.
- 맵 벽 시트는 64px 칸(64 PPU)이지만 32px로 그린 뒤 2배로 키워 캐릭터와 픽셀 크기를 맞춥니다.
- 교체 시 대응 `.meta`와 GUID를 유지합니다. 같은 크기·같은 칸 배치로 픽셀만 바꾸면 씬·타일 참조가 그대로 유지됩니다.

## 다시 만들기

- 모션 시트 → 클립·컨트롤러·씬/프리팹 연결: Unity 메뉴 `Tools/Art/Apply Lively Motions` (`Assets/Editor/LivelyMotionSetupTool.cs`)
- README 미리보기 이미지: `python3 Tools/art-preview/make_readme_images.py`

`Concepts/QueenEarly/`의 프롬프트 기록(md·json)에 적힌 절대 경로는 생성 당시의 위치입니다. 파일은 저장소 루트에서 이 폴더로 옮겼습니다.
