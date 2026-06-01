# Task Summary

`Code Load` 시 `Invalid object field: canvas` 오류가 나는 원인을 수정하고, 파싱 실패가 나도 `{code}.json` 캐시 파일은 남도록 저장 순서를 조정했다.

# Scope

Windows 클라이언트의 오버레이 JSON 파서와 Code Load 캐시 저장 흐름만 수정했다.

# Changed Files

- `msp-windows/msp-windows.csproj`
- `msp-windows/Overlay/OverlayJsonParser.cs`
- `msp-windows/RemoteControlForm.cs`
- `docs/worklogs/_index.md`
- `docs/worklogs/2026-06-01_fix-code-load-json-parse-and-cache.md`

# Verification Result

- 서버 샘플 `valid-overlay.json` 을 기존 파서로 읽을 때 `canvas` 가 `System.Object` 로 내려오는 현상을 재현
- 파서 변경 후 기준 프로젝트 빌드 성공

# Decisions Made

- 중첩 JSON 객체 파싱은 `DataContractJsonSerializer` 대신 `JavaScriptSerializer` 기반으로 전환
- Code Load 는 캐시 저장 후 파싱/적용 순서로 변경해 원본 응답 보존을 우선

# Issues

- 일부 기존 문서는 인코딩 문제로 한글이 깨져 보인다
- 서버 응답이 구조적으로 잘못된 경우에는 캐시 파일은 남아도 적용은 실패할 수 있다

# Next Steps

- 필요하면 캐시 파일 경로를 UI에서 직접 열어볼 수 있게 진단 기능 추가
- Code Load 실패 시 캐시 저장 경로를 상태 메시지나 로그에 함께 표시
