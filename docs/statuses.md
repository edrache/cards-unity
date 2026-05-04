# Status Definitions

Statuses are persistent penalties attached to the player, evaluated at the start of each turn before slot effects resolve. Each entry lists all fields needed to create a `StatusDefinition` ScriptableObject asset.

Fields: `statusName` · `description` · `durationType` · `durationTurns` · `action` · `targetCardType` · `actionValue`

---

## Force

Physical penalties — the player is injured, pursued, or physically compromised.

| # | Name | Description | Duration | Action | Target | Value |
|---|------|-------------|----------|--------|--------|-------|
| 1 | Bloodied | A wound slows every move. Force erodes with each passing moment. | Turns 3 | DamageToThreat | Force | 1 |
| 2 | Winded | Pushed too hard, breath won't return. Force crumbles turn by turn. | Turns 2 | DamageToThreat | Force | 1 |
| 3 | Disarmed | Without a weapon, every confrontation costs more than it should. | Turns 3 | DamageToThreat | Force | 2 |
| 4 | Broken | Something snapped. The body refuses to comply. Force bleeds out. | Permanent | DamageToThreat | Force | 1 |
| 5 | Cornered | No exit. The longer this lasts, the tighter the net closes. | Turns 4 | AddToClock | Force | 1 |
| 6 | Chased | They are close and gaining. Every second spent is a second lost. | Turns 2 | AddToClock | Force | 2 |
| 7 | Marked | They know your face now. Opponents are better prepared for you. | Turns 3 | DrawOpponentCard | Force | 1 |
| 8 | Restrained | Movement stripped away — cards placed for Force cannot hold. | Turns 2 | ReturnCardsFromSlots | Force | 0 |
| 9 | Limping | Each step is a gamble. Force cards carry reduced weight. | Turns 3 | ModifyPlayerCardsOfTypeValue | Force | -1 |
| 10 | Exposed Position | No cover left. Opponents read your Force plays before you make them. | Turns 3 | IncreaseOpponentCardsOfTypeValue | Force | 1 |

---

## Presence

Social penalties — the player's reputation, leverage, or identity is compromised.

| # | Name | Description | Duration | Action | Target | Value |
|---|------|-------------|----------|--------|--------|-------|
| 1 | Exposed | Your cover is burned. Every contact you approach pulls further away. | Turns 3 | DamageToThreat | Presence | 1 |
| 2 | Blacklisted | Word has spread. No one in these circles will deal with you. | Permanent | DamageToThreat | Presence | 1 |
| 3 | Humiliated | The scene left a mark. Authority and trust dissolve around you. | Turns 3 | DamageToThreat | Presence | 2 |
| 4 | Compromised | Someone holds something over you. Presence erodes quietly. | Permanent | DamageToThreat | Presence | 1 |
| 5 | Framed | A false story is being told about you, and people are believing it. | Turns 4 | AddToClock | Presence | 1 |
| 6 | In Debt | You owe the wrong people. The debt accrues whether you act or not. | Permanent | AddToClock | Presence | 1 |
| 7 | Burned | Your informants have been turned. Opponents know who you talk to. | Turns 4 | DrawOpponentCard | Presence | 1 |
| 8 | Distrusted | Suspicion precedes you. Presence cards lose their edge. | Turns 2 | ModifyPlayerCardsOfTypeValue | Presence | -1 |
| 9 | Silenced | Threats have been made. Contacts go quiet, options narrow. | Turns 3 | ModifyPlayerCardsOfTypeValue | Presence | -1 |
| 10 | Manipulated | Someone has shaped the narrative around you. Opponents press harder. | Turns 3 | IncreaseOpponentCardsOfTypeValue | Presence | 1 |

---

## Wit

Informational penalties — the player is misled, confused, or mentally outmaneuvered.

