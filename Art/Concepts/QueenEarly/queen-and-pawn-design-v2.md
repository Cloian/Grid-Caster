# 퀸 보스와 폰형 근위대 도트 디자인 v2

## 디자인

- 퀸: 왕관을 쓴 석갑 여왕. 큰 삼지 왕관, 종 모양 망토, 각진 돌팔로 정체성을 표현한다.
- 근위대: 폰형 돌갑옷 방패병. 둥근 투구, 넓은 사각 방패, 짧고 단단한 발로 앞길을 막는 역할을 표현한다.
- 두 디자인은 회청색 석재, 어두운 보라, 소량의 금색과 청록색 눈을 공유한다.
- 사용자 정정 반영: 기갑병마·아르마딜로·쇠뇌는 기존 별도 컨셉이며 이번 제작 대상에서 제외했다.
- 크기 목표: 보스 120×120 논리 픽셀, 폰 68×68 논리 픽셀·본체 약 60픽셀. 사용자 지시에 따라 보스 크기를 일반 몬스터 규격보다 크게 잡았다.
- 상태: 디자인 검토용 생성 확대 이미지. 실제 120×120/68×68 픽셀 원본, 팔레트 수, 정수 확대 구조를 검증한 게임용 최종 에셋이 아니다.
- 내장 image_gen 사용. 투명 배경 처리 없음. 기존 파일 덮어쓰기 없음. Unity 에셋·게임 규칙 변경 없음.

## 기준 문서

- /Users/gimdong-yun/.codex/.chatgpt-projects/g-p-6ab8b6293f14819186c89f7383021597/ART_STYLE_REVISED.md
- /Users/gimdong-yun/Documents/Codex/2026-09-27/referenced-chatgpt-conversation-this-is-an/outputs/ART_STYLE.md
- /Users/gimdong-yun/GitHub/Grid-Caster/IMAGE_CONCEPT_BRIEF.md

## 퀸

결과: /Users/gimdong-yun/GitHub/Grid-Caster/queen-stone-monarch-concept-v2.png

입력:

- /Users/gimdong-yun/Documents/Codex/2026-09-27/referenced-chatgpt-conversation-this-is-an/work/aseprite_exports/jaguar_preview.png — 저해상도 도트 Gold Standard.
- /Users/gimdong-yun/.codex/.chatgpt-projects/g-p-6ab8b6293f14819186c89f7383021597/armadillo_grid_caster_68x68_preview_8x.png — 기존 몬스터 도트 스타일 참고.

정확한 생성 프롬프트:

```text
Create one Grid-Caster queen boss sprite design: a living stone-armored queen with three defining shapes only—a large three-point crown, a broad bell-shaped royal mantle, and chunky angular stone arms. Use a compact front-facing game-sprite pose suitable for the existing top-down tile board. Design on a 120×120 logical pixel grid, shown at an integer enlargement. Keep the jaguar Gold Standard's simplicity and low detail density even though this boss is larger. Use large connected chunky pixel clusters, long simple pixel stair-steps, broad flat color blocks, approximately one logical pixel dark outlines, and only two or three tones per material. Use at most 10 character colors: black outlines, cool gray stone, dark muted purple mantle, sparse warm gold crown accents, and tiny cyan eyes. Keep surfaces plain. No fine armor engraving, tiny ornaments, repeated texture marks, dithering, gradients, glow, bloom, anti-aliasing, semi-transparent edges, realistic anatomy, high-resolution pixel-art illustration, or scenery. Show only this single full-body character on a plain neutral background, with no text.

Input images:
- jaguar_preview.png: authoritative Gold Standard for simple shapes, chunky pixel density, limited colors, and sparse shading.
- armadillo_grid_caster_68x68_preview_8x.png: existing Grid-Caster monster sprite style reference.
```

## 폰 근위대

결과: /Users/gimdong-yun/GitHub/Grid-Caster/pawn-shield-guard-concept-v2.png

입력:

- /Users/gimdong-yun/Documents/Codex/2026-09-27/referenced-chatgpt-conversation-this-is-an/work/aseprite_exports/jaguar_preview.png — 저해상도 도트 Gold Standard.
- /Users/gimdong-yun/GitHub/Grid-Caster/queen-stone-monarch-concept-v2.png — 퀸과 동일 진영 형태 및 팔레트 참고.

정확한 생성 프롬프트:

```text
Create one Grid-Caster pawn royal guard sprite design belonging to the stone queen's faction. Its role is to stand across a lane and block the player's forward path. Use three defining shapes only—a rounded pawn-like helmet, one broad rectangular tower shield covering most of the body, and two short sturdy feet. Keep its stance square and frontal, compact and planted, so a row of these guards would read as a wall. The guard is visibly smaller and simpler than the queen. Design on a 68×68 logical pixel grid with a body approximately 60 logical pixels tall, shown at an integer enlargement. Match the jaguar Gold Standard's chunky pixel size, low detail density, large flat color blocks, long simple pixel stair-steps, approximately one logical pixel dark outlines, and two or three tones per material. Use at most 10 character colors. Share the queen's cool gray stone, dark muted purple, sparse warm gold, and tiny cyan eye accents. Keep the shield face plain apart from one large simple royal mark. No sword, spear, horse, armadillo, or crossbow. No fine engraving, repeated small armor plates, texture noise, dithering, gradients, glow, bloom, anti-aliasing, semi-transparent edges, realistic anatomy, high-resolution pixel-art illustration, or scenery. Show only this single full-body guard on a plain neutral background, with no text.

Input images:
- jaguar_preview.png: authoritative Gold Standard for simple shapes, chunky pixel density, limited colors, and sparse shading.
- queen-stone-monarch-concept-v2.png: queen faction design and shared color reference; match the faction while keeping the pawn guard smaller and simpler.
```

