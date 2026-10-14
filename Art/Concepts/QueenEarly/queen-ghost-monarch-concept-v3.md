# 퀸 도트 디자인 v3 — 유령 여왕

- 요청 반영: 사용자는 폰 근위대 디자인을 선호하고 퀸 디자인의 미감을 수정하도록 피드백했다.
- 변경: 큰 주먹·넓은 어깨·돌가면·거대한 왕관을 제거하고, 작은 왕관·밝은 얼굴과 머리·종 모양 보라 망토의 유령 여왕을 제안한다.
- 승인된 폰 근위대 PNG는 수정하지 않았다.
- 결과: /Users/gimdong-yun/GitHub/Grid-Caster/queen-ghost-monarch-concept-v3.png
- 내장 image_gen 편집 사용. 불투명 검정 배경. 기존 파일 덮어쓰기 없음.
- 상태: 120×120 논리 픽셀을 목표로 한 디자인 검토용 확대 이미지. 실제 원본 크기·색상 수·정수 픽셀 구조를 검증한 최종 게임 스프라이트가 아니다.

## 입력 이미지

- /Users/gimdong-yun/GitHub/Grid-Caster/queen-stone-monarch-concept-v2.png — 편집 대상.
- /Users/gimdong-yun/GitHub/Grid-Caster/pawn-shield-guard-concept-v2.png — 사용자 선호 근위대, 같은 진영 팔레트 참고.
- /Users/gimdong-yun/Documents/Codex/2026-09-27/referenced-chatgpt-conversation-this-is-an/work/aseprite_exports/jaguar_preview.png — 저해상도 도트 Gold Standard.

## 정확한 생성 프롬프트

```text
Redesign only the queen character in the first image for Grid-Caster. Replace the bulky stone-golem queen with an appealing, elegant ghost queen, while keeping the simple low-resolution game-sprite style. Remove the huge stone fists, oversized shoulder pads, skull-like stone face, and enormous heavy crown. Give her a compact three-point gold crown, a small rounded pale face with only two simple cyan eye clusters, pale hair drawn as two or three large smooth pixel masses, small restrained hands, and one flowing bell-shaped dark muted purple royal robe. The silhouette should be poised and regal, narrower at the shoulders and wider at the robe hem. Keep a charming but ominous fairy-tale boss impression. Use no fine facial anatomy, eyelashes, individual hair strands, intricate jewelry, cleavage, or costume engraving. Maintain the pawn guard's faction through sparse warm gold edging and tiny cyan accents, but make the queen clearly distinctive and larger.

Design for a 120×120 logical-pixel boss footprint, presented enlarged. Match the jaguar reference's low detail density rather than high-resolution pixel-art illustration. Use big connected pixel clusters, long simple pixel stair-steps, flat readable color blocks, approximately one logical-pixel dark outlines, a maximum of 12 character colors, and only two or three tones per material. Plain black background, one full-body queen only, no text, no scenery, no additional characters. No gradients, glow, bloom, dithering, anti-aliasing, semi-transparent edges, texture noise, realistic rendering, or tiny decorative details.

Input images:
- queen-stone-monarch-concept-v2.png: edit target; replace the queen's bulky design with the ghost queen described above.
- pawn-shield-guard-concept-v2.png: approved guard design; use only as a shared faction palette reference, do not change or reproduce the guard.
- jaguar_preview.png: authoritative Gold Standard for simple low-resolution pixel shapes, chunky clusters, and sparse shading.
```
