---
name: run-demo-test
description: Run the repeatable BaristaNotes MAUI Android UI demo. Use when the user says "run the demo test", asks to repeat the bean/profile/drink demo, or asks to record this demo. Deploys the MAUI app and uses DevFlow for every app action. Desktop recordings use QuickTime, with Copilot on the left and the emulator on the right.
---

# Run the demo test

Run the complete sequence without questions or pauses unless a real blocker
requires user input. Do not substitute the native Android project.

1. Load `maui-devflow-debug`. For a recording, also load
   `ailoha-recording-devflow`.
2. Inspect the mobile-device canvas capabilities. Open the canvas with instance
   ID `barista-demo`. Select the existing Android emulator
   `BaristaNotes-Pixel8a-API34`. Check its current state before booting it.
   Reuse it if it is already running. Do not erase it, uninstall the app, or
   stop another device. If it is missing, inspect the device catalog and ask
   before creating a replacement.
3. Get the selected device from the canvas. Use its actual `udid`
   (`emulator-5554`, for example), not the AVD name, for deployment.
4. For a requested recording, use QuickTime **File > New Screen Recording >
   Record Entire Screen**. Capture the full macOS desktop, with GitHub Copilot
   on the left and the emulator on the right. Do not replace this with an
   emulator-only recording. Use computer-use tools for desktop UI controls.
   If those tools block Copilot or cannot reach the recording toolbar, ask
   the user to arrange the windows and start recording. Wait for confirmation
   before running the sequence. Do not work around a tool safety block.
5. From the repository root, run:

   ```bash
   python3 -B scripts/demo_test.py --device <selected-emulator-serial> \
     --output-dir <session-files>/demo-<unique-run-id>
   ```

   Use a new output directory for every run. Keep the canvas open throughout
   when it works. If the canvas cannot display the emulator, do not block the
   test or switch to another emulator. Continue with the emulator's own
   desktop window, DevFlow screenshots, and QuickTime. Report the limitation.
   Do not use `--skip-deploy` for an accepted demo run. The script starts the
   broker, configures device-specific forwarding, deploys Debug
   `src/BaristaNotes/BaristaNotes.csproj` for `net11.0-android`, waits for
   DevFlow, and selects the matching app agent explicitly.
6. After the test, stop QuickTime and save the recording. If the toolbar is
   inaccessible, promptly ask the user to stop and save it. Do not leave it
   recording while doing unrelated work.
7. Read `result.json`. Report UI-test success only if `success` is true. Inspect the
   saved screenshots for the forms, configured drink, and reopened drink.
   Inspect the saved desktop recording if its path is available. Otherwise,
   state that saving was confirmed by the user, not inspected by the agent.
   Do not claim that an emulator recording proves the desktop framing.

## Visible sequence

- Create a uniquely named Ethiopia bean with roaster and origin through its
  form. Reopen it and check the saved fields.
- Create a uniquely named local guest profile with “Prefers milk drinks.”
  Reopen it and check the saved fields.
- Show method choices. Select V60, then Espresso. This resets method-specific
  numbers through the app so repeated runs still show changes from defaults.
- Select the new bean's automatically created bag. Show drink choices and
  select Cortado. Set dose to 19 g, yield to 42 g, time to 31 seconds, and
  temperature to 199 F or 93 C, preserving the existing unit preference.
- Select the new guest in the **Made for** column. Save the drink. Open it
  from history and check the stored values.
- Restart through a data-preserving deployment, then reopen the drink from
  history to confirm persistence.

All application navigation, entry, selection, scrolling, saving, and checking
must use DevFlow UI commands. Do not use Shell-route navigation, action APIs,
jobs, direct model calls, preference writes, database access, or seed/reset
commands to replace visible app steps.

Each run adds one bean, its initial bag, one profile, and one drink. Keep earlier
records. Do not delete rehearsal data without explicit permission.

## Failure handling

The script stops on a failed UI action or assertion, writes a result and action
transcript, and attempts to capture the failed screen. Read the evidence before
retrying. Never report a failed or truncated recording as a successful demo.
The Python script does not start or stop QuickTime.
