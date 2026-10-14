# 악랄한 여왕 120px 시안

요청: 기존 여왕의 도트 스타일과 왕관·드레스 정체성을 유지하면서 강함과 악랄함이 읽히게 수정한다. 120×120 실파일로 정리하며 기존 시안은 보존한다.

방향: 좁은 냉소적 눈, 짧은 비웃음, 각진 높은 칼라와 어깨, 처형을 명령하는 손짓. 짙은 청록·절제된 아이보리·오래된 금색. 근육 대신 위계와 공격 명령의 자신감으로 위압감을 만든다.

내장 image_gen 편집 후 Aseprite에서 해상도·팔레트·투명도를 정리한다. 게임에 적용하지 않은 디자인 시안이다.

## 생성 프롬프트

```text
Use case: identity-preserve.
Asset type: low-resolution 2D pixel-art queen boss concept for a top-down tactical chess-inspired game.
Input image 1 is the edit target and authoritative character/style reference: the white-haired teal-and-ivory queen. Input image 2 is only a reference for her cool, unimpressed gaze; do not add its throne, purple hair or purple costume.

Primary request: retain the queen concept and its charmingly coarse pixel-art technique, but make her unmistakably powerful, malicious and villainous. This is a cruel queen who confidently orders the player's execution, not a kind princess or a snarling monster.

Keep the white short swept hair, pale face, recognizable tall gold crown with turquoise gem, dark teal royal dress with ivory central inset and sparse gold edging. Preserve the reference's flat chunky clusters, near-black outline, sparse two-to-three-tone shading and readable game sprite view. Whole standing character, front with a slight game-sprite overhead view, isolated on transparent background.
CHANGE:
- Give the face narrow half-lidded turquoise eyes under sharply descending dark brows. Lower the chin slightly, as though looking down at the player. A tiny restrained one-sided smirk, only a short two-pixel mouth mark, suggests contempt and deliberate cruelty. A firmer angular cheek and jaw contour, not round baby cheeks. Avoid giant grin, fangs, grotesque features, crossed eyes or an exaggerated angry cartoon expression.
- Increase the commanding silhouette through a high rigid collar and broad angular shoulder panels, with a taller three-point chess-queen crown. Keep her face modest relative to the body, not an oversized chibi head.
- Replace the passive symmetrical hands with one clearly extended commanding hand, palm angled down and fingers represented by just two or three bold blocks, as if selecting the player's square for destruction. Other arm held near the waist with a firm closed hand. Proud upright torso, planted weighted stance, long heavy skirt.
- Darken the teal fabric to near-black petrol teal in shadow, with rich teal planes; ivory is reserved to a narrower sharp central dress panel and the hair. Use restrained aged gold highlights. Dark collar frames the pale face so the cold cyan eyes are the focal point.
- Make the skirt an angular, heavy royal silhouette with three large folds and a stepped hem, not frilly or round and cute.

Technical output: sprite designed on a TRUE 120 by 120 logical pixel canvas, full character about 110 pixels tall with clear margin. Render as a uniformly enlarged nearest-neighbor display of that 120px sprite, never add new detail at display resolution. Coarse 1-2 logical-pixel contour, large connected 6-20 pixel color masses, flat colors only, approximately 16 opaque colors maximum, upper-left light. Use the same pixel density and simplicity as the queen reference. Face and costume features must remain readable after reducing to 120x120.
NO gradients, antialiasing, texture, noise, glow, fine jewelry, elaborate ornament, photoreal anatomy, enormous muscles, throne, weapons, scenery, shadow underneath, labels, grid, multiple characters, comparison sheet, text or watermark. No soft alpha; transparent background, opaque sprite pixels. Preserve queen identity and the coarse dot style.
```
