# 인터넷 공개 Web 플레이테스트 배포

이 문서는 최초 공개 빌드의 보존 기록이다. 현재 공개 화면과 새 배포 커밋·검증 결과는 [모바일 보드 중심 UI 갱신](mobile-board-ui-2026-10-02.md)을 따른다.

## 주소와 배포 상태

- 공개 URL: https://cloian.github.io/Grid-Caster/
- 호스팅: GitHub Pages, HTTPS 강제, `gh-pages`의 루트. Mac/로컬 서버와 독립적으로 제공한다.
- 배포 커밋: `7c9a0c4a626807da5f5a3a8628d070a557015622`. API의 최신 build는 `built`, 오류 없음이며 원격 브랜치 SHA와 일치한다.
- 게임 빌드 ID: `59f9a4c2064a8cb9` (Unity 실행 파일 내용 기반). 해당 경로를 manifest에 고정한다.
- `main`과 개발 브랜치, 작업 중 코드/씬/Inspector/Unity PlayerSettings는 변경하지 않았다. 독립 배포 브랜치만 push했으며 PR/병합/force push는 수행하지 않았다.

## 변경한 파일과 배포 파일

- 추가: `Tools/mobile-playtest/package-static.mjs`, 이 검증 문서.
- 수정: `docs/design/mobile-safari-playtest.md`, 검증 자료 `README.md`.
- 공개 브랜치 파일 7개: `.nojekyll`, `index.html`, `build-manifest.json`, 버전 경로의 Unity loader/framework/data/wasm.
- 공개 브랜치에는 C# 소스·Unity 씬·프로젝트 설정·Aseprite 원본·검증 로그·인증 정보를 넣지 않았다. 원래 GitHub 저장소는 이미 공개 상태이며 공개 여부를 변경하지 않았다.

## 패키지와 HTTP 검증

기존에 검증한 Unity 6000.4.0f1 Web 빌드를 그대로 사용한다. 새 Unity 컴파일/빌드나 새 패키지 설치는 하지 않았다. 모바일 어댑터 SHA-256은 이전 검증과 같은 `353862466d4a814565461f3ad665f20b9c85e92341bf33bac90a504197d99b60`이다.

4개 실행 파일을 Brotli 압축 해제한 뒤 원본 바이트와 각각 비교해 모두 일치했다. WASM의 매직 바이트와 파일 허용 목록도 확인했다. 생성 코드의 기존 공백은 바이트 일치 보존을 위해 재포맷하지 않았다. 직접 관리하는 HTML/manifest와 포장 스크립트의 공백 검사/Node 구문 검사는 통과했다.

정적 실행 파일 총량은 **63,934,560 bytes (약 61 MiB)**다. HTTP 응답에서는 Pages CDN이 gzip으로 압축하며 WASM은 `Content-Type: application/wasm`, data는 `application/octet-stream`, 각각 `Content-Encoding: gzip`을 확인했다. 관측한 압축 응답 크기는 WASM 12,886,760 bytes, data 5,957,751 bytes다. 전체 초기 전송량의 실측이나 모든 지역 CDN의 압축 보장은 아니다.

공개 시작 페이지와 manifest, WASM/data는 HTTP 200 응답을 확인했다. 인터넷 URL을 로컬 서버로 전달하는 터널이 아니다.

## 조작 검증

- 포장 파일을 격리 로컬 서버에 제공한 Chrome 모바일 터치 에뮬레이션: 25개 조건 통과, console/page error 없음.
- 공개 HTTPS URL의 Chrome 모바일 터치 에뮬레이션: 25개 조건 통과, console/page error 없음. 로컬 폴더가 아니라 `https://cloian.github.io/Grid-Caster/`를 직접 열어 검사했다.

실행 도구는 `Tools/mobile-playtest/browser-smoke.mjs`다. 실제 UI 터치로 시작 선택, 보상 상세/닫기, 미충전 특수공격, 공격 미리보기·취소·확정, 중복 실행 거부, 강화 목록 입력 차단, 정지/재개, 한 칸 이동, 숨김 탭 정지 훅, 세로 크기 변경과 가로 안내를 검사한다. 게임 상태 조회는 진단용 읽기만 수행한다.

원본 로그·화면은 Git 제외 경로 `Builds/MobileWeb/public-package-verification/`와 `public-live-verification/`에 보존한다. 사람의 재미·실 iPhone Safari·20웨이브 완주 검증이 아니다.

## 플레이와 한계

별도 GameObject/Inspector/태그/레이어/Tilemap/Collider 설정은 필요 없다. 세로 Safari로 링크를 열고 `터치하여 시작` → 시작 특성/증강 선택 → 행동 선택 → 보드 탭 미리보기 → `실행`으로 플레이한다.

실제 아이폰의 첫 로딩 시간, 터치 판독성, 후반 성능·메모리와 발열은 사용자가 확인해야 한다. 새로고침/브라우저 탭 폐기 후 런 이어하기는 제공하지 않는다. 로그인/광고/결제/온라인 전투 서버를 추가하지 않았다. 공유 링크는 공개이므로 받은 사람이 다시 공유할 수 있다.

Pages의 HTML/manifest 캐시는 최대 10분으로 관측됐다. 이후 배포 직후 새 화면이 안 보이면 잠시 기다린 뒤 다시 열 수 있다. 실행 파일의 내용 해시 경로는 서로 다른 빌드 파일이 혼합되는 것을 방지한다.
