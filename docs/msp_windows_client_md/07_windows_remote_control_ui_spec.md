# Windows Client Remote Control UI 명세서

## 1. 목적

이번 단계의 UI는 기존 Remote Control UI에 최소 패널 형태로 추가한다.

큰 별도 관리창을 새로 만들기보다, 현재 Windows Client의 흐름에 자연스럽게 들어가는 구조를 우선한다.

---

## 2. UI 구성 원칙

```text
기존 Remote Control을 유지한다.
서버 연동 기능은 작은 패널로 추가한다.
오버레이 탐색/검색 화면은 만들지 않는다.
Code Load와 Library Apply 중심으로 구성한다.
상태 메시지는 명확하고 짧게 표시한다.
```

---

## 3. 전체 UI 영역

```text
[Server]
- Server URL 입력
- Save 버튼
- Connection Test 버튼

[Code Load]
- Overlay Code 입력
- Load by Code 버튼

[Account]
- Login with Google 버튼
- Logout 버튼
- Current User 표시

[My Library]
- Refresh Library 버튼
- Library 목록
- Apply Selected 버튼

[Overlay Status]
- Current Overlay Name
- Current Overlay Code
- Target Window Name
- Overlay ON/OFF 상태
```

---

## 4. Server 영역

구성 요소:

```text
TextBox: serverBaseUrlTextBox
Button: saveServerUrlButton
Button: testConnectionButton
Label: serverStatusLabel
```

기본값:

```text
http://localhost:8080
```

동작:

```text
Save 버튼 클릭
→ settings.json에 serverBaseUrl 저장

Connection Test 클릭
→ GET /api/platforms 또는 GET /actuator/health 호출
→ 성공 시 "Connected"
→ 실패 시 "Connection Failed"
```

---

## 5. Code Load 영역

구성 요소:

```text
TextBox: overlayCodeTextBox
Button: loadByCodeButton
Label: codeLoadStatusLabel
```

동작:

```text
사용자 code 입력
→ Load by Code 클릭
→ code 형식 검증
→ 서버 API 호출
→ overlayJson 파싱
→ OverlayWindow 적용
→ 상태 메시지 표시
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

## 6. Account 영역

구성 요소:

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

Login 버튼 동작:

```text
1. localhost callback listener 시작
2. Backend login URL 생성
3. 기본 브라우저 열기
4. callback 수신 대기
5. token 저장
6. me API 호출
7. UI 갱신
```

Logout 버튼 동작:

```text
1. settings.json에서 accessToken 제거
2. 메모리 token 제거
3. currentUser 초기화
4. library 목록 초기화
5. UI 비로그인 상태로 변경
```

---

## 7. My Library 영역

구성 요소:

```text
Button: refreshLibraryButton
ListBox 또는 DataGridView: libraryList
Button: applySelectedOverlayButton
Label: libraryStatusLabel
```

Library 표시 필드:

```text
overlay name
code
platform
game
```

DataGridView 추천 컬럼:

```text
Name | Code | Platform | Game
```

동작:

```text
Refresh Library 클릭
→ accessToken 확인
→ GET /api/library 호출
→ 목록 표시
```

```text
Apply Selected 클릭
→ 선택된 overlay의 id 또는 code 확인
→ overlay 상세 조회
→ overlayJson 파싱
→ OverlayWindow 적용
```

---

## 8. Overlay Status 영역

표시 항목:

```text
Current Overlay Name
Current Overlay Code
Target Window Name
Overlay ON/OFF 상태
```

목적:

```text
현재 어떤 overlay가 어떤 창에 적용되어 있는지 빠르게 확인한다.
```

---

## 9. 완료 기준

```text
[ ] Server URL 입력/저장 가능
[ ] Connection Test 가능
[ ] Code 입력 후 Load 가능
[ ] 로그인/로그아웃 UI 동작
[ ] 현재 사용자 표시
[ ] Library 목록 표시
[ ] 선택 overlay 적용 가능
[ ] 현재 overlay 상태 표시
```
