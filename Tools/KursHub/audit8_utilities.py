"""Visible revision-8 electrical distribution and fixture-authored lighting.

Pure authoring data in Unity-local metres.  ``augment`` appends stable placements
and Point-light records to a caller-owned manifest.  It does not open Blender,
write a scene, bake lighting, or simulate electricity.  KH_Floodlight already
contains its own native Spot Light; this module never adds a duplicate Point.
"""
from copy import deepcopy
from math import atan2, cos, degrees, hypot, pi, radians, sin, sqrt


def _rotate(point, rotation):
    """Unity Quaternion.Euler: local Z, then X, then Y rotations."""
    x, y, z = point
    ax, ay, az = map(radians, rotation)
    x, y = x*cos(az)-y*sin(az), x*sin(az)+y*cos(az)
    y, z = y*cos(ax)-z*sin(ax), y*sin(ax)+z*cos(ax)
    return (x*cos(ay)+z*sin(ay), y, -x*sin(ay)+z*cos(ay))


def transform(point, placement):
    local = [point[i]*placement["scale"][i] for i in range(3)]
    result = _rotate(local, placement["rotation"])
    return [result[i]+placement["position"][i] for i in range(3)]


def augment(manifest):
    """Append wall feeders, backed fixtures, and lights at real lamp diffusers.

    Existing rows remain byte-for-byte equivalent.  Repeated application is
    idempotent; a changed existing row with one of our IDs causes refusal.
    Overhead additions crossing circulation stay above 2.7 m.  Low drops are
    explicitly wall/equipment runs, outside the protected walking rectangles.
    """
    original = {key: deepcopy(manifest.get(key, [])) for key in
                ("propInstances", "interiorInstances", "settlementInstances", "settlementLights")}
    placements = manifest.setdefault("settlementInstances", [])
    lights = manifest.setdefault("settlementLights", [])
    by_id = {p["id"]: p for key in ("propInstances", "interiorInstances", "settlementInstances")
             for p in manifest.get(key, [])}
    light_ids = {p["id"]: p for p in lights}
    added, added_lights, runs, fixture_records, lamp_records = [], [], [], [], []

    def rounded(values):
        return [round(float(value), 6) for value in values]

    def put(identifier, prop, position, rotation=(0, 0, 0), scale=(1, 1, 1)):
        identifier = "Hub8 utility " + identifier
        row = dict(id=identifier, prop=prop, position=rounded(position),
                   rotation=rounded(rotation), scale=rounded(scale))
        if identifier in by_id:
            if by_id[identifier] != row:
                raise ValueError("Preserve manually modified utility placement: " + identifier)
        else:
            placements.append(row)
            by_id[identifier] = row
        added.append(identifier)
        return row

    def point(identifier, position, intensity, radius, color, fixture, local):
        identifier = "Hub8 utility " + identifier
        row = dict(id=identifier, position=rounded(position), color=list(color),
                   intensity=float(intensity), range=float(radius))
        if identifier in light_ids:
            if light_ids[identifier] != row:
                raise ValueError("Preserve manually modified utility light: " + identifier)
        else:
            lights.append(row)
            light_ids[identifier] = row
        added_lights.append(identifier)
        lamp_records.append(dict(lightId=identifier, fixtureId=fixture["id"],
                                 sourceLocalPoint=list(local), worldPoint=row["position"],
                                 kind="Point; bounded; native setup uses no shadows"))

    def cable(identifier, a, b, sag=.12, floor=0, routing="overhead", yaw_for_vertical=0):
        difference = [b[i]-a[i] for i in range(3)]
        horizontal = hypot(difference[0], difference[2])
        length = sqrt(sum(v*v for v in difference))
        if length < .10:
            raise ValueError("Cable run too short: " + identifier)
        yaw = degrees(atan2(-difference[2], difference[0])) if horizontal > 1e-7 else yaw_for_vertical
        roll = degrees(atan2(difference[1], horizontal))
        row = put(identifier, "KH_CableSag", [(a[i]+b[i])/2 for i in range(3)],
                  (0, yaw, roll), (length/10, sag, sag))
        samples = [transform((-5+10*i/40, -4*(i/40)*(1-i/40), 0), row)
                   for i in range(41)]
        clearance = min(p[1]-floor for p in samples)-.022*max(row["scale"][1], 1)
        if routing == "overhead" and clearance < 2.7:
            raise AssertionError((identifier, "Overhead cable below 2.7 m", clearance))
        runs.append(dict(placementId=row["id"], routing=routing, floorY=floor,
                         start=rounded(a), end=rounded(b), sagScale=sag,
                         sourceProp="KH_CableSag: 10 m local X, sag 1 m, real end mounts, no collision",
                         minimumCentreLineClearance=round(clearance, 5),
                         clampCount=2, samples=[rounded(p) for p in samples]))
        return row

    def chain(identifier, points, sag=.10, floor=0, routing="overhead", yaw_for_vertical=0):
        for index, (a, b) in enumerate(zip(points, points[1:]), 1):
            cable(identifier+" %02d" % index, a, b, sag, floor, routing, yaw_for_vertical)

    def flood(identifier, position, yaw, backing, floor=0):
        row = put(identifier, "KH_Floodlight", position, (0, yaw, 0))
        fixture_records.append(dict(placementId=row["id"], type="KH_Floodlight native Spot",
                                    backing=backing, nativeLightPosition=rounded(transform((0, 0, -.18), row)),
                                    nativeIntensity=3, nativeRange=14, nativeSpotAngle=100,
                                    additionalPointLight=False))
        # The final short run sits against its mounting wall/column; it is not
        # an overhead crossing and does not create a collider in the lane.
        cable(identifier+" wall drop", (position[0], floor+4.15, position[2]),
              position, .025, floor, "wall drop", yaw)
        return row

    # Wall-backed longitudinal feeders.  Cross-walk branches below are above
    # head height; low risers stay against the original outer wall.
    for floor, label in ((0., "Ground"), (5.5, "Upper")):
        stops = (-48., -32., -16., 0., 16., 32., 48., 64.64) if floor == 0 else (
            -27.885, -12., 4., 20., 36., 52., 64.64)
        for side, side_name in ((-1, "West"), (1, "East")):
            chain(label+" "+side_name+" outer wall feeder",
                  [(side*46.31, floor+4.15, z) for z in stops],
                  .10, floor, "overhead")
    for side, side_name in ((-1, "West"), (1, "East")):
        chain(side_name+" wall riser",
              [(side*46.31, y, 26) for y in (4.15, 6.0, 9.65, 11.20)],
              .025, 0, "wall riser", side*90)
    # Supply link runs beside the retained rear wall, supported at its two ends.
    for floor, label in ((0., "ground"), (5.5, "upper")):
        chain("Rear "+label+" crossfeed",
              [(x, floor+4.15, 64.64) for x in (-46.31, -36, -24, -12, 0, 12, 24, 36, 46.31)],
              .16, floor, "overhead")

    # Connect existing revision-8 room feeder end points to the exterior-wall
    # circuit, and preserve the authored fixtures' own IDs and orientation.
    room_feeders = [p for p in original["settlementInstances"]
                    if p["id"].startswith("Hub8 ") and p["id"].endswith("South wall feeder")]
    for feeder in room_feeders:
        x, y, z = feeder["position"]
        side = -1 if x < 0 else 1
        floor = 5.5 if y > 5 else 0.
        name = feeder["id"].removeprefix("Hub8 ").removesuffix(" South wall feeder")
        endpoint_x = x+side*5
        top = (endpoint_x, floor+4.15, z)
        cable(name+" ceiling branch", (side*46.31, floor+4.15, z), top,
              .08, floor, "overhead")
        cable(name+" room feed drop", top, (endpoint_x, y, z),
              .025, floor, "wall drop", 180)

    flood("Kitchen working light", (-46.32, 3.2, 52), -90,
          "Original west outer wall; faces cooking and washing line")
    flood("Water and power working light", (46.32, 3.2, 56), 90,
          "Original east outer wall; faces treatment and electrical service equipment")
    for index, north_z in ((1, -10.12), (2, 5.88)):
        flood("Clinical treatment light %02d" % index, (34, 8.4, north_z), 0,
              "Retained north partition of Upper East Medical %02d" % index, 5.5)
        cable("Clinical treatment supply %02d" % index,
              (46.31, 9.65, north_z), (34, 9.65, north_z), .08, 5.5)
    for side, name in ((-1, "Mending"), (1, "Laundry")):
        flood(name+" work light", (side*24, 8.7, 32.45), 180,
              "North face of retained concrete column X%s Z32" % (side*24), 5.5)
        cable(name+" task supply", (side*46.31, 9.65, 32.45),
              (side*24, 9.65, 32.45), .10, 5.5)

    # Every new repair bench's physical diffuser receives a small actual Point.
    # The local point is just below the bottom glass face (Y1.526), within 6 mm.
    benches = [p for p in original["settlementInstances"]
               if p["id"].startswith("Hub8 ") and p["prop"] == "KH_RepairBench"]
    for bench in benches:
        source_point = (-.85, 1.520, .10)
        point(bench["id"].removeprefix("Hub8 ")+" bench diffuser",
              transform(source_point, bench), 1.6, 2.7, (1., .80, .60), bench, source_point)
        # Near-wall service benches get a visible branch from the existing
        # sagging feeder to the already-modelled mains cord endpoint.
        district = bench["id"].removeprefix("Hub8 ").removesuffix(" South service station")
        feeder = next((p for p in room_feeders if p["id"] == "Hub8 "+district+" South wall feeder"), None)
        if feeder is not None:
            end = transform((-1.13, .55, .30), bench)
            t = (end[0]-(feeder["position"][0]-5))/10
            if 0 <= t <= 1 and abs(end[2]-feeder["position"][2]) <= 1.6:
                start = (end[0], feeder["position"][1]-4*t*(1-t), feeder["position"][2])
                cable(district+" service bench flexible lead", start, end, .035,
                      bench["position"][1], "equipment drop", 180)

    def festoon_cluster(name, x0, x1, floor, z):
        y = floor+4.30
        main = cable(name+" support cable", (x0, y, z), (x1, y, z), .12, floor)
        centre = (x0+x1)/2
        # Three true 6 m strings, with almost unit X scale; bulbs remain round.
        for index, centre_x in enumerate((centre-8, centre, centre+8), 1):
            a_x, b_x = centre_x-3, centre_x+3
            def support_height(x):
                t = (x-x0)/(x1-x0)
                return y-.12*4*t*(1-t)
            a, b = (a_x, support_height(a_x), z), (b_x, support_height(b_x), z)
            dy = b[1]-a[1]
            row = put(name+" festoon %02d" % index, "KH_StringLights",
                      [(a[i]+b[i])/2 for i in range(3)], (0, 0, degrees(atan2(dy, 6))),
                      (hypot(6, dy)/6, 1, 1))
            fixture_records.append(dict(placementId=row["id"], type="KH_StringLights eight physical bulbs",
                                        backing=main["id"]+"; end ties follow sampled support cable",
                                        additionalPointLight=True, lightedBulbs=[2, 5]))
            for bulb in (2, 5):
                x = -2.65+bulb*5.3/7
                local = (x+.018, -.45*sin(pi*(x+3)/6)-(.11+.035*sin(bulb*1.71))-.135, .008)
                point(name+" festoon %02d bulb %02d" % (index, bulb), transform(local, row),
                      2.2, 3.4, (1., .76, .51), row, local)
    festoon_cluster("Rear communal meals", -15.55, 15.55, 0., 55.55)
    festoon_cluster("Upper front lounge", -15.55, 15.55, 5.5, -40.45)

    # A visible supply from the actual main board top cable, routed within its
    # service strip.  Its low roof segment is not an overhead circulation run.
    board = next((p for p in original["settlementInstances"]
                  if p["id"] == "Hub8 Roof west service Roof service distribution"), None)
    if board:
        source = transform((.24, 2.09, .15), board)
        chain("Roof main distribution cable", [source, (-43.0, source[1], source[2]),
              (-46.31, 11.20, 26)], .025, 11., "equipment perimeter")

    # Existing banner cord endpoints are authored by sc6_cloth(4.5, 6, big=True):
    # X=+/- (4.5/2-.045+.060), Y=6.5, Z=.045 in its local coordinates.
    # The revision-8 longitudinal white mullions are real members at X=+/-3,
    # centre Y12.92, depth .065; tie into their lower faces at Y12.8875.
    banner = by_id.get("Atrium hanging Kurs")
    if banner:
        for side, label in ((-1, "West"), (1, "East")):
            start = transform((side*2.265, 6.5, .045), banner)
            end = (side*3., 12.8875, start[2])
            cable("Kurs banner "+label+" roof suspension", start, end, .20,
                  5.5, "overhead")
        fixture_records.append(dict(placementId=banner["id"], type="Existing Kurs banner retained",
                                    backing="Hub8 glazing longitudinal mullion +/-3; real white steel lower face Y12.8875",
                                    sourceAnchorLocal=[[-2.265, 6.5, .045], [2.265, 6.5, .045]],
                                    additionalPointLight=False))

    for key, prior in original.items():
        if manifest[key][:len(prior)] != prior:
            raise AssertionError("Existing KursHub authoring rows changed: "+key)
    if len(added) != len(set(added)) or len(added_lights) != len(set(added_lights)):
        raise AssertionError("Duplicate stable utility identifiers")
    result = dict(revision=8, status="AUTHORED_DATA_REQUIRES_IMPORTED_SCENE_REVIEW",
                  addedPlacementCount=len(added), addedPointLights=len(added_lights),
                  addedPlacementIds=added, addedLightIds=added_lights,
                  cableRuns=runs, supportedFixtures=fixture_records, pointFixtures=lamp_records,
                  cableCollision=False, minimumProtectedOverheadMetres=2.7,
                  nativeSpotPolicy="KH_Floodlight owns its native Spot Light; no duplicate Point is added",
                  limits=["Visual electrical distribution only; no electricity/fuel gameplay",
                          "Portable bench mains leads remain their existing under-bench geometry unless near a wall feeder",
                          "Blender/Unity import, rendered illumination, route clearance and target-machine load are not proven by this data"])
    manifest["hub8Utilities"] = result
    if "hub8Spatial" in manifest:
        manifest["hub8Spatial"]["lighting"] = "See hub8Utilities: physical fixtures and explicit bounded light records; imported rendering still to verify"
    return result


if __name__ == '__main__':
    import argparse
    import json
    import os
    from pathlib import Path
    import uuid
    project = Path(__file__).resolve().parents[2]
    base = (project/"work/KursHub/audit8").resolve()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=base/"authoring")
    parser.add_argument("--work", type=Path, default=base)
    args = parser.parse_args()
    root, work = args.root.resolve(), args.work.resolve()
    assert root != project and root.is_relative_to(base) and work.is_relative_to(base)
    path = root/"Assets/Environment/KursHub/KursHubManifest.json"
    manifest = json.loads(path.read_text(encoding="utf8"))
    result = augment(manifest)
    def write_atomic(path, data):
        temporary = path.with_name(path.name+"."+uuid.uuid4().hex+".tmp")
        temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2)+"\n", encoding="utf8")
        os.replace(temporary, path)
    write_atomic(path, manifest)
    write_atomic(work/"utilities_layout8.json", result)
    if "hub8Spatial" in manifest:
        write_atomic(work/"functional_layout.json", manifest["hub8Spatial"])
    print("AUDIT8_UTILITY_LAYOUT_AUTHORED", result["addedPlacementCount"], "placements;",
          result["addedPointLights"], "fixture Points; native Floodlight Spots remain in their prefabs")
