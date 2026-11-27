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

The full transport cache includes `history-shape-baseline.json` in its key. A baseline
edit requires a fresh strict run because the proof scenarios also assert that narrow
exemptions reject deliberately broken history. Manual classification remains available
for reviewing raw evidence.

The maintainer's `test_history_shapes` test uses the built harness to exercise the
classifier against disposable raw data.
