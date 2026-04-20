# Windows Client Exception / Test / Completion 명세서

## 1. 문서 목적

이 문서는 Windows Client 연동 작업에서 처리해야 할 예외 상황, 테스트 체크리스트, 완료 기준을 정의한다.

---

## 2. 예외 처리 명세

## 2.1 서버 연결 실패

조건:

```text
서버가 꺼져 있음
serverBaseUrl 잘못됨
네트워크 오류
```

메시지:

```text
서버에 연결할 수 없습니다.
Server connection failed.
```

---

## 2.2 Code 형식 오류

조건:

```text
code가 비어 있음
6자리가 아님
허용되지 않는 문자가 포함됨
```

메시지:

```text
올바른 6자리 오버레이 코드를 입력하세요.
```

---

## 2.3 Code 조회 실패

조건:

```text
404 Not Found
```

메시지:

```text
해당 코드의 오버레이를 찾을 수 없습니다.
```

---

## 2.4 로그인 실패

조건:

```text
callback error 수신
state 불일치
accessToken 없음
me API 실패
```

메시지:

```text
로그인에 실패했습니다.
```

---

## 2.5 토큰 만료

조건:

```text
401 Unauthorized
```

처리:

```text
accessToken 삭제
UI 비로그인 상태로 전환
로그인이 필요하다는 메시지 표시
```

메시지:

```text
로그인이 만료되었습니다. 다시 로그인해주세요.
```

---

## 2.6 Library 조회 실패

조건:

```text
비로그인 상태
401 Unauthorized
서버 오류
```

메시지:

```text
라이브러리를 불러올 수 없습니다.
```

---

## 2.7 지원하지 않는 플랫폼

조건:

```text
overlayJson.platform != "windows"
```

메시지:

```text
Windows에서 지원하지 않는 오버레이입니다.
```

---

## 2.8 지원하지 않는 Element Type

조건:

```text
type이 rect, circle, line이 아님
```

메시지:

```text
지원하지 않는 오버레이 요소가 포함되어 있습니다.
```

---

# 3. 테스트 체크리스트

## 3.1 Code Load

```text
[ ] 정상 code 입력 시 overlay 조회 성공
[ ] 소문자 code 입력 시 대문자 변환
[ ] 공백 포함 code 입력 시 trim 처리
[ ] 6자리 미만 code 차단
[ ] 존재하지 않는 code 안내
[ ] windows platform overlay 적용 성공
[ ] android platform overlay 적용 차단
[ ] invalid overlayJson 차단
```

---

## 3.2 Login

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

---

## 3.3 Library

```text
[ ] 비로그인 상태에서 library 조회 시 로그인 필요 안내
[ ] 로그인 상태에서 library 조회 성공
[ ] library 비어 있을 때 빈 상태 표시
[ ] library overlay 선택 가능
[ ] 선택 overlay 적용 성공
[ ] 서버 오류 시 실패 메시지 표시
```

---

## 3.4 Rendering

```text
[ ] rect 렌더링
[ ] circle 렌더링
[ ] line 렌더링
[ ] opacity 적용
[ ] zIndex 순서 적용
[ ] visible=false 요소 미표시
[ ] 게임 창 크기에 따른 스케일 적용
[ ] 창 크기 변경 시 재계산
```

---

## 4. 구현 순서별 완료 기준

## 4.1 설정 저장 구조 추가

작업:

```text
AppSettings
AppSettingsService
settings.json 생성 / 로드 / 저장
serverBaseUrl 저장
```

완료 기준:

```text
앱 실행 시 settings.json을 읽는다.
없으면 기본 설정으로 생성한다.
Server URL 변경 후 저장 가능하다.
```

---

## 4.2 API Client 추가

작업:

```text
MspApiClient
ApiResponse
DTO 클래스
```

완료 기준:

```text
GET /api/overlays/code/{code} 호출 가능
응답을 DTO로 받을 수 있음
서버 연결 실패 처리 가능
```

---

## 4.3 Code Load UI 추가

작업:

```text
Overlay Code TextBox
Load by Code Button
Status Label
```

완료 기준:

```text
code 입력 후 서버에서 overlay 데이터를 받아온다.
```

---

## 4.4 Overlay JSON Parser 추가

작업:

```text
OverlayDocument 모델
OverlayElement 모델
OverlayJsonParser
```

완료 기준:

```text
서버 응답의 overlayJson을 C# 객체로 변환한다.
rect / circle / line을 구분한다.
기본 검증을 수행한다.
```

---

## 4.5 Overlay Apply 연결

작업:

```text
OverlayApplyService
OverlayWindow.SetOverlayDocument(document)
OverlayWindow.Invalidate()
```

완료 기준:

```text
code로 가져온 overlay가 실제 OverlayWindow에 렌더링된다.
```

---

## 4.6 Local Cache 추가

작업:

```text
OverlayCacheService
```

완료 기준:

```text
적용 성공한 overlayJson이 AppData 경로에 저장된다.
lastSelectedOverlayId가 settings.json에 기록된다.
```

---

## 4.7 Google Login 추가

작업:

```text
WindowsAuthService
LocalCallbackListener
Login Button
Logout Button
```

완료 기준:

```text
Login 버튼 클릭 시 브라우저가 열린다.
Google Login 완료 후 localhost callback으로 token을 받는다.
GET /api/auth/me 호출 성공 시 로그인 상태로 표시된다.
```

---

## 4.8 Library UI 추가

작업:

```text
Refresh Library Button
Library List
Apply Selected Button
```

완료 기준:

```text
로그인 상태에서 내 library 목록을 불러온다.
선택한 overlay를 적용할 수 있다.
```

---

## 5. 최종 완료 기준

이번 Windows Client 연동 작업은 아래 조건을 만족하면 완료로 본다.

```text
1. Windows Client에서 6자리 code를 입력해 overlay를 가져올 수 있다.
2. 가져온 overlayJson을 파싱할 수 있다.
3. rect / circle / line 요소가 OverlayWindow에 출력된다.
4. overlaySettings.opacity가 적용된다.
5. canvas 기준으로 게임 창 크기에 맞게 스케일링된다.
6. Google Login이 localhost callback 방식으로 동작한다.
7. 로그인 후 /api/auth/me로 사용자 정보를 확인할 수 있다.
8. 로그인 사용자는 내 library 목록을 조회할 수 있다.
9. library에서 선택한 overlay를 적용할 수 있다.
10. settings.json에 serverBaseUrl, accessToken, lastSelectedOverlayId가 저장된다.
11. 서버 연결 실패, code 오류, 로그인 실패, JSON 오류를 기본 처리한다.
```
