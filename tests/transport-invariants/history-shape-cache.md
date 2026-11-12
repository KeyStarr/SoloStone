# History-shape classification and cache

New error buckets and unreviewed split shapes fail strict transport. STALE baseline
entries remain in the JSON report and console diagnostics, but do not fail the run.
They mean this corpus did not exercise a previously reviewed shape.

Every completed run writes `sim/out/transport/history-shape-raw.json`, containing
the played-card inventory and raw findings grouped by game/scenario, before applying
the baseline. Reclassify that evidence without running games or proof scenarios:

```sh
dotnet <built TransportInvariants.dll> --classify-history sim/out/transport/history-shape-raw.json
```

This mode always exits 1 for new error shapes, 0 for reviewed shapes (including
stale warnings), and 2 for malformed input. It rewrites `history-shape.json` and
`split-effect-blocks.json`; it does not produce a fresh transport run report.

Normal full transport cache misses also check a raw-shape cache whose key excludes
only `history-shape-baseline.json`. Baseline-only edits rerun classification against
the same game evidence. Binary, runtime, argument, other JSON, card-data and pinned pool snapshot
(`tests/goldens/pools`) changes still invalidate that key. Raw evidence from a shape-only failure is reusable;
transport failures or violations are not. Existing cache bypasses and lock apply.
Both caches live in the maintainer's transport cache folder and
each keeps its newest sixteen entries. Old classification diagnostics are
removed from replayed output so a renewed baseline cannot leave stale NEW/STALE lines.

The maintainer's `test_history_shapes` test requires the transport harness built in
`sim/.build/transport`; it exercises the real classifier using disposable raw data.
Its cache test replaces only the expensive corpus producer and verifies that a
shape-only failure, baseline acceptance, and later rejection require one production
of game evidence total. No test edits the repository baseline or goldens.
