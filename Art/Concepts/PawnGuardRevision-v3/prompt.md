# 퀸 팔레트에 맞춘 폰 근위병 v3

사용자가 승인한 QueenRevision-v7 퀸에 맞춰 기존 승인된 방패 근위병의 색을 변경했다.

- 기존 헬멧, 방패, 작은 눈, 짧은 발, 길을 막는 직선 실루엣 유지.
- 보라색 망토·방패 → 청록색. 갑주는 따뜻한 상아 회색, 테두리와 왕관 문양은 퀸과 같은 금색.
- 방패 중앙에 넓은 상아색 띠 하나를 넣어 퀸의 중앙 패널과 연결.
- 목표 크기 68×68, 32 PPU, Point 필터링. 생성 PNG는 확대된 디자인 시안이며 네이티브 도트 제작·게임 임포트는 미완료.
- 방패 문양은 시각 디자인이며 별도 피해 차단 규칙을 구현했다는 의미는 아니다.
- 생성 도구: 내장 image_gen. 기존 파일은 보존했다.
- 생성 원본: /Users/gimdong-yun/.codex/generated_images/01a0ff99-6f40-7fa1-a793-093bdf079a2b/exec-066255f6-fe4e-4af3-a0ce-3be71aeebfcf.png

## 사용한 프롬프트

```text
Use case: precise-object-edit. Recolor ONE approved pawn shield guard from image 1 to match the approved queen in image 2.
Image 1 is the exact guard edit target. Image 2 is ONLY the palette and faction reference; do not transfer the queen's face, hair, crown size, gown or body proportions.
Preserve the guard's compact pawn-like armored body, rounded closed helmet, cyan eye slit, broad rectangular tower shield covering the chest and legs, short visible feet, simple crown emblem on the shield, shoulder panels and short cloak. Same pose and silhouette. No spear, sword, new accessories, big queen crown, human face or throne.
Replace all purple fabric and purple shield field with the queen's deep desaturated teal. Preserve subdued warm ivory-gray helmet, gauntlets and feet. Match the queen's warm gold for helmet stripe, shield border and crown crest; avoid the previous neon yellow. Use a SINGLE broad warm ivory vertical stripe beneath the gold crown emblem on the teal shield, echoing the queen's ivory center panel, leaving most shield teal. Cyan eyes remain tiny. The large shield and sturdy straight front convey a pawn guard that blocks the path.
Color roles: near-black outline #171D25; teal #183B3C #2F6260 #568780; warm ivory-gray armor #B8B39A #DDD5B8 with darker #626E75 shadow; antique gold #A67A3C #D2AD62; tiny cyan #5FCDE4. Restricted palette, 2–3 flat shades per material.
Compact front-facing slightly overhead 2D game pixel-art concept, target roughly 68x68 native sprite shown enlarged. Large connected blocky pixel clusters, clean staircase edges, 1 logical-pixel outline, upper-left light, no gradients, fine metal texture, antialiasing, bloom or soft shadow. Maintain the approved guard design; change palette only apart from the single central ivory shield stripe.
Output ONE full-body guard, centered on transparent background, no text, no labels, no panels, no ground shadow, no queen or environment.
```

