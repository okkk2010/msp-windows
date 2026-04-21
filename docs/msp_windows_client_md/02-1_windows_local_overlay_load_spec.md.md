# msp-windows (현재 구현 기준) 기능/명세서

> 이 문서는 **현재 코드에 구현되어 있는 내용만**을 기준으로 정리한 명세서입니다. (Google Login, Library 등은 현 시점 코드에 구현되어 있지 않으므로 제외합니다.)

## 1. 개요

현재 애플리케이션은 Windows WinForms 기반의 **간단한 오버레이(투명/클릭 통과) 표시 도구**입니다.

- 실행 시 `RemoteControlForm`(리모트 컨트롤 UI)을 띄웁니다.
- 사용자가 선택한 프로세스(메인 윈도우가 있는 프로세스)의 창 위치/크기를 추적하여, 해당 창 위에 `OverlayForm`을 항상 위(TopMost)로 덮어 씌우듯 표시합니다.
- 오버레이는 클릭을 통과시키며(WS_EX_TRANSPARENT), 단순 도형 UI를 그립니다.
- 오버레이 색상 선택 및 색상 팔레트 저장/불러오기를 지원합니다.
- 로컬 `overlays` 폴더의 overlay JSON 파일을 선택하여 오버레이 렌더링으로 적용할 수 있습니다.

대상 프레임워크:
- `.NET Framework 4.7.2`

---

## 2. 실행 진입점

- `Program.Main()`
  - `RemoteControlForm`을 생성하여 `Application.Run(remoteControl)`로 실행합니다.

---

## 3. UI: RemoteControlForm (오버레이 제어 화면)

클래스:
- `RemoteControlForm : Form`

### 3.1 프로세스 선택

- 컨트롤: `ComboBox processList`
- 동작:
  - 드롭다운을 열거나(`processList.DropDown`) 폼 로드 시 `LoadRunningProcesses()`를 호출합니다.
  - `Process.GetProcesses()` 결과 중 `MainWindowTitle`이 비어있지 않은 항목만 리스트에 표시합니다.
  - 표시용 아이템은 `ProcessItem`을 사용합니다.
    - `ProcessName`: 실제 프로세스 이름(코드에서 사용)
    - `WindowTitle`: 사용자에게 보이는 타이틀(ComboBox 표시)

### 3.2 오버레이 시작/종료

- `오버레이 시작` 버튼
  - 선택된 프로세스가 있으면 `OverlayForm.ShowOverlay(selectedProcess.ProcessName)` 호출
- `오버레이 종료` 버튼
  - `OverlayForm.Instance?.Close()`로 현재 오버레이 폼을 닫음

### 3.3 오버레이 색상 선택 및 팔레트

- `색상 선택` 버튼
  - `ColorDialog`로 색상을 선택
  - 선택 즉시 오버레이에 적용
  - 저장 팔레트에 동일 색상이 없으면 추가(최대 10개)

- 팔레트 표시
  - `FlowLayoutPanel palettePanel`에 저장된 색상 스와치와 HEX 라벨을 표시
  - 스와치 클릭 시 해당 색상을 오버레이에 즉시 적용
  - 스와치 우클릭 메뉴 `삭제`로 팔레트 색상 제거 가능

- 팔레트 제한
  - 최대 10개(`MaxPaletteColors = 10`)

### 3.4 저장된 오버레이 선택/적용

- 컨트롤: `ComboBox overlayList`
- 기본 경로: `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlays")`
- 동작:
  - 오버레이 목록 드롭다운 또는 `새로고침` 버튼에서 `*.json` 파일을 재조회
  - `(기본 오버레이)` 선택 시: 하드코딩 기본 마커 렌더링으로 복귀 (`Renderer.ClearLoadedOverlay()`)
  - 특정 JSON 파일 선택 후 `적용` 클릭 시: `Renderer.LoadOverlayFromFile(filePath)` 호출
  - 오버레이가 실행 중이면 즉시 `Invalidate()` 되어 변경 내용 반영

### 3.5 오버레이 코드 입력/적용 (서버 조회)

- 컨트롤: `TextBox overlayCodeTextBox`, `Button applyOverlayCodeButton`
- code 정책:
  - 입력값 trim 후 대문자 변환
  - `^[A-Z0-9]{6}$` 형식이 아니면 요청 차단
- 동작:
  - `GET http://localhost:8080/api/overlays/code/{code}` 호출
  - 응답 `success`, `data.overlayJson` 확인
  - `overlayJson.platform == "windows"` 검증
  - 검증 성공 시 `overlays/{code}.json`에 저장
  - 저장 후 `LoadOverlayFiles()`로 목록 새로고침
  - 저장된 파일을 자동 선택 후 `적용` 클릭과 동일 경로로 렌더링 반영
  - 실패 시 메시지 표시 및 오류 로그(`E301`) 기록

### 3.6 오류 로그 표시

- `TextBox errorLogTextBox`에 오류 로그를 누적 출력
- `ErrorLogger.OnErrorLogged` 이벤트를 구독하여 UI에 표시

---

## 4. OverlayForm (실제 오버레이 창)

클래스:
- `OverlayForm : Form`

### 4.1 기본 속성

- Borderless(`FormBorderStyle.None`)
- 항상 위(`TopMost = true`)
- 작업표시줄 미표시(`ShowInTaskbar = false`)
- 배경 투명 처리
  - `BackColor`와 `TransparencyKey`를 같은 색으로 맞춰 투명 처리
- 클릭 통과/레이어드 창
  - `CreateParams`에서 `WS_EX_TRANSPARENT`, `WS_EX_LAYERED` 설정
  - `OnHandleCreated`에서 `SetWindowLong`으로 `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST` 적용

