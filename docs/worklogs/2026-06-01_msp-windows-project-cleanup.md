# Task Summary

`msp-windows` 저장소에서 비기준 프로젝트 파일을 제거해 `msp-windows.csproj` 만 기준으로 남도록 정리했다.

# Scope

대체 프로젝트 파일을 삭제하고, 기준 프로젝트 안내 문서와 작업 로그를 갱신했다.

# Changed Files

- `msp-windows/motion sickness prevention program.csproj`
- `README.md`
- `docs/worklogs/_index.md`
- `docs/worklogs/2026-06-01_msp-windows-project-cleanup.md`

# Verification Result

- 솔루션 파일 `msp-windows.slnx` 가 원래부터 `msp-windows.csproj` 만 참조하고 있음을 확인
- 기준 프로젝트 빌드는 이전 단계에서 `dotnet build .\msp-windows\msp-windows.csproj -c Debug` 성공 상태 확인

# Decisions Made

- 비기준 SDK 스타일 프로젝트는 유지하지 않고 제거
- 기준 프로젝트 문구를 README에 남겨 향후 IDE 설정 혼선을 줄이도록 함

# Issues

- `obj\Debug\net8.0-windows` 아래에 비기준 프로젝트의 생성 산출물이 일부 남아 있다
- 일부 기존 문서는 인코딩 문제로 한글이 깨져 보인다

# Next Steps

- 필요하면 다음 작업에서 `obj` 산출물을 정리하고 새 빌드 기준으로 재생성
- 문서 인코딩을 UTF-8로 통일하면서 기준 프로젝트 정보를 관련 명세에도 반영
