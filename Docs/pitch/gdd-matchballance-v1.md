# MatchBallance — Game Design Document & Development Roadmap

**Studio:** Bellseboss Studio — Bogotá D.C., Colombia
**Contact:** Luis Plata · www.luisplata@gmail.com
**Engine:** Unity 6 (6000.3.10f1) · URP 17.3
**Build in hand:** v0.3.2 (Android, Google Play internal track)
**Declared global launch:** 1 March 2028
**Document version:** v1 — November 2026

---

## 1. One-line pitch

A competitive color-matching puzzle where you feed two lanes and lose if either one tips
over — so every tap is a bet between scoring now and surviving the next move.

---

## 2. Executive summary

MatchBallance takes the most reliable loop on mobile — match three of a color, clear them,
score — and puts a second failure state on top of it: **balance**. Balls arrive into two
vertical lanes. You decide, tap by tap, which lane takes the next one. Both lanes are finite,
and they must stay within three balls of each other. Clear greedily and you stack one lane
until it overflows; play safe and you stop scoring.

The result is a puzzle game with the read-at-a-glance tension of a fighting game. That is the
commercial hook: matches last under three minutes, a single combo swings the score, and an
on-screen risk gauge makes a stranger's board legible from across the room. It is built for
one thumb, and it is built to be watched.

We are asking the Unity Fellowship for **$50,000** to finish the game we already have running:
replace the remaining placeholder art, ship the iOS version, produce the content needed for a
live-service launch, and carry both platforms through soft launch into a global release on
1 March 2028.

---

## 3. The core loop

### 3.1 Moment to moment

1. Balls are fed from a source zone into the board, one at a time.
2. The player taps **left** or **right**. The tapped lane receives the ball.
3. When the board settles, three or more balls of the same color on a lane clear and pay out.
4. The payout is multiplied by the combo the player has built, and the multiplier resets when
   the player stops clearing.
5. Repeat until a loss condition fires.

### 3.2 The two ways to lose

| Loss condition | Rule | Player-facing signal |
|---|---|---|
| **Imbalance** | The difference of balls between lanes exceeds 3 | Risk gauge + danger shake on the board |
| **Overflow** | Either lane reaches capacity | Pending-ball deadline shake |

The risk gauge is graded — **Safe / Mild / Strong** — and reports the real number of moves
until either loss, recomputed after every settle. It is implemented as a pure function of the
settled board (`BalanceRiskPolicy`), deliberately free of engine dependencies so the rule can be
tested and tuned without touching the presentation layer.

This gauge is the design centre of the game. Without it, MatchBallance is a guessing game.
With it, it is a game of informed greed: the player always knows the cost of the next tap, and
losing always feels earned.

### 3.3 Power-ups

Power-ups are the second decision layer. Ten are implemented as discrete, composable commands:

- **Freeze** — stop the feed and buy time on a dangerous board.
- **Delete a color** — clear the most numerous color and relieve pressure.
- **Joker in queue** — inject a wildcard ball into the incoming sequence.
- **Send a shell ball** — in PvP, hand the opponent a problem.
- **First in queue** — pull a chosen ball to the front of the feed.
- **Lead power-up** — bias the path toward a chosen color.

Each is a single command object, so new power-ups are content, not refactors.

### 3.4 Local PvP

Two live boards, one device, both players armed with their own power-up loadout. This mode is
the game's strongest differentiator and its cheapest marketing asset: it turns a solo mobile
puzzle into something two people do together on a bus, which is exactly how mobile play
actually happens in our market.

A shared power-up selector panel was extracted into a reusable prefab so PvE and PvP use the
same UI — the same discipline applied to the rest of the meta layer.

---

## 4. Meta layer

MatchBallance is not a demo with a menu screen. The surrounding systems a free-to-play launch
needs are already built and wired:

| System | State | Notes |
|---|---|---|
| Heroes with dialogue | Live | Server-driven dialog graph per hero, with questions and answers (`StartDialogByHero`) |
| Battle pass | Live | Reward track fetched per player, rewards claimed in a dedicated scene |
| Gold economy | Live | Earn, spend, inventory, store items |
| Store | Live | Item listing and purchase flow through use cases |
| Friends & leaderboards | Live | Google Play Games sign-in and friend config |
| Localization | Live | English, Spanish, French, Italian |
| Ads | Live | Rewarded ads via mediation; no ad gates progress |
| Tutorial | Live | Scripted first-run feed that guarantees a first 3+ match on tap six |
| Save & profile | Live | Unity Cloud Save with a local cache and merge on reconnect |
| Live operations | Live | Unity Economy, Cloud Code, Remote Config and Authentication — retuned without shipping a build |

The hero layer matters commercially: it is authored content we own, it is cheap to extend, and
it gives a puzzle game a voice — something the match-3 shelf almost never has.

---

