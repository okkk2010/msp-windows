# Task Summary

`msp-windows` 저장소의 기준 프로젝트를 `msp-windows.csproj` 로 확정하고, VS Code 빌드 흐름을 해당 프로젝트 기준으로 고정했다.

# Scope

`.vscode` 빌드 태스크와 `README.md` 를 정리하고 작업 로그 디렉터리를 추가했다.

# Changed Files

- `.vscode/tasks.json`
- `README.md`
- `docs/worklogs/_index.md`
- `docs/worklogs/2026-06-01_msp-windows-canonical-project.md`

# Verification Result

- `dotnet build .\msp-windows\msp-windows.csproj -c Debug` 성공
- VS Code `launch.json` 이 이미 `msp-windows.exe` 를 가리키는 상태임을 확인

# Decisions Made

- 기준 프로젝트는 구형 WinForms `.NET Framework 4.7.2` 프로젝트인 `msp-windows.csproj` 로 유지
- `tasks.json` 은 `dotnet msbuild` 대신 단순한 `dotnet build` 호출로 정리
- 대체 프로젝트 파일은 이번 작업에서 삭제하지 않고 비기준 상태로만 명시

# Issues

- 동일 폴더에 `motion sickness prevention program.csproj` 가 남아 있어 IDE에서 혼선을 줄 여지는 계속 존재한다
- 기존 일부 문서는 인코딩 문제로 내용이 깨져 보인다

# Next Steps

- 필요하면 다음 작업에서 비기준 프로젝트 파일을 정리하거나 별도 보관 위치로 이동
- 문서 인코딩을 UTF-8로 통일해 프로젝트 기준 정보를 문서에도 반영
