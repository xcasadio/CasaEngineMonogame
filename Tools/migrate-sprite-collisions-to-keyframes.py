#!/usr/bin/env python3
"""Migrate RPGDemo sprite collision volumes into animation collision_keyframes.

Commit 089c2e4c removed the "body per sprite" path of AnimatedSpriteComponent: an animated
sprite now only gets its collision bodies from the `collision_keyframes` timeline carried by
its `.anim2d` asset. This script rebuilds that timeline from the legacy `collisions` arrays
still stored on the `.sprite` assets, reproducing exactly the placement that
SpriteCollisionHelper.UpdateBodyTransformation used to compute (origin = sprite hotspot).

Usage:
    python Tools/migrate-sprite-collisions-to-keyframes.py <project_root> [--dry-run] [--swap-profiles]

    <project_root>   e.g. Projects/RPGDemo. The script scans it recursively for *.sprite and
                      *.anim2d files.
    --dry-run         Print the summary without writing any file.
    --swap-profiles   Before emitting keyframes, fix the collision_profile of the legacy
                      volumes: sword_*.sprite DamageableVolume -> AttackVolume, player_*.sprite
                      AttackVolume -> DamageableVolume. octopus_*.sprite is left untouched.

The script is deterministic and idempotent: running it again on an already migrated project
overwrites `collision_keyframes` with byte-identical content and touches no other file.

Stdlib only (json, uuid, argparse, pathlib, re).
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import uuid
from pathlib import Path

# Fixed namespace used to derive deterministic shape ids: uuid5(NAMESPACE, "<sprite id>:<volume index>").
SHAPE_ID_NAMESPACE = uuid.NAMESPACE_URL

BOX = "Box"
SPHERE = "Sphere"
RECTANGLE = "Rectangle"
CIRCLE = "Circle"


class MigrationError(Exception):
    pass


def read_text(path: Path) -> str:
    # newline="" preserves the file's original line endings verbatim.
    with path.open("r", encoding="utf-8", newline="") as f:
        return f.read()


def write_text(path: Path, text: str) -> None:
    with path.open("w", encoding="utf-8", newline="") as f:
        f.write(text)


def detect_newline(text: str) -> str:
    return "\r\n" if "\r\n" in text else "\n"


def format_number(value: float) -> str:
    """Render a number the way the engine's JSON writer does: integral floats keep a
    trailing '.0' (e.g. 48.0, not 48); Python's repr already matches Newtonsoft's shortest
    round-trip formatting for the simple values this script produces."""
    value = float(value)
    if value == 0.0:
        value = 0.0  # normalize -0.0 to 0.0
    text = repr(value)
    return text


def json_string(value: str) -> str:
    return json.dumps(value)


# ---------------------------------------------------------------------------
# Sprite indexing
# ---------------------------------------------------------------------------

class SpriteRecord:
    def __init__(self, sprite_id: str, name: str, hotspot: dict, collisions: list, path: Path):
        self.id = sprite_id
        self.name = name
        self.hotspot = hotspot
        self.collisions = collisions
        self.path = path


def swap_profile_value(file_name: str, value: str) -> str:
    lower = file_name.lower()
    if lower.startswith("sword_") and value == "DamageableVolume":
        return "AttackVolume"
    if lower.startswith("player_") and value == "AttackVolume":
        return "DamageableVolume"
    return value


def apply_profile_swap_to_text(text: str, file_name: str) -> tuple[str, int]:
    """Text-level replace of collision_profile values inside a .sprite file. Only the two
    relevant string values are ever touched, and only for the matching file prefix, so the
    rest of the document is left byte-identical."""
    lower = file_name.lower()
    if lower.startswith("sword_"):
        pattern = re.compile(r'("collision_profile"\s*:\s*)"DamageableVolume"')
        replacement = r'\1"AttackVolume"'
    elif lower.startswith("player_"):
        pattern = re.compile(r'("collision_profile"\s*:\s*)"AttackVolume"')
        replacement = r'\1"DamageableVolume"'
    else:
        return text, 0

    new_text, count = pattern.subn(replacement, text)
    return new_text, count


def load_sprites(sprite_files: list[Path], swap_profiles: bool, dry_run: bool) -> tuple[dict, int]:
    """Reads every .sprite file, optionally swaps collision_profile values, writes the file
    back (unless dry_run), and returns an id -> SpriteRecord index built from the swapped
    (logical) content."""
    sprites_by_id: dict[str, SpriteRecord] = {}
    swapped_count = 0

    for sprite_path in sprite_files:
        original_text = read_text(sprite_path)
        text = original_text

        if swap_profiles:
            text, count = apply_profile_swap_to_text(text, sprite_path.name)
            swapped_count += count
            if count > 0 and not dry_run:
                write_text(sprite_path, text)

        data = json.loads(text)
        sprite_id = data["id"]
        sprites_by_id[sprite_id] = SpriteRecord(
            sprite_id=sprite_id,
            name=data.get("name", ""),
            hotspot=data.get("hotspot") or {"x": 0, "y": 0},
            collisions=data.get("collisions") or [],
            path=sprite_path,
        )

    return sprites_by_id, swapped_count


# ---------------------------------------------------------------------------
# Fixture construction
# ---------------------------------------------------------------------------

def build_fixtures_for_sprite(sprite: SpriteRecord) -> list[str]:
    """Returns the JSON text (already indented at the fixture-array-item level, i.e. 8 spaces)
    for every fixture of a sprite's legacy collisions, in source order."""
    fixtures_json: list[str] = []
    hotspot_x = float(sprite.hotspot.get("x", 0))
    hotspot_y = float(sprite.hotspot.get("y", 0))

    for index, volume in enumerate(sprite.collisions):
        orientation = float(volume.get("orientation", 0.0))
        if orientation != 0.0:
            raise MigrationError(
                f"sprite '{sprite.name}' ({sprite.id}) volume #{index} has a non-zero "
                f"orientation ({orientation}); the migration only supports axis-aligned "
                f"legacy volumes."
            )

        shape_type = volume.get("shape_type")
        location = volume.get("location") or {"x": 0, "y": 0}
        location_x = float(location.get("x", 0))
        location_y = float(location.get("y", 0))

        shape_id = uuid.uuid5(SHAPE_ID_NAMESPACE, f"{sprite.id}:{index}")
        shape_name = f"Object {shape_id}"

        if shape_type == RECTANGLE:
            w = float(volume["w"])
            h = float(volume["h"])
            local_x = location_x - hotspot_x + w / 2.0
            local_y = -(location_y - hotspot_y + h / 2.0)
            shape_type_written = BOX
            shape_extra_lines = [
                f'"w": {format_number(w)}',
                f'"h": {format_number(h)}',
                f'"l": {format_number(1.0)}',
            ]
        elif shape_type == CIRCLE:
            radius = float(volume["radius"])
            local_x = location_x - hotspot_x + radius
            local_y = -(location_y - hotspot_y + radius)
            shape_type_written = SPHERE
            shape_extra_lines = [f'"radius": {format_number(radius)}']
        else:
            raise MigrationError(
                f"sprite '{sprite.name}' ({sprite.id}) volume #{index} has an unsupported "
                f"shape_type '{shape_type}'."
            )

        collision_profile = volume.get("collision_profile")
        profile_json = json_string(collision_profile) if collision_profile is not None else "null"
        tag_json = json_string(sprite.name) if sprite.name is not None else "null"

        shape_extra_text = ",\n".join("            " + line for line in shape_extra_lines)

        fixture_text = (
            "        {\n"
            "          \"shape\": {\n"
            f'            "id": {json_string(str(shape_id))},\n'
            f'            "name": {json_string(shape_name)},\n'
            f'            "shape_type": {json_string(shape_type_written)},\n'
            f"{shape_extra_text}\n"
            "          },\n"
            "          \"local_position\": {\n"
            f'            "x": {format_number(local_x)},\n'
            f'            "y": {format_number(local_y)},\n'
            f'            "z": {format_number(0.0)}\n'
            "          },\n"
            "          \"local_rotation\": {\n"
            f'            "x": {format_number(0.0)},\n'
            f'            "y": {format_number(0.0)},\n'
            f'            "z": {format_number(0.0)},\n'
            f'            "w": {format_number(1.0)}\n'
            "          },\n"
            f'          "collision_profile": {profile_json},\n'
            f'          "tag": {tag_json}\n'
            "        }"
        )
        fixtures_json.append(fixture_text)

    return fixtures_json


