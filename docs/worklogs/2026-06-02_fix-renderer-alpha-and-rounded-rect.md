# Task Summary

Windows 렌더러에서 프론트 JSON의 `cornerRadius` 와 `rgba(...)` 색상 알파가 반영되지 않던 문제를 수정했다.

# Scope

`Renderer.cs` 의 사각형 렌더링과 색상 파싱만 수정했다.

# Changed Files

- `msp-windows/Renderer.cs`
- `docs/worklogs/_index.md`
- `docs/worklogs/2026-06-02_fix-renderer-alpha-and-rounded-rect.md`

# Verification Result

- 코드 기준으로 `cornerRadius` 가 실제 `GraphicsPath` 렌더링에 연결되도록 변경
- `rgb(...)`, `rgba(...)`, `#RRGGBB`, `#AARRGGBB` 색상 파싱을 모두 지원하도록 변경

# Decisions Made

- 사각형은 `FillRectangle` / `DrawRectangle` 대신 둥근 사각형 경로로 렌더링
- 색상 알파는 색상 자체의 alpha 와 요소/오버레이 opacity 를 곱하는 기존 규칙을 유지

# Issues

- 실행 중인 디버그 세션이 있으면 `msp-windows.exe` 잠금 때문에 재빌드가 실패할 수 있다

# Next Steps

- 실제 Code Load JSON으로 사각형 라운딩과 반투명 fill이 기대한 시각과 같은지 화면에서 확인
