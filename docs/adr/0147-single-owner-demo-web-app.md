# ADR 0147 · Single-owner web interface on the demo VM

**Accepted November 4, 2026.** The owner selected the [web proposal](../history/2026-11-04-web-proposal.md). Implementation and hosted verification are pending.

## Context

The current terminal runner supports operator review. Judges need a web entry point on Vultr, while model calls spend the owner's budget and browser jobs require explicit approval.

## Decision

Serve a static form and minimal ASP.NET API behind HTTPS on the disposable VM. One authenticated owner can request a draft, inspect the exact packet and approve its digest. The private worker serializes paid work, consumes each approval once and records the outcome. A separate read-only judge view serves only redacted snapshots selected for publication. Follow the access, hosting and verification criteria in the proposal.

## Consequences

Operating the demo requires a certificate, a reviewed 443 firewall rule, session and CSRF protection, and isolation between the public gallery and execution authority. Verify these on the intended VM before publishing the link. The existing terminal flow remains available for qualification and local demonstrations.
