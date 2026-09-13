# Diagnosing a blocked development command

When a command or preview fails, record the local time and time zone, exact executable path and arguments, exit code, and complete error in ignored `artifacts/` diagnostics. Read relevant Windows and Bitdefender events around that timestamp. Record the security feature, detection name, detected path and action, and correlate them with the process or download involved.

Do not assume a compiler error, an OpenGL startup failure, or an access-denied sandbox error is antivirus interference. A signed launcher does not establish that every command it runs is safe. Do not restore quarantined troubleshooting files, disable protection, or recommend a broad PowerShell or project-directory exception without establishing what was blocked and why the change is needed.

Only ask the parent to go to the computer when there is a specific, evidenced action to take. Otherwise retain the diagnostic details and continue work that is unaffected. Never include private event logs or machine/account details in a public pull request.
