# 퀸 얼굴 수정 v7

사용자 피드백: v6 표정이 어색하며 처음 후보 C(왕좌 여왕)의 얼굴 표정을 가장 선호한다.

v6의 두꺼운 기울어진 눈꺼풀과 일자 입을 제거했다. 후보 C의 세로형 청록 눈, 작은 안쪽 동공, 짧은 윗눈 경계, 입 생략을 참고해 얼굴을 수정했다. 왕관·청록 의상·큰 삼각 실루엣은 유지한다. 얼굴만 차분하게 만들며, 보스의 존재감은 왕관과 실루엣으로 표현한다.

내장 image_gen으로 편집한 디자인 시안이다. 네이티브 120×120 게임용 도트 정리는 아직 진행하지 않았다.

## 사용한 프롬프트

```text
Use case: precise-object-edit.
Image 1 is the edit target: full-body teal-and-ivory queen. Image 2 is the authoritative FACE reference: the original seated purple queen.

Replace ONLY the facial region of image 1 with the facial design and expression of image 2. This is a faithful reference-matching correction, not a new interpretation of "stern", "evil" or "cute". The user prefers exactly the original seated queen's restrained, calm expression.

Match reference 2's eyes closely: two upright small rectangular cyan irises, a tiny dark pupil at the bottom toward the center, a simple SHORT horizontal dark upper edge. No thick slanted brows, no broad black eye sockets, no angry V angles, no rounded sparkling innocent eyes. Remove the horizontal mouth entirely: reference 2 has no visible mouth. Match the softly tapered simple ivory face and the original eye-to-face spacing. Give the face the reference's neutral pale ivory color rather than peach blush. Keep the facial expression quiet and composed, with the same pixel economy as reference 2.

Do not transfer reference 2's purple hairstyle, throne, crown, outfit or jewelry. Preserve image 1's existing pale side hair, large three-point crown, cyan crown inset, teal shoulder panels, ivory dress panel, tiny hands, gold edging, triangular silhouette, dimensions, pose and centered framing. All edits should be confined to face skin and eyes; no accessory additions.

Maintain the existing blocky pixel-art style, connected flat clusters, staircase outline, low detail density and limited material shading. One queen only, transparent background, no text or panels. This is a design concept for a roughly 120-pixel game sprite.
```

