# PUZZLE_ACTION_CONTRACT_V1.1_REPORT

## 1. P02 사례 분석
- **Interaction**: P02ObjectPngOverlayController를 통한 2D 오버레이 객체(좌/우측 눈알 부품) 클릭 이벤트.
- **Action State**: `PZL_P02_LEFT_EYE_INSERTED`, `PZL_P02_RIGHT_EYE_INSERTED` 플래그를 도입하여 각 부품의 개별 삽입 상태(중간 상태)를 독립적으로 관리.
- **Solve Condition**: 좌/우측 눈알 플래그가 모두 활성화(`true`)되었을 때 퍼즐 클리어로 판정하고 `PZL_P02_SOLVED` 설정.
- **Persist**: 상태 변화가 발생할 때마다 `GameSession.Persist()`를 호출하여 World State(room_flags)에 진실 데이터(Game Truth) 기록.
- **Restore**: Scene 및 방(Room) 로드 시 World State에서 데이터를 읽어와서 로컬 상태 복원.
- **Visual Binding**: 로드된 진실 데이터(`PZL_P02_LEFT_EYE_INSERTED` 등)만을 기반으로 화면(부엉이 본체 및 오버레이 눈알 이미지)의 표시 여부를 결정. (Game Truth = Visual)

## 2. 공통 Contract 필드 정의
모든 퍼즐은 중앙 관리자(PuzzleManager) 없이, 방 단위의 로컬 환경에서 자율적으로 아래 Contract 프레임을 준수합니다.

- **PuzzleId**: 퍼즐 고유 식별자 (예: P03)
- **InteractionTarget**: 유저 입력을 수신하는 대상 (예: 2D PNG 오버레이, 3D Collider 등)
- **ActionType**: 조작 방식 (예: 단발성 토글, 단계별 회전, 연속 드래그 등)
- **StateModel**: 논리 상태를 World State와 연동하는 방식.
- **InitialState**: 진입 시의 기본 상태 (Load 과정에서 불러온 World State 기반).
- **IntermediateStates**: 클리어 전 반드시 기록되어야 하는 필수 Action State (예: 회전 각도, 패널 활성화 여부 등)
- **SolveCondition**: `IntermediateStates`가 정답 조건에 부합하는지 판정하는 기준.
- **FailureCondition**: 오답 발생 시의 로컬 동작 (예: 무시, 리셋, 데스 카운트 증가)
- **SavePolicy**: 진실(Game Truth)인 World Flags에 상태를 기록하고 `Persist()`를 호출하는 동기화 시점.
- **VisualMapping**: 오직 기록된 State(Truth) 데이터만을 근거로 화면(Visual) 요소의 렌더링(회전, On/Off 등)을 처리하는 단방향 동기화 규칙.

## 3. 적용성 검토 (P03, P04, P05)

### P03 (Rotating Lamp)
- **기존 구조로 표현 가능한가?**: 가능. 회전 각도 또는 회전 단계를 `IntermediateStates`로 World Flag에 기록 가능.
- **Local Action Extension 필요성**: **필요함**. 현재 단발성 클릭만 존재하므로, 좌/우측 회전을 처리하고 각도를 추적하는 로컬 Action 로직의 신규 작성이 불가피함.
- **추가 범용 시스템 필요성**: 불필요. 거대 엔진 없이 로컬 스크립트만으로 해결 가능.

### P04 (Fake Core Door)
- **기존 구조로 표현 가능한가?**: 가능. 개별 문/다이얼 조작 상태를 각각의 Action State로 추적.
- **Local Action Extension 필요성**: **필요함**. P04 전용 조작 인터페이스(InteractionTarget)와 상태 전환(ActionType) 구현 필요.
- **추가 범용 시스템 필요성**: 불필요.

### P05 (Delta Glyph)
- **기존 구조로 표현 가능한가?**: 가능. 복수 Glyph의 점등 패턴을 상태로 기록하고 비교.
- **Local Action Extension 필요성**: **필요함**. 다중 패널 상태 조합 검증(SolveCondition)을 위한 로컬 스크립트 구현.
- **추가 범용 시스템 필요성**: 불필요.

**최종 결론**:
중앙 관리자(PuzzleManager)나 범용 엔진 없이도, 각 방(로컬 스크립트)이 **Puzzle Action Contract V1.1 프레임**을 엄격히 준수하도록 설계한다면 일관된 Save/Load 및 시각 복원을 완벽하게 달성할 수 있습니다. 각 퍼즐 방마다 자체적인 Local Action Extension 코딩을 수행하는 방식이 가장 적합합니다.