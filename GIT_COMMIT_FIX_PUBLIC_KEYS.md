# ?? Fix: Auto-create public_keys.json if missing

## Problem
Users were receiving "File not found" error for `public_keys.json`, causing:
- ?? Cannot encrypt: recipient public key not found
- ?? Sending UNENCRYPTED message - no public key available

## Root Cause
`PublicKeyManager` only **read** the file if it existed, but never **created** it initially.

## Solution
1. **Auto-create** `public_keys.json` with empty JSON `{}` on first run
2. **Improved error handling** for empty or corrupted JSON files
3. **Added recovery** mechanism to recreate file if invalid

## Changes
- `Security/PublicKeyManager.cs`:
  - Constructor now creates empty `public_keys.json` if missing
  - `LoadPublicKeys()` handles empty JSON gracefully
  - Added JSON corruption recovery

## Files Added
- `FIX_PUBLIC_KEYS_FILE.md` - Detailed fix documentation
- `fix_permissions.ps1` - PowerShell script for permission issues

## Testing
? Fresh install creates file automatically
? Existing installations work without changes
? Corrupted JSON files are recovered
? Public key exchange works correctly
? Messages encrypt successfully

## Impact
- **Before**: Users couldn't exchange keys ? messages sent unencrypted
- **After**: Automatic file creation ? E2EE works out of the box

Fixes #N/A (user-reported issue)
