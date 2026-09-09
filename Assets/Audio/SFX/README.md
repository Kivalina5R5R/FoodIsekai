# Tavern gameplay sound effects

Original synthesized sounds created for FoodIsekaiZ. No external recordings or licensed samples are used.
48 kHz, mono, 16-bit PCM WAV; shared headroom and softened attacks. Generated with seeded string excitation, damped resonators, metallic bell partials and filtered rustle.

Cues follow GameSoundCue order. Food pickup uses a plate tap/rustle boosted by 6 dB; accepted service resolves upward; wrong food uses a low wooden double tap. Visible order panels use a short paper flick with a muted wooden tick. Only the first emoji popup per NPC uses an audible low water bubble; face changes and later re-shows stay silent. Menu dismissal is silent. A short dry pop boosted by 3.5 dB plays when the successful-order particle burst starts after collapse, with a 0.18-second shared cooldown. Distinct NPC reveals queue up to six bubbles with 0.12-second spacing, instead of muting the second NPC; menu sounds have a separate 0.6-second cooldown. Payout, collection and banking use different coin clusters. Expiry uses an audible descending bell pair with playback priority. Uncollected piles use a short coin rattle when their reminder animation starts; simultaneous reminders share a 0.35-second cooldown; wave opening, break and completion have separate musical cadences. TimeWarning plays once at 10 and 5 seconds remaining.

Scene object Gameplay Sound Effects holds an independent 2D pool of six voices, master volume 0.65 and per-cue cooldowns. A dedicated BGM source lives on the same Gameplay Sound Effects object at volume 0.18, separate from the six effect voices. No sound plays for an empty bank visit or an unsuccessful pickup.

Generation source: Tools/Audio/GenerateTavernSounds.py (Python standard library; regenerates these authored assets).
