# AI test inputs

`*.json` here are replay fixtures, and `journals/` holds live-match journals. Both are inputs for the AI tuning
harness (`sim/AiHarness`), which is not in this repository yet but will be published. No suite in this repository
reads them today, and the C# files next to them do not use them.

The commands (such as `tools/hs ai hard-fixture`) and docs named inside some fixtures belong to that harness and are not published yet. Do not edit the files by hand.
