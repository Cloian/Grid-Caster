# 실루엣으로 위압감을 주는 여왕 120px

최신 요청: 특유의 표정이 아니라 생김새에서 오는 아우라와 위압감. 이전 냉소 표정 시안은 별도 보존한다.

여왕의 청록·아이보리·금색, 흰 머리, 큰 픽셀 군집을 유지한다. 얼굴을 작게 하고 높은 어두운 칼라·넓은 각진 어깨·무거운 치맛단으로 존재감을 만든다. 표정은 담담하게, 자세는 정적인 직립. 이펙트로 아우라를 대신하지 않는다.

내장 image_gen으로 편집하고 Aseprite에서 실제 120×120 PNG와 편집 원본을 정리한다. 디자인 검토용, Unity 미적용.

## 프롬프트

```text
Use case: identity-preserve.
Edit target: the provided white-haired teal-and-ivory queen sprite. Keep this character's recognizable crown, short white swept hair, royal teal dress, ivory inset and gold accents, and the exact economical chunky pixel-art style.
The user's correction is crucial: her power and villainous aura must come from her APPEARANCE AND SILHOUETTE, NOT A SPECIAL FACIAL EXPRESSION. Do not give her a smirk, angry eyebrows, evil grin, snarling mouth or theatrical acting. Her expression is calm and nearly neutral, with simple small level cyan eyes and no conspicuous mouth. The eyes should be ordinary small pixel eyes, not enormous glowing eye effects.

Redesign her stature and costume into an imposing, immovable tyrant queen:
- Reduce the head/face by about 25 percent relative to the full body. Mature regal proportions while still using simple blocky sprite anatomy. A small pale face partly framed by deep collar shadows, short white hair in two crisp swept masses.
- A tall rigid gold crown with three clean architectural points, dark recesses and one small teal gem. It should look like the uppermost structure of an unyielding sovereign, not cute round ornaments. Preserve the original queen identity.
- Make the high collar near-black teal, standing behind and beside the head, and make the shoulder silhouette significantly broader and more angular. Heavy shoulder panels with just one sparse gold boundary each, no filigree or spikes everywhere. This large dark frame around a comparatively small calm face is the main source of threat.
- An upright, still, composed stance. Arms lowered slightly away from the torso, simple closed hands, with enough gaps to read the silhouette. No claw gesture, waving, action pose or weapon. She seems able to dominate the board without needing to exert herself.
- A long heavy royal dress that expands into a wide, almost fortress-like base. Three broad flat folds and a few angular steps at the hem; not fluffy, frilly or a ballerina skirt. Keep a narrower ivory vertical central panel but mute it to aged ivory. Most of the body is deep petrol teal and near-black shadow. The gold is restrained and aged, concentrated on crown, shoulders and a small hem accent rather than bright stripes everywhere.

Composition: one full-body standing queen alone on transparent background, both body edges visible with margin, same character height as the original queen. Game-sprite front view with slight overhead, no cinematic camera. No throne, background, floor shadow, scenery, magic circles, aura cloud or supporting characters. Presence must be conveyed by the body silhouette itself.

Technical: designed on a TRUE 120x120 logical pixel canvas, full character 110-112 logical pixels high. Head and hair about 18-22 logical pixels across, face about 14-16 logical pixels wide; shoulder frame about 46-50 logical pixels across, broad skirt about 76-84 pixels wide. Render as uniformly enlarged nearest-neighbor presentation of the tiny sprite. No detail at presentation scale. Coarse 1-2 pixel dark contour, large connected clusters, 2-3 flat tones per material, approximately 16 colors, upper-left lighting. Absolutely no gradients, antialiasing, soft shading, textures, noisy fine pixels, bloom, semitransparent character pixels, labels, text, watermark, framing or duplicates. Preserve the queen's original low-resolution dot-art vocabulary.
```
