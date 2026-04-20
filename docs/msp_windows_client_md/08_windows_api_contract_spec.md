# Windows Client API Contract 명세서

## 1. 문서 목적

이 문서는 Windows Client가 Backend Server와 연동하기 위해 사용하는 API 계약을 정의한다.

Windows Client는 이번 단계에서 아래 API만 사용한다.

```text
1. Code 기반 Overlay 조회
2. Windows Google Login 시작
3. Localhost Callback 수신
4. 내 정보 조회
5. 내 Library 조회
6. Overlay 상세 조회
7. 서버 연결 테스트
```

---

## 2. 서버 기본 URL

개발 기본값:

```text
http://localhost:8080
```

운영 또는 배포 시 `settings.json`에서 변경 가능해야 한다.

---

## 3. Code 기반 Overlay 조회

API:

```http
GET /api/overlays/code/{code}
```

인증:

```text
불필요
```

목적:

```text
6자리 code로 공개 overlay를 단건 조회한다.
```

요청 예시:

```http
GET http://localhost:8080/api/overlays/code/ABC123
```

응답에는 `overlayJson`이 포함되어야 한다.

---

## 4. Windows Google Login 시작

API:

```http
GET /api/auth/windows/google/start
```

인증:

```text
불필요
```

목적:

```text
Windows Client에서 브라우저 로그인 URL로 사용한다.
```

요청 파라미터:

```text
callbackUrl
state
```

요청 예시:

```http
GET /api/auth/windows/google/start?callbackUrl=http://127.0.0.1:51321/auth/callback&state={state}
```

처리:

```text
Backend가 Google OAuth 로그인으로 redirect한다.
로그인 성공 후 callbackUrl로 accessToken과 state를 전달한다.
```

---

## 5. Localhost Callback

Callback URL 예시:

```text
http://127.0.0.1:51321/auth/callback
```

성공 응답 형태:

```text
http://127.0.0.1:51321/auth/callback?accessToken={jwt}&state={state}
```

실패 응답 형태:

```text
http://127.0.0.1:51321/auth/callback?error=LOGIN_FAILED&state={state}
```

---

## 6. 내 정보 조회

API:

```http
GET /api/auth/me
Authorization: Bearer {accessToken}
```

인증:

```text
필요
```

목적:

```text
토큰이 유효한지 확인하고 현재 로그인 사용자를 조회한다.
```

응답 예시:

```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "Kim Jongmin",
    "email": "user@example.com",
    "profileImageUrl": "https://..."
  },
  "message": "ok"
}
```

---

## 7. 내 Library 조회

API:

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

응답 필드:

```text
libraryId
savedAt
overlay.id
overlay.overlayId
overlay.code
overlay.name
overlay.platform
overlay.game
overlay.thumbnailUrl
```

---

## 8. Overlay 상세 조회

Library에서 선택한 overlay를 적용하기 위해 상세 조회가 필요할 수 있다.

API:

```http
GET /api/overlays/{id}
```

인증:

```text
선택
```

정책:

```text
공개 overlay라면 비로그인도 가능하게 둔다.
단, isSaved 같은 사용자별 필드는 로그인 시에만 계산한다.
```

응답:

```text
overlayJson을 포함해야 Windows Client가 바로 적용할 수 있다.
```

---

## 9. 서버 연결 테스트

권장 API:

```http
GET /api/platforms
```

또는 Actuator 사용 시:

```http
GET /actuator/health
```

결과 처리:

```text
성공 → Connected
실패 → Connection Failed
```

---

## 10. 공통 응답 형태

권장 형태:

```json
{
  "success": true,
  "data": {},
  "message": "ok"
}
```

에러 형태:

```json
{
  "success": false,
  "code": "ERROR_CODE",
  "message": "error message"
}
```

---

## 11. API Client 책임

`MspApiClient`는 다음을 전담한다.

```text
Backend API 호출
Authorization Header 처리
공통 응답 파싱
에러 응답 처리
서버 URL 조합
타임아웃 처리
```

---

## 12. 완료 기준

```text
[ ] Server URL 기반으로 API 호출 가능
[ ] GET /api/overlays/code/{code} 호출 가능
[ ] GET /api/auth/me 호출 가능
[ ] GET /api/library 호출 가능
[ ] GET /api/overlays/{id} 호출 가능
[ ] Authorization Bearer token 적용 가능
[ ] 공통 응답 파싱 가능
[ ] 400/401/404/500 에러 처리 가능
```
