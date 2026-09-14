## Transient infrastructure failures

Do not treat temporary OpenAI/Codex infrastructure failures as task failures. This includes auto-approval reviewer capacity errors, temporary service-unavailable responses, transient rate limits, and similar retryable service errors.

For retryable failures:
- Preserve all current progress.
- Retry the failed operation with bounded backoff for up to 5 minutes.
- Honor any explicit server-provided retry interval when available.
- Otherwise use increasing delays between attempts.
- Continue independent work during the retry period when it is safe to do so.
- Resume from the failed operation rather than restarting completed work.
- Do not terminate the task solely because of a transient infrastructure error unless retries have been exhausted or user intervention is genuinely required.