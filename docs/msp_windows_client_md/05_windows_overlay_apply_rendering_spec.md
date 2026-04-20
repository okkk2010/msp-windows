# Windows Client Overlay Apply / Rendering 명세서

## 1. 목적

Overlay Apply는 서버에서 받은 `overlayJson`을 Windows Client 내부 모델로 파싱하고, 현재 OverlayWindow에 실제로 렌더링하는 기능이다.

이번 단계에서는 편집기 기능보다 **JSON 기반 적용과 렌더링**에 집중한다.

---

## 2. 처리 대상

현재 Overlay JSON MVP 기준 지원 요소:

```text
rect
circle
line
```

지원 제외 요소:

```text
image
text
```

`overlaySettings`는 `opacity`만 사용한다.

---

## 3. 기준 JSON 구조

Windows Client는 다음 구조를 기준으로 JSON을 파싱한다.

```text
schemaVersion
overlayId
name
platform
game
canvas
overlaySettings
elements
meta
```

---

## 4. C# 모델 구조

권장 모델:

```text
OverlayDocument
OverlayCanvas
OverlaySettings
OverlayGame
OverlayMeta
OverlayElementBase
RectElement
CircleElement
LineElement
```

---

## 5. C# 클래스 필드

## 5.1 OverlayDocument

```text
SchemaVersion
OverlayId
Name
Platform
Game
Canvas
OverlaySettings
Elements
Meta
```

## 5.2 OverlayCanvas

```text
BaseWidth
BaseHeight
```

## 5.3 OverlaySettings

```text
Opacity
```

## 5.4 OverlayGame

```text
Id
Name
```

## 5.5 OverlayMeta

```text
CreatedAt
UpdatedAt
```

## 5.6 RectElement

```text
Id
Type
X
Y
Width
Height
Rotation
Opacity
ZIndex
Visible
Locked
FillColor
StrokeColor
StrokeWidth
CornerRadius
```

## 5.7 CircleElement

```text
Id
Type
X
Y
Width
Height
Rotation
Opacity
ZIndex
Visible
Locked
FillColor
StrokeColor
StrokeWidth
```

## 5.8 LineElement

```text
Id
Type
X1
Y1
X2
Y2
Opacity
ZIndex
Visible
Locked
StrokeColor
StrokeWidth
DashStyle
```

---

## 6. JSON 필드 매핑 정책

```text
C# 클래스는 PascalCase 사용
JSON 필드는 camelCase 사용
System.Text.Json 사용
JsonPropertyName attribute로 매핑
```

예시:

```csharp
[JsonPropertyName("schemaVersion")]
public string SchemaVersion { get; set; }
```

---

## 7. Element Type 처리

`elements` 배열은 `type` 값에 따라 클래스를 분기한다.

```text
type == "rect"   → RectElement
type == "circle" → CircleElement
type == "line"   → LineElement
```

지원하지 않는 type이 들어오면 이번 단계에서는 에러 처리한다.

```text
Unsupported element type: {type}
```

---

## 8. 기본 검증 규칙

Windows Client는 서버 검증을 신뢰하되, 최소 검증을 한 번 더 수행한다.

```text
schemaVersion 존재 여부
overlayId 존재 여부
name 존재 여부
platform == "windows" 여부
canvas.baseWidth > 0
canvas.baseHeight > 0
overlaySettings.opacity 0.0 ~ 1.0 여부
elements 배열 여부
element type 지원 여부
```

---

## 9. 렌더링 기준

Overlay JSON은 기준 canvas 해상도를 가진다.

예시:

```text
baseWidth = 1920
baseHeight = 1080
```

실제 게임 창 크기가 다르면 좌표를 변환한다.

---

## 10. 스케일 정책

이번 단계에서는 **X/Y 개별 스케일**을 사용한다.

```text
scaleX = targetWindowWidth / canvas.baseWidth
scaleY = targetWindowHeight / canvas.baseHeight
```

Rect 변환:

```text
renderX = x * scaleX
renderY = y * scaleY
renderWidth = width * scaleX
renderHeight = height * scaleY
```

Circle 변환:

```text
renderX = x * scaleX
renderY = y * scaleY
renderWidth = width * scaleX
renderHeight = height * scaleY
```

Line 변환:

```text
renderX1 = x1 * scaleX
renderY1 = y1 * scaleY
renderX2 = x2 * scaleX
renderY2 = y2 * scaleY
```

---

## 11. Opacity 적용

Opacity는 두 단계로 적용한다.

```text
전체 opacity = overlaySettings.opacity
개별 element opacity = element.opacity
```

최종 투명도 계산:

```text
finalOpacity = overlaySettings.opacity * element.opacity
```

---

## 12. ZIndex 처리

렌더링 전 elements를 다음 기준으로 정렬한다.

```text
zIndex 오름차순
```

낮은 zIndex를 먼저 그리고 높은 zIndex를 나중에 그린다.

---

## 13. Visible / Locked 처리

```text
visible == false인 element는 렌더링하지 않는다.
```

`locked`는 편집기용 속성이다.

```text
이번 단계의 Windows Client 적용 기능에서는 렌더링에 영향을 주지 않는다.
locked 값은 무시한다.
```

---

## 14. 완료 기준

```text
[ ] overlayJson을 OverlayDocument로 파싱
[ ] rect 렌더링
[ ] circle 렌더링
[ ] line 렌더링
[ ] opacity 적용
[ ] zIndex 순서 적용
[ ] visible=false 요소 미표시
[ ] 게임 창 크기에 따른 스케일 적용
[ ] 창 크기 변경 시 재계산
[ ] unsupported element type 에러 처리
```
