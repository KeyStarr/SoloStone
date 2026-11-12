# AI test inputs

`*.json` here are replay fixtures, and `journals/` holds live-match journals. Both are inputs for the AI tuning
harness (`sim/AiHarness`), which is not in this repository yet but will be published. No suite in this repository
reads them today, and the C# files next to them do not use them.

The `tools/hs ai hard-fixture` commands named inside some fixtures refer to that harness. Do not edit the files by hand.
