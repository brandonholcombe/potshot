#!/usr/bin/env python3
"""Symbolic Alignment Tool — verify project state via declarative symbols.

Symbols define project truth as typed properties and doc references.
Interlocks are edges between symbols that must stay consistent.
A Merkle-like hash tree locks the entire state for integrity checking.

Usage:
    python scripts/align.py init     Create starter manifest and lock
    python scripts/align.py lock     Regenerate manifest.lock from manifest.json
    python scripts/align.py check    Verify alignment (exit 0=ok, 1=broken, 2=stale)
    python scripts/align.py status   Human-readable alignment report
"""

import argparse
import hashlib
import json
import os
import sys
from datetime import datetime, timezone

MANIFEST_PATH = "symbols/manifest.json"
LOCK_PATH = "symbols/manifest.lock"
DOCS_DIR = "docs"

# ─── Color helpers ────────────────────────────────────────────────────────────

USE_COLOR = sys.stdout.isatty()


def _c(code, text):
    return f"\033[{code}m{text}\033[0m" if USE_COLOR else text


def green(t):
    return _c("32", t)


def yellow(t):
    return _c("33", t)


def red(t):
    return _c("31", t)


def bold(t):
    return _c("1", t)


def dim(t):
    return _c("2", t)


# ─── Hashing helpers ─────────────────────────────────────────────────────────

def hash_bytes(data: bytes) -> str:
    """SHA-256, truncated to 16 hex chars."""
    return hashlib.sha256(data).hexdigest()[:16]


def hash_file(path: str) -> str | None:
    """Hash a file's contents. Returns None if file missing."""
    try:
        with open(path, "rb") as f:
            return hash_bytes(f.read())
    except FileNotFoundError:
        return None


def hash_properties(props: dict) -> str:
    """Deterministic hash of a properties dict."""
    canonical = json.dumps(props, sort_keys=True, separators=(",", ":"))
    return hash_bytes(canonical.encode())


def compute_leaf_hash(doc_hashes: list[str], prop_hash: str) -> str:
    """Leaf hash = hash(sorted doc hashes + prop hash)."""
    parts = sorted(doc_hashes) + [prop_hash]
    combined = "|".join(parts)
    return hash_bytes(combined.encode())


def compute_root_hash(leaf_hashes: list[str]) -> str:
    """Root hash = hash(sorted leaf hashes)."""
    combined = "|".join(sorted(leaf_hashes))
    return hash_bytes(combined.encode())


# ─── Interlock validation ────────────────────────────────────────────────────

def validate_interlocks(symbols: dict) -> list[dict]:
    """Check all interlock edges. Returns list of results."""
    results = []
    for sym_name, sym in symbols.items():
        for foreign_ref, local_prop in sym.get("interlocks", {}).items():
            # Parse "other_symbol.their_property"
            parts = foreign_ref.split(".", 1)
            if len(parts) != 2:
                results.append({
                    "symbol": sym_name,
                    "interlock": foreign_ref,
                    "local_prop": local_prop,
                    "status": "INVALID_REF",
                    "detail": f"Cannot parse reference '{foreign_ref}'"
                })
                continue

            other_sym, other_prop = parts

            if other_sym not in symbols:
                results.append({
                    "symbol": sym_name,
                    "interlock": foreign_ref,
                    "local_prop": local_prop,
                    "status": "MISSING_SYMBOL",
                    "detail": f"Symbol '{other_sym}' not found"
                })
                continue

            other_props = symbols[other_sym].get("properties", {})
            local_props = sym.get("properties", {})

            if other_prop not in other_props:
                results.append({
                    "symbol": sym_name,
                    "interlock": foreign_ref,
                    "local_prop": local_prop,
                    "status": "MISSING_PROPERTY",
                    "detail": f"Property '{other_prop}' not found on '{other_sym}'"
                })
                continue

            if local_prop not in local_props:
                results.append({
                    "symbol": sym_name,
                    "interlock": foreign_ref,
                    "local_prop": local_prop,
                    "status": "MISSING_PROPERTY",
                    "detail": f"Local property '{local_prop}' not found on '{sym_name}'"
                })
                continue

            other_val = other_props[other_prop]
            local_val = local_props[local_prop]

            if other_val == local_val:
                results.append({
                    "symbol": sym_name,
                    "interlock": foreign_ref,
                    "local_prop": local_prop,
                    "status": "PASS",
                    "detail": f"{other_val} == {local_val}"
                })
            else:
                results.append({
                    "symbol": sym_name,
                    "interlock": foreign_ref,
                    "local_prop": local_prop,
                    "status": "FAIL",
                    "detail": f"{other_val} != {local_val}"
                })

    return results


