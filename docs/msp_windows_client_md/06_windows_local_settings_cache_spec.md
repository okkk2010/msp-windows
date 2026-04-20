# Windows Client Local Settings / Cache 명세서

## 1. 목적

Windows Client는 서버 URL, 로그인 토큰, 최근 적용 overlay 정보를 로컬에 저장한다.

이 기능의 목적은 다음과 같다.

```text
서버 URL 유지
로그인 상태 복원
최근 적용 overlay 복원
서버 연결 실패 시 마지막 overlay 재사용 가능
디버깅용 overlayJson 보관
```

---

## 2. 저장 위치

기본 저장 루트:

```text
%AppData%\msp-overlay\
```

설정 파일:

```text
%AppData%\msp-overlay\settings.json
```

캐시 폴더:

```text
%AppData%\msp-overlay\cache\overlays\{overlayId}\overlay.json
```

예시:

```text
C:\Users\{UserName}\AppData\Roaming\msp-overlay\cache\overlays\ovl_001\overlay.json
```

---

## 3. settings.json 구조

```json
{
  "serverBaseUrl": "http://localhost:8080",
  "accessToken": null,
  "lastSelectedOverlayId": null,
  "cacheEnabled": true
}
```

---

## 4. 설정 필드 설명

| 필드 | 설명 |
|---|---|
| serverBaseUrl | Backend Server 기본 URL |
| accessToken | 로그인 성공 후 받은 JWT |
| lastSelectedOverlayId | 마지막으로 적용한 overlayId |
| cacheEnabled | 로컬 캐시 사용 여부 |

---

## 5. settings.json 생성 정책

```text
앱 실행 시 settings.json을 읽는다.
파일이 없으면 기본 설정으로 생성한다.
파일이 깨졌으면 백업 후 기본 설정으로 재생성한다.
Server URL 변경 시 즉시 저장한다.
Logout 시 accessToken을 제거한다.
Overlay 적용 성공 시 lastSelectedOverlayId를 갱신한다.
```

---

## 6. 캐시 저장 시점

```text
code로 overlay 로드 성공 시
library overlay 적용 성공 시
```

저장 대상:

```text
overlay.json 원본
필요 시 overlay 요약 메타데이터
```

---

## 7. 캐시 사용 정책

이번 단계에서는 자동 동기화는 하지 않는다.

```text
서버에서 성공적으로 받은 JSON을 저장한다.
다음 실행 시 lastSelectedOverlayId가 있으면 캐시에서 복원할 수 있다.
수동 새로고침 시 서버 데이터를 다시 받는다.
```

---

## 8. Token 저장 정책

개발 단계:

```text
settings.json에 accessToken 저장
```

추후 개선:

```text
Windows Credential Manager 사용 검토
```

주의:

```text
현재 단계에서는 구현 속도를 우선한다.
배포 단계에서는 보안 저장소로 이전하는 것을 권장한다.
```

---

## 9. 관련 클래스

```text
AppSettings
AppSettingsService
AuthTokenStore
OverlayCacheService
```

---

## 10. 완료 기준

```text
[ ] 앱 실행 시 settings.json 로드
[ ] settings.json 없으면 기본 생성
[ ] Server URL 저장 가능
[ ] accessToken 저장 가능
[ ] Logout 시 accessToken 삭제
[ ] overlay 적용 성공 시 cache 저장
[ ] lastSelectedOverlayId 저장
[ ] 앱 재시작 시 최근 overlay 복원 가능
```
