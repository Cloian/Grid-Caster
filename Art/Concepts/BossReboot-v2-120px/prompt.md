# 저해상도 보스 120px 수정

요청: 이전 보스의 형태를 저해상도로 다시 그리고, 여왕과 같은 120×120 캔버스/표시 높이를 사용한다. 여왕 이미지는 픽셀 군집의 크기와 정보량을 비교하는 기준이며 의상·얼굴을 복사하지 않는다.

내장 image_gen으로 형태를 다시 단순화한 뒤 Aseprite에서 정확한 120×120 캔버스, 제한 팔레트, 0/255 알파로 정리한다. 원본 생성 이미지는 별도로 보존한다. 디자인 검토용이며 게임에 임포트하지 않는다.

## 프롬프트

```text
Use case: style-transfer.
Image 1 is the edit target: the dark muscular giant boss. Image 2 is the user's previous queen and is the authoritative reference for COARSE PIXEL DENSITY AND SIMPLICITY, not for costume or identity.

Redraw the boss as truly low-resolution pixel sprite artwork designed to fit a 120 by 120 pixel canvas, at the same displayed character height as the queen. The user specifically wants a much lower-resolution drawing, not the previous large detailed illustration.

Preserve the boss's enormous muscular shoulders and chest, small stern head, dark violet-charcoal flesh, tiny burnt-orange eyes, plain bronze wrist bands, charcoal waist cloth, bent legs, asymmetric lowered forward fist and drawn-back fist. Keep the determined hostile expression and forward-weighted attack stance. Show the entire character, including both feet.
Match the queen's chunky, extremely economical flat pixel shapes and sparse hard shading. Most edges step in blocks of 2 logical pixels on the 120-pixel canvas, with broad 8–24 pixel masses, approximately 2-pixel dark outer contour, and clean connected color clusters. Head roughly 16–18 pixels tall; each eye only about two pixels. Simplify each chest/shoulder/arm muscle into one base plane and one broad lit plane, with one dark separation where needed. The fists are simple blocky shapes with at most two knuckle marks. Remove muscle striations, numerous small abdominal bands, individual fingers/toes, thin creases, noise and fine highlights. Keep visible gaps between arms and torso. A compact chunky game sprite, not a detailed fighting-game portrait or realistic anatomy.

Strict target palette: near-black #0B0D14; flesh #262131 #493D51 #6B5870; cloth #191D29 #333949; bronze #493827 #806342 #B08A56; eye #C46F4D. Flat colors only, ten colors maximum, no gradients, anti-aliasing, texture, glow, soft shadows or semi-transparent character pixels. Upper-left light.
Transparent background, one boss alone, centered full body with an even margin. No floor or shadow, checkerboard, text, labels, comparison sheet or extra characters. Do not copy the queen's face, crown, dress, hair, gold costume trim or teal colors. Return a uniformly enlarged nearest-neighbor presentation of the small pixel sprite so its coarse grid is unambiguous; do not add any detail at presentation resolution.
```

## 검수 결과

- 실제 PNG/Aseprite 캔버스 120×120. 10색 + 투명색, 알파는 0/255만 사용.
- 캐릭터 외곽 범위 x=4..115, y=6..115. 여왕 비교본은 동일한 120×120 캔버스와 발 기준선을 사용한다.
- 6배 확대본은 최근접 확대. 원본 크기와 확대본 및 여왕 비교본을 육안 검토했다.
- 디자인 시안이며 Unity 임포트와 전장 내 검증은 수행하지 않았다.
- 정확한 자동 검수와 생성 출처는 qa-report.json 및 asset-manifest.json을 따른다.
