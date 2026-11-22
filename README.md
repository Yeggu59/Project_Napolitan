# Project : Napolitan

**모르는 채로 겪고, 겪은 것을 수칙으로 남긴다. 그 수칙이 다음에도 맞는다는 보장은 없다.**

나폴리탄 괴담을 바탕으로 한 1인칭 3D 호러 게임입니다. 플레이어는 가상의 공기업 <도시이상현상관리국> 신입 사원으로 일상 공간에 파견되어 이상현상을 직접 겪고, 살아 돌아와 `[조건] + [이상현상] + [대응]` 형식의 대응 수칙을 써서 제출합니다.

- **상태**: 버티컬 슬라이스 개발 중 (에디터에서 실행, 빌드 없음)
- **엔진**: Unity 6 (6000.3.9f1) · URP
- **형태**: 개인 포트폴리오 프로젝트

> A personal portfolio project: a first-person rule-horror game in Unity 6. Vertical slice in progress; game logic lives in a pure C# assembly covered by 70 EditMode tests.

## 지금 플레이할 수 있는 것

서림역 B2 환승통로(화이트박스)에서 이상현상 **붉은 자판기** 하나를 겪는 한 판입니다.

- **1인칭 이동 · 조사**: 걷고 둘러보며 현장의 기록물을 조사
- **이상현상 상태머신**: 전조 → 현상 → 해결 / 위반. 어떻게 행동했느냐에 따라 결과가 갈립니다
  - 계속 쳐다봄 → 시야가 붉게 좁아짐 (즉시 피해)
  - 다가감 → 몇 초 뒤 손등에 붉은 자국 (지연 피해)
  - 자국이 있는 채로 다시 마주침 → 평소의 정답을 해도 즉사 (숨은 조건)
- **현장 행동 판정**: 거리 · 시선 · 몸 방향 · 눈 감기 입력으로 응시 / 다가감 / 외면 / 뒷걸음질 등을 판정
- **관측 일지**: 겪은 일이 기록으로 쌓이고, 해금된 키워드를 칸별로 정리
- **수칙 채점**: 제출한 수칙을 정답 / 부분정답 / 오답 / 중복 / 미기재로 판정 (로직과 테스트만 있음)

아직 없는 것: 수칙 조립 UI, 정산 화면, 아트 · 사운드 (지금은 화이트박스와 임시 효과음)

## 설계 포인트

- **로직과 엔진 분리**: 판정 · 상태머신 · 일지 로직은 UnityEngine을 참조하지 않는 `Napolitan.Core` 어셈블리에 두고, Play 없이 EditMode 테스트 70개로 검증합니다.
- **결정적인 결과**: 같은 상태에서 같은 행동을 하면 항상 같은 결과가 나옵니다. 랜덤은 파견 시작 때 이상현상의 변형을 고르는 데에만 씁니다 (시드 고정).
- **데이터 주도**: 이상현상과 결과 표는 JSON으로 정의하고, 데이터 무결성도 테스트로 검사합니다.
- **프로토타입 먼저**: 3D를 만들기 전에 수칙 판정 로직을 TypeScript 웹 프로토타입으로 검증한 뒤 C#으로 옮겼습니다.

## 기술 스택

| 영역 | 사용 |
|---|---|
| 엔진 | Unity 6000.3.9f1, URP |
| 언어 | C#, TypeScript (프로토타입) |
| 입력 · UI | Input System, UI Toolkit |
| 데이터 | JSON (Newtonsoft.Json) |
| 테스트 | Unity Test Framework (NUnit), Vitest |
| 버전 관리 | Git, Git LFS |

## 실행 방법

1. Unity Hub에서 `Unity/` 폴더를 프로젝트로 추가합니다 (에디터 `6000.3.9f1`).
2. `Assets/Scenes/SeorimB2.unity`를 열고 **Play**.
3. 테스트: **Window → General → Test Runner → EditMode → Run All**

| 키 | 동작 |
|---|---|
| WASD / 마우스 | 이동 / 시점 |
| Shift | 달리기 |
| E | 조사 |
| C (길게) | 눈 감기 |
| Tab | 관측 일지 |
| F1 | 디버그 표시 |

일지의 한글은 Windows의 맑은 고딕으로 표시합니다. 다른 OS에서는 글자가 깨질 수 있습니다.

웹 프로토타입 테스트 (Node.js 필요):

```bash
cd Prototype/web
npm install
npm test
```

## 폴더 구조

```
Unity/Assets/Scripts/Core/    판정 · 상태머신 · 일지 (순수 C#)
Unity/Assets/Scripts/Game/    플레이어 · 이상현상 연출 · UI (MonoBehaviour)
Unity/Assets/Tests/EditMode/  Core 단위 테스트
Unity/Assets/Data/            구역 · 이상현상 데이터 (JSON)
Prototype/web/                판정 로직 웹 프로토타입 (보관용)
```

## 개발 방식

개인 프로젝트입니다. 게임 방향, 결정, 플레이테스트는 직접 하고, 구현은 AI 에이전트(Claude)와 나눠 진행했습니다.