### 4.2 게임(대상) 창 추적

- 대상 프로세스명(`targetProcessName`)을 기반으로 주기적으로 윈도우 위치/크기 추적
- 타이머:
  - `System.Windows.Forms.Timer` 사용
  - Interval: `30ms`
  - Tick: `TrackGameWindow`

동작:
- `WindowTracker.GetGameWindowBounds(targetProcessName)`로 대상 창 영역을 얻음
  - 영역이 비어있으면 오버레이를 `Hide()`
  - 영역이 유효하면 오버레이를 `Show()`하고 `Bounds = gameBounds`로 동기화
  - `Invalidate()`로 재렌더링

### 4.3 렌더링

- `OnPaint`에서 `Renderer.DrawOverlayUI(e.Graphics, this.ClientRectangle)` 호출
- 오버레이 도형 색상은 기본적으로 `SettingsManager.SelectedOverlayColor`에 의해 결정됨
- 로컬 overlay JSON을 적용한 경우, JSON 요소별 색상이 우선 사용되며 없으면 현재 선택 색상 사용

### 4.4 단축키(오버레이 닫기)

- `HotkeyManager`를 사용하여 전역 핫키 등록
- 핫키: `ALT + SHIFT + S`
- 핫키 입력 시 `OverlayForm.Close()` 호출

---

## 5. Renderer (현재 오버레이 그리기 방식)

클래스:
- `Renderer`

동작:
- `DrawOverlayUI(Graphics g, Rectangle bounds)`
  - 로드된 overlay JSON이 있으면 JSON 요소를 렌더링
  - 없으면 기존 하드코딩 기본 마커(상/하/좌/우 + 중앙 소형 4개)를 렌더링

overlay JSON 로드:
- `LoadOverlayFromFile(string filePath)`
  - JSON 루트 객체의 `elements`(또는 `Elements`) 배열을 파싱
  - 요소 타입 지원: `rect`/`rectangle`, `circle`, `line`
  - 좌표/크기/두께/채움(`fill`) 및 색상(`color`) 사용
- `ClearLoadedOverlay()`
  - 로드된 오버레이를 해제하고 기본 마커 렌더링으로 전환

---

## 6. WindowTracker (대상 창 영역 계산)

클래스:
- `WindowTracker`

동작 개요:
1. `Process.GetProcessesByName(processName)`로 대상 프로세스 열거
2. `MainWindowHandle`이 없으면 오류 로그 출력 후 계속
3. 우선 `GetClientRect` + `ClientToScreen`으로 클라이언트 영역을 화면 좌표로 변환하여 반환 시도
4. 실패하거나 대체 경로 필요 시:
   - `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS=9)`로 윈도우 프레임 포함 영역 계산 시도
   - 해당 호출 실패 시 `GetWindowRect`로 폴백

오류 로그 코드(현재 코드 기준):
- `E101`: main window handle 없음
- `E102`: window rect 획득 실패

---

## 7. SettingsManager (색상/팔레트 로컬 저장)

클래스:
- `SettingsManager`

### 7.1 저장 위치

현재 구현은 AppData가 아니라, **실행 파일이 있는 폴더(BaseDirectory)**에 저장합니다.

- 오버레이 색상 파일: `overlaySettings.json`
- 팔레트 파일: `colorPalette.json`

경로:
- `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "overlaySettings.json")`
- `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "colorPalette.json")`

### 7.2 저장/로드 포맷(현재 구현)

- 오버레이 색상 저장
  - `SaveOverlayColor(Color color)`
  - JSON Serializer를 통해 `ColorArgb` 정수(ARGB) 저장

- 오버레이 색상 로드
  - `LoadOverlayColor()`
  - JSON 파싱으로 `ColorArgb` 또는 `SelectedOverlayColorArgb` 정수 추출

- 팔레트 저장
  - `SaveColorPalette(List<Color> palette)`
  - JSON Serializer로 `Colors: [argb1, argb2, ...]` 저장

- 팔레트 로드
  - `LoadColorPalette()`
  - JSON 파싱으로 `Colors` 또는 `SavedColors` 배열을 추출

---

## 8. ErrorLogger (오류 이벤트 전달)

클래스:
- `ErrorLogger`

동작:
- `LogError(string errorCode, string message)` 호출 시
  - `[{errorCode}] {message}` 형태로 메시지를 생성
  - `OnErrorLogged` 이벤트로 전달

---

## 9. 현재 구현의 제한/미구현(현 상태 명시)

현재 코드 기준으로 아래 기능은 구현되어 있지 않습니다.

- 서버 API 연동(로그인, 라이브러리 등)
- settings.json(AppData) 기반의 서버 URL/토큰/캐시 관리
- 오버레이 opacity/zIndex/visible 등 스펙 기반 처리

---

## 10. 사용 방법(현재 구현)

1. 프로그램 실행 → `Oculo Remote Control` 창 표시
2. `게임 프로세스 선택`에서 대상 프로세스 선택
3. `오버레이 시작` 클릭 → 선택한 프로세스의 창 위에 오버레이 표시
4. `오버레이 코드 입력` 후 `코드 적용` 클릭 시 서버에서 오버레이 조회/저장/적용
5. `저장된 오버레이 선택`에서 JSON 오버레이를 선택 후 `적용` 클릭 (또는 `(기본 오버레이)`로 복귀)
6. `색상 선택` 또는 팔레트 스와치 클릭으로 오버레이 색상 변경
7. 오버레이 종료 방법
   - 컨트롤 창에서 `오버레이 종료` 클릭
   - 또는 오버레이 활성 중 `ALT + SHIFT + S` 입력
