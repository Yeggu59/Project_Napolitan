// 판정 규칙 테스트 케이스 (T1~T13)
import { describe, expect, it } from "vitest";
import { anomalyPoolForRank, judgeManual, RANK_CONFIGS } from "./index";
import type { AnomalyDef, Rank, Rule } from "./index";

const RANKS: Rank[] = ["Intern", "AssistantManager", "GeneralManager"];

// 기준 케이스: 붉은빛 + 기계음 + {뒷걸음질, 눈 감기}
const VENDING: AnomalyDef = {
  id: "vending_machine",
  siteId: "subway",
  name: "붉은 자판기",
  conditions: ["kw.cond.red_light"],
  anomalies: ["kw.anom.mech_noise"],
  correctResponses: ["kw.resp.back_away", "kw.resp.close_eyes"],
  clues: [],
};

// 다중 조건 + 정답 대응 1개 (부장 풀에도 남음)
const FLICKER: AnomalyDef = {
  id: "flicker_corridor",
  siteId: "subway",
  name: "깜빡이는 통로",
  conditions: ["kw.cond.night", "kw.cond.flicker"],
  anomalies: ["kw.anom.footsteps"],
  correctResponses: ["kw.resp.stand_still"],
  clues: [],
};

const rule = (condition: string[], anomaly: string, response: string): Rule => ({
  condition,
  anomaly,
  response,
});

const VENDING_CORRECT = rule(["kw.cond.red_light"], "kw.anom.mech_noise", "kw.resp.back_away");
const VENDING_WRONG_RESPONSE = rule(["kw.cond.red_light"], "kw.anom.mech_noise", "kw.resp.press_button");

describe("판정 테스트 케이스", () => {
  it.each(["Intern", "AssistantManager"] as Rank[])("T1 %s: 3칸 일치 → 정답", (rank) => {
    const result = judgeManual([VENDING_CORRECT], [VENDING], RANK_CONFIGS[rank]);
    expect(result.rules[0].verdict).toBe("correct");
    expect(result.rules[0].score).toBe(10);
  });

  it("T1 부장: 정답 대응 1개짜리 이상현상에서 3칸 일치 → 정답", () => {
    const r = rule(["kw.cond.night", "kw.cond.flicker"], "kw.anom.footsteps", "kw.resp.stand_still");
    const result = judgeManual([r], [FLICKER], RANK_CONFIGS.GeneralManager);
    expect(result.rules[0].verdict).toBe("correct");
  });

  it("T2 인턴: 대응만 틀림 → 부분정답 + 대응 칸 표시", () => {
    const result = judgeManual([VENDING_WRONG_RESPONSE], [VENDING], RANK_CONFIGS.Intern);
    expect(result.rules[0]).toMatchObject({ verdict: "partial", wrongSlots: ["response"], score: 3 });
  });

  it("T3 대리: 대응만 틀림 → 부분정답, 틀린 칸 표시 없음", () => {
    const result = judgeManual([VENDING_WRONG_RESPONSE], [VENDING], RANK_CONFIGS.AssistantManager);
    expect(result.rules[0].verdict).toBe("partial");
    expect(result.rules[0].wrongSlots).toBeUndefined();
  });

  it("T4 부장: 대응만 틀림 → 오답", () => {
    const r = rule(["kw.cond.night", "kw.cond.flicker"], "kw.anom.footsteps", "kw.resp.run");
    const result = judgeManual([r], [FLICKER], RANK_CONFIGS.GeneralManager);
    expect(result.rules[0]).toMatchObject({ verdict: "wrong", score: -6 });
  });

  it("T5 인턴: 두 번째 정답 대응도 정답", () => {
    const r = rule(["kw.cond.red_light"], "kw.anom.mech_noise", "kw.resp.close_eyes");
    const result = judgeManual([r], [VENDING], RANK_CONFIGS.Intern);
    expect(result.rules[0].verdict).toBe("correct");
  });

  it("T6 부장: 정답 대응 2개짜리 이상현상은 풀에서 제외", () => {
    const pool = anomalyPoolForRank([VENDING, FLICKER], RANK_CONFIGS.GeneralManager);
    expect(pool.map((a) => a.id)).toEqual(["flicker_corridor"]);
  });

  it.each(RANKS)("T7 %s: 수칙 없음 → 전부 미기재", (rank) => {
    const result = judgeManual([], [VENDING, FLICKER], RANK_CONFIGS[rank]);
    const expected = rank === "GeneralManager" ? ["flicker_corridor"] : ["vending_machine", "flicker_corridor"];
    expect(result.missing).toEqual(expected);
    expect(result.totalScore).toBe(0);
  });

  it("T8 인턴: 같은 이상현상에 정답 + 오답 → +10, -2", () => {
    const wrong = rule(["kw.cond.flicker"], "kw.anom.mech_noise", "kw.resp.press_button");
    const result = judgeManual([VENDING_CORRECT, wrong], [VENDING], RANK_CONFIGS.Intern);
    expect(result.rules.map((r) => r.verdict)).toEqual(["correct", "wrong"]);
    expect(result.totalScore).toBe(8);
  });

  it("T9 인턴: 다중 조건 중 1개만 넣어도 조건 인정 / 대리: 전부 필요", () => {
    const r = rule(["kw.cond.night"], "kw.anom.footsteps", "kw.resp.stand_still");
    expect(judgeManual([r], [FLICKER], RANK_CONFIGS.Intern).rules[0].verdict).toBe("correct");
    const am = judgeManual([r], [FLICKER], RANK_CONFIGS.AssistantManager).rules[0];
    expect(am.verdict).toBe("partial");
  });

  it("T10 인턴: 같은 이상현상에 정답 2개 → 하나만 점수, 나머지 중복(0점)", () => {
    const second = rule(["kw.cond.red_light"], "kw.anom.mech_noise", "kw.resp.close_eyes");
    const result = judgeManual([VENDING_CORRECT, second], [VENDING], RANK_CONFIGS.Intern);
    expect(result.rules.map((r) => r.verdict)).toEqual(["correct", "duplicate"]);
    expect(result.totalScore).toBe(10);
  });

  it("T11 부장: 제출 한도(이상현상 수) 초과 → 제출 불가", () => {
    const r = rule(["kw.cond.night", "kw.cond.flicker"], "kw.anom.footsteps", "kw.resp.stand_still");
    expect(() => judgeManual([r, r], [FLICKER], RANK_CONFIGS.GeneralManager)).toThrow("제출 한도 초과");
  });

  it("T12 인턴: 1칸만 일치 → 오답", () => {
    const r = rule(["kw.cond.red_light"], "kw.anom.footsteps", "kw.resp.press_button");
    expect(judgeManual([r], [VENDING], RANK_CONFIGS.Intern).rules[0].verdict).toBe("wrong");
  });

  it("T13 인턴: 정답에 없는 조건이 섞이면 조건 칸 틀림", () => {
    const r = rule(["kw.cond.red_light", "kw.cond.night"], "kw.anom.mech_noise", "kw.resp.back_away");
    const result = judgeManual([r], [VENDING], RANK_CONFIGS.Intern);
    expect(result.rules[0]).toMatchObject({ verdict: "partial", wrongSlots: ["condition"] });
  });
});
