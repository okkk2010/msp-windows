# Windows Client Code Load 명세서

## 1. 목적

Code Load는 사용자가 웹 또는 다른 곳에서 확인한 6자리 overlay code를 Windows Client에 입력하여, 서버에서 해당 overlay를 가져오고 즉시 적용하는 기능이다.

이 기능은 로그인 없이도 공개 overlay를 빠르게 적용할 수 있게 하는 것이 목적이다.

---

## 2. 사용자 흐름

```text
사용자 code 입력
→ Load 버튼 클릭
→ code 형식 검증
→ GET /api/overlays/code/{code}
→ 서버 응답 수신
→ overlayJson 파싱
→ platform 확인
→ Windows OverlayWindow에 적용
→ 로컬 캐시 저장
→ UI 상태 갱신
```

---

## 3. Code 정책

```text
code는 6자리 대문자 영문 / 숫자를 기준으로 한다.
code 입력 시 앞뒤 공백은 제거한다.
소문자 입력 시 대문자로 변환한다.
잘못된 code 형식이면 서버 요청 전에 차단한다.
```

권장 정규식:

```text
^[A-Z0-9]{6}$
```

Code 예시:

```text
ABC123
M4N8Q2
7KQ9PA
```

---

## 4. API

```http
GET /api/overlays/code/{code}
```

인증:

```text
불필요
```

요청 예시:

```http
GET http://localhost:8080/api/overlays/code/ABC123
```

---

## 5. 응답 처리

Code 조회 응답에는 Windows Client가 바로 적용할 수 있도록 `overlayJson`이 포함되어야 한다.

처리 순서:

```text
1. HTTP 성공 여부 확인
2. success == true 확인
3. data.overlayJson 존재 여부 확인
4. overlayJson.platform == "windows" 확인
5. OverlayJsonParser로 파싱
6. OverlayApplyService로 적용
7. OverlayCacheService로 캐시 저장
8. settings.json의 lastSelectedOverlayId 갱신
```

---

## 6. 실패 처리

| 상황 | 처리 메시지 |
|---|---|
| code 비어 있음 | 올바른 6자리 오버레이 코드를 입력하세요. |
| code 형식 오류 | Invalid overlay code. |
| 서버 연결 실패 | Server connection failed. |
| 404 Not Found | Overlay code not found. |
| platform 불일치 | Unsupported overlay platform. |
| overlayJson 오류 | Invalid overlay JSON. |
| 지원하지 않는 element | Unsupported element type. |

---

## 7. UI 요소

```text
TextBox: overlayCodeTextBox
Button: loadByCodeButton
Label: codeLoadStatusLabel
```

성공 메시지:

```text
Overlay loaded: {overlayName}
```

실패 메시지:

```text
Overlay code not found.
Invalid overlay code.
Failed to load overlay.
Unsupported overlay platform.
Invalid overlay JSON.
```

---

## 8. 완료 기준

```text
[ ] 정상 code 입력 시 overlay 조회 성공
[ ] 소문자 code 입력 시 대문자 변환
[ ] 공백 포함 code 입력 시 trim 처리
[ ] 6자리 미만 code 차단
[ ] 존재하지 않는 code 안내
[ ] windows platform overlay 적용 성공
[ ] android platform overlay 적용 차단
[ ] invalid overlayJson 차단
[ ] 적용 성공 시 로컬 캐시 저장
```
