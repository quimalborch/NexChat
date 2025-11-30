# Security Policy for NexChat

## Supported Versions

| Version         | Supported         |
|-----------------|-------------------|
| 1.0.x (latest)  | ✅                |
| < 1.0           | ❌                |

## Reporting a Vulnerability

If you find a potential security issue in NexChat, please do **not** open a public issue or pull request. Instead, report it privately so we can handle it safely.

To report a vulnerability:

- Go to the **Security → Report a vulnerability** section of this GitHub repository (if “Private vulnerability reporting” is enabled). :contentReference[oaicite:1]{index=1}  
- Provide a clear description of the problem, including:
  - The type of vulnerability (e.g. buffer overflow, authentication bypass, etc.).  
  - The file(s) and exact location(s) in code (branch/commit/line number) involved.  
  - Steps or minimal repro instructions to trigger the issue.  
  - If possible: minimal proof-of-concept code, screenshots or logs.  
  - Impact: what could happen (data leak, remote code exec, loss of confidentiality, etc.).

After submitting the report, you’ll be notified that we got it. We might ask follow-up questions if needed.

If for any reason private reporting is not available, as fallback send a message to **[@quimalborch](https://github.com/quimalborch)** — try to use a secure channel (like email or encrypted message), and mention “NexChat security issue” in the subject.

## Response Process & Timeline

| Step                     | Typical time to respond |
|--------------------------|-------------------------|
| Acknowledge receipt      | Within **48 hours**     |
| Request further info     | If needed, ASAP         |
| Fix & publish advisory   | As soon as possible — depends on severity |
| Public disclosure        | After fix is released and users are notified |

## Important Guidelines

- Do **not** publicly disclose a vulnerability before a fix or advisory is ready.  
- Provide enough detail so we can reproduce and evaluate the issue — vague reports may delay resolution.  
- Respect coordinated disclosure: allow reasonable time to release patches before sharing exploit details.  
- Avoid posting sensitive data (keys, passwords, personal info) in public.  

---

Thanks for helping keep NexChat safe and secure 💜  
