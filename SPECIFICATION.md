# Oculo - 멀미 방지 프로그램 기능 및 목적 상세 명세서

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| **프로젝트명** | Oculo (motion-sickness-prevention-program) |
| **플랫폼** | Windows (WinForms, .NET 8.0) |
| **개발 언어** | C# |
| **빌드 시스템** | .NET SDK (MSBuild) |

---

## 2. 목적

### 2.1 배경

게임이나 영상 시청 중 화면 전체가 움직이면 뇌와 전정 기관(귀) 사이에 감각 불일치가 발생하여 멀미(Motion Sickness)가 유발된다. 이를 완화하는 가장 효과적인 방법 중 하나는 **시각적 기준점(fixation point)** 을 화면에 고정 표시하여 뇌가 신체 위치를 안정적으로 인식하도록 돕는 것이다.

### 2.2 프로그램 목적

Oculo는 게임 화면 위에 **투명 오버레이** 창을 띄워 고정된 시각적 기준 마커를 표시함으로써 사용자가 게임 플레이 중 겪는 멀미 증상을 예방하거나 완화한다.

- 게임 실행 중 화면 위에 클릭-통과(click-through) 투명 오버레이를 표시한다.
- 오버레이는 게임 화면의 상·하·좌·우 및 중앙 부근에 사각형 마커를 그려 고정 기준점을 제공한다.
- 게임 플레이를 전혀 방해하지 않으면서 작동한다.

---

## 3. 시스템 요구사항

| 항목 | 요구사항 |
|------|---------|
| **운영체제** | Windows 10 이상 (Windows Forms 및 DWM API 사용) |
| **런타임** | .NET 8.0 Windows |
| **권한** | 일반 사용자 권한 (핫키 등록을 위해 관리자 권한 불필요) |
| **의존성** | user32.dll, dwmapi.dll (OS 기본 포함) |

---

## 4. 구성 요소 및 파일 목록

```
motion-sickness-prevention-program/
├── Program.cs              # 앱 진입점
├── OverlayForm.cs          # 오버레이 창 구현
├── RemoteControlForm.cs    # 컨트롤 패널 UI
├── Renderer.cs             # GDI+ 그래픽 렌더링
├── WindowTracker.cs        # 게임 창 위치 추적
├── WinApiHelper.cs         # Windows API 유틸리티
├── HotkeyManager.cs        # 전역 단축키 관리
├── SettingsManager.cs      # 설정 저장·불러오기 (JSON)
└── ErrorLogger.cs          # 오류 로깅 시스템
```

---

## 5. 기능 상세 명세

### 5.1 오버레이 창 (`OverlayForm`)

| 속성 | 값 |
|------|-----|
| 창 스타일 | WS_EX_LAYERED (레이어드 창), WS_EX_TRANSPARENT (클릭 통과), WS_EX_TOPMOST (항상 위) |
| 배경색 | Lime (투명 키 색상, 실제로는 투명하게 표시됨) |
| 갱신 주기 | 30ms (약 33fps) 타이머 기반 위치 추적 |
| 이중 버퍼링 | 활성화 (깜빡임 방지) |

#### 기능 목록

1. **투명 클릭-통과 오버레이 표시**
   - 게임 창 위에 겹쳐져 표시되며 마우스 클릭·드래그가 그대로 게임에 전달된다.
   - 투명 키(Lime)를 사용하여 배경을 완전 투명 처리한다.

2. **게임 창 실시간 추적**
   - 30ms 간격으로 대상 프로세스의 창 위치와 크기를 계산한다.
   - 게임 창이 이동·크기 조정될 경우 오버레이가 즉시 따라 움직인다.

3. **마커 렌더링**
   - `Renderer.DrawOverlayUI()` 를 호출하여 GDI+로 마커를 그린다.
   - 렌더링 마커 종류 및 위치:

     | 마커 종류 | 크기 | 위치 |
     |---------|------|------|
     | 대형 사각형 ×4 | 50×50 px | 상·하·좌·우 (창 가장자리 기준) |
     | 소형 사각형 ×4 | 10×10 px | 중앙 십자 패턴 |

