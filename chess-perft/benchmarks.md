# Perft benchmarks

Each row is one `--record`ed perft run. The commit column carries `-dirty` when the working tree had uncommitted changes.

| date       | commit        | position         | depth | nodes     | time (ms) | nodes/sec | config  | status   |
| ---------- | ------------- | ---------------- | ----- | --------- | --------- | --------- | ------- | -------- |
| 2026-04-17 | a82ad47-dirty | start-oracle     | 1     | 20        | 6.2       | 3,239     | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start-oracle     | 2     | 400       | 3.2       | 126,326   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start-oracle     | 3     | 8,902     | 67.8      | 131,317   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start-oracle     | 4     | 197,281   | 399.2     | 494,211   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start-oracle     | 5     | 4,865,609 | 5172.7    | 940,635   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | kiwipete-oracle  | 1     | 48        | 0.1       | 576,923   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | kiwipete-oracle  | 2     | 2,043     | 2.8       | 725,986   | Release | MISMATCH |
| 2026-04-17 | a82ad47-dirty | kiwipete-oracle  | 3     | 98,196    | 129.6     | 757,533   | Release | MISMATCH |
| 2026-04-17 | a82ad47-dirty | position3-oracle | 1     | 14        | 0.0       | 608,695   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | position3-oracle | 2     | 191       | 0.2       | 1,168,195 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | position3-oracle | 3     | 2,812     | 2.2       | 1,266,438 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start            | 1     | 20        | 6.7       | 2,972     | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start            | 2     | 400       | 0.7       | 575,456   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start            | 3     | 8,902     | 7.9       | 1,123,819 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start            | 4     | 197,281   | 149.8     | 1,316,768 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | start            | 5     | 4,865,609 | 716.1     | 6,794,536 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | kiwipete         | 1     | 48        | 0.2       | 250,914   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | kiwipete         | 2     | 2,039     | 0.6       | 3,196,926 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | kiwipete         | 3     | 97,862    | 10.5      | 9,287,022 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | position3        | 1     | 14        | 0.1       | 202,020   | Release | OK       |
| 2026-04-17 | a82ad47-dirty | position3        | 2     | 191       | 0.1       | 1,335,664 | Release | OK       |
| 2026-04-17 | a82ad47-dirty | position3        | 3     | 2,812     | 0.7       | 4,068,875 | Release | OK       |
