"""Puts the ranking API's signing key into the Firestore rules.

The ranking API (freny-online, api/run/finish) signs the scores of the runs it checked with the
SCORE_KEY env var on Vercel; the rules only take scores with that signature. The key must not be
committed (this repository is public), and the game no longer has it:
  python Tools/ScoreKey/make_score_key.py <key-file>
    writes firebase/firestore.rules.publish (git-ignored): firebase/firestore.rules with the
    key in place of __SCORE_KEY__, ready to paste in the Firebase console or for `firebase deploy`.
  python Tools/ScoreKey/make_score_key.py --new <key-file>
    makes a new random key first. Changing the key needs new rules AND the new SCORE_KEY on
    Vercel at the same time.
"""
import os
import secrets
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def main(args):
    new = "--new" in args
    args = [a for a in args if a != "--new"]
    if len(args) != 1:
        sys.exit(__doc__)
    key_file = args[0]
    if new:
        with open(key_file, "w") as f:
            f.write(secrets.token_hex(32))
    with open(key_file) as f:
        key = f.read().strip()
    if len(key) < 32 or "'" in key or "\\" in key:
        sys.exit("bad key")

    with open(os.path.join(ROOT, "firebase", "firestore.rules")) as f:
        rules = f.read()
    if "__SCORE_KEY__" not in rules:
        sys.exit("firebase/firestore.rules has no __SCORE_KEY__")
    with open(os.path.join(ROOT, "firebase", "firestore.rules.publish"), "w", newline="\n") as f:
        f.write(rules.replace("__SCORE_KEY__", key))
    print("wrote firebase/firestore.rules.publish; SCORE_KEY on Vercel must be the same key")


if __name__ == "__main__":
    main(sys.argv[1:])