4. **전역 단축키 (ALT+SHIFT+S)**
   - 오버레이가 활성화된 상태에서 `ALT+SHIFT+S` 를 누르면 오버레이가 즉시 종료된다.
   - 게임 중 키보드에서 손을 떼지 않고도 오버레이를 끌 수 있다.

5. **싱글톤 인스턴스**
   - `OverlayForm.Instance` 정적 속성으로 단 하나의 오버레이만 생성·관리된다.
   - 중복 오버레이 생성을 방지한다.

---

### 5.2 컨트롤 패널 (`RemoteControlForm`)

컨트롤 패널은 애플리케이션 메인 창("Oculo Remote Control")으로 오버레이를 설정하고 제어하는 UI를 제공한다. 창 크기는 370×520 px이다.

#### UI 구성 요소

| 컴포넌트 | 설명 |
|---------|------|
| 프로세스 드롭다운 | 현재 실행 중인 창 있는 프로세스 목록 표시 |
| 오버레이 시작 버튼 | 선택한 프로세스에 오버레이를 붙임 |
| 오버레이 닫기 버튼 | 현재 활성 오버레이를 종료 |
| 색상 선택 버튼 | ColorDialog를 열어 사용자 정의 색상 선택 |
| 색상 팔레트 패널 | 저장된 색상 최대 10개를 색상 버튼으로 표시 |
| 오류 로그 텍스트박스 | 런타임 오류 메시지를 실시간으로 표시 |

#### 기능 목록

1. **프로세스 목록 불러오기**
   - 애플리케이션 시작 및 드롭다운 클릭 시 `Process.GetProcesses()` 로 실행 중인 프로세스를 조회한다.
   - 메인 윈도우 핸들이 존재하고 윈도우 제목이 있는 프로세스만 표시한다.

2. **오버레이 시작 / 닫기**
   - "오버레이 시작": 선택한 프로세스 이름을 `OverlayForm`에 전달하여 오버레이를 생성하고 표시한다.
   - "오버레이 닫기": 현재 활성화된 `OverlayForm` 인스턴스를 닫는다.

3. **색상 선택 및 적용**
   - `ColorDialog`를 통해 사용자가 임의의 색상을 선택한다.
   - 선택한 색상이 `SettingsManager`를 통해 파일에 저장되고 즉시 오버레이에 반영된다.
   - 새 색상은 팔레트에 자동 추가된다(최대 10개, 초과 시 가장 오래된 색상 제거).

4. **색상 팔레트 관리**
   - 저장된 색상을 패널에 컬러 버튼으로 표시한다.
   - **좌클릭**: 해당 색상을 현재 오버레이 색상으로 즉시 적용한다.
   - **우클릭**: 해당 색상을 팔레트에서 삭제한다.
   - 팔레트는 JSON 파일(`colorPalette.json`)에 영구 저장된다.

5. **오류 로그 표시**
   - `ErrorLogger.OnErrorLogged` 이벤트를 구독하여 오류 발생 시 텍스트박스에 추가한다.
   - 오류 코드와 메시지를 함께 표시한다.

---

### 5.3 그래픽 렌더링 (`Renderer`)

```
DrawOverlayUI(Graphics g, Rectangle bounds)
```

| 마커 | 좌표 기준 | 크기 |
|------|---------|------|
| 상단 중앙 | `(bounds.Width/2 - 25, 0)` | 50×50 |
| 하단 중앙 | `(bounds.Width/2 - 25, bounds.Height - 50)` | 50×50 |
| 좌측 중앙 | `(0, bounds.Height/2 - 25)` | 50×50 |
| 우측 중앙 | `(bounds.Width - 50, bounds.Height/2 - 25)` | 50×50 |
| 중앙 소형 ×4 | 중앙 기준 오프셋 (-20,-5), (10,-5), (-5,-20), (-5,10) | 10×10 |

