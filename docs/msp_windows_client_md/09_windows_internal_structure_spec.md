# Windows Client Internal Structure 명세서

## 1. 문서 목적

이 문서는 Windows Client 내부 클래스와 폴더 구조 권장안을 정의한다.

목표는 UI 코드에 서버 통신, 인증, 파싱, 캐시 로직이 뒤섞이지 않도록 기능 책임을 분리하는 것이다.

---

## 2. 권장 폴더 구조

```text
MspOverlay.Windows
 ┣ Api
 ┃ ┣ MspApiClient.cs
 ┃ ┣ ApiResponse.cs
 ┃ ┗ Dtos
 ┃   ┣ OverlayDetailResponse.cs
 ┃   ┣ LibraryItemResponse.cs
 ┃   ┗ UserMeResponse.cs
 ┣ Auth
 ┃ ┣ WindowsAuthService.cs
 ┃ ┣ LocalCallbackListener.cs
 ┃ ┗ AuthTokenStore.cs
 ┣ Settings
 ┃ ┣ AppSettings.cs
 ┃ ┗ AppSettingsService.cs
 ┣ Overlay
 ┃ ┣ Models
 ┃ ┃ ┣ OverlayDocument.cs
 ┃ ┃ ┣ OverlayCanvas.cs
 ┃ ┃ ┣ OverlaySettings.cs
 ┃ ┃ ┣ OverlayGame.cs
 ┃ ┃ ┣ OverlayMeta.cs
 ┃ ┃ ┣ OverlayElementBase.cs
 ┃ ┃ ┣ RectElement.cs
 ┃ ┃ ┣ CircleElement.cs
 ┃ ┃ ┗ LineElement.cs
 ┃ ┣ OverlayJsonParser.cs
 ┃ ┣ OverlayApplyService.cs
 ┃ ┗ OverlayCacheService.cs
 ┣ UI
 ┃ ┗ RemoteControlForm.cs
 ┗ Program.cs
```

---

## 3. API Client

클래스:

```text
MspApiClient
```

책임:

```text
Backend API 호출 전담
Authorization Header 처리
공통 응답 파싱
에러 응답 처리
```

메서드:

```text
Task<ApiResponse<OverlayDetailResponse>> GetOverlayByCodeAsync(string code)
Task<ApiResponse<UserMeResponse>> GetMeAsync()
Task<ApiResponse<List<LibraryItemResponse>>> GetMyLibraryAsync()
Task<ApiResponse<OverlayDetailResponse>> GetOverlayDetailAsync(long id)
```

---

## 4. Auth Service

클래스:

```text
WindowsAuthService
```

책임:

```text
Google Login 시작
localhost callback listener 실행
callback token 수신
state 검증
token 저장
로그아웃 처리
```

메서드:

```text
Task<LoginResult> LoginWithGoogleAsync()
Task LogoutAsync()
Task<UserMeResponse?> RestoreLoginAsync()
```

---

## 5. Local Callback Listener

클래스:

```text
LocalCallbackListener
```

책임:

```text
127.0.0.1 기반 임시 HTTP listener 실행
/auth/callback 요청 수신
accessToken / error / state 파라미터 추출
로그인 완료 후 listener 종료
```

---

## 6. Token Store

클래스:

```text
AuthTokenStore
```

책임:

```text
accessToken 저장
accessToken 로드
accessToken 삭제
```

메서드:

```text
string? LoadToken()
void SaveToken(string token)
void ClearToken()
```

---

## 7. Settings Service

클래스:

```text
AppSettingsService
```

책임:

```text
settings.json 로드
settings.json 저장
serverBaseUrl 관리
lastSelectedOverlayId 관리
```

관련 모델:

```text
AppSettings
```

---

## 8. Overlay Parser

클래스:

```text
OverlayJsonParser
```

책임:

```text
overlayJson 문자열 또는 JsonElement를 OverlayDocument로 변환
element type별 파싱
기본 검증 수행
```

---

## 9. Overlay Apply Service

클래스:

```text
OverlayApplyService
```

책임:

```text
OverlayDocument를 OverlayWindow 또는 OverlayManager에 전달
현재 적용 overlay 상태 관리
Invalidate 호출
```

---

## 10. Overlay Cache Service

클래스:

```text
OverlayCacheService
```

책임:

```text
overlayJson 캐시 저장
캐시 불러오기
최근 overlay 복원
```

---

## 11. UI Form

클래스:

```text
RemoteControlForm
```

책임:

```text
사용자 입력 처리
버튼 이벤트 처리
서비스 호출
상태 메시지 표시
현재 overlay 상태 표시
```

주의:

```text
RemoteControlForm에 API 호출, JSON 파싱, 토큰 저장 로직을 직접 넣지 않는다.
각 기능은 Service / Client 클래스로 분리한다.
```

---

## 12. 구현 순서

```text
1. AppSettings / AppSettingsService
2. ApiResponse / DTO / MspApiClient
3. Overlay 모델 / OverlayJsonParser
4. OverlayApplyService
5. OverlayCacheService
6. LocalCallbackListener
7. WindowsAuthService / AuthTokenStore
8. RemoteControlForm UI 연결
```

---

## 13. 완료 기준

```text
[ ] API 호출 로직이 MspApiClient로 분리됨
[ ] 인증 로직이 WindowsAuthService로 분리됨
[ ] settings.json 로직이 AppSettingsService로 분리됨
[ ] overlayJson 파싱 로직이 OverlayJsonParser로 분리됨
[ ] 렌더링 적용 로직이 OverlayApplyService로 분리됨
[ ] 캐시 로직이 OverlayCacheService로 분리됨
[ ] UI는 서비스 호출과 상태 표시만 담당함
```