# ─── Build lock data ─────────────────────────────────────────────────────────

def build_means(sym_name: str, props: dict) -> str:
    """Generate a human-readable summary of what a symbol's hash represents."""
    if not props:
        return f"{sym_name}: no properties defined"
    parts = [f"{k}={v}" for k, v in sorted(props.items())]
    return f"{sym_name}: {', '.join(parts)}"


def build_lock(manifest: dict) -> dict:
    """Build the full lock structure from a manifest."""
    symbols = manifest.get("symbols", {})
    interlock_results = validate_interlocks(symbols)

    # Index interlock results by symbol
    interlock_by_sym = {}
    for r in interlock_results:
        interlock_by_sym.setdefault(r["symbol"], []).append(r)

    lock_symbols = {}
    leaf_hashes = []
    all_aligned = True

    for sym_name in sorted(symbols.keys()):
        sym = symbols[sym_name]
        props = sym.get("properties", {})
        docs = sym.get("docs", [])

        # Hash docs
        doc_entries = {}
        doc_hashes = []
        for doc_path in sorted(docs):
            h = hash_file(doc_path)
            if h is None:
                doc_entries[doc_path] = {"hash": "0" * 16, "status": "MISSING"}
                all_aligned = False
            else:
                doc_entries[doc_path] = {"hash": h, "status": "current"}
                doc_hashes.append(h)

        # Compute leaf hash
        prop_hash = hash_properties(props)
        leaf = compute_leaf_hash(doc_hashes, prop_hash)
        leaf_hashes.append(leaf)

        # Interlocks for this symbol
        sym_interlocks = {}
        for r in interlock_by_sym.get(sym_name, []):
            key = f"{r['interlock']} == {r['local_prop']}"
            sym_interlocks[key] = r["status"]
            if r["status"] != "PASS":
                all_aligned = False

        lock_symbols[sym_name] = {
            "hash": leaf,
            "means": build_means(sym_name, props),
            "docs": doc_entries,
            "interlocks": sym_interlocks,
        }

    root = compute_root_hash(leaf_hashes) if leaf_hashes else hash_bytes(b"empty")
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    return {
        "root": root,
        "generated": now,
        "status": "aligned" if all_aligned else "broken",
        "symbols": lock_symbols,
    }


# ─── Subcommands ─────────────────────────────────────────────────────────────

def cmd_init(args):
    """Create starter manifest and lock."""
    if os.path.exists(MANIFEST_PATH) and not args.force:
        print(f"{MANIFEST_PATH} already exists. Use --force to overwrite.")
        return 1

    os.makedirs("symbols", exist_ok=True)
    os.makedirs("docs", exist_ok=True)

    manifest = {
        "version": "1.0.0",
        "project": {
            "name": "my-project",
            "intent": "Describe what this project does",
        },
        "symbols": {
            "architecture": {
                "description": "Core system architecture and provider decisions",
                "docs": ["docs/architecture.md"],
                "properties": {
                    "provider": "akamai",
                    "schema_version": 3,
                },
                "interlocks": {},
            },
            "deployment": {
                "description": "Deployment targets and infrastructure requirements",
                "docs": ["docs/deployment.md"],
                "properties": {
                    "expects_provider": "akamai",
                    "expects_schema": 3,
                },
                "interlocks": {
                    "architecture.provider": "expects_provider",
                    "architecture.schema_version": "expects_schema",
                },
            },
        },
    }

    # Write manifest
    with open(MANIFEST_PATH, "w") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"Created {MANIFEST_PATH}")

    # Create placeholder docs
    for sym in manifest["symbols"].values():
        for doc_path in sym.get("docs", []):
            os.makedirs(os.path.dirname(doc_path), exist_ok=True)
            if not os.path.exists(doc_path):
                with open(doc_path, "w") as f:
                    f.write(f"# {os.path.splitext(os.path.basename(doc_path))[0].replace('-', ' ').title()}\n\nTODO: Document this.\n")
                print(f"Created {doc_path}")

    # Generate lock
    lock = build_lock(manifest)
    with open(LOCK_PATH, "w") as f:
        json.dump(lock, f, indent=2)
        f.write("\n")
    print(f"Created {LOCK_PATH}")
    print(f"\nRoot hash: {lock['root']}")
    print(f"Status: {green('aligned') if lock['status'] == 'aligned' else red('broken')}")
    return 0


