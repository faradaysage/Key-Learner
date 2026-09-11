# Effect recipes

An effect plugin is a JSON data file in %LOCALAPPDATA%/KeyLearner/effects/ (or <your --data directory>/effects/). It contains no executable code. Copy moon-dust.effect.json from this folder there, then reopen the parent studio. The recipe appears in each dictionary entry's Celebration selector.

Fields:
- Name: unique display name, 1-40 characters.
- Shape: 0 confetti, 1 rain, 2 orbit, 3 bubbles, 4 embers.
- Count: 1-400 particles, still subject to the global particle budget.
- Speed: 0-3 multiplier.
- Lifetime: 0.5-8 seconds.
- Curl: -200 to 200 flow force.
- Gravity: -2 to 2 multiplier of global gravity.
- Trail: 0-1 trail brightness.

Values are clamped. Up to 50 recipe files are read; files over 32 KB or malformed JSON are skipped. Gentle motion reduces emission and movement and disables trails. Names should differ from built-in celebration names. Existing active particles retain the recipe values they were created with.

This deliberately small format gives words custom visual responses without running third-party code in a child's session. Images and WAV actions are attached separately on the same word. General scripted actions, shader plugins and full fluid simulation are not implemented.