## 5. Positioning

**The shelf we are competing with** asks the player to clear a board. Clear it faster, with
fewer moves, with a booster when stuck. The failure state is running out of moves.

**MatchBallance** asks the player to keep two boards alive while getting greedy. The failure
state is their own decision, and they can see it coming. That single change produces three
things the shelf does not have:

1. **Spectacle.** A near-loss board is visually tense. It clips well in fifteen seconds.
2. **Dueling.** Two boards on one device turn a solo habit into a social one.
3. **Skill expression.** High-level play is not "avoid risk", it is "price risk correctly" —
   which gives us a ranked ladder worth climbing.

**Audience:** 18–34 mobile puzzle players, with a strong secondary audience in Latin America
where shared-device play is the norm rather than a novelty.

**Business model:** free to play. Rewarded ads for optional boosts, a battle pass, gold, and
cosmetic hero content. No pay-to-win: power-ups are earned or bought with soft currency.

---

## 6. Where the project actually is

Verified against the repository on 20 November 2026, not from memory.

| Area | Status |
|---|---|
| Core loop (feed, tap, settle, match, score, multiplier) | Complete and playable |
| Loss conditions + graded risk gauge | Complete |
| Ten power-ups | Complete |
| Local PvP, including shared power-up selector | Complete |
| Heroes, dialogue, battle pass, gold, store | Complete |
| Play Games friends and leaderboards | Complete |
| Localization (en / es / fr / it) | Complete |
| Tutorial | Complete, pending art refresh |
| Android build + signed AAB | Automated in CI, with icon and manifest gates |
| WebGL build | Automated in CI |
| **iOS** | **Not started** — no build lane, no TestFlight |
| **Art** | **86 assets marked REPLACE, 38 marked NEW** in the art manifest |
| **Automated tests** | **1 test file** — the largest engineering gap in the project |
| Store listing | Android internal track at v0.3.2 |

Scenes in the build: `Login`, `MainMenu`, `Lobby`, `Matching`, `Game`, `PvPPowerUps`, `BallPass`,
`DevGame`.

**What this means:** the game is not a prototype and it is not finished. The systems are built;
what remains is art, the second platform, content depth, and the hardening that turns a working
build into a shippable product.

---

## 7. Roadmap — March 2027 to March 2028

Assumes a Fellowship start in March 2027 and a global launch on **1 March 2028**.
*(Note: the Fellowship terms require release before 1 March 2028 — see Risk R1.)*

### Phase 0 — Pre-Fellowship (now → February 2027)

- Submit Fellowship application with trailer, build, and this document.
- Stabilise v0.3.x on the Android internal track.
- Close the art brief so production can start on day one.
- **Exit gate:** a build a stranger can play start to finish without hand-holding.

### Phase 1 — Identity and platform spike (March → May 2027)

- Art production begins: the 86 REPLACE assets, in board → HUD → menu order.
- Tutorial rewritten around the risk gauge instead of the match rule.
- iOS technical spike: Xcode lane, CI runner, plugin parity (ads, Play Games equivalent,
  in-app purchase), portrait-only manifest, TestFlight distribution.
- First test suite: rules, risk policy, scoring, dialog parsing.
- **Exit gate:** iOS build running on device; art v1 on the board.

### Phase 2 — Content and iOS parity (June → August 2027)

- Three new heroes with full dialogue trees.
- Twelve new board layouts and two new power-ups.
- Meta tuning: battle pass pacing, gold sinks, ad placement.
- iOS internal beta via TestFlight; parity tickets closed.
- **Exit gate:** feature parity across Android and iOS.

### Phase 3 — Hardening and closed beta (September → November 2027)

- Performance and memory budgets on the low-end device matrix (URP, 60 fps target, thermal).
- Closed beta, both platforms, 4 weeks, instrumented.
- Localization QA across four languages.
- Store assets: screenshots, capsule, video, listing copy per locale.
- **Exit gate:** crash-free sessions above target, no P0 bugs open for two consecutive weeks.

### Phase 4 — Soft launch (December 2027 → mid-January 2028)

- Soft launch in one Spanish-speaking market plus one English-speaking market.
- Paid UA tests to establish CPI and D1/D7 retention baselines.
- Live-ops loop exercised: remote config, battle pass season rollover, event content.
- **Exit gate:** retention and CPI inside the band we need for a global spend.

### Phase 5 — Launch (late January → 1 March 2028)

- Feature freeze and release candidate on both platforms.
- Submission to App Store and Google Play with full localisation.
- Launch trailer, press outreach, creator seeding.
- **Exit gate:** both stores approved, launch date confirmed.

### Milestone summary