def cmd_lock(args):
    """Regenerate manifest.lock from manifest.json."""
    if not os.path.exists(MANIFEST_PATH):
        print(red(f"Error: {MANIFEST_PATH} not found. Run 'align.py init' first."))
        return 1

    with open(MANIFEST_PATH) as f:
        manifest = json.load(f)

    lock = build_lock(manifest)
    with open(LOCK_PATH, "w") as f:
        json.dump(lock, f, indent=2)
        f.write("\n")

    print(f"Lock generated: {LOCK_PATH}")
    print(f"Root hash: {lock['root']}")
    print(f"Status: {green('aligned') if lock['status'] == 'aligned' else red(lock['status'])}")

    if lock["status"] != "aligned":
        print(f"\n{yellow('Warning:')} Lock was generated but alignment is broken.")
        print("Run 'align.py status' for details.")
        return 0  # Lock was written successfully even if broken

    return 0


def cmd_check(args):
    """Verify alignment. Exit 0=aligned, 1=broken, 2=stale."""
    quiet = args.quiet

    if not os.path.exists(MANIFEST_PATH):
        if quiet:
            print("ALIGNMENT: manifest missing", file=sys.stderr)
        else:
            print(red(f"Error: {MANIFEST_PATH} not found."))
        return 1

    if not os.path.exists(LOCK_PATH):
        if quiet:
            print("ALIGNMENT: lock missing", file=sys.stderr)
        else:
            print(red(f"Error: {LOCK_PATH} not found. Run 'align.py lock' first."))
        return 1

    with open(MANIFEST_PATH) as f:
        manifest = json.load(f)
    with open(LOCK_PATH) as f:
        stored_lock = json.load(f)

    # Rebuild from current state
    current_lock = build_lock(manifest)

    # Compare root hashes
    issues = []

    if current_lock["root"] != stored_lock["root"]:
        issues.append("Root hash mismatch — lock is stale")

    # Check for broken interlocks
    for sym_name, sym_data in current_lock["symbols"].items():
        for interlock_key, status in sym_data.get("interlocks", {}).items():
            if status != "PASS":
                issues.append(f"Broken interlock in '{sym_name}': {interlock_key} → {status}")

        for doc_path, doc_data in sym_data.get("docs", {}).items():
            if doc_data["status"] == "MISSING":
                issues.append(f"Missing doc in '{sym_name}': {doc_path}")

    # Determine exit code
    if not issues:
        if not quiet:
            print(green("Alignment: OK"))
        return 0

    has_broken = any("Broken interlock" in i or "Missing doc" in i for i in issues)
    has_stale = any("stale" in i.lower() for i in issues)

    if quiet:
        if has_broken:
            print(f"ALIGNMENT: broken — {len(issues)} issue(s)", file=sys.stderr)
        elif has_stale:
            print("ALIGNMENT: stale lock — run 'align.py lock'", file=sys.stderr)
        return 1 if has_broken else 2

    print(red(f"Alignment: BROKEN — {len(issues)} issue(s)\n"))
    for issue in issues:
        print(f"  • {issue}")
    print(f"\nRun '{bold('python scripts/align.py status')}' for full details.")

    return 1 if has_broken else 2