def build_collision_keyframes_block(animation: dict, sprites_by_id: dict, anim_path: Path) -> tuple[str, int, int]:
    """Returns (json_text_of_the_array_value, keyframe_count, fixture_count) for the
    `collision_keyframes` array of one animation, or (None, 0, 0) if the animation has no
    Sprite track."""
    tracks = animation.get("tracks") or []
    sprite_tracks = [t for t in tracks if t.get("property") == "Sprite"]

    if not sprite_tracks:
        return None, 0, 0

    # time_seconds -> ordered list of fixture json fragments
    fixtures_by_time: dict[float, list[str]] = {}
    time_order: list[float] = []

    for track in sprite_tracks:
        for keyframe in track.get("sprite_keyframes") or []:
            time_seconds = float(keyframe["time_seconds"])
            sprite_id = keyframe["value"]
            sprite = sprites_by_id.get(sprite_id)
            if sprite is None:
                raise MigrationError(
                    f"{anim_path}: sprite keyframe at t={time_seconds} references unknown "
                    f"sprite id {sprite_id}."
                )

            fixtures = build_fixtures_for_sprite(sprite)

            if time_seconds not in fixtures_by_time:
                fixtures_by_time[time_seconds] = []
                time_order.append(time_seconds)
            fixtures_by_time[time_seconds].extend(fixtures)

    sorted_times = sorted(time_order)

    keyframe_blocks = []
    fixture_count = 0
    for time_seconds in sorted_times:
        fixtures = fixtures_by_time[time_seconds]
        fixture_count += len(fixtures)
        if fixtures:
            fixtures_text = "[\n" + ",\n".join(fixtures) + "\n      ]"
        else:
            fixtures_text = "[]"

        keyframe_blocks.append(
            "    {\n"
            f'      "time_seconds": {format_number(time_seconds)},\n'
            f'      "fixtures": {fixtures_text}\n'
            "    }"
        )

    array_text = "[\n" + ",\n".join(keyframe_blocks) + "\n  ]"
    return array_text, len(keyframe_blocks), fixture_count