| Milestone | Date | Measurable proof |
|---|---|---|
| M1 — iOS build on device | 31 May 2027 | TestFlight build in testers' hands |
| M2 — Art v1 complete | 31 August 2027 | 124 assets delivered and integrated |
| M3 — Feature parity | 31 August 2027 | Same build behaviour on both platforms |
| M4 — Closed beta | 30 September 2027 | External testers, instrumented |
| M5 — Soft launch | 15 December 2027 | Live in two markets |
| M6 — Global launch | 1 March 2028 | Both stores live |

---

## 8. Use of funds — $50,000

| Line | Amount | What it buys |
|---|---|---|
| **iOS port, pipeline and submission** | **$12,000** | Xcode build lane and macOS CI runner, plugin parity for ads / IAP / achievements, TestFlight cycles, App Store review handling, iOS device QA, Apple Developer programme |
| **Art production** | **$16,000** | 86 REPLACE assets and 38 NEW assets: board, HUD, menus, hero portraits and expressions, power-up icons, store capsule and screenshots |
| **Content** | **$8,000** | Three heroes with dialogue trees, twelve board layouts, two power-ups, tutorial rewrite |
| **QA and device matrix** | **$6,000** | Contract testers, low-end and mid-range Android plus iOS devices, four weeks of closed beta |
| **Marketing and UA** | **$6,000** | Launch trailer, press kit, vertical short-form clips, soft-launch paid UA tests |
| **Tooling and backend** | **$2,000** | Remote config, analytics, crash reporting, server costs through launch |
| **Total** | **$50,000** | |

iOS is the single largest line on purpose. The game is Android-only today, and every system we
built — rewards, ads, leaderboards, saves — has to be proven on a second platform before it can
be called a product. That is the difference the Fellowship makes: without it, iOS slips past
launch and the game ships to half its addressable market.

---

## 9. Success metrics

| Metric | Target at launch |
|---|---|
| D1 retention | 40% |
| D7 retention | 18% |
| Session length | 6–9 minutes |
| Crash-free sessions | 99.5% |
| Cost per install (soft launch) | Within band set by soft-launch tests |
| Store rating | 4.3+ |

These are targets, not results. We are pre-soft-launch and we will publish the real numbers from
the soft launch to the Fellowship partner.

---

## 10. Risks and mitigations

**R1 — Launch date sits exactly on the Fellowship deadline.**
The terms require release *before* 1 March 2028. **Mitigation:** declare the launch window as
February 2028 and treat 29 February as the internal hard date, so a two-week slip does not break
eligibility.

**R2 — The project has already shipped builds, which can read as "already launched".**
**Mitigation:** be explicit that v0.3.x exists only on the internal track, and that soft launch is
a measurement phase, not a release. The roadmap dates and the store state back this up.

**R3 — Art is the critical path.**
124 assets cannot be compressed into a month. **Mitigation:** the art manifest is already an
itemised contract with filenames, sizes and import settings; production starts in Phase 1, and
the board comes first because it is what reviewers see.

**R4 — Bus factor.**
The codebase is concentrated in one developer. **Mitigation:** the Fellowship-funded phase pays
for a second engineer on iOS parity and for the test suite, and the architecture is already
layered (domain / app / infra / presentation) with the rules isolated from the engine.

**R5 — Free-to-play economics in a small market.**
**Mitigation:** soft launch with paid UA tests before committing to a global spend, and the
battle pass plus hero content as retention rather than acquisition levers.

---

## 11. Team

| Role | Name | Responsibility |
|---|---|---|
| Founder, design and engineering | Luis Plata | Core loop, architecture, live systems, build pipeline |
| Engineering | PeryLoth | Gameplay and tooling |
| Art | *contract, funded by the grant* | Board, HUD, heroes, store assets |
| Audio | *to confirm* | Music and SFX |
| QA | *contract, funded by the grant* | Device matrix and closed beta |

Studio: Bellseboss Studio, Bogotá D.C., Colombia. Seven people across development, art and
production.

---

## 12. Technical appendix

- **Engine:** Unity 6000.3.10f1, URP 17.3.0
- **Architecture:** four layers — `Domain` (entities, rules, events), `App` (use cases, ports),
  `Infra` (web, ads, persistence, economy), `Presentation` (views, mediators, view models)
- **Packages in use:** Addressables, Input System 1.18, Localization 1.5.8, URP, Cinemachine 3,
  Timeline, Unity Gaming Services (Economy, Cloud Save, Cloud Code, Authentication, Friends),
  Remote Config, Unity Test Framework, Google Play Games, Google Mobile Ads
- **Build:** automated CI for Android (signed AAB, launcher icon verification, manifest
  orientation verification) and WebGL; single codebase, portrait, minimum Android API 30
- **Data:** server-driven heroes, dialogue, battle pass and remote config through Unity Gaming
  Services; Cloud Save with a local cache for offline play
- **Testing:** unit tests for rules and parsing, currently one suite — expanded from Phase 1

---

*Prepared for the Unity Fellowship application, November 2026.*