- 모든 마커는 `SettingsManager.SelectedOverlayColor`로 채운다(기본값: 진홍색 RGB(192, 0, 0)).
- `SolidBrush`를 사용하여 채워진 사각형을 그린다.

---

### 5.4 게임 창 위치 추적 (`WindowTracker`)

```
GetGameWindowBounds(string processName) → Rectangle
```

대상 프로세스의 실제 클라이언트 영역 좌표와 크기를 반환한다. 세 단계의 방법을 순차적으로 시도한다:

| 순위 | 방법 | API | 설명 |
|------|------|-----|------|
| 1순위 | 클라이언트 영역 | `GetClientRect` + `ClientToScreen` | 타이틀바·테두리를 제외한 실제 게임 화면 영역 |
| 2순위 | DWM 확장 프레임 | `DwmGetWindowAttribute` | DPI 인식, 하드웨어 가속 창에 정확 |
| 3순위 | 창 전체 영역 | `GetWindowRect` | 폴백, 데코레이션 포함 |

- 프로세스의 `MainWindowHandle`이 없는 경우 E101 오류를 기록하고 `Rectangle.Empty`를 반환한다.
- 창 크기 획득 실패 시 E102 오류를 기록한다.

---

### 5.5 Windows API 헬퍼 (`WinApiHelper`)

```
SetWindowTransparent(IntPtr handle)
```

주어진 창 핸들에 투명·레이어드 스타일을 적용한다.

| 상수 | 값 | 설명 |
|------|-----|------|
| `WS_EX_LAYERED` | 0x80000 | 레이어드 창 (알파 투명도 지원) |
| `WS_EX_TRANSPARENT` | 0x20 | 클릭 이벤트 통과 |
| `WS_POPUP` | 0x80000000 | 팝업 스타일 (테두리 없음) |

---

### 5.6 전역 단축키 관리 (`HotkeyManager`)

| 항목 | 값 |
|------|-----|
| 단축키 | ALT + SHIFT + S |
| 수식키 조합 | `MOD_ALT (0x0001)` \| `MOD_SHIFT (0x0004)` |
| Windows 메시지 | `WM_HOTKEY (0x0312)` |
| HOTKEY_ID | 1 |

- 생성자에서 `RegisterHotKey` WinAPI를 호출하여 시스템 전역 단축키를 등록한다.
- 등록 실패 시 E103 오류를 기록한다.
- `IDisposable` 구현: `Dispose()` 시 `UnregisterHotKey`를 호출하여 리소스를 해제한다.
- `HandleHotkeyMessage(Message m)`으로 `WndProc` 메시지를 처리하고 콜백을 실행한다.

---

### 5.7 설정 저장 및 불러오기 (`SettingsManager`)

#### 저장 파일

| 파일명 | 내용 | 형식 |
|--------|------|------|
| `overlaySettings.json` | 현재 선택된 오버레이 색상 | `{ "ColorArgb": <int> }` |
| `colorPalette.json` | 저장된 색상 팔레트 목록 | `{ "Colors": [<int>, ...] }` |

#### 전역 속성

| 속성 | 설명 |
|------|------|
| `SelectedOverlayColor` | 현재 활성 오버레이 색상 (기본: RGB 192,0,0) |

#### 메서드

| 메서드 | 설명 |
|--------|------|
| `LoadOverlayColor()` | 파일에서 색상 로드, 없으면 기본값 사용 |
| `SaveOverlayColor(Color)` | 현재 색상을 파일에 저장 |
| `LoadColorPalette()` | 팔레트 파일 로드, 없으면 빈 리스트 반환 |
| `SaveColorPalette(List<Color>)` | 팔레트를 파일에 저장 |

---

### 5.8 오류 로깅 (`ErrorLogger`)

이벤트 기반 오류 로깅 시스템.

```csharp
ErrorLogger.OnErrorLogged += (msg) => logTextBox.AppendText(msg + "\n");
ErrorLogger.LogError("E101", "프로세스에 메인 윈도우 핸들이 없습니다.");
```

