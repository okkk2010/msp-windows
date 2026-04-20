# Windows Client Overview / Scope 명세서

## 1. 문서 목적

이 문서는 msp overlay Windows Client의 현재 구현 범위와 제외 범위를 정의한다.

Windows Client는 전체 시스템에서 실제 오버레이 실행을 담당하는 핵심 클라이언트지만, 이번 단계에서는 모든 기능을 한 번에 구현하지 않고 **서버에서 오버레이를 가져와 적용하는 기능**에 집중한다.

---

## 2. Windows Client의 전체 역할

장기적으로 Windows Client는 다음 역할을 가진다.

```text
실행 중인 게임 또는 프로그램 창 추적
창 위치와 크기에 맞춰 오버레이 출력
오버레이 편집
로컬 overlay.json 저장 / 불러오기
서버 업로드
내 라이브러리 오버레이 다운로드
JSON 기반 오버레이 렌더링
```

---

## 3. 현재 단계 목표

이번 단계의 핵심 목표는 다음 두 가지다.

```text
비로그인 사용자:
6자리 code를 입력해 공개 overlay를 가져와 적용한다.

로그인 사용자:
Google Login 후 내 library에 저장된 overlay를 조회하고 선택해 적용한다.
```

이번 단계의 Windows Client는 오버레이를 검색하고 탐색하는 앱이 아니라, **이미 알고 있는 code 또는 library를 통해 overlay를 적용하는 실행 앱**으로 동작한다.

---

## 4. 포함 기능

```text
1. 서버 URL 설정
2. 6자리 overlay code 입력
3. code 기반 overlay 조회
4. overlayJson 수신
5. overlayJson 파싱
6. Windows OverlayWindow에 적용
7. Google Login
8. localhost callback 기반 인증 처리
9. JWT accessToken 저장
10. 로그인 사용자 정보 조회
11. 내 library 조회
12. library overlay 선택 및 적용
13. 최근 적용 overlay 로컬 캐시 저장
14. 기본 에러 메시지 처리
```

---

## 5. 제외 기능

```text
1. Windows Client에서 overlay 업로드
2. Windows Client에서 library 저장
3. Windows Client에서 overlay 수정 후 서버 반영
4. 자동 게임 프리셋 적용
5. 추천 overlay 탐색
6. 전체 검색 UI
7. 댓글 / 좋아요 / 신고
8. 관리자 기능
9. Android 연동
10. image element 렌더링
11. text element 렌더링
12. 복잡한 캐시 동기화
13. Windows Credential Manager 저장
```

---

## 6. 기능 영역 분리

Windows Client는 다음 5개 기능 영역으로 분리한다.

```text
1. Code Load
2. Google Login
3. My Library
4. Overlay Apply
5. Local Settings / Cache
```

UI 관점에서는 위 기능들을 기존 Remote Control UI에 최소 패널 형태로 추가한다.

---

## 7. 개발 방향

- 기능을 한 화면에 과하게 넣지 않는다.
- Server URL, Code Load, Account, My Library, Overlay Status 정도만 먼저 추가한다.
- Windows Client는 서버 데이터의 소비자 역할에 집중한다.
- 오버레이 검색/업로드/라이브러리 저장은 Web Frontend의 역할로 둔다.