| # | Name | Description | Duration | Action | Target | Value |
|---|------|-------------|----------|--------|--------|-------|
| 1 | Confused | False leads have muddied the picture. Wit fractures with every turn. | Turns 3 | DamageToThreat | Wit | 1 |
| 2 | Misled | The map is wrong. Every step taken in confidence leads further astray. | Turns 2 | DamageToThreat | Wit | 2 |
| 3 | Misinformed | A planted lie has taken root. Wit drains as the truth grows harder to find. | Permanent | DamageToThreat | Wit | 1 |
| 4 | Blind Spot | Something important keeps slipping just out of sight. | Turns 3 | DamageToThreat | Wit | 2 |
| 5 | Distracted | Too many threads at once. Focus fragments, and time with it. | Turns 3 | AddToClock | Wit | 1 |
| 6 | Rattled | Thrown off your rhythm. Each turn costs a beat you can't afford. | Turns 2 | AddToClock | Wit | 1 |
| 7 | Memory Lapse | A crucial detail slips away. Opponents retrieve what you forgot. | Turns 3 | DrawOpponentCard | Wit | 1 |
| 8 | Overwhelmed | Too much, too fast. Cards you placed cannot keep their footing. | Turns 3 | ReturnCardsFromSlots | Wit | 0 |
| 9 | Paranoid | Every clue looks like a trap. Wit cards buckle under the doubt. | Turns 4 | ModifyPlayerCardsOfTypeValue | Wit | -1 |
| 10 | Outmaneuvered | They predicted your move before you made it. Opponents grow sharper. | Turns 2 | IncreaseOpponentCardsOfTypeValue | Wit | 1 |

---

## Tier 1

Moderate penalties — early-stage consequences. Turn-based, single-bar pressure, value 1.

| # | Name | Description | Duration | Action | Target | Value |
|---|------|-------------|----------|--------|--------|-------|
| 1 | Under Surveillance | Someone is watching every move you make. Time is slipping. | Turns 3 | AddToClock | Presence | 1 |
| 2 | Sprained | One bad step. Force carries the cost for a few turns. | Turns 2 | DamageToThreat | Force | 1 |
| 3 | Second-Guessing | Doubt has crept in. Wit erodes while certainty waits. | Turns 2 | DamageToThreat | Wit | 1 |
| 4 | Bruised Ego | A public slight that lingers. Presence fades as the story spreads. | Turns 2 | DamageToThreat | Presence | 1 |
| 5 | Tailed | A shadow follows. Opponents learn more than they should. | Turns 3 | DrawOpponentCard | Force | 1 |
| 6 | Shaken | An unexpected blow left you rattled. The clock doesn't wait. | Turns 2 | AddToClock | Wit | 1 |
| 7 | Strained | Pushing past limits. Force bleeds slowly but steadily. | Turns 3 | DamageToThreat | Force | 1 |
| 8 | Gossiped About | Rumors circulate in the wrong circles. Presence takes the hit. | Turns 3 | DamageToThreat | Presence | 1 |
| 9 | Spooked | Jumpiness replaces clarity. Wit cards lose their footing. | Turns 3 | ModifyPlayerCardsOfTypeValue | Wit | -1 |
| 10 | Loose Talk | You said too much. Opponents are already better informed. | Turns 3 | IncreaseOpponentCardsOfTypeValue | Presence | 1 |

---

## Tier 2

Serious penalties — late-stage consequences. Permanent or long duration, value 2–3, often targeting multiple resources.

| # | Name | Description | Duration | Action | Target | Value |
|---|------|-------------|----------|--------|--------|-------|
| 1 | Hunted | They have committed resources to finding you. The clock never stops. | Permanent | AddToClock | Force | 2 |
| 2 | Crippled | The damage is done and it isn't healing. Force drains every turn, without end. | Permanent | DamageToThreat | Force | 2 |
| 3 | Disgraced | Your name is ruin. No one in the city will stand with you now. | Permanent | DamageToThreat | Presence | 2 |
| 4 | Obsessed | The case has swallowed everything else. Wit hollows out turn by turn. | Permanent | DamageToThreat | Wit | 2 |
| 5 | Broken Will | The pressure has done its work. Presence collapses from the inside. | Turns 5 | DamageToThreat | Presence | 3 |
| 6 | Shattered | A blow too many. Force hemorrhages at a rate the body cannot sustain. | Turns 4 | DamageToThreat | Force | 3 |
| 7 | Mind Fractured | Too many contradictions at once. Wit shatters under the weight of false truths. | Turns 4 | DamageToThreat | Wit | 3 |
| 8 | Burned Bridges | Every favor called in, every contact spent. Opponents fill the vacuum. | Permanent | DrawOpponentCard | Presence | 2 |
| 9 | Wanted | A bounty posted and believed. Opponents mobilize faster with every turn. | Turns 5 | AddToClock | Force | 2 |
| 10 | Fully Compromised | Everything about you is known. Opponents anticipate every play you have left. | Permanent | IncreaseOpponentCardsOfTypeValue | Wit | 2 |
