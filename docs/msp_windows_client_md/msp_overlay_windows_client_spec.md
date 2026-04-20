# msp overlay Windows Client 총괄 명세서

## 1. 문서 목적

이 문서는 **msp overlay Windows Client 전체 구현을 담당하는 상위 명세서**다.

Windows Client는 서버에 저장된 Overlay JSON을 Windows 환경에서 실제 오버레이로 적용하는 실행 클라이언트이며, 이번 단계에서는 전체 편집기 기능보다 **서버 연동 기반 적용 기능**을 우선 구현한다.

이 문서는 세부 구현 내용을 모두 직접 담기보다, Windows Client가 가져야 할 역할과 구현 틀을 정의하고, 각 틀을 구현할 때 참고해야 하는 하위 명세서를 라우팅하는 역할을 한다.

---

## 2. Windows Client의 현재 역할

Windows Client는 msp overlay 시스템에서 실제 사용자가 게임 화면 위에 오버레이를 적용하는 실행 영역이다.

현재 단계의 핵심 역할은 다음과 같다.

```text
비로그인 사용자:
6자리 overlay code 입력 → 공개 overlay 조회 → overlayJson 적용

로그인 사용자:
Google Login → 내 library 조회 → 저장된 overlay 선택 → overlayJson 적용
```

즉, 이번 단계의 Windows Client는 **오버레이 탐색 앱**이 아니라 **오버레이 적용 앱**으로 설계한다.

---

## 3. 이번 단계 확정 방향

```text
1. Code 조회 API는 별도로 사용한다.
   GET /api/overlays/code/{code}

2. Code 조회 응답에는 overlayJson이 포함되어야 한다.

3. Google Login은 localhost callback 방식으로 처리한다.

4. Token은 개발 단계에서 settings.json에 저장한다.
   추후 Windows Credential Manager로 개선할 수 있다.

5. Library는 조회 / 적용만 구현한다.
   Library 저장은 Web에서 처리한다.

6. UI는 기존 Remote Control에 최소 패널 형태로 추가한다.
```

---

## 4. Windows Client 전체 구현 틀

Windows Client는 다음 기능 영역으로 나누어 구현한다.

| 영역 | 역할 | 참고 문서 |
|---|---|---|
| Overview / Scope | 현재 단계 목표, 포함/제외 범위 정의 | `01_windows_client_overview.md` |
| Code Load | 6자리 코드로 오버레이 조회 및 적용 | `02_windows_code_load_spec.md` |
| Google Login | localhost callback 기반 로그인 처리 | `03_windows_google_login_spec.md` |
| My Library | 로그인 사용자의 라이브러리 조회 및 적용 | `04_windows_library_spec.md` |
| Overlay Apply / Rendering | overlayJson 파싱, 검증, 렌더링 적용 | `05_windows_overlay_apply_rendering_spec.md` |
| Local Settings / Cache | settings.json, 토큰, 최근 overlay 캐시 관리 | `06_windows_local_settings_cache_spec.md` |
| Remote Control UI | 기존 Remote Control에 추가할 최소 UI 구성 | `07_windows_remote_control_ui_spec.md` |
| API Contract | Windows Client가 사용하는 서버 API 계약 | `08_windows_api_contract_spec.md` |
| Internal Structure | C# 클래스, 서비스, 폴더 구조 권장안 | `09_windows_internal_structure_spec.md` |
| Exception / Test / Done | 예외 처리, 테스트 체크리스트, 완료 기준 | `10_windows_exception_test_completion_spec.md` |

---

## 5. 전체 처리 흐름

## 5.1 비로그인 Code Load 흐름

```text
사용자 code 입력
→ code 형식 검증
→ GET /api/overlays/code/{code}
→ overlayJson 수신
→ platform == windows 확인
→ OverlayDocument 파싱
→ OverlayWindow 적용
→ 로컬 캐시 저장
→ 현재 overlay 상태 UI 갱신
```

참고 문서:

- `02_windows_code_load_spec.md`
- `05_windows_overlay_apply_rendering_spec.md`
- `06_windows_local_settings_cache_spec.md`
- `08_windows_api_contract_spec.md`

---

## 5.2 로그인 / Library 적용 흐름

```text
Login 버튼 클릭
→ LocalCallbackListener 시작
→ Backend Google Login URL 브라우저 오픈
→ Google 로그인 완료
→ localhost callback으로 accessToken 수신
→ state 검증
→ settings.json에 token 저장
→ GET /api/auth/me 호출
→ 로그인 상태 표시
→ GET /api/library 호출
→ library 목록 표시
→ 선택한 overlay 적용
```

참고 문서:

- `03_windows_google_login_spec.md`
- `04_windows_library_spec.md`
- `06_windows_local_settings_cache_spec.md`
- `08_windows_api_contract_spec.md`

---

## 6. 구현 우선순위

```text
1. 설정 저장 구조 추가
2. API Client 추가
3. Code Load UI 추가
4. Overlay JSON Parser 추가
5. Overlay Apply 연결
6. Local Cache 추가
7. Google Login 추가
8. Library UI 추가
9. 통합 테스트
```

각 단계의 상세 완료 기준은 `10_windows_exception_test_completion_spec.md`를 기준으로 확인한다.

---

## 7. 현재 단계에서 제외하는 기능

이번 Windows Client 연동 단계에서는 아래 기능을 구현하지 않는다.

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

단, 이후 확장을 막지 않도록 서비스/폴더 구조는 분리해서 설계한다.

---

## 8. 핵심 설계 원칙

- Windows Client는 서버 API와 Overlay JSON 구조를 재사용한다.
- Web은 탐색/저장 중심, Windows Client는 실행/적용 중심으로 역할을 분리한다.
- 현재 단계에서는 `rect`, `circle`, `line`만 렌더링한다.
- `image`, `text`는 지원하지 않는다.
- Token 저장은 개발 단계에서 `settings.json`으로 처리한다.
- UI는 기존 Remote Control 흐름을 해치지 않는 최소 패널 방식으로 추가한다.
- 서버 연결 실패, code 오류, 로그인 실패, JSON 오류는 기본 처리한다.

---

## 9. 최종 완료 기준 요약

Windows Client 연동 작업은 아래 조건을 만족하면 1차 완료로 본다.

```text
1. 6자리 code로 overlay를 가져올 수 있다.
2. 가져온 overlayJson을 C# 객체로 파싱할 수 있다.
3. rect / circle / line 요소가 OverlayWindow에 출력된다.
4. overlaySettings.opacity가 적용된다.
5. canvas 기준으로 게임 창 크기에 맞게 스케일링된다.
6. Google Login이 localhost callback 방식으로 동작한다.
7. /api/auth/me로 로그인 사용자를 확인할 수 있다.
8. 내 library 목록을 조회할 수 있다.
9. library에서 선택한 overlay를 적용할 수 있다.
10. settings.json에 serverBaseUrl, accessToken, lastSelectedOverlayId가 저장된다.
11. 서버 연결 실패, code 오류, 로그인 실패, JSON 오류를 기본 처리한다.
```
