# Native Rebuild Model and Token Cost Assessment

## Summary

The native BaristaNotes rebuild used 7,363 model events and 814,292.653
internal AI usage units. Measured strict waste was no more than 0.50% of the
internal cost. Measured broader avoidable overhead was no more than 0.53%.

There is no reliable USD value for this work. The GitHub internal model IDs do
not have a public token price table. The usage ledger records internal AI usage
units, not dollars. Applying OpenAI API prices from a different model would
give a misleading result.

## Total Session Model Use

| Measure | Total |
|---|---:|
| Model events | 7,363 |
| Gross input tokens | 3,292,674,160 |
| Cache-read tokens | 3,184,764,471 |
| Cache-write tokens | 107,843,413 |
| Input not classified as cache activity | 66,276 |
| Output tokens | 7,756,201 |
| Reasoning tokens | 3,741,423 |
| Aggregate model time | 58.125 hours |
| Internal AI usage units | 814,292.653 |

The gross input total is large because each agent call includes its available
context. Almost all input was served through caching. Aggregate model hours
overlap because agents ran in parallel.

## Cost by Model

| Model | Effort | Events | Gross input | Output | Reasoning | AI units | Share |
|---|---|---:|---:|---:|---:|---:|---:|
| GPT-6 Astra | Extra High | 3,319 | 1.637B | 4.149M | 2.203M | 452,326.128 | 55.55% |
| GPT-6 Astra | High | 2,449 | 1.100B | 2.641M | 1.259M | 334,200.416 | 41.04% |
| GPT-6 Sol | High | 1,384 | 520.898M | 746,293 | 227,452 | 22,762.510 | 2.80% |
| GPT-6 Astra | Default | 3 | 2.487M | 43,546 | 0 | 2,588.207 | 0.32% |
| GPT-5.6 Sol | High | 177 | 26.898M | 107,805 | 38,322 | 1,815.621 | 0.22% |
| GPT-5.6 Sol | Extra High | 25 | 3.926M | 26,704 | 13,387 | 381.223 | 0.05% |
| GPT-5.6 Sol | Default | 6 | 1.200M | 41,219 | 0 | 218.548 | 0.03% |

GPT-6 Astra High and Extra High account for 96.59% of all AI usage units.
GPT-6 Sol High handled much of the later platform work but accounted for only
2.80% of the session total. The work scopes were different, so this is not a
controlled model-price comparison.

## Productive Work and Waste

| Classification | AI units | Share of total |
|---|---:|---:|
| Confirmed misrouted work | 613.826 | 0.075% |
| Strict-waste upper bound | 4,040.824 | 0.496% |
| Broader avoidable-overhead upper bound | 4,322.462 | 0.531% |
| Remaining productive or unclassified work | 809,970.191 | 99.469% |

The strict-waste upper bound includes:

- The iOS Equipment and Grind task sent to the Android worker: 613.826 AI
  units, 2.810 million gross input tokens, and 5,511 output tokens. This work
  was read-only and produced no implementation or evidence.
- The Computer Use and software-keyboard detour: at most 3,426.998 AI units,
  18.730 million gross input tokens, and 32,387 output tokens. This is an upper
  bound because the same interval also included valid Ailoha and DevFlow
  checks.

The broader upper bound adds 281.638 AI units for the separate `idb` client
investigation. DevFlow already supported coordinate input, but the
investigation produced a useful distinction between Ailoha element activation
and platform input. It also led to an Ailoha feature request. This is avoidable
diagnostic rework, not pure waste.

Some discarded work cannot be separated from productive calls:

- Wrong-simulator, wrong-app, and black screenshots that were excluded from
  acceptance evidence.
- An early Android people-summary layout that was later replaced.
- Small routing and tool-selection corrections mixed into productive agent
  turns.

Failed builds, SQLite investigations, NativeAOT retries, lifecycle debugging,
and verification retries are not classified as waste. Those activities
produced retained fixes, working packages, test evidence, or reliable
technical decisions.

## Assessment

Measured strict waste was no more than 0.50% of internal cost. Measured broader
avoidable overhead was no more than 0.53%. The larger cost concern was not
discarded work. It was the broad use of GPT-6 Astra Extra High before model
routing was narrowed.

For similar work:

- Use GPT-6 Sol High for platform implementation.
- Use GPT-6 Astra High for independent review.
- Use Extra High only for isolated lifecycle, concurrency, or interop defects.
- Time-box tooling investigations.
- Confirm the capabilities of existing automation before adding another tool.
- Record exact agent roles immediately after parallel agents start.

## Method and Limits

The values come from the local session usage ledger. Gross input includes
cache-read and cache-write activity. Cache tokens are components of gross
input and are not added to it again.

AI usage units are internal cost units. They are not USD. Model hours are
aggregate execution time and can overlap when agents run in parallel.

Waste classification uses these rules:

- **Productive:** Output remains in source, tests, reports, decisions, fixes,
  accepted evidence, or an approved capability.
- **Productive rework:** A failed attempt or investigation directly produced a
  retained fix or reliable decision.
- **Wasted:** Work was discarded, invalidated, sent to the wrong scope, or
  redirected because it was off track.

Mixed usage windows are reported as upper bounds rather than exact waste.
