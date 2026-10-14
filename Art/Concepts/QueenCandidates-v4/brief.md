# 퀸 후보 v4 — 제작 브리프

- 적용 스킬: /Users/gimdong-yun/.codex/skills/create-game-assets/SKILL.md 및 imagegen.
- 요청 범위: 승인된 폰 근위대에 맞는 퀸 디자인 후보 3개. 게임 구현과 기존 에셋은 변경하지 않는다.
- A: 가면 여왕 — 삼각형 망토와 큰 가면 머리.
- B: 왕관나방 — 넓은 M/마름모 날개와 짧은 몸.
- C: 왕좌 여왕 — 여왕과 각진 부유 왕좌의 사각 실루엣.
- 원본 크기 목표: 보스 120×120 논리 픽셀. 생성 출력은 디자인 초안이며 실제 120×120 원본 규격 충족 여부를 별도 기록한다.
- 공통: 큰 머리·압축된 몸, 최대 세 특징, 11색 목표, 재질별 2~3단계 명암, 좌상단 광원, 큰 픽셀 군집, 약 1픽셀 외곽선.
- 기준: ART_STYLE_REVISED.md, 초기 ART_STYLE.md, IMAGE_CONCEPT_BRIEF.md, 승인 근위대, 재규어 Gold Standard.
- 크기/점유: 120픽셀을 32 PPU로 사용할 경우 3.75 월드 유닛이다. 외형 크기와 게임 규칙 점유 크기는 다르며 이번에 보스 점유 규칙을 확정하지 않는다.
- 검토: 확대 이미지, 120픽셀 폭 검토 썸네일, 저장된 게임 화면 위 크기 비교. 비교 화면은 플레이 가능한 Unity 검증이 아니다.
- 승인 상태: 후보, 미승인. 최종 에셋 아님.

## 이미지 입력

- 1. /Users/gimdong-yun/Documents/Codex/2026-09-27/referenced-chatgpt-conversation-this-is-an/work/aseprite_exports/jaguar_preview.png
- 2. /Users/gimdong-yun/GitHub/Grid-Caster/pawn-shield-guard-concept-v2.png
- 3. /Users/gimdong-yun/GitHub/Grid-Caster/docs/verification/captures/pc-screen-2026-10-02.png

## 공통 생성 프롬프트

```text
ROLE/PURPOSE: One original queen boss design candidate for Grid-Caster, a top-down tile-based tactical game. This is a single neutral master-pose design draft, not an animation sheet. The queen commands the approved pawn shield guards and controls straight and diagonal attack lines and temporary barriers. Express royal authority through the large silhouette, not extra decorative symbols.
VIEW: Compact front-facing 2D RPG game sprite compatible with the slightly overhead board characters in reference 3. No perspective scene or full-body fashion illustration. Entire character visible, centered with a small even margin.
ART DIRECTION: Use reference 1 as the strict upper bound for detail density and pixel-cluster simplicity. Large flat connected color masses, long deliberate stair-step edges, approximately one logical-pixel dark outline. At most three defining visual features. Only two or three hard-separated tones per material. A 120×120 logical-pixel boss design, enlarged uniformly for viewing; keep the large head and compact body proportions of small board-game characters. Do not add detail just because the boss is larger.
LOCKED PALETTE: Use only these eleven flat colors for the character: #000000 outline; #2E223C, #50355E, #795482 purple; #626E75, #9BADB7, #DADADA cool stone/pale material; #D7782E, #F19F38, #F4AA4D gold; #5FCDE4 cyan accent. Upper-left light, large shadow planes, no intermediate color ramps. Gold and cyan are small accents, not thick borders over the entire costume. The approved pawn reference supplies faction colors and royal vocabulary, not its gradients or fine rendering.
GAME-SCALE READ: Crown and face remain distinct from body at thumbnail size. Main silhouette has a clear centered occupied-cell anchor. Avoid wide detached effects and loose floating particles.
OUTPUT: One character only, genuinely transparent background, no floor, contact shadow, scenery, labels, typography, borders, comparison panels, watermark, or additional characters. Do not render a checkerboard.
EXCLUDE: gradients, soft shading, glow, bloom, anti-aliasing, semi-transparent character edges, microtexture, repeated 1-pixel markings, intricate armor, skeletal scary face, oversized stone fists, thin realistic limbs, long fashion-model torso, individual hair strands, eyelashes, anatomical fingers, ornate jewelry, or HD pixel-art illustration.
REFERENCES: Image 1 is jaguar_preview.png, the native low-detail Gold Standard. Image 2 is pawn-shield-guard-concept-v2.png, the approved faction identity. Image 3 is pc-screen-2026-10-02.png, game-view and head/body proportion context. Do not reproduce the jaguar, pawn, board, HUD, or screenshot.
```

## A: 가면 여왕

```text
SUBJECT A — MASKED QUEEN: A compact royal spellcaster with three dominant shapes: a large rounded ivory mask/head topped by a small three-point gold crown, a short broad triangular purple mantle, and two tiny pale mitten hands holding the mantle edges. The head including crown occupies roughly 40 percent of character height; the body is short and compressed. Mask is a smooth pale color block with two tiny cyan eye clusters, no mouth, skull sockets, or long nose. Hair, if visible, is just one short pale mass. The mantle widens toward the base and its split hem makes two bold diagonal edges. A composed, approachable-but-mysterious royal boss, visually charming rather than grotesque. No staff, throne, wings, floating magic, enormous shoulder pads, or giant gold trim.
```

## B: 왕관나방

```text
SUBJECT B — CROWN MOTH QUEEN: An original compact royal moth monster with three dominant shapes: a small rounded pale moth face with a simple three-point gold crown/crest, two broad purple wings folded into one bold cape-like silhouette, and a short chunky body. The wings form a strong wide M/diamond outline with two readable diagonal edges on each side; they are only large flat purple planes with a single sparse gold tip on each upper outer corner, not patterned insect wings. Head and thorax are large and cute, with two tiny cyan eyes and short simple antenna shapes integrated into the crest. Feet are two small pale blocks tucked under the body. Restrained magical monarch character, more creature than person, no realistic insect anatomy. No multiple skinny legs, veins, eye spots, butterfly texture, human dress, weapons, additional crown jewels, or detached effects.
```

## C: 왕좌 여왕

```text
SUBJECT C — THRONE QUEEN: A small compact masked queen seated inside a compact hovering stone throne, treated together as one character sprite. Three dominant shapes: a large rounded pale face/head with a small three-point gold crown, a short purple royal mantle/body, and a square stepped cool-gray throne frame with two short thick armrests. The throne frame surrounds rather than hides her, and forms a bold stepped rectangular silhouette with one centered short base. No huge legs, wheels, terrain, platform floor, or background architecture. The queen has two tiny cyan eye clusters, a simple rounded face without a skull shape, and small mitten hands resting on the armrests. Head and crown are about 40 percent of the seated queen's visible height. Throne is only three large gray stone planes, with one small gold accent, not carved ornament. A dignified compact magical ruler with friendly shape clarity, not a bulky stone golem, tank, or realistic portrait. The throne is a visual motif only, no action effects.
```

