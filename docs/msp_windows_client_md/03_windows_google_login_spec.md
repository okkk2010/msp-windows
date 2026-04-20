# Windows Client Google Login 명세서

## 1. 목적

Windows Client에서 Google Login을 지원하여 로그인 사용자의 library를 조회하고, 저장된 overlay를 선택해 적용할 수 있도록 한다.

Windows Client는 Google OAuth Client Secret을 직접 보관하지 않는다. OAuth 처리는 Backend Server가 담당하고, Windows Client는 브라우저 로그인 결과를 localhost callback으로 받아 JWT를 저장한다.

---

## 2. 로그인 방식

```text
localhost callback 방식
```

핵심 구조:

```text
Windows Client
→ Backend OAuth Start URL 열기
→ Google OAuth
→ Backend OAuth Success
→ localhost callback redirect
→ Windows Client token 저장
```

---

## 3. 로그인 흐름

```text
1. Windows Client에서 Login 버튼 클릭
2. Windows Client가 임시 localhost callback listener 실행
3. 기본 브라우저로 Backend Login URL 열기
4. 사용자가 Google 로그인 진행
5. Backend가 로그인 성공 후 localhost callback URL로 redirect
6. Windows Client가 callback 요청에서 accessToken 수신
7. state 검증
8. accessToken 저장
9. /api/auth/me 호출로 사용자 정보 확인
10. UI에 로그인 상태 표시
```

---

## 4. Login Start API

```http
GET /api/auth/windows/google/start
```

인증:

```text
불필요
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

---

## 5. Localhost Callback

권장 주소:

```text
127.0.0.1
```

권장 포트:

```text
51321
```

Callback path:

```text
/auth/callback
```

전체 Callback URL:

```text
http://127.0.0.1:51321/auth/callback
```

포트 충돌을 대비해 사용 가능한 포트를 자동 탐색할 수 있게 구현하는 것이 좋다.

---

## 6. Callback 성공 / 실패 형태

성공 예시:

```text
http://127.0.0.1:51321/auth/callback?accessToken={jwt}&state={state}
```

실패 예시:

```text
http://127.0.0.1:51321/auth/callback?error=LOGIN_FAILED&state={state}
```

---

## 7. state 검증

로그인 요청 시 Windows Client는 랜덤 state 값을 생성한다.

```text
state = random string
```

callback으로 돌아온 state가 최초 생성한 state와 같아야 한다.

목적:

```text
잘못된 callback 또는 외부 요청 방지
```

실패 처리:

```text
state 불일치 시 로그인 실패 처리
accessToken 저장하지 않음
```

---

## 8. Token 저장

개발 단계 저장 위치:

```text
%AppData%\msp-overlay\settings.json
```

저장 예시:

```json
{
  "serverBaseUrl": "http://localhost:8080",
  "accessToken": "jwt...",
  "lastSelectedOverlayId": "ovl_001",
  "cacheEnabled": true
}
```

주의:

```text
개발 단계에서는 settings.json 저장을 허용한다.
운영 또는 배포 단계에서는 Windows Credential Manager 사용을 검토한다.
```

---

## 9. 앱 시작 시 로그인 복원

```text
1. settings.json 로드
2. accessToken 존재 여부 확인
3. accessToken이 있으면 GET /api/auth/me 호출
4. 성공하면 로그인 상태로 UI 표시
5. 실패하면 accessToken 삭제 또는 만료 상태 표시
```

---

## 10. UI 요소

```text
Button: loginGoogleButton
Button: logoutButton
Label: currentUserLabel
Label: loginStatusLabel
```

비로그인 상태 표시:

```text
Not logged in
```

로그인 상태 표시:

```text
Logged in as {userName}
```

---

## 11. Logout 처리

```text
1. settings.json에서 accessToken 제거
2. 메모리 token 제거
3. currentUser 초기화
4. library 목록 초기화
5. UI 비로그인 상태로 변경
```

---

## 12. 완료 기준

```text
[ ] Login 버튼 클릭 시 브라우저 열림
[ ] localhost callback listener 정상 시작
[ ] Google 로그인 성공 후 callback 수신
[ ] state 검증 성공
[ ] accessToken 저장
[ ] /api/auth/me 성공
[ ] 앱 재시작 후 로그인 상태 복원
[ ] Logout 시 token 삭제
[ ] 토큰 만료 시 비로그인 상태 전환
```
