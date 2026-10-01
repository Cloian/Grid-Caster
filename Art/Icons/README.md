# 아이콘 제작 원본

게임이 로드하는 파일은 `Assets/UI/Icons/Movement/`와 `Assets/Resources/UI/ProgressionIcons/`에 있다. 이 폴더는 제작/비교 원본을 보존하며 Unity 런타임에서 직접 로드하지 않는다.

- `draw_movement_pieces.lua`: 기존 Teleport 아이콘의 테두리를 보존하고 20×20 나이트·비숍·룩 말 아이콘을 만드는 Aseprite 스크립트. 출력은 `Assets/UI/Icons/Movement/`의 `KnightMove`, `BishopMove`, `RookMove`다.
- `draw_icons.lua`: 기존 이동 아이콘 테두리 안에 이동술·범위 공격·봉쇄 시안을 만드는 스크립트. 출력은 `HandPixel_v1/`이다.
- `HandPixel_v1/`: PNG·편집 가능한 Aseprite·비교 이미지. 이동술 동작 시안 `KnightLeap/BishopPhase/RookRush`는 현재 HUD의 체스 말 아이콘과 구분한다. `ChainBurst/BindingLanding/EchoPressure`는 범위/봉쇄 아이콘의 제작 원본이다.

스크립트는 프로젝트 루트가 작업 디렉터리인 Aseprite 환경에서 실행한다. 같은 경로의 파일을 다시 저장하므로, 실행 전 원본 diff와 로컬 편집을 확인한다. 이 게시 작업에서는 재생성하지 않고 기존 파일을 보존했다.

Unity 적용 시 32 PPU·Point 및 대응 `.meta`를 유지한다. 정적 아이콘에 자동 Model Prefab을 생성하지 않는다. 최종 아트 스타일이나 모든 성장 항목의 아이콘을 완성했다는 뜻은 아니다.