def cmd_status(args):
    """Human-readable alignment report."""
    if not os.path.exists(MANIFEST_PATH):
        print(red(f"Error: {MANIFEST_PATH} not found. Run 'align.py init' first."))
        return 1

    with open(MANIFEST_PATH) as f:
        manifest = json.load(f)

    project = manifest.get("project", {})
    symbols = manifest.get("symbols", {})

    print(bold("═══ Symbolic Alignment Status ═══"))
    print(f"Project: {project.get('name', 'unknown')}")
    print(f"Intent:  {project.get('intent', 'not specified')}")
    print()

    # Check if lock exists
    lock_exists = os.path.exists(LOCK_PATH)
    stored_lock = None
    if lock_exists:
        with open(LOCK_PATH) as f:
            stored_lock = json.load(f)
        print(f"Lock: {stored_lock['root']} (generated {stored_lock['generated']})")
    else:
        print(yellow("Lock: not generated — run 'align.py lock'"))

    # Rebuild current state
    current_lock = build_lock(manifest)

    stale = lock_exists and current_lock["root"] != stored_lock["root"]
    if stale:
        print(yellow(f"Current root: {current_lock['root']} — STALE, run 'align.py lock'"))
    elif lock_exists:
        print(green(f"Root: {current_lock['root']} — current"))

    print()

    # Per-symbol report
    interlock_results = validate_interlocks(symbols)
    interlock_by_sym = {}
    for r in interlock_results:
        interlock_by_sym.setdefault(r["symbol"], []).append(r)

    overall_ok = True

    for sym_name in sorted(symbols.keys()):
        sym = symbols[sym_name]
        props = sym.get("properties", {})
        docs = sym.get("docs", [])
        sym_ok = True

        print(bold(f"── {sym_name} ──"))
        print(f"  {dim(sym.get('description', 'no description'))}")

        # Docs
        if docs:
            print(f"  Docs:")
            for doc_path in sorted(docs):
                if os.path.exists(doc_path):
                    h = hash_file(doc_path)
                    print(f"    {green('✓')} {doc_path} [{h}]")
                else:
                    print(f"    {red('✗')} {doc_path} [MISSING]")
                    sym_ok = False
        else:
            print(f"  Docs: {dim('none')}")

        # Properties
        if props:
            print(f"  Properties:")
            for k, v in sorted(props.items()):
                print(f"    {k}: {v}")

        # Interlocks
        sym_interlocks = interlock_by_sym.get(sym_name, [])
        if sym_interlocks:
            print(f"  Interlocks:")
            for r in sym_interlocks:
                if r["status"] == "PASS":
                    print(f"    {green('✓')} {r['interlock']} == {r['local_prop']} ({r['detail']})")
                else:
                    print(f"    {red('✗')} {r['interlock']} == {r['local_prop']} → {r['status']} ({r['detail']})")
                    sym_ok = False
        else:
            print(f"  Interlocks: {dim('none')}")

        status_str = green("ALIGNED") if sym_ok else red("BROKEN")
        print(f"  Status: {status_str}")
        print()

        if not sym_ok:
            overall_ok = False

    # Summary
    if overall_ok and not stale:
        print(green(bold("All symbols aligned.")))
    elif stale and overall_ok:
        print(yellow(bold("Symbols consistent but lock is stale. Run 'align.py lock'.")))
    else:
        print(red(bold("Alignment broken. Fix issues above, then run 'align.py lock'.")))

    return 0


# ─── CLI ──────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="Symbolic Alignment Tool — verify project state via declarative symbols.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    sub = parser.add_subparsers(dest="command")

    init_p = sub.add_parser("init", help="Create starter manifest and lock")
    init_p.add_argument("--force", action="store_true", help="Overwrite existing manifest")

    sub.add_parser("lock", help="Regenerate manifest.lock from manifest.json")

    check_p = sub.add_parser("check", help="Verify alignment (exit 0=ok, 1=broken, 2=stale)")
    check_p.add_argument("--quiet", action="store_true", help="One-line output to stderr")

    sub.add_parser("status", help="Human-readable alignment report")

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return 1

    commands = {
        "init": cmd_init,
        "lock": cmd_lock,
        "check": cmd_check,
        "status": cmd_status,
    }

    return commands[args.command](args)


if __name__ == "__main__":
    sys.exit(main())