#### 오류 코드 목록

| 코드 | 발생 위치 | 설명 |
|------|----------|------|
| **E101** | `WindowTracker` | 프로세스에 메인 윈도우 핸들이 존재하지 않음 |
| **E102** | `WindowTracker` | 창 사각형(위치·크기) 획득 실패 |
| **E103** | `HotkeyManager` | 전역 단축키 등록 실패 |

---

## 6. 데이터 흐름

```
[사용자]
    │ 프로세스 선택 + 오버레이 시작 클릭
    ▼
[RemoteControlForm]
    │ OverlayForm 생성 및 대상 프로세스 이름 전달
    ▼
[OverlayForm]
    │ 30ms 타이머 틱
    ▼
[WindowTracker.GetGameWindowBounds()]
    │ 창 좌표 반환
    ▼
[OverlayForm] 위치·크기 갱신 → OnPaint() 호출
    ▼
[Renderer.DrawOverlayUI()]
    │ SettingsManager.SelectedOverlayColor 읽기
    ▼
[화면에 마커 표시]

[사용자] 색상 선택
    │
    ▼
[RemoteControlForm] → [SettingsManager.SaveOverlayColor()] → overlaySettings.json
                    → [팔레트 갱신] → [SettingsManager.SaveColorPalette()] → colorPalette.json
```

---

## 7. 기술 스택

| 범주 | 기술 |
|------|------|
| 언어 | C# 12 (nullable 활성화) |
| 프레임워크 | .NET 8.0 Windows |
| UI | Windows Forms (WinForms) |
| 그래픽 | System.Drawing (GDI+) |
| Windows 통합 | P/Invoke → user32.dll, dwmapi.dll |
| 설정 저장 | System.Text.Json |
| IDE | Visual Studio 2022 이상 |

---

## 8. 아키텍처 구조

```
Program (진입점)
    └── RemoteControlForm (컨트롤 패널 UI)
            ├── OverlayForm (오버레이 창)
            │       ├── Renderer          (GDI+ 마커 렌더링)
            │       ├── WindowTracker     (게임 창 좌표 추적)
            │       └── HotkeyManager     (전역 단축키)
            ├── SettingsManager           (JSON 설정 영속성)
            └── ErrorLogger               (이벤트 기반 오류 로그)
```

### 설계 패턴

| 패턴 | 적용 위치 |
|------|----------|
| **싱글톤** | `OverlayForm.Instance` |
| **정적 관리자** | `SettingsManager`, `ErrorLogger`, `Renderer`, `WindowTracker` |
| **이벤트 기반** | `ErrorLogger.OnErrorLogged`, `HotkeyManager` 콜백 |
| **IDisposable** | `HotkeyManager` (전역 단축키 해제) |
| **이중 버퍼링** | `OverlayForm` (깜빡임 방지 페인팅) |

---

## 9. 단축키

| 단축키 | 동작 |
|--------|------|
| `ALT + SHIFT + S` | 현재 활성 오버레이 즉시 종료 |

---

## 10. 설정 파일 스키마

### overlaySettings.json

```json
{
  "ColorArgb": -4194304
}
```

- `ColorArgb`: 색상의 ARGB 정수값 (예: `-4194304` = RGB(192,0,0) 진홍색)

### colorPalette.json

```json
{
  "Colors": [-4194304, -16776961, -16711936]
}
```

- `Colors`: ARGB 정수값 배열, 최대 10개

---

## 11. 주요 제약사항

| 제약 | 내용 |
|------|------|
| 지원 OS | Windows 전용 (user32.dll, dwmapi.dll 의존) |
| 최대 팔레트 색상 수 | 10개 |
| 동시 오버레이 수 | 1개 (싱글톤) |
| 핫키 충돌 | 다른 프로그램이 동일 핫키를 점유하면 E103 발생 |
| DPI | DWM API를 통해 고DPI 환경 지원 |
