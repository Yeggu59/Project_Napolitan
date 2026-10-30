// 구역 데이터 무결성 검사 — 데이터를 고치면 여기서 먼저 깨진다
import { describe, expect, it } from "vitest";
import { anomalyPoolForRank, judgeManual, RANK_CONFIGS } from "../core";
import type { SiteData, Slot } from "../core";
import subwayTransfer from "./subway_transfer.json";

const SITES = [subwayTransfer as SiteData];

describe.each(SITES.map((s) => [s.id, s] as const))("%s", (_, site) => {
  const byId = new Map(site.keywords.map((k) => [k.id, k]));
  const allClues = [...site.anomalies.flatMap((a) => a.clues), ...site.noiseClues];
  const revealed = new Set(allClues.flatMap((c) => c.revealsKeywords));

  const expectSlot = (ids: string[], slot: Slot) => {
    for (const id of ids) expect(byId.get(id)?.slot, id).toBe(slot);
  };

  it("id 중복 없음", () => {
    const ids = [...site.keywords.map((k) => k.id), ...site.anomalies.map((a) => a.id), ...allClues.map((c) => c.id)];
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("이상현상이 참조하는 키워드가 존재하고 칸이 맞음", () => {
    for (const a of site.anomalies) {
      expectSlot(a.conditions, "condition");
      expectSlot(a.anomalies, "anomaly");
      expectSlot(a.correctResponses, "response");
    }
  });

  it("단서가 해금하는 키워드가 존재함", () => {
    for (const id of revealed) expect(byId.has(id), id).toBe(true);
  });

  it("모든 키워드는 어떤 단서로든 해금 가능", () => {
    const unreachable = site.keywords.filter((k) => !revealed.has(k.id)).map((k) => k.id);
    expect(unreachable).toEqual([]);
  });

  it("각 이상현상의 정답 키워드는 그 이상현상 자신의 단서로 해금됨 (억까 방지)", () => {
    for (const a of site.anomalies) {
      const own = new Set(a.clues.flatMap((c) => c.revealsKeywords));
      for (const id of [...a.conditions, ...a.anomalies, ...a.correctResponses]) {
        expect(own.has(id), `${a.id} → ${id}`).toBe(true);
      }
    }
  });

  it("부장 풀이 비어 있지 않음", () => {
    expect(anomalyPoolForRank(site.anomalies, RANK_CONFIGS.GeneralManager).length).toBeGreaterThan(0);
  });

  it("정답 수칙을 모두 내면 직급마다 만점", () => {
    for (const config of Object.values(RANK_CONFIGS)) {
      const pool = anomalyPoolForRank(site.anomalies, config);
      const manual = pool.map((a) => ({ condition: a.conditions, anomaly: a.anomalies[0], response: a.correctResponses[0] }));
      const result = judgeManual(manual, site.anomalies, config);
      expect(result.missing).toEqual([]);
      expect(result.totalScore).toBe(pool.length * config.score.correct);
    }
  });
});
