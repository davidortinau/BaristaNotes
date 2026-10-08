# Test generation from recordings

Use recordings and transcripts to generate repeatable automation, not to encode
raw gestures. The generated output should be maintainable in the target repo.

## Choose the right test layer

| Defect or scenario | Prefer |
|---|---|
| Pure validation, formatting, or state reducer bug | Existing unit test framework |
| Real app navigation/rendering behavior | Ailoha.Driver integration/UI test |
| Platform-specific gesture or native control issue | Existing platform UI test layer |
| Human support or issue report | Manual repro plus minimal evidence |
| Cross-agent protocol replay | Ailoha CLI commands or JSONL batch |

Do not introduce a new test framework just because a recording exists. Follow
the repo's existing test style and fixtures.

## Required sections

Every generated test or replay should include:

1. Preconditions and setup.
2. Stable selectors or route names used.
3. Actions grouped by user intent.
4. Assertions after meaningful transitions.
5. Cleanup when state is created.

Avoid hard-coded credentials, personal data, timestamps, local file paths, and
recording paths inside tests.

## CLI replay example

```bash
ailoha agent wait --timeout 60
LOGIN_BUTTON=$(ailoha ui query --automationId LoginButton --json | jq -r '.[0].id')
EMAIL=$(ailoha ui query --automationId EmailEntry --json | jq -r '.[0].id')
PASSWORD=$(ailoha ui query --automationId PasswordEntry --json | jq -r '.[0].id')
ailoha ui fill "$EMAIL" "test@example.com"
ailoha ui fill "$PASSWORD" "$TEST_PASSWORD"
ailoha ui tap "$LOGIN_BUTTON"
ailoha ui query --automationId HomeTitle
```

Use shell variables only for local replay scripts. Tests should use typed helper
methods or the repo's existing test utilities.

## CLI batch pattern

Use batch mode for deterministic command sequences that should run through the
CLI. Input lines are CLI commands; output lines are JSONL command results.

```bash
cat <<'EOF' | ailoha batch
ui fill <resolved-email-element-id> "test@example.com"
ui fill <resolved-password-element-id> "$TEST_PASSWORD"
ui tap <resolved-login-button-id>
ui assert --id <resolved-home-title-id> IsVisible true
EOF
```

Keep secrets as environment variables or test fixtures, not literals. If a
command needs an element id that is not stable across runs, resolve it before
constructing the batch or use a shell script/driver test where lookup results can
feed later actions.

## Ailoha.Driver test shape

For .NET integration tests, follow the existing fixture style in the repo. A
typical shape is:

```csharp
[Fact]
public async Task Login_WithValidCredentials_ShowsHome()
{
    await Driver.ConnectAsync();

    var email = await FindByAutomationIdAsync("EmailEntry");
    var password = await FindByAutomationIdAsync("PasswordEntry");
    var login = await FindByAutomationIdAsync("LoginButton");

    await Driver.FillAsync(email.Id, "test@example.com");
    await Driver.FillAsync(password.Id, TestSecrets.Password);
    await Driver.TapAsync(login.Id);

    var home = await FindByAutomationIdAsync("HomeTitle");
    Assert.True(home.IsVisible);
}
```

Adapt names to the actual driver/test helpers in the target project. Do not
paste this skeleton unchanged if the repo already has better helpers.

## Assertion guidance

Good assertions prove user-visible outcomes:

- target screen title is visible
- created item appears with expected text
- deleted item no longer appears
- error banner has expected message
- expected request succeeded and UI reflects it
- persisted preference changed

Weak assertions only prove mechanics:

- tap returned success
- screenshot file exists
- no exception was thrown

## Generation stop signals

- The output uses verified selectors from the app or recording transcript.
- Setup and cleanup are explicit.
- There is at least one meaningful final assertion.
- The generated code follows the target repo's existing conventions.
