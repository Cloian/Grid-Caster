# 승인한 여왕의 얼굴만 수정

사용자가 두 번째 시안을 선택한 것으로 해석하여 직전 v9 여왕을 기준으로 한다. 왕관·드레스·체형·팔레트·자세는 그대로 보존하고 얼굴의 눈·입만 더 악랄하게 수정한다.

내장 image_gen 편집 결과에서 얼굴 영역만 Aseprite로 합성하여 승인된 본체의 나머지 픽셀을 보존한다. 실제 120×120, 고정 16색, 이진 알파. 게임에 적용하지 않은 디자인 시안.

## 생성 프롬프트

```text
Use case: precise-object-edit.
Edit target: the provided 6x nearest-neighbor preview of the approved 120x120 pixel-art queen.
User request: EVERYTHING about this chosen queen is approved except her facial expression. Change only the face expression to look more malicious and cruel.
LOCKS: Keep the exact composition, transparent canvas, full-body position and scale, outline, face shape and head size, white hair, crown and gem, collar and shoulders, pose, hands, teal/ivory/gold clothing, skirt shape, every non-face element and their colors unchanged. Do not redesign her.

Within the small exposed pale facial area only, make the existing cyan eyes narrower with subtly lowered upper lids and cool downward-angled brows, conveying ruthless confidence rather than screaming anger. Add a very small, restrained asymmetric dark mouth line, a slight cruel smile. Two or three logical pixels only; no large grin, teeth, fangs, blush, wrinkles or grotesque features. Keep both eyes looking forward with consistent pupils, no crossed eyes.
Preserve the approved powerful body silhouette and understated villain character. The expression should suggest that she already knows how she will destroy the player.

Exact same chunky low-resolution 120px game sprite style. Large flat pixel clusters, 1-2 logical-pixel features, no new fine details, gradients, antialiasing, soft shading, lighting changes or glow. Return the whole queen in the same framing, as a uniformly enlarged nearest-neighbor view; single queen, genuine transparent background, no text or labels. ONLY facial pixels may change.
```