# ---------------------------------------------------------------------------
# Text-level insertion into the .anim2d file
# ---------------------------------------------------------------------------

TRACKS_KEY_PATTERN = re.compile(r'"tracks"\s*:\s*')


def find_matching_bracket(text: str, open_index: int) -> int:
    """Given the index of an opening '[' or '{', returns the index of its matching close,
    skipping over string literals (with escape handling)."""
    open_char = text[open_index]
    close_char = "]" if open_char == "[" else "}"
    depth = 0
    i = open_index
    in_string = False
    escape = False
    while i < len(text):
        ch = text[i]
        if in_string:
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_string = False
        else:
            if ch == '"':
                in_string = True
            elif ch in "[{":
                depth += 1
            elif ch in "]}":
                depth -= 1
                if depth == 0:
                    return i
        i += 1
    raise MigrationError("unbalanced brackets while scanning JSON text")


def insert_collision_keyframes(raw_text: str, keyframes_array_text: str, anim_path: Path) -> str:
    match = TRACKS_KEY_PATTERN.search(raw_text)
    if not match:
        raise MigrationError(f"{anim_path}: no top-level \"tracks\" key found.")

    if len(TRACKS_KEY_PATTERN.findall(raw_text)) != 1:
        raise MigrationError(f"{anim_path}: expected exactly one \"tracks\" key, found more.")

    value_start = match.end()
    if raw_text[value_start] != "[":
        raise MigrationError(f"{anim_path}: \"tracks\" is not a JSON array.")

    tracks_close = find_matching_bracket(raw_text, value_start)

    # After the tracks array must come only whitespace and the final root '}': tracks is
    # required to be the last top-level property for this straightforward insertion to be safe.
    tail = raw_text[tracks_close + 1:]
    tail_stripped = tail.strip()
    if tail_stripped != "}":
        raise MigrationError(
            f"{anim_path}: \"tracks\" is not the last top-level property; "
            f"refusing to guess where to insert collision_keyframes."
        )

    newline = detect_newline(raw_text)
    insertion = (
        ",\n"
        f"  \"collision_keyframes\": {keyframes_array_text}"
    )
    if newline != "\n":
        insertion = insertion.replace("\n", newline)

    return raw_text[:tracks_close + 1] + insertion + tail


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("project_root", help="Project root to scan, e.g. Projects/RPGDemo")
    parser.add_argument("--dry-run", action="store_true", help="Print the summary without writing files")
    parser.add_argument("--swap-profiles", action="store_true", help="Fix sword_*/player_* collision_profile values before emitting keyframes")
    args = parser.parse_args()

    root = Path(args.project_root)
    if not root.is_dir():
        print(f"error: '{root}' is not a directory", file=sys.stderr)
        return 1

    sprite_files = sorted(root.rglob("*.sprite"))
    anim_files = sorted(root.rglob("*.anim2d"))

    try:
        sprites_by_id, swapped_count = load_sprites(sprite_files, args.swap_profiles, args.dry_run)
    except MigrationError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    migrated = []
    skipped = []
    errors = []
    total_keyframes = 0
    total_fixtures = 0

    for anim_path in anim_files:
        raw_text = read_text(anim_path)
        try:
            animation = json.loads(raw_text)
        except json.JSONDecodeError as error:
            errors.append(f"{anim_path}: invalid JSON ({error})")
            continue

        try:
            keyframes_text, keyframe_count, fixture_count = build_collision_keyframes_block(
                animation, sprites_by_id, anim_path)
        except MigrationError as error:
            errors.append(str(error))
            continue

        if keyframes_text is None:
            skipped.append(anim_path)
            continue

        try:
            new_text = insert_collision_keyframes(raw_text, keyframes_text, anim_path)
        except MigrationError as error:
            errors.append(str(error))
            continue

        # Validate the result is well-formed JSON with the expected content before writing.
        reparsed = json.loads(new_text)
        if "collision_keyframes" not in reparsed:
            errors.append(f"{anim_path}: internal error, collision_keyframes missing after insertion")
            continue

        migrated.append(anim_path)
        total_keyframes += keyframe_count
        total_fixtures += fixture_count

        if not args.dry_run and new_text != raw_text:
            write_text(anim_path, new_text)

    print(f"Sprites indexed: {len(sprites_by_id)} ({len(sprite_files)} files)")
    if args.swap_profiles:
        print(f"collision_profile values swapped: {swapped_count}")
    print(f"Animations migrated: {len(migrated)}")
    for path in skipped:
        print(f"  ignored (no Sprite track): {path}")
    print(f"Animations ignored: {len(skipped)}")
    print(f"Collision keyframes emitted: {total_keyframes}")
    print(f"Fixtures emitted: {total_fixtures}")
    print(f"Errors: {len(errors)}")
    for error in errors:
        print(f"  error: {error}", file=sys.stderr)

    if args.dry_run:
        print("(dry run: no file written)")

    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
