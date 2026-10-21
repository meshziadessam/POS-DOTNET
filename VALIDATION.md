# Push 1.2.0 validation — 2026-10-21

- Build: zero errors / warnings, .NET Framework 4.8. x64 and x86 test processes passed.
- 165 operational/release checks on each architecture: sales, totals, stock, idempotent checkout, waiting orders, backups/restore including a synthetic shop logo, reports, manager/employee policy and UI layouts.
- Neutral shop defaults and persisted name/phone/address/footer checked. No former restaurant logo is embedded.
- 19 licensing checks on each architecture: valid signature, wrong device, modified payload, forged signature, unrelated issuer, malformed/oversized/missing license, wrong product/version, inactive state, persistent import, invalid import preserving activation, no private RSA parameters in client key, installer-only initialization, mixed initialize/setup bypass rejected, actual application startup gates for account setup and printer test, valid activation reaches initial-manager setup.
- Activation dialog rendered and visually reviewed. Valid account setup was observed through the actual application entry point; credentials were not entered by Windows UI automation.
- Installer smoke: separate AppId and isolated directory, install exit 0, executable and empty database created, no preactivated license. Uninstall exit 0, executable removed, data preserved. Full and small customer installers built from the same client binary.
- Existing Restaurant POS database SHA256 stayed 026823DD812AB4CDB8097944D3680289385574274EC1643C174C9BA4AF0F2494.

The private signing key is outside this project and delivery packages. Only the public verification key is embedded. QA activation files stay outside the source and artifacts directories. Packages are assembled using explicit allowlists and ZIP CRC checks.

This offline MVP rejects an unchanged license on a different device; it does not promise protection from binary patching, full Windows/VM cloning or remote revocation. No Windows Authenticode certificate is included. Physical printer output, Windows 7/10 hardware, alternate DPI and long-running shifts remain acceptance tests. Live emulator printing was not repeated because its trial quota was exhausted; receipt-file tests passed.

## 1.2.1 — customer branding

Added installer branding pages and Unicode-safe filename validation. Arabic-named executable and matching config successfully installed and initialized a fresh database with the requested Arabic restaurant name, test phone/address and logo. Setup data is written before the initialize run; later upgrades preserve manager edits. Cashier logo and gray credit placement were verified at 1024x768, with screenshots at additional widths. Existing license issuer window was preserved during the build; the updated seller tool has a separate versioned output directory. Tests also cover one-time setup branding import.

Final 1.2.1 checks: 170 operational/UI checks and 19 licensing checks passed. Installer fresh setup with an Arabic business name, test phone/address and an image produced the renamed executable and matching config, persisted the requested data and normalized the logo. Rename-on-upgrade removed the previous executable and retained manager-edited settings. The matching Unicode shortcut was verified against the installed executable filename. Test uninstall succeeded and preserved the data. Existing issuer process was left open. Physical printing was not repeated.
# Version 1.2.2 executable icon fix

The installer now converts the selected restaurant image into six Windows icon sizes (16–256 pixels) and embeds them in the installed executable before creating shortcuts. The original image remains available for the cashier header and receipts.

An isolated silent installer test verified the actual extracted EXE icon against the selected image and confirmed the modified executable successfully initialized its database. Invalid image input leaves the executable unchanged. Existing client data and activation files are not modified by this helper.

