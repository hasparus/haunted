interface Env {
  SCORES: KVNamespace;
}

type Entry = { score: number; players: number; at: string };

const KEY = "survival";
const cors = {
  "access-control-allow-origin": "*",
  "access-control-allow-methods": "GET,POST,OPTIONS",
  "access-control-allow-headers": "content-type",
};

const json = (data: unknown, status = 200) =>
  new Response(JSON.stringify(data), { status, headers: { ...cors, "content-type": "application/json" } });

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    if (req.method === "OPTIONS") return new Response(null, { headers: cors });
    if (new URL(req.url).pathname !== "/scores") return json({ error: "not found" }, 404);

    const scores: Entry[] = (await env.SCORES.get(KEY, "json")) ?? [];
    if (req.method === "GET") return json({ scores: scores.slice(0, 10) });
    if (req.method !== "POST") return json({ error: "method not allowed" }, 405);

    const body = (await req.json().catch(() => null)) as Partial<Entry> | null;
    const score = Number(body?.score);
    const players = Number(body?.players);
    if (!Number.isFinite(score) || score <= 0 || score > 100_000 || !Number.isInteger(players) || players < 1 || players > 4)
      return json({ error: "bad score" }, 400);

    // ponytail: read-modify-write on one KV key; concurrent posts can drop a score. Fine for a jam, use D1 if it matters.
    const entry: Entry = { score: Math.round(score), players, at: new Date().toISOString() };
    const next = [...scores, entry].sort((a, b) => b.score - a.score).slice(0, 50);
    await env.SCORES.put(KEY, JSON.stringify(next));
    return json({ rank: next.indexOf(entry) + 1, scores: next.slice(0, 10) });
  },
} satisfies ExportedHandler<Env>;
