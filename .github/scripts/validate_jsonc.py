#!/usr/bin/env python3
# .github/scripts/validate_jsonc.py
"""Validates the game's JSON-with-comments data files for CI (job `data-files`).

What it checks, for every file it is given or finds:
  1. The first line starts with `//` (the mandatory file header comment).
  2. After `//` line comments and `/* */` block comments are removed (string
     contents are left untouched, so a "http://x" value survives), the text is
     strict JSON: no trailing commas, no duplicate object keys, no NaN/Infinity.

Usage (run from any directory; only the standard library is used):
  python3 -I .github/scripts/validate_jsonc.py [--root DIR] [PATH_OR_GLOB ...]

With no PATH_OR_GLOB it checks the default sets below, relative to --root.
Exit code 0 when every file passes, 1 when any file fails (all failures are
printed, one per line, as `path:line: problem`), 2 on a usage error or when no
file at all is found (a single default set with no match only warns).
"""
import argparse
import glob
import json
import os
import sys

# Default file sets, as globs relative to the repository root. `**` is recursive.
# Unit: glob pattern strings. Range: any glob understood by glob.glob(recursive=True).
DEFAULT_PATTERNS = (
    "rts/data/**/*.json",   # game data plus the command scripts in rts/data/scripts/ (same header rule)
)


def strip_comments(text):
    """Returns `text` with // and /* */ comments replaced by nothing.

    Newlines inside removed comments are kept so line numbers in JSON parse
    errors still point at the right line. Characters inside string literals
    (including escaped quotes) are copied through unchanged.
    Raises ValueError("unterminated ...") for an unclosed string or block comment.
    """
    out = []                 # output characters. Unit: characters.
    i = 0                    # read position in `text`. Unit: index. Range: 0..len(text).
    n = len(text)            # total input length. Unit: characters.
    while i < n:
        c = text[i]          # current character.
        if c == '"':
            # Copy a whole string literal, honouring backslash escapes.
            j = i + 1        # scan position inside the string. Unit: index.
            while j < n and text[j] != '"':
                if text[j] == "\\":
                    j += 1   # skip the escaped character
                if j < n and text[j] == "\n":
                    raise ValueError("unterminated string (newline inside string)")
                j += 1
            if j >= n:
                raise ValueError("unterminated string")
            out.append(text[i:j + 1])
            i = j + 1
        elif text.startswith("//", i):
            j = text.find("\n", i)   # end of the comment line. Unit: index, -1 = end of file.
            i = n if j == -1 else j  # keep the newline itself
        elif text.startswith("/*", i):
            j = text.find("*/", i + 2)  # end of the block comment. Unit: index, -1 = unclosed.
            if j == -1:
                raise ValueError("unterminated /* comment")
            out.append("\n" * text.count("\n", i, j))
            i = j + 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def _reject_duplicates(pairs):
    """json object_pairs_hook: builds a dict but refuses a repeated key."""
    result = {}              # the object being built. Keys are unique.
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate key '%s'" % key)
        result[key] = value
    return result


def _reject_constant(name):
    """json parse_constant hook: NaN, Infinity and -Infinity are not JSON."""
    raise ValueError("non-JSON constant '%s'" % name)


def check_file(path):
    """Returns a list of problem strings for one file (empty list = file passes)."""
    problems = []            # collected problems, each formatted `path:line: text`.
    try:
        with open(path, "r", encoding="utf-8-sig") as handle:   # utf-8-sig tolerates a BOM
            text = handle.read()
    except (OSError, UnicodeDecodeError) as error:
        return ["%s:1: cannot read file: %s" % (path, error)]
    first_line = text.split("\n", 1)[0].lstrip("﻿")   # header candidate line.
    if not first_line.startswith("//"):
        problems.append("%s:1: first line must be a // header comment (path, then what the file does)" % path)
    try:
        stripped = strip_comments(text)
        json.loads(stripped, object_pairs_hook=_reject_duplicates, parse_constant=_reject_constant)
    except json.JSONDecodeError as error:
        problems.append("%s:%d: invalid JSON after comment stripping: %s (column %d)" % (path, error.lineno, error.msg, error.colno))
    except ValueError as error:
        problems.append("%s:1: %s" % (path, error))
    return problems


def main(argv):
    """Entry point. Returns the process exit code (0 ok, 1 file problems, 2 usage)."""
    parser = argparse.ArgumentParser(description="Validate JSON-with-comments data files.")
    parser.add_argument("--root", default=".", help="repository root the default globs are relative to (default: current directory)")
    parser.add_argument("patterns", nargs="*", help="files or globs to check instead of the defaults")
    args = parser.parse_args(argv)
    patterns = args.patterns or list(DEFAULT_PATTERNS)       # globs to expand.
    explicit = bool(args.patterns)                           # True when the caller named the files.
    files = []                                               # sorted, de-duplicated file list.
    for pattern in patterns:
        full = pattern if explicit else os.path.join(args.root, pattern)   # explicit globs are used as typed.
        matches = sorted(glob.glob(full, recursive=True))    # files this pattern matched.
        if not matches:
            if explicit:
                print("error: pattern matched no files: %s" % full, file=sys.stderr)
                return 2
            print("warning: default set matched no files: %s" % full, file=sys.stderr)
            continue
        files.extend(m for m in matches if os.path.isfile(m))
    files = sorted(set(files))
    if not files:
        print("error: no files to check", file=sys.stderr)
        return 2
    failures = []                                            # all problems across all files.
    for path in files:
        problems = check_file(path)
        failures.extend(problems)
        print("%s %s" % ("FAIL" if problems else "ok  ", path))
    for line in failures:
        print(line, file=sys.stderr)
    print("%d file(s) checked, %d problem(s)" % (len(files), len(failures)))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
