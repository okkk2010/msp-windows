# Windows Client My Library 명세서

## 1. 목적

My Library 기능은 로그인 사용자가 Web에서 저장한 overlay 목록을 Windows Client에서 조회하고, 선택한 overlay를 실제 OverlayWindow에 적용하는 기능이다.

Windows Client는 이번 단계에서 library를 저장하지 않는다. 저장은 Web Frontend에서 처리하고, Windows Client는 **조회 / 적용**만 담당한다.

---

## 2. 기본 정책

```text
Windows Client에서는 library 저장 기능을 제공하지 않는다.
library 저장은 Web Frontend에서 처리한다.
Windows Client는 조회 / 적용만 담당한다.
```

이 정책은 Windows Client를 가볍게 유지하는 데 유리하다.

역할 분리:

```text
Web Frontend:
오버레이 탐색, 업로드, library 저장

Windows Client:
library 조회, overlay 선택, overlay 적용
```

---

## 3. Library 조회 흐름

```text
사용자 로그인 완료
→ Refresh Library 버튼 클릭
→ accessToken 확인
→ GET /api/library
→ library 목록 수신
→ ListBox 또는 DataGridView에 표시
```

---

## 4. Library Overlay 적용 흐름

```text
사용자가 overlay 선택
→ Apply Selected 버튼 클릭
→ 선택된 overlay의 id 또는 code 확인
→ overlay 상세 조회 또는 library 응답의 overlayJson 사용
→ overlayJson 파싱
→ platform == windows 확인
→ OverlayWindow 적용
→ 로컬 캐시 저장
```

---

## 5. API

```http
GET /api/library
Authorization: Bearer {accessToken}
```

인증:

```text
필요
```

목적:

```text
현재 로그인 사용자의 저장 overlay 목록을 조회한다.
```

---

## 6. 응답 표시 필드

최소 표시:

```text
overlay name
code
platform
game
```

DataGridView 사용 시 추천 컬럼:

```text
Name | Code | Platform | Game
```

---

## 7. UI 요소

```text
Button: refreshLibraryButton
ListBox 또는 DataGridView: libraryList
Button: applySelectedOverlayButton
Label: libraryStatusLabel
```

---

## 8. 비로그인 처리

비로그인 상태에서 Refresh Library를 클릭하면 서버 요청을 보내지 않고 로그인 필요 메시지를 표시한다.

권장 메시지:

```text
로그인이 필요합니다.
Login is required.
```

---

## 9. 실패 처리

| 상황 | 처리 |
|---|---|
| 비로그인 상태 | 로그인 필요 안내 |
| 401 Unauthorized | 토큰 삭제, 비로그인 전환 |
| 서버 연결 실패 | 라이브러리 조회 실패 표시 |
| 빈 library | 빈 상태 메시지 표시 |
| 선택 항목 없음 | overlay 선택 필요 안내 |
| overlayJson 오류 | Invalid overlay JSON 표시 |

---

## 10. 완료 기준

```text
[ ] 비로그인 상태에서 library 조회 시 로그인 필요 안내
[ ] 로그인 상태에서 library 조회 성공
[ ] library 비어 있을 때 빈 상태 표시
[ ] library overlay 선택 가능
[ ] 선택 overlay 적용 성공
[ ] 서버 오류 시 실패 메시지 표시
[ ] 토큰 만료 시 비로그인 상태 전환
```
