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
